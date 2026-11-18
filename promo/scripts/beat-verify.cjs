// beat-verify.cjs — 分段复核 BPM/相位，避免倍频误判
const fs = require('fs');
const SR = 22050;
const raw = fs.readFileSync('out/track.raw');
const n = Math.floor(raw.length / 2);
const s = new Float64Array(n);
for (let i = 0; i < n; i++) s[i] = raw.readInt16LE(i * 2) / 32768;

const HOP = 110;                 // 200Hz 包络
const FR = 200;
const frames = Math.floor(n / HOP);

function onsetEnv(t0, t1) {
  const f0 = Math.floor(t0 * FR), f1 = Math.floor(t1 * FR);
  const e = new Float64Array(f1 - f0 + 2);
  for (let f = f0; f < f1; f++) {
    let sum = 0;
    for (let i = 0; i < HOP; i++) { const v = s[f * HOP + i]; sum += v * v; }
    e[f - f0] = Math.sqrt(sum / HOP);
  }
  const flux = new Float64Array(e.length);
  for (let i = 1; i < e.length; i++) flux[i] = Math.max(0, e[i] - e[i - 1]);
  const out = new Float64Array(e.length);
  const W = 200;
  for (let i = 0; i < e.length; i++) {
    let sum = 0, c = 0;
    for (let k = Math.max(0, i - W); k < Math.min(e.length, i + W); k++) { sum += flux[k]; c++; }
    out[i] = Math.max(0, flux[i] - sum / Math.max(1, c));
  }
  return out;
}

function analyze(t0, t1, label) {
  const env = onsetEnv(t0, t1);
  const L = env.length;
  const acf = (lag) => {
    let acc = 0, a = 0, b = 0;
    for (let i = 0; i + lag < L; i++) { acc += env[i] * env[i + lag]; a += env[i] * env[i]; b += env[i + lag] * env[i + lag]; }
    return acc / Math.sqrt(Math.max(1e-9, a * b));
  };
  let best = { bpm: 0, sc: -1 };
  for (let bpm = 60; bpm <= 200; bpm += 0.02) {
    const lag = Math.round((60 / bpm) * FR);
    if (lag < 30 || lag > L / 2) continue;
    const sc = acf(lag);
    if (sc > best.sc) best = { bpm, sc };
  }
  // 检查倍频：若 2x 的分数接近，说明取到了半拍
  const half = { bpm: best.bpm * 2 };
  half.sc = acf(Math.round((60 / half.bpm) * FR));
  const dbl = { bpm: best.bpm / 2 };
  dbl.sc = acf(Math.round((60 / dbl.bpm) * FR));

  const per = 60 / best.bpm;
  let ph = 0, phScore = -1;
  for (let p = 0; p < per; p += 0.0025) {
    let sum = 0, c = 0;
    for (let t = p; t < L / FR - 0.15; t += per) {
      const idx = Math.round(t * FR);
      let pk = 0;
      for (let d = -3; d <= 3; d++) if (idx + d >= 0 && idx + d < L) pk = Math.max(pk, env[idx + d]);
      sum += pk; c++;
    }
    const sc = sum / Math.max(1, c);
    if (sc > phScore) { phScore = sc; ph = p; }
  }
  console.log(`${label}: BPM=${best.bpm.toFixed(2)} (相关 ${best.sc.toFixed(3)}) | 半拍 ${dbl.bpm.toFixed(2)} sc=${dbl.sc.toFixed(3)} | 双拍 ${half.bpm.toFixed(2)} sc=${half.sc.toFixed(3)} | 相位=${ph.toFixed(4)}s 强度=${phScore.toFixed(5)}`);
  return { bpm: best.bpm, period: per, phase: ph, phaseScore: phScore, offset: t0 };
}

console.log('=== 分段分析（200Hz 包络、0.02BPM 分辨率）===');
const seg1 = analyze(8, 172, '第1遍 8–172s ');
const seg2 = analyze(186, 350, '第2遍 186–350s');
const segAll = analyze(8, 350, '全曲 8–350s  ');

// 第二遍的相位换算到绝对时间轴
const ph2abs = (seg2.phase + (0.4164 * 0 + 0)) + 0; // 占位
const absolutePhaseSeg2 = (seg2.phase + 186) % seg2.period;
console.log(`\n第2遍相位（绝对）≈ ${absolutePhaseSeg2.toFixed(4)}s；第1遍相位 ${seg1.phase.toFixed(4)}s`);
console.log(`结论: BPM≈${segAll.bpm.toFixed(2)} 周期=${segAll.period.toFixed(5)}s 相位=${segAll.phase.toFixed(4)}s`);

fs.writeFileSync('out/beats-true.json', JSON.stringify({
  bpm: +segAll.bpm.toFixed(3),
  period: +segAll.period.toFixed(6),
  phase: +segAll.phase.toFixed(4),
  seg1, seg2,
}, null, 2));
console.log('已写出 out/beats-true.json');