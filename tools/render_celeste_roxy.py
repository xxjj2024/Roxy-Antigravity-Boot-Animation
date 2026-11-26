# -*- coding: utf-8 -*-
"""Render 30fps 1280x720 single-take Roxy Migurdia MP4s & plugin audio:
  - windows-overlay/assets/intro.mp4    (12.0s, H.264 Constrained Baseline + 44.1kHz stereo Roxy chant & score)
  - windows-overlay/assets/loop.mp4     ( 8.0s, H.264 Constrained Baseline seamless fluid-hair cosmos loop)
  - windows-overlay/assets/loop-x2.mp4  (16.0s, loop.mp4 x2 for zero-flash overlapping handoff)
  - antigravity-plugin/assets_src/roxy_chant.mp3 + chant_timing.json

Usage:
  python tools/render_celeste_roxy.py --keyframes-dir <dir_with_closed_open_climax_images>
  (Defaults to antigravity-plugin/assets_src/ WebP keyframes if --keyframes-dir is omitted.)
"""
import argparse
import json
import math
import os
import shutil
import subprocess
import wave

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy.signal import butter, filtfilt

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(REPO_ROOT, "windows-overlay", "assets")
PLUG_ASSETS = os.path.join(REPO_ROOT, "antigravity-plugin", "assets_src")

W, H, FPS = 1280, 720, 30
INTRO_SEC = 12.0
LOOP_SEC = 8.0


def find_ffmpeg():
    env_bin = os.environ.get("FFMPEG_BIN")
    if env_bin and os.path.exists(env_bin):
        return env_bin
    return shutil.which("ffmpeg") or "ffmpeg"


