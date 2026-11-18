// beat-grid.cjs v3 — 快速稳健：全局周期网格搜索 + 分段相位（预计算峰值数组）
const fs = require('fs');

const SR = 22050, HOP = 220, FR = 100;   // 100 Hz 包络，足够对齐
const raw = fs.readFileSync('out/track.raw');
const n = Math.floor(raw.length / 2);
const s = new Float64Array(n);
for (let i = 0; i < n; i++) s[i] = raw.readInt16LE(i * 2) / 32768;

const frames = Math.floor(n / HOP);
const DURATION = 355.2;
const SEAM = 177.5997;

// 能量 + 上升沿包络
const energy = new Float64Array(frames);
for (let f = 0; f < frames; f++) {
  let sum = 0;
  const off = f * HOP;
  for (let i = 0; i < HOP; i++) { const v = s[off + i]; sum += v * v; }
  energy[f] = Math.sqrt(sum / HOP);
}
const flux = new Float64Array(frames);
for (let f = 1; f < frames; f++) flux[f] = Math.max(0, energy[f] - energy[f - 1]);
const env = new Float64Array(frames);
{
  const W = 100;
  let running = 0;
  for (let f = 0; f < frames; f++) {
    running += flux[f];
    if (f - W > 0) running -= flux[f - W - 1];
    const c = Math.min(f, W) + 1;
    env[f] = Math.max(0, flux[f] - running / Math.max(1, c));
  }
}
// 峰值数组：每个位置取 ±2 帧内最大值（≈±20ms）
const peak = new Float64Array(frames);
for (let f = 0; f < frames; f++) {
  let m = 0;
  for (let d = -2; d <= 2; d++) { const i = f + d; if (i >= 0 && i < frames && env[i] > m) m = env[i]; }
  peak[f] = m;
}
const at = (t) => { const i = Math.round(t * FR); return (i >= 0 && i < frames) ? peak[i] : 0; };

// 1) 周期 + 相位联合搜索（两阶段）
function scoreGrid(period, phase, t0, t1) {
  let sum = 0, c = 0;
  let k = Math.ceil((t0 - phase) / period);
  for (let t = phase + k * period; t < t1; t += period) { sum += at(t); c++; }
  return sum / Math.max(1, c);
}
function bestFor(period, t0, t1, step, keep) {
  let best = { ph: 0, sc: -1 };
  for (let ph = 0; ph < period; ph += step) {
    const sc = (scoreGrid(period, ph, t0, t1) + scoreGrid(period, ph, SEAM + 2, DURATION - 2)) / 2;
    if (sc > best.sc) best = { ph, sc };
  }
  return best;
}

// 阶段 A：粗扫周期（0.545–0.585）+ 10ms 相位
let cands = [];
for (let p = 0.5450; p <= 0.5850; p += 0.0002) {
  const b = bestFor(p, 2, SEAM - 2, 0.010);
  cands.push({ p, sc: b.sc });
}
cands.sort((a, b) => b.sc - a.sc);
console.log(`粗扫前 5 周期: ` + cands.slice(0, 5).map(c => `${c.p.toFixed(4)}(${c.sc.toFixed(5)})`).join(' '));

// 阶段 B：对前 3 名做 1ms 相位细化
let best = { p: 0, ph: 0, sc: -1 };
for (const c of cands.slice(0, 3)) {
  const b = bestFor(c.p, 2, SEAM - 2, 0.001);
  if (b.sc > best.sc) best = { p: c.p, ph: b.ph, sc: b.sc };
}
const period = best.p;
console.log(`选定周期=${period.toFixed(5)}s → BPM=${(60 / period).toFixed(3)}  平均强度=${best.sc.toFixed(5)}`);

// 2) 分段拟合相位（各自独立，缝两侧可能不同）
function fitSeg(t0, t1) {
  let b = { ph: 0, sc: -1 };
  for (let ph = 0; ph < period; ph += 0.001) {
    const sc = scoreGrid(period, ph, t0, t1);
    if (sc > b.sc) b = { ph, sc };
  }
  return b;
}
const A = fitSeg(2, SEAM - 2);
const B = fitSeg(SEAM + 2, DURATION - 2);
console.log(`第1段相位=${A.ph.toFixed(4)}s (${A.sc.toFixed(5)})   第2段相位=${B.ph.toFixed(4)}s (${B.sc.toFixed(5)})`);

// 3) 生成拍点
function gen(t0, t1, ph) {
  const out = [];
  let k = Math.ceil((t0 - ph) / period);
  for (let t = ph + k * period; t < t1; t += period) if (t >= t0) out.push(t);
  return out;
}
const beats = [...gen(0, SEAM, A.ph), ...gen(SEAM, DURATION, B.ph)].sort((a, b) => a - b);
console.log(`拍点数=${beats.length} 首=${beats[0].toFixed(4)} 末=${beats[beats.length - 1].toFixed(4)}`);

// 4) 校验
const avg = (list, off = 0) => list.reduce((acc, b) => acc + at(b + off), 0) / Math.max(1, list.length);
const on = avg(beats), off = avg(beats, period / 2);
const ratio = on / Math.max(1e-9, off);
console.log(`拍点能量=${on.toFixed(5)} 反拍能量=${off.toFixed(5)} 比值=${ratio.toFixed(2)}x ${ratio > 1.15 ? '✓ 有效' : '✗ 无效'}`);

const ts = `// 自动生成：scripts/beat-grid.cjs
// 实测 BPM=${(60 / period).toFixed(3)}，周期=${period.toFixed(5)}s；拼接缝 ${SEAM}s 处分段处理相位
// 拍点能量/反拍能量 = ${ratio.toFixed(2)}x（>1.15 视为有效对齐）
// 重新生成：node scripts/beat-grid.cjs
export const BEAT_TIMES: number[] = [${beats.map(b => b.toFixed(4)).join(',')}];
export const BPM_MEASURED = ${(60 / period).toFixed(3)};
export const PERIOD_MEASURED = ${period.toFixed(5)};
export const SEAM_TIME = ${SEAM};
`;
fs.writeFileSync('src/lib/beatGrid.generated.ts', ts);
console.log('已写出 src/lib/beatGrid.generated.ts');