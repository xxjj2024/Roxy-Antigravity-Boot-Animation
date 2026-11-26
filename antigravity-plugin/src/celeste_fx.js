const clamp = (v, a = 0, b = 1) => Math.max(a, Math.min(b, v));
const smooth = (a, b, x) => {
  const t = clamp((x - a) / (b - a));
  return t * t * (3 - 2 * t);
};

export class CelesteFX {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.w = 1280;
    this.h = 720;
    this.motes = [];
    this.outroDust = [];
    let seed = 20260425;
    const rand = () => {
      seed = (seed * 1664525 + 1013904223) >>> 0;
      return seed / 4294967296;
    };
    for (let i = 0; i < 68; i++) {
      this.motes.push({
        x: rand(),
        y: rand(),
        r: 0.9 + rand() * 2.1,
        spdY: 0.012 + rand() * 0.022,
        spdX: (rand() - 0.5) * 0.014,
        phase: rand() * Math.PI * 2,
        tw: 1.4 + rand() * 2.4,
        star: rand() > 0.62,
        hue: rand() > 0.45 ? '185,244,255' : '212,216,255',
      });
    }
    // Delicate rising starlight dust that gently disperses during the 10.2..13.2s fade-out
    for (let i = 0; i < 48; i++) {
      this.outroDust.push({
        x: 0.16 + rand() * 0.76,
        y0: 0.22 + rand() * 0.68,
        lift: 0.12 + rand() * 0.24,
        drift: (rand() - 0.5) * 0.07,
        delay: rand() * 0.95,
        dur: 1.65 + rand() * 0.95,
        r: 0.8 + rand() * 1.9,
        star: rand() > 0.58,
      });
    }
    this.resize();
    window.addEventListener('resize', () => this.resize());
  }

  resize() {
    this.dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.w = Math.max(1, this.canvas.clientWidth || window.innerWidth);
    this.h = Math.max(1, this.canvas.clientHeight || window.innerHeight);
    this.canvas.width = Math.round(this.w * this.dpr);
    this.canvas.height = Math.round(this.h * this.dpr);
  }

  imgToScreen(nx, ny, px = 0, py = 0) {
    const imgAspect = 16 / 9;
    const viewAspect = this.w / this.h;
    let drawW, drawH, offX, offY;
    if (viewAspect > imgAspect) {
      drawW = this.w * 1.06;
      drawH = drawW / imgAspect;
      offX = (this.w - drawW) * 0.62;
      offY = (this.h - drawH) * 0.44;
    } else {
      drawH = this.h * 1.06;
      drawW = drawH * imgAspect;
      offX = (this.w - drawW) * 0.62;
      offY = (this.h - drawH) * 0.44;
    }
    return [offX + nx * drawW + px, offY + ny * drawH + py];
  }

  drawStarFlare(cx, cy, radius, intensity, anamorphic = 0, angle = 0) {
    if (intensity <= 0.004) return;
    const ctx = this.ctx;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';

    const halo = ctx.createRadialGradient(cx, cy, 0, cx, cy, radius * 1.15);
    halo.addColorStop(0, `rgba(255,255,255,${clamp(intensity * 0.88)})`);
    halo.addColorStop(0.18, `rgba(170,242,255,${clamp(intensity * 0.62)})`);
    halo.addColorStop(0.48, `rgba(88,168,255,${clamp(intensity * 0.24)})`);
    halo.addColorStop(1, 'rgba(20,50,140,0)');
    ctx.fillStyle = halo;
    ctx.beginPath();
    ctx.arc(cx, cy, radius * 1.15, 0, Math.PI * 2);
    ctx.fill();

    ctx.translate(cx, cy);
    ctx.rotate(angle);
    const drawSpike = (len, thick, alpha) => {
      const g = ctx.createLinearGradient(-len, 0, len, 0);
      g.addColorStop(0, 'rgba(130,225,255,0)');
      g.addColorStop(0.42, `rgba(190,246,255,${clamp(alpha * 0.55)})`);
      g.addColorStop(0.5, `rgba(255,255,255,${clamp(alpha)})`);
      g.addColorStop(0.58, `rgba(190,246,255,${clamp(alpha * 0.55)})`);
      g.addColorStop(1, 'rgba(130,225,255,0)');
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.moveTo(-len, 0);
      ctx.quadraticCurveTo(0, -thick, len, 0);
      ctx.quadraticCurveTo(0, thick, -len, 0);
      ctx.fill();
    };
    drawSpike(radius * 1.35, Math.max(2.5, radius * 0.055), intensity * 0.92);
    ctx.rotate(Math.PI / 2);
    drawSpike(radius * 1.55, Math.max(2.5, radius * 0.055), intensity * 0.95);
    ctx.rotate(Math.PI / 4);
    drawSpike(radius * 0.52, Math.max(1.4, radius * 0.025), intensity * 0.42);
    ctx.rotate(Math.PI / 2);
    drawSpike(radius * 0.52, Math.max(1.4, radius * 0.025), intensity * 0.42);
    ctx.restore();

    if (anamorphic > 0.01) {
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      const bw = this.w * 0.96;
      const bh = Math.max(3, radius * 0.065);
      const ag = ctx.createLinearGradient(cx - bw * 0.5, cy, cx + bw * 0.5, cy);
      ag.addColorStop(0, 'rgba(110,220,255,0)');
      ag.addColorStop(0.32, `rgba(145,232,255,${clamp(anamorphic * 0.32)})`);
      ag.addColorStop(0.5, `rgba(255,255,255,${clamp(anamorphic * 0.92)})`);
      ag.addColorStop(0.68, `rgba(145,232,255,${clamp(anamorphic * 0.32)})`);
      ag.addColorStop(1, 'rgba(110,220,255,0)');
      ctx.fillStyle = ag;
      ctx.fillRect(cx - bw * 0.5, cy - bh * 0.5, bw, bh);
      ctx.restore();
    }
  }

  drawOutroDispersion(t, px, py) {
    // 10.20s..13.30s: Ethereal starlight silk-sheen exhale & fine upward stardust dissolve (no harsh circle ring)
    if (t < 10.20 || t > 13.40) return;
    const ctx = this.ctx;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';

    // Subtle horizontal starlight silk-exhale fading out from the giant 4-point star
    const sp = clamp((t - 10.20) / 1.95);
    if (sp < 1) {
      const exhale = Math.sin(sp * Math.PI) * (1 - sp * 0.45);
      const [bx, by] = this.imgToScreen(0.495, 0.425, px, py);
      const bw = this.w * (0.45 + sp * 0.65);
      const bh = Math.max(2, this.h * (0.018 * (1 - sp * 0.6)));
      const g = ctx.createLinearGradient(bx - bw * 0.5, by, bx + bw * 0.5, by);
      g.addColorStop(0, 'rgba(120,225,255,0)');
      g.addColorStop(0.35, `rgba(160,238,255,${clamp(exhale * 0.16)})`);
      g.addColorStop(0.5, `rgba(240,252,255,${clamp(exhale * 0.34)})`);
      g.addColorStop(0.65, `rgba(160,238,255,${clamp(exhale * 0.16)})`);
      g.addColorStop(1, 'rgba(120,225,255,0)');
      ctx.fillStyle = g;
      ctx.fillRect(bx - bw * 0.5, by - bh * 0.5, bw, bh);
    }

    // Ultra-fine starlight dust drifting upward and gently vanishing
    for (const s of this.outroDust) {
      const dp = clamp((t - 10.20 - s.delay) / s.dur);
      if (dp <= 0 || dp >= 1) continue;
      const a = Math.sin(dp * Math.PI) * Math.pow(1 - dp, 0.6) * 0.72;
      const easeUp = 1 - Math.pow(1 - dp, 2.2);
      const sx = (s.x + s.drift * easeUp) * this.w + px * 0.35;
      const sy = (s.y0 - s.lift * easeUp) * this.h + py * 0.35;
      if (s.star) {
        const d = s.r * 3.4;
        ctx.strokeStyle = `rgba(195,245,255,${a.toFixed(3)})`;
        ctx.lineWidth = 0.95;
        ctx.beginPath();
        ctx.moveTo(sx - d, sy); ctx.lineTo(sx + d, sy);
        ctx.moveTo(sx, sy - d); ctx.lineTo(sx, sy + d);
        ctx.stroke();
      }
      ctx.fillStyle = `rgba(225,248,255,${a.toFixed(3)})`;
      ctx.beginPath();
      ctx.arc(sx, sy, s.r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  render(t, smx, smy, fadeOut = 0) {
    const ctx = this.ctx;
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    ctx.clearRect(0, 0, this.w, this.h);

    const px = -smx * 12;
    const py = -smy * 8;
    // As fadeOut approaches 1, star flare softens gracefully while faint background stars keep breathing
    const flareKeep = 1 - fadeOut * 0.84;

    // 1. Drifting cosmic star motes (keep subtle calm stars alive even after fade-out)
    const moteAlpha = smooth(1.4, 3.6, t) * (0.78 - fadeOut * 0.36);
    if (moteAlpha > 0.01) {
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      for (const m of this.motes) {
        const x = ((m.x + t * m.spdX) % 1 + 1) % 1 * this.w + px * 0.6;
        const y = ((m.y - t * m.spdY) % 1 + 1) % 1 * this.h + py * 0.6;
        const tw = 0.35 + 0.65 * (0.5 + 0.5 * Math.sin(t * m.tw + m.phase));
        const a = moteAlpha * tw * 0.85;
        if (m.star && tw > 0.6) {
          const s = m.r * 3.6;
          ctx.strokeStyle = `rgba(${m.hue},${a.toFixed(3)})`;
          ctx.lineWidth = 1.0;
          ctx.beginPath();
          ctx.moveTo(x - s, y); ctx.lineTo(x + s, y);
          ctx.moveTo(x, y - s); ctx.lineTo(x, y + s);
          ctx.stroke();
        }
        ctx.fillStyle = `rgba(${m.hue},${a.toFixed(3)})`;
        ctx.beginPath();
        ctx.arc(x, y, m.r, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();
    }

    // 2. Beat 3 (3.85..5.75s): Luminous vertical flare gliding along Aqua Heartia staff
    if (t >= 3.85 && t <= 5.75) {
      const fp = clamp((t - 3.85) / 1.9);
      const fg = Math.sin(fp * Math.PI);
      const [sx, sy] = this.imgToScreen(0.835 - fp * 0.06, 0.18 + fp * 0.42, px, py);
      this.drawStarFlare(sx, sy, Math.min(this.w, this.h) * 0.22, fg * 0.9, fg * 0.28);
    }

    // 3. Beat 4 (6.35..7.45s): Delicate crystal glint as Roxy opens her eyes
    if (t >= 6.35 && t <= 7.45) {
      const eg = Math.sin(clamp((t - 6.35) / 1.1) * Math.PI);
      const [cx, cy] = this.imgToScreen(0.835, 0.195, px, py);
      this.drawStarFlare(cx, cy, Math.min(this.w, this.h) * 0.19, eg * 0.82, eg * 0.22);
    }

    // 4. Beat 5 (8.28..10.35s): Blinding cyan-white 4-point starburst eruption + anamorphic beam
    if (t >= 8.28 && t <= 10.35) {
      const bp = clamp((t - 8.28) / 2.07);
      const burst = smooth(0.0, 0.22, bp) * Math.pow(1 - smooth(0.20, 1.0, bp), 1.2);
      const [bx, by] = this.imgToScreen(0.495, 0.425, px, py);
      const [cx, cy] = this.imgToScreen(0.835, 0.195, px, py);
      this.drawStarFlare(bx, by, Math.min(this.w, this.h) * 0.56, burst * 1.15, burst * 0.92, bp * 0.08);
      this.drawStarFlare(cx, cy, Math.min(this.w, this.h) * 0.28, burst * 0.9, 0);
    }

    // 5. Beat 6 Ending Fade-Out Dispersion (10.20..13.30s)
    this.drawOutroDispersion(t, px, py);

    // 6. Living cosmos starlight pulse (fades smoothly into whisper glow during ending fade-out)
    const loopIn = smooth(9.2, 10.5, t);
    if (loopIn > 0.01 && flareKeep > 0.01) {
      const pulse = 0.55 + 0.45 * Math.sin(t * 1.57);
      const [bx, by] = this.imgToScreen(0.495, 0.425, px, py);
      const [cx, cy] = this.imgToScreen(0.835, 0.195, px, py);
      this.drawStarFlare(
        bx, by, Math.min(this.w, this.h) * 0.36,
        loopIn * (0.28 + 0.16 * pulse) * flareKeep,
        loopIn * 0.10 * pulse * flareKeep,
        Math.sin(t * 0.4) * 0.03
      );
      this.drawStarFlare(
        cx, cy, Math.min(this.w, this.h) * 0.16,
        loopIn * (0.22 + 0.15 * Math.sin(t * 2.1 + 1.1)) * flareKeep,
        0
      );
    }
  }
}
