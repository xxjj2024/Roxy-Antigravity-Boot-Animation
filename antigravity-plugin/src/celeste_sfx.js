export class CelesteSfx {
  constructor(chantAudioUrl) {
    this.enabled = true;
    this.audio = null;
    this.started = false;
    this.blocked = false;
    if (chantAudioUrl) {
      this.audio = new Audio(chantAudioUrl);
      this.audio.preload = 'auto';
      this.audio.volume = 0.9;
    }
  }

  tryStart(t = 0) {
    if (!this.enabled || !this.audio) return;
    if (t >= 11.8) return;
    try {
      this.audio.currentTime = Math.max(0, Math.min(11.8, t));
      const p = this.audio.play();
      this.started = true;
      if (p && typeof p.catch === 'function') {
        p.catch(() => {
          this.blocked = true;
          this.started = false;
        });
      }
    } catch {
      this.blocked = true;
    }
  }

  unlock(t = 0) {
    if (this.enabled && !this.started && t < 11.5) {
      this.blocked = false;
      this.tryStart(t);
    }
  }

  toggle(t = 0) {
    this.enabled = !this.enabled;
    if (!this.enabled) {
      this.stop();
    } else {
      this.blocked = false;
      this.tryStart(t);
    }
    return this.enabled;
  }

  stop() {
    this.started = false;
    if (this.audio) {
      try {
        this.audio.pause();
        this.audio.currentTime = 0;
      } catch {}
    }
  }

  reset() {
    this.stop();
    if (this.enabled) this.tryStart(0);
  }

  cue(t) {
    if (!this.enabled || !this.audio) return;
    if (!this.started && !this.blocked && t >= 0.02 && t < 11.2) {
      this.tryStart(t);
    }
  }
}
