// spectacle.tsx — 「产品舞台」视觉层：镜面倒影、扫光、景深粒子、3D 挤出字、转台、
// 擦除对比、取色飞出、微距放大镜、光标拖尾、拍点网格涟漪、光斑暗角、截图卡层叠。
// 全部为帧驱动、确定性可复现（不使用 Math.random）。
import React from 'react';
import {AbsoluteFill, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {COLOR, FONT} from '../spec';
import {clamp, easeOut} from './fx';

const PERIOD = 60 / 108;

/** 镜面地板：卡片下方的倒影 + 柔光投影，让主体"站"在舞台上。 */
export const ReflectFloor: React.FC<{
  x: number; y: number; w: number; h: number; opacity?: number; tint?: string;
}> = ({x, y, w, h, opacity = 0.5, tint = '#7ED957'}) => {
  const f = useCurrentFrame();
  const breathe = 1 + Math.sin(f / 40) * 0.015;
  return (
    <div style={{position: 'absolute', left: x, top: y, width: w, height: h, pointerEvents: 'none'}}>
      {/* 主体正下方的柔光投影 */}
      <div style={{
        position: 'absolute', left: w * 0.08, top: 0, width: w * 0.84, height: h * 0.9,
        borderRadius: '50%',
        background: `radial-gradient(ellipse at 50% 0%, rgba(0,0,0,0.55), transparent 70%)`,
        filter: 'blur(26px)', opacity: opacity * 0.9,
        transform: `scale(${breathe})`,
      }}/>
      {/* 地面反射光带 */}
      <div style={{
        position: 'absolute', left: 0, top: h * 0.42, width: w, height: h * 0.58,
        background: `linear-gradient(180deg, ${tint}22 0%, ${tint}0d 40%, transparent 100%)`,
        opacity: opacity * 0.55,
        maskImage: 'linear-gradient(180deg, rgba(0,0,0,0.9), transparent)',
        WebkitMaskImage: 'linear-gradient(180deg, rgba(0,0,0,0.9), transparent)',
      }}/>
    </div>
  );
};

/** 斜向扫光：按拍点掠过一次，给金属表面高光。 */
export const LightSweep: React.FC<{
  delay?: number; periodBeats?: number; strength?: number; angle?: number;
}> = ({delay = 0, periodBeats = 8, strength = 0.16, angle = 112}) => {
  const f = useCurrentFrame();
  const span = periodBeats * PERIOD * 30;
  const local = (f - delay) % span;
  if (f < delay) return null;
  const p = local / span;
  const pos = interpolate(p, [0, 0.42, 1], [-40, 130, 130], clamp);
  const op = interpolate(p, [0, 0.12, 0.42, 1], [0, strength, strength, 0], clamp);
  return (
    <AbsoluteFill style={{pointerEvents: 'none', mixBlendMode: 'screen', opacity: op}}>
      <AbsoluteFill style={{
        background: `linear-gradient(${angle}deg, transparent ${pos - 18}%, rgba(255,255,255,0.85) ${pos}%, transparent ${pos + 18}%)`,
      }}/>
    </AbsoluteFill>
  );
};

/** 景深粒子：确定性分布，近处大而亮、远处小而暗。 */
export const ParticleField: React.FC<{count?: number; energy?: number; color?: string}> = ({
  count = 220, energy = 1, color = '#9EF07A',
}) => {
  const f = useCurrentFrame();
  const dust = Array.from({length: count}, (_, i) => {
    const a = i * 0.61803398875;
    const seedX = (a % 1) * 100;
    const seedY = (((i * 0.7548776662) % 1) * 100);
    const depth = 0.25 + ((i * 0.3141592653) % 1) * 0.75;
    const drift = Math.sin((f / 90) + i) * (6 * depth);
    const rise = ((f * (0.15 + depth * 0.25)) + i * 7) % 100;
    const size = 1.4 + depth * 4.2;
    const op = (0.10 + depth * 0.5) * energy;
    return {x: seedX + drift / 12, y: 100 - rise, size, op};
  });
  return (
    <AbsoluteFill style={{pointerEvents: 'none'}}>
      {dust.map((d, i) => (
        <div key={i} style={{
          position: 'absolute', left: `${d.x}%`, top: `${d.y}%`,
          width: d.size, height: d.size, borderRadius: 999,
          background: color, opacity: d.op,
          boxShadow: `0 0 ${d.size * 3}px ${color}`,
        }}/>
      ))}
    </AbsoluteFill>
  );
};

/** 3D 挤出文字：多层错位阴影模拟厚度，再叠金属渐变。 */
export const ExtrudeText: React.FC<{
  children: React.ReactNode; size?: number; delay?: number; depth?: number;
  tint?: 'silver' | 'gold' | 'green' | 'blue'; tilt?: number; align?: 'center' | 'left';
}> = ({children, size = 120, delay = 0, depth = 8, tint = 'silver', tilt = 0, align = 'center'}) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: f - delay, fps, config: {damping: 15, mass: 0.7, stiffness: 160}});
  const o = interpolate(f, [delay, delay + 5], [0, 1], clamp);
  const sc = 0.76 + p * 0.24;
  const grads: Record<string, string> = {
    silver: 'linear-gradient(180deg,#FFFFFF 0%,#F5F5F7 18%,#AEAEB2 36%,#FFFFFF 52%,#7C7C80 72%,#E5E5EA 90%,#FFFFFF 100%)',
    gold: 'linear-gradient(180deg,#FFF8E1 0%,#FFD60A 30%,#FFF4B0 50%,#B8860B 74%,#FFE680 100%)',
    green: 'linear-gradient(180deg,#F2FFF2 0%,#7ED957 28%,#ECFFE8 50%,#2DB84D 74%,#B8F5A0 100%)',
    blue: 'linear-gradient(180deg,#F0FAFF 0%,#64D2FF 28%,#F5FCFF 50%,#007AFF 74%,#B8EAFF 100%)',
  };
  const layers = Array.from({length: depth}, (_, i) => depth - i);
  return (
    <div style={{position: 'relative', display: 'inline-block', opacity: o,
      transform: `perspective(900px) rotateX(${tilt}deg) scale(${sc})`, transformStyle: 'preserve-3d'}}>
      {layers.map(i => (
        <div key={i} aria-hidden style={{
          position: 'absolute', left: 0, top: 0, width: '100%',
          fontFamily: FONT, fontSize: size, fontWeight: 800, letterSpacing: -3, lineHeight: 1,
          textAlign: align, whiteSpace: 'pre-line',
          color: `rgba(${18 + i * 2},${20 + i * 2},${24 + i * 3},${0.16 + (depth - i) * 0.02})`,
          transform: `translateZ(-${i * 1.2}px) translate(${i * 0.6}px, ${i * 0.9}px)`,
          filter: 'blur(0.3px)',
        }}>{children}</div>
      ))}
      <div style={{
        position: 'relative', fontFamily: FONT, fontSize: size, fontWeight: 800, letterSpacing: -3,
        lineHeight: 1, textAlign: align, whiteSpace: 'pre-line',
        backgroundImage: grads[tint], backgroundClip: 'text', WebkitBackgroundClip: 'text',
        color: 'transparent', WebkitTextStroke: '0.7px rgba(255,255,255,0.12)',
        filter: 'drop-shadow(0 14px 44px rgba(0,0,0,0.55))',
        textShadow: 'none',
      }}>{children}</div>
    </div>
  );
};

