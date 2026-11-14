/**
 * beats.ts — beat grid and cut helpers for "3 Strikes" (177.6s), analyzed ~108 BPM.
 * Use these to snap motion/cuts to music. All times in seconds unless noted.
 */
export const BPM = 108;
export const PERIOD = 60 / BPM; // 0.555...
export const PHASE = 0.4164;    // first beat anchor (s)
export const DURATION = 177.6;

export const beatTime = (i: number) => PHASE + i * PERIOD;
export const beatFrame = (i: number, fps: number) => Math.round(beatTime(i) * fps);
export const barTime = (bar: number) => beatTime(bar * 4); // 4/4
export const barFrame = (bar: number, fps: number) => beatFrame(bar * 4, fps);

/** Total beats covering the track. */
export const BEAT_COUNT = Math.ceil((DURATION - PHASE) / PERIOD) + 1;

/** Progress 0..1 since beat i over `beats` beats. */
export const sinceBeat = (t: number, i: number, beats = 1) => {
  const start = beatTime(i);
  return Math.min(1, Math.max(0, (t - start) / (PERIOD * beats)));
};

/** Nearest beat index for time t. */
export const nearestBeat = (t: number) => Math.round((t - PHASE) / PERIOD);

/** A sharp "hit" envelope peaking on a beat then falling, for flashes/punch. */
export const hit = (t: number, i: number, decayBeats = 0.6) => {
  const dt = t - beatTime(i);
  if (dt < 0) return 0;
  const d = PERIOD * decayBeats;
  return Math.max(0, 1 - dt / d) * Math.max(0, 1 - dt / d);
};