def fast_bloom(layer, sigma):
    small = cv2.resize(layer, (W // 4, H // 4), interpolation=cv2.INTER_AREA)
    blur = cv2.GaussianBlur(small, (0, 0), max(0.8, sigma * 0.25))
    return cv2.resize(blur, (W, H), interpolation=cv2.INTER_LINEAR)


def clamp(v, a=0.0, b=1.0):
    return a if v < a else (b if v > b else v)


def smooth(a, b, x):
    t = clamp((x - a) / (b - a))
    return t * t * (3.0 - 2.0 * t)


def load_bgr(path):
    im = Image.open(path).convert("RGB").resize((W, H), Image.LANCZOS)
    return cv2.cvtColor(np.asarray(im, np.uint8), cv2.COLOR_RGB2BGR).astype(np.float32)


def scharr_mag(ch):
    gx = cv2.Scharr(ch, cv2.CV_32F, 1, 0) / 32.0
    gy = cv2.Scharr(ch, cv2.CV_32F, 0, 1) / 32.0
    return np.hypot(gx, gy)


def build_derived_layers(closed_bgr):
    rgb = closed_bgr[..., ::-1] / 255.0
    lum = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]

    subject_mask = cv2.GaussianBlur((lum > 0.14).astype(np.float32), (0, 0), 9.0)
    subject_mask = np.clip((subject_mask - 0.12) / 0.35, 0.0, 1.0)

    grad = np.maximum(scharr_mag(lum), 0.7 * scharr_mag(rgb[..., 2]))
    valley = np.clip(cv2.GaussianBlur(lum, (0, 0), 1.8) - lum, 0.0, 1.0)
    lines = np.clip(grad * 2.8 + valley * 4.5, 0.0, 1.0) * subject_mask
    lines = np.clip((lines - 0.12) / 0.80, 0.0, 1.0) ** 0.85

    line_glow = cv2.GaussianBlur(lines, (0, 0), 2.2)
    l_int = np.clip(lines * 1.15 + line_glow * 0.45, 0.0, 1.0)[..., None]
    line_col = np.array([255.0, 214.0, 198.0], np.float32) * (1.0 - lines[..., None] * 0.45) + 255.0 * (lines[..., None] * 0.45)
    line_art = np.clip(line_col * l_int, 0, 255).astype(np.float32)

    rim_blur = cv2.GaussianBlur(lines, (0, 0), 7.5)
    neon_b = np.clip(closed_bgr[..., 0] * 0.85 + rim_blur * 220.0 + lines * 140.0, 0, 255)
    neon_g = np.clip(closed_bgr[..., 1] * 0.52 + rim_blur * 125.0 + lines * 185.0, 0, 255)
    neon_r = np.clip(closed_bgr[..., 2] * 0.72 + rim_blur * 190.0 + lines * 180.0, 0, 255)
    neon_aura = np.stack([neon_b, neon_g, neon_r], axis=-1) * subject_mask[..., None]

    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    nx, ny = xx / W, yy / H
    face_dist = np.hypot((nx - 0.66) / 0.16, (ny - 0.55) / 0.34)
    staff_dist = np.hypot((nx - 0.81) / 0.11, (ny - 0.38) / 0.42)
    lock = np.minimum(
        np.clip((face_dist - 0.72) / 0.55, 0.0, 1.0),
        np.clip((staff_dist - 0.72) / 0.50, 0.0, 1.0),
    )
    hair_mask = cv2.GaussianBlur(lock * subject_mask, (0, 0), 14.0)
    head_r = np.hypot((nx - 0.65) * (W / H), (ny - 0.42))
    return line_art, neon_aura, hair_mask, head_r, xx, yy


def apply_hair_wave(img_bgr, xx, yy, hair_mask, phase, amp=1.0):
    if amp <= 0.001:
        return img_bgr
    ang = phase * 2.0 * math.pi
    nx, ny = xx / W, yy / H
    w1 = np.sin(nx * 10.5 + ny * 4.5 + ang)
    w2 = np.cos(nx * 6.5 - ny * 7.0 + ang * 2.0)
    dx = (w1 * 5.8 + w2 * 2.4) * hair_mask * amp
    dy = (np.cos(nx * 9.0 + ny * 3.5 + ang) * 4.6 + w2 * 1.8) * hair_mask * amp
    return cv2.remap(img_bgr, (xx + dx).astype(np.float32), (yy + dy).astype(np.float32), interpolation=cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)


def load_cjk_serif_font(size):
    fonts_dir = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")
    for name in ("NotoSerifSC-VF.ttf", "STSONG.TTF", "simkai.ttf", "msyh.ttc"):
        p = os.path.join(fonts_dir, name)
        if os.path.exists(p):
            try:
                f = ImageFont.truetype(p, size)
                if hasattr(f, "set_variation_by_axes") and "VF" in name:
                    try:
                        f.set_variation_by_axes([500])
                    except Exception:
                        pass
                return f
            except Exception:
                continue
    return ImageFont.load_default()


def make_subtitle_sprite(primary_text, sub_text=None):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d, sd = ImageDraw.Draw(im), ImageDraw.Draw(sh)
    font_main = load_cjk_serif_font(36)
    font_sub = load_cjk_serif_font(21)

    bb1 = d.textbbox((0, 0), primary_text, font=font_main)
    tw1, th1 = bb1[2] - bb1[0], bb1[3] - bb1[1]
    x1 = (W - tw1) // 2
    y1 = int(H * (0.810 if sub_text else 0.840)) - th1 // 2

    sd.text((x1, y1 + 3), primary_text, font=font_main, fill=(0, 0, 0, 225), stroke_width=4, stroke_fill=(0, 0, 0, 225))
    d.text((x1, y1), primary_text, font=font_main, fill=(252, 254, 255, 255), stroke_width=2, stroke_fill=(6, 10, 22, 245))

    if sub_text:
        bb2 = d.textbbox((0, 0), sub_text, font=font_sub)
        tw2, th2 = bb2[2] - bb2[0], bb2[3] - bb2[1]
        x2 = (W - tw2) // 2
        y2 = y1 + th1 + 12
        sd.text((x2, y2 + 2), sub_text, font=font_sub, fill=(0, 0, 0, 210), stroke_width=3, stroke_fill=(0, 0, 0, 210))
        d.text((x2, y2), sub_text, font=font_sub, fill=(214, 242, 255, 232), stroke_width=2, stroke_fill=(6, 12, 26, 235))

    sh = sh.filter(ImageFilter.GaussianBlur(radius=5))
    arr = np.asarray(Image.alpha_composite(sh, im), np.uint8)
    return cv2.cvtColor(arr[..., :3], cv2.COLOR_RGB2BGR).astype(np.float32), arr[..., 3:4].astype(np.float32) / 255.0


def add_star_flare(frame, cx, cy, size, intensity, anamorphic=0.0):
    if intensity <= 0.005:
        return frame
    layer = np.zeros_like(frame)
    ix, iy = int(cx), int(cy)
    r_v, r_h = int(size * 1.15), int(size * 1.35)
    pts_v = np.array([[ix, iy - r_v], [ix + int(size * 0.06), iy], [ix, iy + r_v], [ix - int(size * 0.06), iy]], np.int32)
    pts_h = np.array([[ix - r_h, iy], [ix, iy - int(size * 0.05)], [ix + r_h, iy], [ix, iy + int(size * 0.05)]], np.int32)
    cv2.fillConvexPoly(layer, pts_v, (255, 245, 225), cv2.LINE_AA)
    cv2.fillConvexPoly(layer, pts_h, (255, 245, 225), cv2.LINE_AA)
    cv2.circle(layer, (ix, iy), max(4, int(size * 0.14)), (255, 255, 255), -1, cv2.LINE_AA)
    if anamorphic > 0.01:
        th = max(2, int(4 + anamorphic * 16))
        cv2.rectangle(layer, (0, iy - th // 2), (W, iy + th // 2), (255 * anamorphic, 225 * anamorphic, 160 * anamorphic), -1)
    tint = np.array([1.0, 0.88, 0.62], np.float32)
    added = (layer * 0.85 + fast_bloom(layer, 10) * 0.95 * tint + fast_bloom(layer, 30) * 0.78 * tint) * intensity
    return np.clip(frame + added, 0, 255)


def add_cosmic_motes(frame, phase, motes, opacity=1.0):
    if opacity <= 0.01:
        return frame
    layer = np.zeros_like(frame)
    for mx0, my0, rad, k_spd, ph0, is_cyan in motes:
        x = int((mx0 * W + 16.0 * math.sin((phase * k_spd + ph0) * 2.0 * math.pi)) % W)
        y = int(((my0 - phase * k_spd) % 1.0) * H)
        tw = (0.35 + 0.65 * (0.5 + 0.5 * math.sin((phase * 2.0 + ph0) * 2.0 * math.pi))) * opacity
        col = (int(255 * tw), int(232 * tw), int(155 * tw)) if is_cyan else (int(255 * tw), int(244 * tw), int(235 * tw))
        cv2.circle(layer, (x, y), max(1, int(rad)), col, -1, cv2.LINE_AA)
    return np.clip(frame + layer * 0.75 + fast_bloom(layer, 6) * 0.58, 0, 255)


def load_mono_wav_44k(path, sr=44100):
    if not path or not os.path.exists(path):
        return None
    with wave.open(path, "rb") as wf:
        ch, sw, fs = wf.getnchannels(), wf.getsampwidth(), wf.getframerate()
        raw = wf.readframes(wf.getnframes())
    if sw == 2:
        x = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
    elif sw == 4:
        x = np.frombuffer(raw, dtype=np.int32).astype(np.float32) / 2147483648.0
    else:
        return None
    if ch > 1:
        x = x.reshape(-1, ch).mean(axis=1)
    if fs != sr and len(x) > 1:
        x = np.interp(np.linspace(0, len(x) - 1, int(round(len(x) * sr / fs))), np.arange(len(x)), x).astype(np.float32)
    return x


def add_celestial_reverb(mono, sr=44100, wet=0.16):
    n = len(mono)
    b, a = butter(2, 4200.0 / (sr * 0.5), btype="low")
    soft = filtfilt(b, a, mono).astype(np.float32)
    rev_l, rev_r = np.zeros(n, dtype=np.float32), np.zeros(n, dtype=np.float32)
    for d_sec, gain in [(0.058, 0.42), (0.113, 0.30), (0.197, 0.22), (0.311, 0.14), (0.467, 0.08)]:
        d = int(d_sec * sr)
        if d < n:
            rev_l[d:] += soft[: n - d] * gain
    for d_sec, gain in [(0.067, 0.40), (0.131, 0.29), (0.223, 0.20), (0.349, 0.13), (0.503, 0.07)]:
        d = int(d_sec * sr)
        if d < n:
            rev_r[d:] += soft[: n - d] * gain
    return mono + rev_l * wet, mono + rev_r * wet


def synthesize_audio(wav_path, chant_meta, ffmpeg_bin):
    sr = 44100
    n = int(sr * INTRO_SEC)
    t = np.linspace(0.0, INTRO_SEC, n, endpoint=False, dtype=np.float32)
    rng = np.random.default_rng(108)
    L, R = np.zeros(n, dtype=np.float32), np.zeros(n, dtype=np.float32)

    def add_tone(t0, dur, f0, f1, amp, pan=0.0, attack=0.04):
        i0 = int(t0 * sr)
        m = min(n - i0, int(dur * sr))
        if m <= 0:
            return
        lt = np.linspace(0.0, dur, m, endpoint=False, dtype=np.float32)
        ph = 2.0 * np.pi * (f0 * lt + 0.5 * (f1 - f0) * lt * lt / max(dur, 1e-4))
        env = np.minimum(lt / attack, 1.0) * np.exp(-lt / (dur * 0.36))
        sig = (np.sin(ph) * 0.8 + np.sin(ph * 2.0) * 0.2) * env * amp
        L[i0 : i0 + m] += sig * (0.5 - 0.38 * pan) * 1.4
        R[i0 : i0 + m] += sig * (0.5 + 0.38 * pan) * 1.4

    for f, p in [(130.81, -0.3), (196.00, 0.3), (261.63, -0.15), (392.00, 0.15)]:
        add_tone(0.15, 5.4, f, f * 1.003, 0.055, p, attack=1.2)
    for idx, f in enumerate([523.25, 587.33, 659.25, 783.99, 987.77]):
        add_tone(1.90 + idx * 0.16, 1.6, f, f * 1.004, 0.042, -0.5 + idx * 0.25, attack=0.06)
    add_tone(3.40, 1.6, 880.0, 1174.66, 0.065, 0.22, attack=0.015)
    add_tone(3.52, 1.8, 1318.5, 1318.5, 0.040, -0.22, attack=0.02)
    add_tone(6.38, 1.8, 987.77, 987.77, 0.068, -0.25, attack=0.025)
    add_tone(6.50, 1.9, 1174.66, 1174.66, 0.060, 0.25, attack=0.025)

    b0, b1 = int(8.22 * sr), int(10.15 * sr)
    bn = b1 - b0
    bt = np.linspace(0.0, bn / sr, bn, dtype=np.float32)
    b_bp, a_bp = butter(2, [450.0 / (sr * 0.5), 3400.0 / (sr * 0.5)], btype="band")
    water_whoosh = filtfilt(b_bp, a_bp, rng.standard_normal(bn).astype(np.float32)).astype(np.float32)
    wenv = np.minimum(bt / 0.07, 1.0) * np.exp(-bt / 0.48)
    L[b0:b1] += water_whoosh * wenv * 0.42
    R[b0:b1] += water_whoosh * wenv * 0.42
    add_tone(8.28, 1.35, 98.0, 49.0, 0.26, 0.0, attack=0.02)
    for idx, f in enumerate([261.63, 392.00, 523.25, 659.25, 783.99, 1046.50, 1318.51]):
        add_tone(8.32 + idx * 0.055, 3.3, f, f * 1.001, 0.075 - idx * 0.006, -0.6 + idx * 0.2, attack=0.05)

    voice_dry = np.zeros(n, dtype=np.float32)
    loaded = 0
    for key in ("line1", "line2"):
        info = chant_meta.get(key, {})
        clip = load_mono_wav_44k(info.get("wav"), sr=sr)
        if clip is None or len(clip) == 0:
            continue
        loaded += 1
        rms = float(np.sqrt(np.mean(clip * clip) + 1e-10))
        if rms > 1e-5:
            clip = clip * (0.185 / rms)
        peak = float(np.max(np.abs(clip)) + 1e-9)
        if peak > 0.88:
            clip = clip * (0.88 / peak)
        fn = min(int(0.015 * sr), len(clip) // 4)
        if fn > 1:
            clip[:fn] *= np.linspace(0.0, 1.0, fn, dtype=np.float32)
            clip[-fn:] *= np.linspace(1.0, 0.0, fn, dtype=np.float32)
        i0 = max(0, int(round(float(info.get("start", 2.05)) * sr)))
        m = min(len(clip), n - i0)
        if m > 0:
            voice_dry[i0 : i0 + m] += clip[:m]

    if loaded > 0:
        b_env, a_env = butter(2, 4.5 / (sr * 0.5), btype="low")
        duck = 1.0 - 0.56 * np.clip(filtfilt(b_env, a_env, np.abs(voice_dry)) * 5.2, 0.0, 1.0).astype(np.float32)
        L, R = L * duck, R * duck
        b_hp, a_hp = butter(2, 85.0 / (sr * 0.5), btype="high")
        vl, vr = add_celestial_reverb(filtfilt(b_hp, a_hp, voice_dry).astype(np.float32), sr=sr, wet=0.16)
        L += vl * 1.12
        R += vr * 1.12

    fade_out = np.clip((INTRO_SEC - t) / 1.15, 0.0, 1.0)
    fade_out = fade_out * fade_out * (3.0 - 2.0 * fade_out)
    stereo = np.stack([np.tanh(L * fade_out * 1.05), np.tanh(R * fade_out * 1.05)], axis=-1)
    pcm = (np.clip(stereo, -1.0, 1.0) * 32767.0).astype(np.int16)
    with wave.open(wav_path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(sr)
        wf.writeframes(pcm.tobytes())

    os.makedirs(PLUG_ASSETS, exist_ok=True)
    plug_mp3 = os.path.join(PLUG_ASSETS, "roxy_chant.mp3")
    subprocess.run([ffmpeg_bin, "-y", "-loglevel", "error", "-i", wav_path, "-c:a", "libmp3lame", "-b:a", "128k", plug_mp3], check=True)


def start_ffmpeg(ffmpeg_bin, out_mp4, audio_wav=None):
    cmd = [
        ffmpeg_bin, "-y", "-loglevel", "error",
        "-f", "rawvideo", "-vcodec", "rawvideo", "-pix_fmt", "bgr24",
        "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
    ]
    if audio_wav:
        cmd += ["-i", audio_wav]
    cmd += [
        "-c:v", "libx264", "-profile:v", "baseline", "-level", "3.1",
        "-preset", "medium", "-crf", "16",
        "-bf", "0", "-g", "15", "-keyint_min", "15", "-sc_threshold", "0",
        "-pix_fmt", "yuv420p", "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv",
        "-movflags", "+faststart",
    ]
    if audio_wav:
        cmd += ["-c:a", "aac", "-ar", "44100", "-ac", "2", "-b:a", "192k", "-shortest"]
    cmd += [out_mp4]
    return subprocess.Popen(cmd, stdin=subprocess.PIPE)


def render_loop_frame(climax_bgr, xx, yy, hair_mask, phase, motes):
    frame = apply_hair_wave(climax_bgr, xx, yy, hair_mask, phase, amp=1.0)
    breath = 0.16 + 0.14 * (0.5 + 0.5 * math.sin(phase * 2.0 * math.pi))
    frame = add_star_flare(frame, W * 0.495, H * 0.425, 190, breath * 0.55, anamorphic=0.0)
    frame = add_star_flare(frame, W * 0.835, H * 0.195, 120, breath * 0.72, anamorphic=0.0)
    return add_cosmic_motes(frame, phase, motes, opacity=0.82)


def main():
    ap = argparse.ArgumentParser(description="Render WPF-safe Roxy boot animation MP4s and plugin audio.")
    ap.add_argument("--keyframes-dir", default=os.environ.get("ROXY_KEYFRAMES_DIR", PLUG_ASSETS), help="Directory containing roxy_closed/roxy_open/roxy_climax (.webp/.jpg/.png)")
    ap.add_argument("--render-loop", action="store_true", help="Force re-rendering loop.mp4 and loop-x2.mp4")
    args = ap.parse_args()

    ffmpeg_bin = find_ffmpeg()
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(PLUG_ASSETS, exist_ok=True)

    def resolve_kf(stem):
        for ext in (".webp", ".png", ".jpg", ".jpeg"):
            p = os.path.join(args.keyframes_dir, stem + ext)
            if os.path.exists(p):
                return p
        raise FileNotFoundError(f"Missing keyframe {stem}.(webp|png|jpg) in {args.keyframes_dir}")

    meta_path = os.path.join(OUT_DIR, "_chant_meta.json")
    chant_meta = {
        "line1": {"wav": os.path.join(OUT_DIR, "_chant_1.wav"), "start": 2.05, "end": 5.553, "zh": "汝所求索之处，当有伟大水之加护——", "ja": "汝の求める所に、大いなる水の加護あらん"},
        "line2": {"wav": os.path.join(OUT_DIR, "_chant_2.wav"), "start": 5.73, "end": 10.07, "zh": "将清凉潺湲的流水，于此地呈现……水球。", "ja": "清涼なるせせらぎの流れを、今ここに……ウォーターボール"},
    }
    if os.path.exists(meta_path):
        with open(meta_path, "r", encoding="utf-8") as f:
            chant_meta = json.load(f)

    l1, l2 = chant_meta["line1"], chant_meta["line2"]
    s1_in = max(1.90, float(l1.get("start", 2.05)) - 0.12)
    s1_out = min(float(l2.get("start", 5.73)) - 0.04, float(l1.get("end", 5.55)) + 0.15)
    s2_in = max(s1_out + 0.02, float(l2.get("start", 5.73)) - 0.08)
    s2_out = min(10.75, max(10.10, float(l2.get("end", 10.07)) + 0.45))

    im_closed = load_bgr(resolve_kf("roxy_closed"))
    im_open = load_bgr(resolve_kf("roxy_open"))
    im_climax = load_bgr(resolve_kf("roxy_climax"))
    line_art, neon_aura, hair_mask, head_r, xx, yy = build_derived_layers(im_closed)
    sub1_bgr, sub1_a = make_subtitle_sprite(l1["zh"], l1.get("ja"))
    sub2_bgr, sub2_a = make_subtitle_sprite(l2["zh"], l2.get("ja"))

    rng = np.random.default_rng(2026)
    motes = [(rng.random(), rng.random(), 1.2 + rng.random() * 2.2, 1 if i % 3 else 2, rng.random(), (i % 3 != 0)) for i in range(44)]

    wav_path = os.path.join(OUT_DIR, "_intro_audio.wav")
    synthesize_audio(wav_path, chant_meta, ffmpeg_bin)

    intro_mp4 = os.path.join(OUT_DIR, "intro.mp4")
    total_intro = int(INTRO_SEC * FPS)
    proc = start_ffmpeg(ffmpeg_bin, intro_mp4, audio_wav=wav_path)
    for fi in range(total_intro):
        t = fi / FPS
        phase = ((t - INTRO_SEC) / LOOP_SEC) % 1.0
        wave_amp = smooth(1.6, 3.4, t)
        if t < 3.8:
            r_lim = 0.24 + smooth(0.0, 1.75, t) * 0.16 + smooth(1.75, 3.55, t) * 1.15
            rad_mask = np.clip((r_lim - head_r) / 0.14, 0.0, 1.0)[..., None]
            base = line_art * rad_mask * (smooth(0.08, 0.75, t) * (1.0 - smooth(2.6, 3.8, t) * 0.45)) + neon_aura * rad_mask * smooth(1.70, 3.35, t)
            frame = add_cosmic_motes(apply_hair_wave(np.clip(base, 0, 255), xx, yy, hair_mask, phase, amp=wave_amp), phase, motes, opacity=smooth(1.5, 3.6, t) * 0.55)
        elif t < 6.4:
            mat = smooth(3.8, 4.75, t)
            frame = apply_hair_wave(neon_aura * (1.0 - mat) + im_closed * mat if mat < 0.999 else im_closed, xx, yy, hair_mask, phase, amp=1.0)
            if 3.85 <= t <= 5.75:
                fp = clamp((t - 3.85) / 1.90)
                fg = math.sin(fp * math.pi)
                frame = add_star_flare(frame, W * (0.835 - fp * 0.06), H * (0.18 + fp * 0.42), 180, fg * 0.92, anamorphic=fg * 0.28)
            frame = add_cosmic_motes(frame, phase, motes, opacity=0.65)
        elif t < 8.3:
            eye_p = smooth(6.40, 7.15, t)
            frame = apply_hair_wave(im_closed * (1.0 - eye_p) + im_open * eye_p, xx, yy, hair_mask, phase, amp=1.0)
            if 6.35 <= t <= 7.45:
                eg = math.sin(clamp((t - 6.35) / 1.10) * math.pi)
                frame = add_star_flare(frame, W * 0.835, H * 0.195, 160, eg * 0.85, anamorphic=eg * 0.22)
            frame = add_cosmic_motes(frame, phase, motes, opacity=0.72)
        else:
            cp = smooth(8.30, 9.25, t)
            frame = add_cosmic_motes(apply_hair_wave(im_open * (1.0 - cp) + im_climax * cp, xx, yy, hair_mask, phase, amp=1.0), phase, motes, opacity=0.72 + 0.10 * cp) if cp < 0.999 else render_loop_frame(im_climax, xx, yy, hair_mask, phase, motes)
            if 8.28 <= t <= 10.35:
                bp = clamp((t - 8.28) / 2.07)
                burst = smooth(0.0, 0.22, bp) * ((1.0 - smooth(0.20, 1.0, bp)) ** 1.2)
                frame = add_star_flare(frame, W * 0.495, H * 0.425, 420, burst * 1.15, anamorphic=burst * 0.95)
                frame = add_star_flare(frame, W * 0.835, H * 0.195, 240, burst * 0.95, anamorphic=0.0)

        op_s1 = smooth(s1_in, s1_in + 0.35, t) * (1.0 - smooth(s1_out - 0.35, s1_out, t))
        if op_s1 > 0.005:
            frame = frame * (1.0 - sub1_a * op_s1) + sub1_bgr * (sub1_a * op_s1)
        op_s2 = smooth(s2_in, s2_in + 0.35, t) * (1.0 - smooth(s2_out - 0.40, s2_out, t))
        if op_s2 > 0.005:
            frame = frame * (1.0 - sub2_a * op_s2) + sub2_bgr * (sub2_a * op_s2)
        proc.stdin.write(np.clip(frame, 0, 255).astype(np.uint8).tobytes())

    proc.stdin.close()
    if proc.wait() != 0:
        raise RuntimeError("ffmpeg failed on intro.mp4")
    if os.path.exists(wav_path):
        os.remove(wav_path)

    loop_mp4, loop_x2 = os.path.join(OUT_DIR, "loop.mp4"), os.path.join(OUT_DIR, "loop-x2.mp4")
    if args.render_loop or not (os.path.exists(loop_mp4) and os.path.exists(loop_x2)):
        total_loop = int(LOOP_SEC * FPS)
        proc2 = start_ffmpeg(ffmpeg_bin, loop_mp4)
        for fi in range(total_loop):
            proc2.stdin.write(np.clip(render_loop_frame(im_climax, xx, yy, hair_mask, fi / total_loop, motes), 0, 255).astype(np.uint8).tobytes())
        proc2.stdin.close()
        if proc2.wait() != 0:
            raise RuntimeError("ffmpeg failed on loop.mp4")
        list_txt = os.path.join(OUT_DIR, "_concat.txt")
        with open(list_txt, "w", encoding="utf-8") as f:
            f.write("file 'loop.mp4'\nfile 'loop.mp4'\n")
        subprocess.run([ffmpeg_bin, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", list_txt, "-c", "copy", "-movflags", "+faststart", loop_x2], check=True)
        os.remove(list_txt)

    with open(os.path.join(PLUG_ASSETS, "chant_timing.json"), "w", encoding="utf-8") as f:
        json.dump(
            {
                "s1": {"in": round(s1_in, 2), "out": round(s1_out, 2), "zh": l1["zh"], "ja": l1.get("ja", "")},
                "s2": {"in": round(s2_in, 2), "out": round(s2_out, 2), "zh": l2["zh"], "ja": l2.get("ja", "")},
            },
            f,
            ensure_ascii=False,
            indent=2,
        )
    print("Render complete.")


if __name__ == "__main__":
    main()