/** 转台：绕 Y 轴连续旋转，配合倒影使用。 */
export const Turntable: React.FC<{
  children: React.ReactNode; speed?: number; delay?: number; amp?: number;
}> = ({children, speed = 34, delay = 0, amp = 1}) => {
  const f = useCurrentFrame();
  const t = Math.max(0, f - delay);
  const ry = Math.sin((t / 30) * (speed / 34)) * 22 * amp;
  const rx = Math.sin((t / 47)) * 4 * amp;
  return (
    <div style={{position: 'absolute', inset: 0, transformStyle: 'preserve-3d',
      transform: `rotateY(${ry}deg) rotateX(${rx}deg)`}}>
      {children}
    </div>
  );
};

/** 擦除对比：左侧原图、右侧新图，擦除线随拍点推进。 */
export const SplitCompare: React.FC<{
  leftFile: string; rightFile: string; x: number; y: number; w: number; h: number;
  from?: number; to?: number; startFrame: number; endFrame: number; labelLeft?: string; labelRight?: string;
}> = ({leftFile, rightFile, x, y, w, h, from = 12, to = 88, startFrame, endFrame, labelLeft = '光影关闭', labelRight = '光影开启'}) => {
  const f = useCurrentFrame();
  const p = interpolate(f, [startFrame, endFrame], [0, 1], clamp);
  const wipe = interpolate(p, [0, 1], [from, to], clamp);
  return (
    <div style={{position: 'absolute', left: x, top: y, width: w, height: h, overflow: 'hidden',
      borderRadius: 18, border: `1px solid ${COLOR.line}`, boxShadow: '0 40px 90px rgba(0,0,0,0.75)'}}>
      <Img src={staticFile(`shots-v4/${leftFile}`)} style={{position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top left'}}/>
      <div style={{position: 'absolute', inset: 0, clipPath: `inset(0 ${100 - wipe}% 0 0)`}}>
        <Img src={staticFile(`shots-v4/${rightFile}`)} style={{width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top left'}}/>
      </div>
      {/* 擦除线 */}
      <div style={{position: 'absolute', top: 0, bottom: 0, left: `${wipe}%`, width: 3,
        background: `linear-gradient(180deg, ${COLOR.accent}, #fff, ${COLOR.accent})`,
        boxShadow: `0 0 26px ${COLOR.accent}`}}/>
      <Tag2 text={labelLeft} side="left"/>
      <Tag2 text={labelRight} side="right"/>
    </div>
  );
};

const Tag2: React.FC<{text: string; side: 'left' | 'right'}> = ({text, side}) => (
  <div style={{position: 'absolute', bottom: 14, [side]: 16, padding: '7px 14px', borderRadius: 999,
    background: 'rgba(0,0,0,0.62)', border: `1px solid ${COLOR.line}`,
    fontFamily: FONT, fontSize: 17, fontWeight: 600, color: COLOR.ink}}>{text}</div>
);

/** 取色飞出：从壁纸裁出的色卡依次飞出并环绕。 */
export const SwatchExtractor: React.FC<{
  colors: string[]; x: number; y: number; delay?: number; radius?: number;
}> = ({colors, x, y, delay = 0, radius = 150}) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  return (
    <AbsoluteFill style={{pointerEvents: 'none'}}>
      {colors.map((c, i) => {
        const d = delay + i * 4;
        const p = spring({frame: f - d, fps, config: {damping: 14, stiffness: 130}});
        if (f < d) return null;
        const ang = (i / colors.length) * Math.PI * 2 - Math.PI / 2;
        const cx = x + Math.cos(ang) * radius * p;
        const cy = y + Math.sin(ang) * radius * p;
        return (
          <div key={c + i} style={{
            position: 'absolute', left: cx - 17, top: cy - 17, width: 34, height: 34, borderRadius: 10,
            background: c, opacity: interpolate(p, [0, 1], [0, 1], clamp),
            transform: `scale(${0.4 + p * 0.7}) rotate(${(1 - p) * -45}deg)`,
            boxShadow: `0 0 22px ${c}, 0 10px 26px rgba(0,0,0,0.6)`,
            border: '2px solid rgba(255,255,255,0.85)',
          }}/>
        );
      })}
    </AbsoluteFill>
  );
};

/** 微距放大镜：圆形镜片放大 UI 局部，带描边与十字准心。 */
export const ZoomLens: React.FC<{
  x: number; y: number; r?: number; delay?: number; zoom?: number; label?: string;
}> = ({x, y, r = 120, delay = 0, zoom = 2.2, label}) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: f - delay, fps, config: {damping: 16, stiffness: 140}});
  if (f < delay) return null;
  const s = 0.6 + p * 0.4;
  return (
    <div style={{position: 'absolute', left: x - r, top: y - r, width: r * 2, height: r * 2,
      borderRadius: '50%', transform: `scale(${s})`, opacity: interpolate(p, [0, 1], [0, 1], clamp),
      border: `3px solid rgba(255,255,255,0.5)`,
      boxShadow: `0 0 0 6px rgba(0,0,0,0.35), 0 24px 60px rgba(0,0,0,0.6), inset 0 0 40px rgba(255,255,255,0.12)`,
      backdropFilter: `blur(0.5px)`, pointerEvents: 'none'}}>
      <div style={{position: 'absolute', left: '50%', top: 0, bottom: 0, width: 1, background: 'rgba(255,255,255,0.28)'}}/>
      <div style={{position: 'absolute', top: '50%', left: 0, right: 0, height: 1, background: 'rgba(255,255,255,0.28)'}}/>
      <div style={{position: 'absolute', left: '50%', top: '50%', width: 18, height: 18, marginLeft: -9, marginTop: -9,
        borderRadius: 999, background: COLOR.accent, boxShadow: `0 0 20px ${COLOR.accent}`}}/>
      <div style={{position: 'absolute', left: 0, right: 0, bottom: -34, textAlign: 'center',
        fontFamily: FONT, fontSize: 15, fontWeight: 700, color: COLOR.accent, letterSpacing: 1}}>
        {label ?? `${zoom}×`}
      </div>
    </div>
  );
};

