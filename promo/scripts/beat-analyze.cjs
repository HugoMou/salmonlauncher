// beat-analyze.cjs — 从原始 PCM 分析真实 BPM 与相位（不用猜测）
// 方法：能量包络（差分流）→ 自相关求周期 → 在周期内搜索使拍点能量最大的相位
const fs = require('fs');

const SR = 22050;
const raw = fs.readFileSync('out/track.raw');
const n = Math.floor(raw.length / 2);
const samples = new Float64Array(n);
for (let i = 0; i < n; i++) samples[i] = raw.readInt16LE(i * 2) / 32768;

// 1) 计算 100Hz 的起始强度包络（每 10ms 一个值）
const hop = Math.round(SR / 100);            // 220 samples per hop
const frames = Math.floor(n / hop);
const energy = new Float64Array(frames);
for (let f = 0; f < frames; f++) {
  let sum = 0;
  const off = f * hop;
  for (let i = 0; i < hop; i++) { const v = samples[off + i]; sum += v * v; }
  energy[f] = Math.sqrt(sum / hop);
}
// 差分（谱通量近似）：只保留能量上升沿，突出鼓点
const flux = new Float64Array(frames);
for (let f = 1; f < frames; f++) flux[f] = Math.max(0, energy[f] - energy[f - 1]);
// 局部去均值
const win = 100; // 1s
const onset = new Float64Array(frames);
for (let f = 0; f < frames; f++) {
  let s = 0, c = 0;
  for (let k = Math.max(0, f - win); k < Math.min(frames, f + win); k++) { s += flux[k]; c++; }
  const m = s / Math.max(1, c);
  onset[f] = Math.max(0, flux[f] - m);
}
const fpsEnv = 100;

// 2) 自相关求 BPM：在 70–180 BPM 区间搜索
function acf(lag) {
  let s = 0, a = 0, b = 0;
  for (let i = 0; i + lag < frames; i++) { s += onset[i] * onset[i + lag]; a += onset[i] * onset[i]; b += onset[i + lag] * onset[i + lag]; }
  return s / Math.sqrt(Math.max(1e-9, a * b));
}
let best = { bpm: 0, score: -1, lag: 0 };
for (let bpm = 70; bpm <= 180; bpm += 0.05) {
  const lag = Math.round((60 / bpm) * fpsEnv);
  if (lag < 20 || lag >= frames) continue;
  const sc = acf(lag);
  if (sc > best.score) best = { bpm, score: sc, lag };
}
// 细分：在该 BPM 附近的亚采样点用抛物线拟合
const bpm0 = best.bpm;
console.log(`自相关最佳: BPM=${bpm0.toFixed(2)} 相关=${best.score.toFixed(3)} lag=${best.lag}帧(${(best.lag / fpsEnv).toFixed(3)}s)`);

// 3) 搜索相位：使所有拍点上的平均 onset 最大
const period = 60 / bpm0;
let bestPhase = 0, bestPhaseScore = -1;
for (let ph = 0; ph < period; ph += 0.005) {
  let sum = 0, cnt = 0;
  for (let t = ph; t < frames / fpsEnv - 0.1; t += period) {
    const f = Math.round(t * fpsEnv);
    let peak = 0;
    for (let d = -2; d <= 2; d++) if (f + d >= 0 && f + d < frames) peak = Math.max(peak, onset[f + d]);
    sum += peak; cnt++;
  }
  const score = sum / Math.max(1, cnt);
  if (score > bestPhaseScore) { bestPhaseScore = score; bestPhase = ph; }
}
console.log(`相位最佳: PHASE=${bestPhase.toFixed(4)}s  平均拍点强度=${bestPhaseScore.toFixed(5)}`);

// 4) 对比当前代码中的设定，估算漂移
const CUR_BPM = 108, CUR_PHASE = 0.4164;
console.log(`\n当前代码设定: BPM=${CUR_BPM} PHASE=${CUR_PHASE}`);
const dur = 355.2;
const driftAt = (t) => {
  const beatsCur = (t - CUR_PHASE) / (60 / CUR_BPM);
  const beatsNew = (t - bestPhase) / period;
  return (beatsCur - beatsNew) * period; // 秒
};
for (const t of [30, 60, 120, 180, 240, 300, 350]) {
  console.log(`  t=${t}s 时，旧网格相对真实网格偏差 ${(driftAt(t) * 1000).toFixed(0)} ms`);
}

// 5) 打印前 40 个真实拍点时间，供代码使用
const beats = [];
for (let i = 0; i < 660; i++) beats.push(+(bestPhase + i * period).toFixed(4));
fs.writeFileSync('out/beats-true.json', JSON.stringify({
  bpm: +bpm0.toFixed(3), period: +period.toFixed(6), phase: +bestPhase.toFixed(4), beats,
}, null, 2));
console.log(`\n已写出 out/beats-true.json（前 8 拍: ${beats.slice(0, 8).join(', ')}）`);