/**
 * beat.ts — time helpers for the SalmonLauncher promo.
 * Audio: "3 Strikes" — Terror Jr (177.6s). Tempo estimated ~107.7 BPM; structural cuts
 * are placed on a steady beat grid so motion lands with musical regularity.
 * All functions are pure (frame/fps only through callers).
 */
export const AUDIO_FILE = 'audio/track.m4a';
export const AUDIO_DURATION = 177.6; // seconds, ffprobe
export const BPM = 108;              // working grid (close to measured 107.67)
export const BEAT = 60 / BPM;        // seconds per beat
export const BAR = BEAT * 4;         // 4/4 bar

export const f = (seconds: number, fps: number) => Math.round(seconds * fps);
export const s = (frame: number, fps: number) => frame / fps;
export const beatAt = (n: number) => n * BEAT;
export const barAt = (n: number) => n * BAR;