/** 光标拖尾：真实录屏里的鼠标之外，再叠一层高亮轨迹，帮助观众锁定操作点。 */
export const CursorTrail: React.FC<{
  from: {x: number; y: number}; to: {x: number; y: number}; at: number; dur: number;
}> = ({from, to, at, dur}) => {
  const f = useCurrentFrame();
  const t = f - at;
  if (t < 0 || t > dur + 16) return null;
  const p = interpolate(t, [0, dur], [0, 1], {...clamp, easing: easeOut});
  const cx = from.x + (to.x - from.x) * p;
  const cy = from.y + (to.y - from.y) * p;
  const o = interpolate(t, [0, 4, dur, dur + 16], [0, 1, 1, 0], clamp);
  const trail = Array.from({length: 7}, (_, i) => i / 7);
  return (
    <AbsoluteFill style={{pointerEvents: 'none'}}>
      {trail.map((q, i) => {
        const tp = Math.max(0, p - q * 0.18);
        const tx = from.x + (to.x - from.x) * tp;
        const ty = from.y + (to.y - from.y) * tp;
        return <div key={i} style={{position: 'absolute', left: tx - 7, top: ty - 7, width: 14, height: 14,
          borderRadius: 999, background: COLOR.accent, opacity: o * (0.5 - q * 0.5),
          filter: 'blur(2px)', boxShadow: `0 0 16px ${COLOR.accent}`}}/>;
      })}
      <div style={{position: 'absolute', left: cx - 20, top: cy - 20, width: 40, height: 40, borderRadius: 999,
        border: `2px solid ${COLOR.accent}`, opacity: o * 0.9, boxShadow: `0 0 26px ${COLOR.accent}`}}/>
    </AbsoluteFill>
  );
};

