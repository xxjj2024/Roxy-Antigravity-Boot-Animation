import { CelesteFX } from './celeste_fx.js';
import { CelesteSfx } from './celeste_sfx.js';

const clamp = (v, a = 0, b = 1) => Math.max(a, Math.min(b, v));
const smooth = (a, b, x) => {
  const t = clamp((x - a) / (b - a));
  return t * t * (3 - 2 * t);
};
const smoother = (a, b, x) => {
  const t = clamp((x - a) / (b - a));
  return t * t * t * (t * (t * 6 - 15) + 10);
};

export const INTRO_DURATION = 13.5;

export function startCelesteDirector() {
  const assets = window.__ROXY_CELESTE__ || {};
  const timing = assets.chantTiming || {
    s1: {
      in: 3.30,
      out: 6.35,
      zh: '汝所求索之处，当有伟大水之加护——',
      ja: '汝の求める処に、大いなる水の加護あらん',
    },
    s2: {
      in: 6.42,
      out: 10.65,
      zh: '将清凉潺湲的流水，于此地呈现……水球！',
      ja: '清涼なるせせらぎの流れを、今ここに……ウォーターボール！',
    },
  };
  const $ = (s) => document.querySelector(s);

  const stage = $('#stage');
  const cam = $('#cam');
  const imgLine = $('#img-line');
  const imgNeon = $('#img-neon');
  const imgClosed = $('#img-closed');
  const imgOpen = $('#img-open');
  const imgClimax = $('#img-climax');
  const endVeil = $('#end-veil');
  const endBadge = $('#end-badge');
  const subtitle = $('#subtitle');
  const subZh = $('#sub-zh');
  const subJa = $('#sub-ja');
  const btnMode = $('#btn-mode');
  const btnSfx = $('#btn-sfx');
  const btnReplay = $('#btn-replay');

  if (assets.line) imgLine.src = assets.line;
  if (assets.neon) imgNeon.src = assets.neon;
  if (assets.closed) imgClosed.src = assets.closed;
  if (assets.open) imgOpen.src = assets.open;
  if (assets.climax) imgClimax.src = assets.climax;

  const fx = new CelesteFX($('#fx'));
  const sfx = new CelesteSfx(assets.chantAudio || null);

  let startTime = performance.now();
  let pinnedAt = null;
  let currentT = 0;
  let finished = false;
  let fading = false;
  let fullBrightMode = false;
  let fadeBlend = 0;
  let mx = 0, my = 0, smx = 0, smy = 0;

  const syncButtons = () => {
    if (btnSfx) btnSfx.textContent = sfx.enabled ? '咏唱 开' : '咏唱 关';
    if (btnMode) btnMode.textContent = fullBrightMode ? '柔暗收尾' : '全亮观赏';
  };

  const applyFrame = (t) => {
    currentT = t;
    const isFading = t >= 10.6;
    if (isFading && !fading) {
      fading = true;
      stage.classList.add('is-fading');
    } else if (!isFading && fading) {
      fading = false;
      stage.classList.remove('is-fading');
    }

    const done = t >= INTRO_DURATION;
    if (done && !finished) {
      finished = true;
      stage.classList.add('is-done');
    } else if (!done && finished) {
      finished = false;
      stage.classList.remove('is-done');
    }

    const pxActive = smooth(9.2, 11.2, t);
    smx += (mx * pxActive - smx) * 0.08;
    smy += (my * pxActive - smy) * 0.08;

    const outroCurve = smoother(10.90, 13.50, t);
    const targetFade = fullBrightMode ? 0 : outroCurve;
    if (pinnedAt !== null) {
      fadeBlend = targetFade;
    } else {
      fadeBlend += (targetFade - fadeBlend) * 0.075;
    }

    const breathScale =
      1.0 +
      0.011 * Math.sin(t * 0.785) * smooth(1.5, 4.5, t) +
      0.018 * (1 - smooth(0, 9.2, t)) +
      0.042 * fadeBlend;
    const dx = -smx * 10 + Math.sin(t * 0.62) * 3.2 * smooth(2.0, 5.0, t);
    const dy = -smy * 7 + Math.cos(t * 0.52) * 2.2 * smooth(2.0, 5.0, t);
    cam.style.transform = `translate3d(${dx.toFixed(2)}px,${dy.toFixed(2)}px,0) scale(${breathScale.toFixed(4)})`;

    cam.style.opacity = (1 - fadeBlend * 0.86).toFixed(3);
    cam.style.filter = fadeBlend > 0.004
      ? `brightness(${(1 - fadeBlend * 0.32).toFixed(3)}) blur(${(fadeBlend * 2.8).toFixed(2)}px)`
      : 'none';
    if (endVeil) endVeil.style.opacity = (fadeBlend * 0.92).toFixed(3);

    const badgeCurve = smoother(12.35, 13.60, t);
    const badgeOp = badgeCurve * fadeBlend;
    if (endBadge) {
      endBadge.style.opacity = badgeOp.toFixed(3);
      endBadge.style.transform = `translate3d(-50%,calc(-50% + ${((1 - badgeCurve) * 8).toFixed(1)}px),0)`;
    }

    fx.render(t, smx, smy, fadeBlend);
    sfx.cue(t);

    const rPct = 17 + smooth(0.0, 1.75, t) * 14 + smooth(1.75, 3.55, t) * 95;
    const maskVal = `radial-gradient(circle at 64% 42%, #000 ${Math.max(0, rPct - 12).toFixed(1)}%, transparent ${rPct.toFixed(1)}%)`;

    const opLine = smooth(0.08, 0.75, t) * (1 - smooth(2.8, 4.2, t));
    imgLine.style.opacity = opLine.toFixed(3);
    imgLine.style.webkitMaskImage = maskVal;
    imgLine.style.maskImage = maskVal;

    const opNeon = smooth(1.70, 3.30, t) * (1 - smooth(3.85, 4.85, t));
    imgNeon.style.opacity = opNeon.toFixed(3);
    imgNeon.style.webkitMaskImage = maskVal;
    imgNeon.style.maskImage = maskVal;

    const opClosed = smooth(3.80, 4.75, t) * (1 - smooth(6.40, 7.20, t));
    imgClosed.style.opacity = opClosed.toFixed(3);

    const opOpen = smooth(6.40, 7.15, t) * (1 - smooth(8.35, 9.30, t));
    imgOpen.style.opacity = opOpen.toFixed(3);

    const opClimax = smooth(8.30, 9.25, t);
    imgClimax.style.opacity = opClimax.toFixed(3);

    // Synchronized Bilingual Water-Magic Chant Subtitles
    const s1 = timing.s1;
    const s2 = timing.s2;
    const opS1 = smooth(s1.in, s1.in + 0.35, t) * (1 - smooth(s1.out - 0.35, s1.out, t));
    const opS2 = smooth(s2.in, s2.in + 0.35, t) * (1 - smoother(s2.out - 0.40, s2.out, t));
    if (opS1 > 0.005) {
      subZh.textContent = s1.zh;
      subJa.textContent = s1.ja || '';
      subtitle.style.opacity = opS1.toFixed(3);
      subtitle.style.transform = `translate3d(-50%,${((1 - smooth(s1.in, s1.in + 0.35, t)) * 6).toFixed(1)}px,0)`;
    } else if (opS2 > 0.005) {
      subZh.textContent = s2.zh;
      subJa.textContent = s2.ja || '';
      subtitle.style.opacity = opS2.toFixed(3);
      subtitle.style.transform = `translate3d(-50%,${((1 - smooth(s2.in, s2.in + 0.35, t)) * 6 - smoother(s2.out - 0.40, s2.out, t) * 5).toFixed(1)}px,0)`;
    } else {
      subtitle.style.opacity = '0';
    }
  };

  const skipToEnd = () => {
    pinnedAt = null;
    sfx.stop();
    startTime = performance.now() - INTRO_DURATION * 1000;
    applyFrame(INTRO_DURATION);
  };

  const replay = () => {
    pinnedAt = null;
    finished = false;
    fading = false;
    fullBrightMode = false;
    fadeBlend = 0;
    stage.classList.remove('is-done', 'is-fading');
    syncButtons();
    startTime = performance.now();
    sfx.reset();
    applyFrame(0);
  };

  window.addEventListener('pointermove', (e) => {
    mx = (e.clientX / Math.max(1, window.innerWidth) - 0.5) * 2;
    my = (e.clientY / Math.max(1, window.innerHeight) - 0.5) * 2;
  });

  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' || e.key === ' ' || e.key === 'Enter') {
      if (!finished) skipToEnd();
    } else if (e.key === 'r' || e.key === 'R') {
      replay();
    }
  });

  stage.addEventListener('click', (e) => {
    if (e.target.closest('#controls')) return;
    if (sfx.blocked && !finished) {
      sfx.unlock(currentT);
      return;
    }
    if (!finished) skipToEnd();
  });

  if (btnMode) {
    btnMode.addEventListener('click', (e) => {
      e.stopPropagation();
      fullBrightMode = !fullBrightMode;
      syncButtons();
    });
  }
  if (btnSfx) {
    btnSfx.addEventListener('click', (e) => {
      e.stopPropagation();
      sfx.toggle(currentT);
      syncButtons();
    });
  }
  if (btnReplay) {
    btnReplay.addEventListener('click', (e) => {
      e.stopPropagation();
      replay();
    });
  }

  window.__ROXY_SEEK__ = (sec) => {
    pinnedAt = sec;
    applyFrame(sec);
  };

  const params = new URLSearchParams(window.location.search);
  if (params.has('t')) {
    pinnedAt = parseFloat(params.get('t')) || 0;
  }

  syncButtons();
  const loop = (now) => {
    const t = pinnedAt !== null ? pinnedAt : (now - startTime) / 1000;
    applyFrame(t);
    requestAnimationFrame(loop);
  };
  requestAnimationFrame(loop);
}
