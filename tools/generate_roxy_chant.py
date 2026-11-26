# -*- coding: utf-8 -*-
"""Synthesize Roxy Migurdia (Roxy3 ONNX / Genie-TTS) calm Japanese water-magic chant:
  - Line 1 (calm, eyes closed):      汝の求める所に、大いなる水の加護あらん。
  - Line 2 (calm continuous mentor): 清涼なるせせらぎの流れを、今ここに、ウォーターボール。

Usage (with Genie-TTS / dsh-anime-pet-voice-cpu Python):
  python tools/generate_roxy_chant.py --voice-root <path_to_roxy_tts_dir>
or set environment variable ROXY_VOICE_ROOT.
"""
import argparse
import csv
import hashlib
import json
import os

import numpy as np
import onnxruntime as ort
import soundfile as sf

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_OUT_DIR = os.path.join(REPO_ROOT, "windows-overlay", "assets")

SR = 32000


def trim(w, head=0.04, tail=0.14):
    n = int(SR * 0.02)
    if len(w) < n:
        return w
    env = np.convolve(np.abs(w), np.ones(n) / n, "same")
    on = np.nonzero(env > max(env.max() * 0.025, 1e-3))[0]
    if not len(on):
        return w
    return w[max(0, on[0] - int(SR * head)) : min(len(w), on[-1] + int(SR * tail))]


def synth_piece(M, names, vocab, text, ref_obj, get_phones_and_bert, temp=0.62, seed_salt=0):
    rng = np.random.default_rng((int(hashlib.md5(text.encode("utf-8")).hexdigest()[:8], 16) + seed_salt) % 100000)
    seq, bert = get_phones_and_bert("。" + text, language="Japanese")
    x, prompts = M.T2S_ENCODER.run(
        None,
        {
            "ref_seq": ref_obj.phonemes_seq,
            "text_seq": seq,
            "ref_bert": ref_obj.text_bert,
            "text_bert": bert,
            "ssl_content": ref_obj.ssl_content,
        },
    )
    T = np.array(temp, dtype=np.float32)
    noise = lambda: rng.exponential(size=vocab).astype(np.float32)
    y, y_emb, *kv = M.T2S_FIRST_STAGE_DECODER.run(
        None, {"x": x, "prompts": prompts, "temperature": T, "noise": noise()}
    )
    idx = 0
    for idx in range(500):
        feed = dict(zip(names, [y, y_emb, *kv]))
        feed["temperature"] = T
        feed["noise"] = noise()
        y, y_emb, stop, *kv = M.T2S_STAGE_DECODER.run(None, feed)
        if stop:
            break
    sem = y[:, -(idx + 2) : -1]
    bad = np.nonzero(sem[0] >= 1024)[0]
    if len(bad):
        sem = sem[:, : bad[0]]
    if sem.shape[1] == 0:
        return np.zeros(int(SR * 0.2), np.float32)
    sem = np.expand_dims(sem, axis=0)
    w = M.VITS.run(
        None,
        {
            "text_seq": seq,
            "pred_semantic": sem,
            "ge": ref_obj.global_emb,
            "ge_advanced": ref_obj.global_emb_advanced,
        },
    )[0].reshape(-1)
    pk = float(np.abs(w).max())
    if pk > 1.0:
        w = w / pk
    return trim(w.astype(np.float32))


def main():
    ap = argparse.ArgumentParser(description="Synthesize calm Roxy3 water-magic chant WAVs.")
    ap.add_argument("--voice-root", default=os.environ.get("ROXY_VOICE_ROOT", ""), help="Directory containing onnx/roxy3, onnx/GenieData, and voice_config_全用Roxy3.tsv")
    ap.add_argument("--out-dir", default=DEFAULT_OUT_DIR, help="Output directory for _chant_1.wav, _chant_2.wav, _chant_meta.json")
    args = ap.parse_args()

    if not args.voice_root or not os.path.isdir(args.voice_root):
        raise SystemExit("Please pass --voice-root <path_to_roxy-tts> (or set ROXY_VOICE_ROOT).")

    os.makedirs(args.out_dir, exist_ok=True)
    _orig_init = ort.InferenceSession.__init__

    def _patched_session(self, path_or_bytes, sess_options=None, providers=None, provider_options=None, **kw):
        so = sess_options or ort.SessionOptions()
        so.intra_op_num_threads = 4
        so.inter_op_num_threads = 1
        so.enable_cpu_mem_arena = False
        return _orig_init(self, path_or_bytes, sess_options=so, providers=providers, provider_options=provider_options, **kw)

    ort.InferenceSession.__init__ = _patched_session
    os.environ["GENIE_DATA_DIR"] = os.path.join(args.voice_root, "onnx", "GenieData")

    from genie_tts.Audio.ReferenceAudio import ReferenceAudio
    from genie_tts.GetPhonesAndBert import get_phones_and_bert
    from genie_tts.ModelManager import model_manager

    model_manager.load_character("roxy3", os.path.join(args.voice_root, "onnx", "roxy3"), "Japanese")
    M = model_manager.get("roxy3")
    names = [i.name for i in M.T2S_STAGE_DECODER.get_inputs()]
    vocab = next(i.shape[0] for i in M.T2S_STAGE_DECODER.get_inputs() if i.name == "noise")

    conf = {}
    with open(os.path.join(args.voice_root, "voice_config_全用Roxy3.tsv"), encoding="utf-8") as f:
        for r in csv.DictReader(f, delimiter="\t"):
            conf[(r["tag"], r["lang"])] = r

    row = conf[("平静", "ja")]
    ref_calm = ReferenceAudio(os.path.join(args.voice_root, row["ref_wav"]), row["ref_text"], "Japanese")
    ref_calm.update_global_emb(prompt_encoder=M.PROMPT_ENCODER)

    w1 = synth_piece(M, names, vocab, "汝の求める所に、大いなる水の加護あらん。", ref_calm, get_phones_and_bert, temp=0.68, seed_salt=411)
    w2 = synth_piece(M, names, vocab, "清涼なるせせらぎの流れを、今ここに、ウォーターボール。", ref_calm, get_phones_and_bert, temp=0.60, seed_salt=11)

    d1, d2 = len(w1) / SR, len(w2) / SR
    start1 = 2.05
    start2 = max(start1 + d1 + 0.18, 5.73)

    wav1 = os.path.join(args.out_dir, "_chant_1.wav")
    wav2 = os.path.join(args.out_dir, "_chant_2.wav")
    sf.write(wav1, w1, SR, subtype="PCM_16")
    sf.write(wav2, w2, SR, subtype="PCM_16")

    meta = {
        "engine": "Roxy3 ONNX (Genie-TTS, calm mentor take)",
        "line1": {
            "wav": wav1,
            "start": round(start1, 3),
            "end": round(start1 + d1, 3),
            "duration": round(d1, 3),
            "zh": "汝所求索之处，当有伟大水之加护——",
            "ja": "汝の求める所に、大いなる水の加護あらん",
        },
        "line2": {
            "wav": wav2,
            "start": round(start2, 3),
            "end": round(start2 + d2, 3),
            "duration": round(d2, 3),
            "zh": "将清凉潺湲的流水，于此地呈现……水球。",
            "ja": "清涼なるせせらぎの流れを、今ここに……ウォーターボール",
        },
    }
    meta_path = os.path.join(args.out_dir, "_chant_meta.json")
    with open(meta_path, "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)
    print(json.dumps(meta, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