/** 拍点网格涟漪：以某点为中心扩散的同心圆，用来强调音乐重拍。 */
export const RippleGrid: React.FC<{
  x: number; y: number; at: number; count?: number; color?: string;
}> = ({x, y, at, count = 3, color = COLOR.accent}) => {
  const f = useCurrentFrame();
  return (
    <AbsoluteFill style={{pointerEvents: 'none'}}>
      {Array.from({length: count}, (_, i) => {
        const t = f - at - i * 6;
        if (t < 0 || t > 34) return null;
        const r = interpolate(t, [0, 34], [40, 620], {...clamp, easing: easeOut});
        const o = interpolate(t, [0, 6, 34], [0, 0.42, 0], clamp);
        return <div key={i} style={{position: 'absolute', left: x - r, top: y - r, width: r * 2, height: r * 2,
          borderRadius: 999, border: `2px solid ${color}`, opacity: o,
          boxShadow: `0 0 30px ${color}`}}/>;
      })}
    </AbsoluteFill>
  );
};

/** 景深暗角 + 光斑：收边、突出主体。 */
export const BokehVignette: React.FC<{strength?: number}> = ({strength = 1}) => {
  const f = useCurrentFrame();
  const blobs = [
    {x: 12, y: 22, r: 190, o: 0.07},
    {x: 88, y: 30, r: 240, o: 0.05},
    {x: 74, y: 82, r: 210, o: 0.06},
    {x: 22, y: 76, r: 160, o: 0.05},
  ];
  return (
    <AbsoluteFill style={{pointerEvents: 'none'}}>
      {blobs.map((b, i) => (
        <div key={i} style={{
          position: 'absolute', left: `${b.x}%`, top: `${b.y}%`,
          width: b.r, height: b.r, marginLeft: -b.r / 2, marginTop: -b.r / 2,
          borderRadius: '50%',
          background: `radial-gradient(circle, rgba(255,255,255,${b.o * strength}) 0%, transparent 70%)`,
          filter: 'blur(18px)',
          transform: `scale(${1 + Math.sin((f / 120) + i) * 0.06})`,
        }}/>
      ))}
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 92% 80% at 50% 48%, transparent 42%, rgba(0,0,0,0.62) 100%)', opacity: strength}}/>
    </AbsoluteFill>
  );
};

/** 截图卡层叠视差：最多 5 张卡按深度错开，随拍点轻微位移。 */
export const FrameStack: React.FC<{
  files: string[]; x: number; y: number; w: number; gapX?: number; gapY?: number; delay?: number;
}> = ({files, x, y, w, gapX = 26, gapY = 20, delay = 0}) => {
  const f = useCurrentFrame();
  return (
    <AbsoluteFill style={{pointerEvents: 'none'}}>
      {files.map((file, i) => {
        const depth = files.length - i;
        const p = interpolate(f, [delay + i * 3, delay + i * 3 + 8], [0, 1], clamp);
        const drift = Math.sin((f / 60) + i) * 3;
        return (
          <div key={file + i} style={{
            position: 'absolute', left: x + i * gapX, top: y + i * gapY + drift,
            width: w, opacity: p,
            transform: `translateZ(${-depth * 40}px) rotateY(${depth * -2.4}deg) scale(${1 - depth * 0.02})`,
            transformStyle: 'preserve-3d',
            boxShadow: '0 30px 70px rgba(0,0,0,0.7)',
            borderRadius: 14, overflow: 'hidden', border: `1px solid ${COLOR.line}`,
          }}>
            <Img src={staticFile(`shots-v4/${file}`)} style={{width: '100%', display: 'block', objectFit: 'cover', objectPosition: 'top left'}}/>
          </div>
        );
      })}
    </AbsoluteFill>
  );
};
