/**
 * beats.ts — 拍点网格（数据来自实测：scripts/beat-grid.cjs）
 *
 * 旧版本用「BPM=108 + 固定相位」的公式推算，实测真实速度是 107.07 BPM，
 * 且两遍拼接处存在相位跳变；公式网格到片尾会偏差 5 秒以上，导致卡点不准。
 * 现在直接用实测拍点表 BEAT_TIMES（634 拍），每拍的屏幕时间都是真实鼓点。
 */
import {BEAT_TIMES, BPM_MEASURED, PERIOD_MEASURED, SEAM_TIME} from './beatGrid.generated';

export const BPM = BPM_MEASURED;          // 实测 ≈107.07
export const PERIOD = PERIOD_MEASURED;    // 实测 ≈0.56040s
export const PHASE = BEAT_TIMES[0];       // 首拍 ≈0.403s
export const DURATION = 355.2;
export const SEAM = SEAM_TIME;            // 歌曲第二遍拼接位置
export const BEAT_COUNT = BEAT_TIMES.length;

const clampIdx = (i: number) => Math.max(0, Math.min(BEAT_TIMES.length - 1, Math.round(i)));

/** 第 i 拍的真实时间（秒） */
export const beatTime = (i: number) => BEAT_TIMES[clampIdx(i)];

/** 第 i 拍对应的帧号 */
export const beatFrame = (i: number, fps: number) => Math.round(beatTime(i) * fps);

/** 第 bar 小节（4/4）起始拍 */
export const barBeat = (bar: number) => bar * 4;
export const barTime = (bar: number) => beatTime(barBeat(bar));
export const barFrame = (bar: number, fps: number) => beatFrame(barBeat(bar), fps);

/** 距最近拍点的时间差（秒，正数表示在拍点之后） */
export const beatOffset = (t: number) => t - beatTime(nearestBeat(t));

/** 最接近时间 t 的拍序号（二分查找，网格非等距也能用） */
export const nearestBeat = (t: number) => {
  let lo = 0, hi = BEAT_TIMES.length - 1;
  while (lo < hi) {
    const mid = (lo + hi) >> 1;
    if (BEAT_TIMES[mid] < t) lo = mid + 1; else hi = mid;
  }
  const a = Math.max(0, lo - 1), b = lo;
  return Math.abs(BEAT_TIMES[a] - t) <= Math.abs(BEAT_TIMES[b] - t) ? a : b;
};

/** 从第 i 拍起经过 beats 拍后的时间进度 0..1 */
export const sinceBeat = (t: number, i: number, beats = 1) => {
  const start = beatTime(i);
  const end = beatTime(i + beats);
  return Math.min(1, Math.max(0, (t - start) / Math.max(1e-6, end - start)));
};

/** 拍点冲击包络：在拍点瞬间为 1，随后按 decayBeats 衰减到 0 */
export const hit = (t: number, i: number, decayBeats = 0.6) => {
  const dt = t - beatTime(i);
  if (dt < 0) return 0;
  const d = PERIOD * decayBeats;
  const k = Math.max(0, 1 - dt / d);
  return k * k;
};

/** 拍点强度（用于让强拍更亮）：每 4 拍一个重拍 */
export const isDownbeat = (i: number) => i % 4 === 0;