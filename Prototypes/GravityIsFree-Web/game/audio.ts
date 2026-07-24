export type GravityCue =
  | "jump"
  | "land"
  | "spring"
  | "portal"
  | "death"
  | "paper"
  | "victory";

/**
 * Tiny procedural score used by the prototype. The AudioContext is only
 * created after a player gesture, so importing the game never triggers an
 * autoplay permission prompt.
 */
export class GravityAudio {
  private context: AudioContext | null = null;
  private master: GainNode | null = null;
  private muted = false;
  private readonly reducedMotion: boolean;
  private fanTimer = 0;

  constructor() {
    this.reducedMotion =
      typeof window !== "undefined" &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  }

  setMuted(muted: boolean): void {
    this.muted = muted;
    if (this.master && this.context) {
      this.master.gain.setTargetAtTime(
        muted ? 0 : 0.17,
        this.context.currentTime,
        0.015,
      );
    }
  }

  unlock(): void {
    if (this.muted || typeof window === "undefined") return;

    if (!this.context) {
      const AudioContextClass =
        window.AudioContext ??
        (
          window as typeof window & {
            webkitAudioContext?: typeof AudioContext;
          }
        ).webkitAudioContext;

      if (!AudioContextClass) return;
      this.context = new AudioContextClass();
      this.master = this.context.createGain();
      this.master.gain.value = 0.17;
      this.master.connect(this.context.destination);
    }

    if (this.context.state === "suspended") {
      void this.context.resume();
    }
  }

  cue(cue: GravityCue): void {
    if (this.muted) return;
    this.unlock();
    const context = this.context;
    const master = this.master;
    if (!context || !master) return;

    const now = context.currentTime;
    switch (cue) {
      case "jump":
        this.tone(190, 305, 0.09, "sine", 0.2, now);
        break;
      case "land":
        this.noise(0.045, 0.06, now, 760);
        break;
      case "spring":
        this.tone(150, 560, 0.18, "triangle", 0.3, now);
        break;
      case "portal":
        this.tone(330, 660, 0.22, "sine", 0.22, now);
        this.tone(495, 825, 0.19, "triangle", 0.12, now + 0.045);
        break;
      case "death":
        this.noise(0.34, 0.2, now, 1450);
        this.tone(175, 58, 0.32, "sawtooth", 0.12, now);
        break;
      case "paper":
        if (!this.reducedMotion) this.noise(0.2, 0.08, now, 2400);
        break;
      case "victory":
        [0, 0.11, 0.22, 0.36].forEach((delay, index) => {
          const notes = [330, 440, 550, 660];
          this.tone(
            notes[index],
            notes[index] * 1.04,
            0.24,
            "triangle",
            0.18,
            now + delay,
          );
        });
        break;
    }
  }

  /**
   * A very quiet, throttled puff makes fan fields legible without becoming a
   * constant oscillator that has to be managed across level rebuilds.
   */
  fanPuff(nowMs: number): void {
    if (this.muted || nowMs < this.fanTimer) return;
    this.fanTimer = nowMs + 430;
    this.unlock();
    if (this.context) {
      this.noise(0.12, 0.018, this.context.currentTime, 520);
    }
  }

  destroy(): void {
    const context = this.context;
    this.context = null;
    this.master = null;
    if (context) void context.close();
  }

  private tone(
    fromHz: number,
    toHz: number,
    duration: number,
    type: OscillatorType,
    volume: number,
    at: number,
  ): void {
    const context = this.context;
    const master = this.master;
    if (!context || !master) return;

    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = type;
    oscillator.frequency.setValueAtTime(fromHz, at);
    oscillator.frequency.exponentialRampToValueAtTime(
      Math.max(20, toHz),
      at + duration,
    );
    gain.gain.setValueAtTime(0.0001, at);
    gain.gain.exponentialRampToValueAtTime(volume, at + 0.012);
    gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);
    oscillator.connect(gain);
    gain.connect(master);
    oscillator.start(at);
    oscillator.stop(at + duration + 0.025);
  }

  private noise(
    duration: number,
    volume: number,
    at: number,
    lowpassHz: number,
  ): void {
    const context = this.context;
    const master = this.master;
    if (!context || !master) return;

    const length = Math.max(1, Math.floor(context.sampleRate * duration));
    const buffer = context.createBuffer(1, length, context.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < length; i += 1) {
      data[i] = (Math.random() * 2 - 1) * (1 - i / length);
    }

    const source = context.createBufferSource();
    const filter = context.createBiquadFilter();
    const gain = context.createGain();
    filter.type = "lowpass";
    filter.frequency.value = lowpassHz;
    gain.gain.setValueAtTime(volume, at);
    gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);
    source.buffer = buffer;
    source.connect(filter);
    filter.connect(gain);
    gain.connect(master);
    source.start(at);
    source.stop(at + duration + 0.025);
  }
}
