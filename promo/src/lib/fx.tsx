// lib/fx.tsx — premium 3D/metal/glass primitives for beat-synced Apple-keynote motion.
// All visuals are pure functions of frame/time; no randomness outside deterministic helpers.
import React from 'react';
import {AbsoluteFill, Easing, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {COLOR, FONT, toF} from '../spec';
import {beatTime, hit, PERIOD} from './beats';

export const clamp = {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'} as const;
export const easeOut = Easing.bezier(0.16, 1, 0.3, 1);
export const easeInOut = Easing.bezier(0.65, 0, 0.35, 1);
export const snap = Easing.bezier(0.2, 0.9, 0.1, 1);

export const sec = (f: number, fps: number) => f / fps;

/** Beat-synced pulse (0..1) peaking at beat index b. */
export const useBeatHit = (b: number, decay = 0.55) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  return hit(sec(f, fps), b, decay);
};

/** Cinematic black stage with depth fog and subtle moving light. */
export const Stage: React.FC<{children?: React.ReactNode; light?: number}> = ({children, light = 1}) => {
  const f = useCurrentFrame();
  const drift = Math.sin(f / 90) * 2;
  return (
    <AbsoluteFill style={{backgroundColor: '#000', perspective: 2400, overflow: 'hidden'}}>
      <AbsoluteFill style={{
        background: `radial-gradient(ellipse 70% 50% at ${50 + drift}% ${15 + Math.cos(f / 120) * 5}%, rgba(126,217,87,${0.10 * light}), transparent 60%)`,
      }} />
      <AbsoluteFill style={{
        background: `radial-gradient(ellipse 60% 40% at ${20 - drift}% 85%, rgba(100,210,255,${0.07 * light}), transparent 60%)`,
      }} />
      <AbsoluteFill style={{
        background: `radial-gradient(ellipse 50% 30% at 85% 60%, rgba(255,214,10,${0.04 * light}), transparent 55%)`,
      }} />
      {/* vignette */}
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 85% 75% at 50% 50%, transparent 35%, rgba(0,0,0,0.55) 100%)'}} />
      {/* film grain (deterministic pattern) */}
      <AbsoluteFill style={{
        opacity: 0.035, mixBlendMode: 'overlay',
        backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='120'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.8' numOctaves='2' seed='${(f % 7) + 1}'/%3E%3C/filter%3E%3Crect width='120' height='120' filter='url(%23n)' opacity='0.5'/%3E%3C/svg%3E")`,
      }} />
      {children}
    </AbsoluteFill>
  );
};

/** Beat flash overlay for hard cuts/hits. */
export const BeatFlash: React.FC<{beats: number[]; color?: string; intensity?: number}> = ({beats, color = '#fff', intensity = 0.22}) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  const t = sec(f, fps);
  let a = 0;
  for (const b of beats) a = Math.max(a, hit(t, b, 0.35));
  if (a < 0.001) return null;
  return <AbsoluteFill style={{backgroundColor: color, opacity: a * intensity, mixBlendMode: 'screen', pointerEvents: 'none'}} />;
};

/** Metallic gradient text (chrome/steel). */
export const MetalText: React.FC<{
  children: React.ReactNode; size?: number; weight?: number; align?: 'center'|'left'|'right';
  delay?: number; y?: number; tracking?: number; maxWidth?: number; from?: 'bottom'|'top'|'left'|'right'|'scale';
  tint?: 'silver'|'gold'|'green'|'blue';
}> = ({children, size = 100, weight = 800, align = 'center', delay = 0, y = 0, tracking = -3, maxWidth, from = 'bottom', tint = 'silver'}) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: f - delay, fps, config: {damping: 18, mass: 0.7, stiffness: 140}});
  const o = interpolate(f, [delay, delay + 8], [0, 1], clamp);
  const blur = interpolate(p, [0, 1], [16, 0], clamp);
  const dx = from === 'left' ? (1 - p) * -80 : from === 'right' ? (1 - p) * 80 : 0;
  const dy = from === 'bottom' ? (1 - p) * 70 : from === 'top' ? (1 - p) * -70 : 0;
  const sc = from === 'scale' ? 0.85 + p * 0.15 : 0.97 + p * 0.03;
  const grads: Record<string,string> = {
    silver: 'linear-gradient(180deg, #FFFFFF 0%, #F5F5F7 18%, #C7C7CC 38%, #FFFFFF 52%, #8E8E93 72%, #E5E5EA 100%)',
    gold: 'linear-gradient(180deg, #FFF7D6 0%, #FFD60A 30%, #FFF0A6 50%, #B8860B 75%, #FFE680 100%)',
    green: 'linear-gradient(180deg, #F0FFF0 0%, #7ED957 28%, #E8FFE0 48%, #34C759 72%, #A8F08A 100%)',
    blue: 'linear-gradient(180deg, #E8F6FF 0%, #64D2FF 28%, #F0FAFF 48%, #0A84FF 72%, #A8E4FF 100%)',
  };
  const shineX = interpolate(f - delay, [0, 30], [-30, 130], clamp);
  return (
    <div style={{
      fontFamily: FONT, fontSize: size, fontWeight: weight, letterSpacing: tracking, lineHeight: 1.02,
      textAlign: align, maxWidth, whiteSpace: 'pre-line',
      opacity: o, transform: `translate(${dx}px, ${y + dy}px) scale(${sc})`,
      filter: `blur(${blur}px)`,
      backgroundImage: `${grads[tint]}, linear-gradient(110deg, transparent ${shineX - 20}%, rgba(255,255,255,0.85) ${shineX}%, transparent ${shineX + 20}%)`,
      backgroundClip: 'text', WebkitBackgroundClip: 'text', color: 'transparent',
      WebkitTextStroke: '0.5px rgba(255,255,255,0.08)',
    } as React.CSSProperties}>{children}</div>
  );
};

/** White/ink headline. */
export const Big: React.FC<{
  children: React.ReactNode; size?: number; weight?: number; color?: string; delay?: number; y?: number; align?: 'center'|'left'|'right'; tracking?: number; maxWidth?: number;
}> = ({children, size=92, weight=700, color=COLOR.ink, delay=0, y=0, align='center', tracking=-2.5, maxWidth}) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: f - delay, fps, config:{damping:20, mass:0.75, stiffness:130}});
  const o = interpolate(f,[delay,delay+8],[0,1],clamp);
  return <div style={{fontFamily:FONT,fontSize:size,fontWeight:weight,color,letterSpacing:tracking,lineHeight:1.05,textAlign:align,maxWidth,whiteSpace:'pre-line',opacity:o,transform:`translateY(${y + (1-p)*50}px) scale(${0.97+p*0.03})`}}>{children}</div>;
};

export const Sub: React.FC<{children: React.ReactNode; delay?: number; size?: number; color?: string; align?: 'center'|'left'; y?: number}> =
({children, delay=0, size=32, color=COLOR.dim, align='center', y=0}) => {
  const f = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:f-delay,fps,config:{damping:22,stiffness:120}});
  const o = interpolate(f,[delay,delay+10],[0,1],clamp);
  return <div style={{fontFamily:FONT,fontSize:size,fontWeight:500,color,lineHeight:1.4,textAlign:align,opacity:o,transform:`translateY(${y+(1-p)*26}px)`}}>{children}</div>;
};

/** 3D screenshot/device card with metallic bezel and specular sweep. */
export const Shot3D: React.FC<{
  file: string; x: number; y: number; w: number; h?: number; delay?: number;
  rx?: number; ry?: number; rz?: number; z?: number; scale?: number; label?: string; op?: number;
  floatAmp?: number; sweep?: boolean;
}> = ({file, x, y, w, h, delay=0, rx=0, ry=0, rz=0, z=0, scale=1, label, op=1, floatAmp=0, sweep=true}) => {
  const f = useCurrentFrame(); const {fps} = useVideoConfig();
  const p = spring({frame: f - delay, fps, config:{damping:15, mass:0.8, stiffness:100}});
  const o = interpolate(f,[delay,delay+7],[0,1],clamp);
  const enterZ = interpolate(p,[0,1],[-520,0]);
  const enterY = interpolate(p,[0,1],[90,0]);
  const enterRx = interpolate(p,[0,1],[22,0]);
  const floatY = floatAmp ? Math.sin((f-delay)/36) * floatAmp : 0;
  const shine = interpolate(f-delay,[0,40],[ -40, 140],{...clamp, easing:easeOut});
  const hh = h ?? Math.round(w * 0.72);
  return (
    <div style={{
      position:'absolute', left:x, top:y, width:w, height:hh, transformStyle:'preserve-3d',
      transform:`translateZ(${z+enterZ}px) translateY(${enterY+floatY}px) rotateX(${rx+enterRx}deg) rotateY(${ry}deg) rotateZ(${rz}deg) scale(${scale*(0.9+p*0.1)})`,
      opacity: o*op,
    }}>
      {/* metallic bezel */}
      <div style={{
        position:'absolute', inset:-3, borderRadius:26,
        background:'linear-gradient(135deg,#F5F5F7 0%,#8E8E93 30%,#E5E5EA 50%,#636366 75%,#D1D1D6 100%)',
        boxShadow:'0 50px 120px rgba(0,0,0,0.8), 0 2px 0 rgba(255,255,255,0.15) inset',
      }}/>
      <div style={{position:'absolute', inset:0, borderRadius:23, overflow:'hidden', background:'#111', border:'1px solid rgba(0,0,0,0.5)'}}>
        <Img src={staticFile(`shots/${file}`)} style={{width:'100%', height:'100%', objectFit:'cover', objectPosition:'top left'}} />
        {sweep && <div style={{position:'absolute', inset:0, background:`linear-gradient(115deg, transparent ${shine-25}%, rgba(255,255,255,0.18) ${shine}%, transparent ${shine+25}%)`, mixBlendMode:'screen'}}/>}
        <div style={{position:'absolute', inset:0, boxShadow:'inset 0 0 0 1px rgba(255,255,255,0.08)'}}/>
      </div>
      {label && <div style={{
        position:'absolute', left:20, bottom:-16, padding:'8px 18px', borderRadius:999,
        background:'rgba(28,28,30,0.78)', backdropFilter:'blur(24px)', border:`1px solid ${COLOR.line}`,
        color:COLOR.ink, fontFamily:FONT, fontSize:18, fontWeight:600, boxShadow:'0 8px 30px rgba(0,0,0,0.4)',
      }}>{label}</div>}
    </div>
  );
};

/** Glass pill/tag. */
export const Tag: React.FC<{text: string; x?: number; y?: number; delay?: number; color?: string; accent?: boolean}> =
({text, x=0, y=0, delay=0, color=COLOR.ink, accent}) => {
  const f = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:f-delay,fps,config:{damping:16,stiffness:150}});
  const o = interpolate(f,[delay,delay+8],[0,1],clamp);
  return <div style={{
    position: x||y ? 'absolute':'relative', left:x, top:y,
    padding:'10px 22px', borderRadius:999,
    background: accent ? 'rgba(126,217,87,0.14)' : COLOR.glass,
    border: `1px solid ${accent ? 'rgba(126,217,87,0.4)' : COLOR.line}`,
    color: accent ? COLOR.accent : color, fontFamily:FONT, fontSize:22, fontWeight:600,
    opacity:o, transform:`scale(${0.8+p*0.2}) translateY(${(1-p)*12}px)`,
    backdropFilter:'blur(18px)', boxShadow:'0 10px 30px rgba(0,0,0,0.25)',
  }}>{text}</div>;
};

/** Full-frame kinetic text slam. */
export const Slam: React.FC<{children: React.ReactNode; delay?: number; size?: number; tint?: 'silver'|'gold'|'green'|'blue'}> =
({children, delay=0, size=140, tint='silver'}) => (
  <AbsoluteFill style={{justifyContent:'center', alignItems:'center'}}>
    <MetalText size={size} delay={delay} from="scale" tint={tint}>{children}</MetalText>
  </AbsoluteFill>
);

/** Animated divider/scan line. */
export const ScanLine: React.FC<{delay?: number; y?: number; color?: string}> = ({delay=0, y=540, color=COLOR.accent}) => {
  const f = useCurrentFrame();
  const w = interpolate(f,[delay,delay+18],[0,1920],{...clamp,easing:easeOut});
  const o = interpolate(f,[delay,delay+6,delay+30,delay+45],[0,1,1,0],clamp);
  return <div style={{position:'absolute', left:(1920-w)/2, top:y, width:w, height:2, background:`linear-gradient(90deg,transparent,${color},transparent)`, opacity:o, boxShadow:`0 0 30px ${color}`}}/>;
};

/** Persistent legal strip (small). */
export const LegalStrip: React.FC = () => {
  const f = useCurrentFrame();
  const o = interpolate(f,[30,50],[0,0.55],clamp);
  return (
    <div style={{position:'absolute', right:24, bottom:14, maxWidth:980, textAlign:'right', fontFamily:FONT, fontSize:12.5, lineHeight:1.4, color:'rgba(245,245,247,0.48)', opacity:o}}>
      SalmonLauncher · GPL-3.0 · 制作 Hugo Mou · 配乐 “3 Strikes” — Terror Jr (Bilibili) · 画面为 2.0.13 真实截图（全新默认状态）
      <div style={{fontSize:11, color:'rgba(245,245,247,0.32)', marginTop:2}}>Minecraft 为 Mojang Studios/Microsoft 商标，与本项目无官方关联；Modrinth/Campfire 等归各自权利人。仅作产品展示。</div>
    </div>
  );
};

/** Camera rig: applies 3D transform to children for cinematic moves. */
export const Camera: React.FC<{children: React.ReactNode; tx?: number; ty?: number; tz?: number; rx?: number; ry?: number; rz?: number; scale?: number}> =
({children, tx=0, ty=0, tz=0, rx=0, ry=0, rz=0, scale=1}) => (
  <AbsoluteFill style={{perspective: 2200, transformStyle: 'preserve-3d'}}>
    <AbsoluteFill style={{transformStyle:'preserve-3d', transform:`translate3d(${tx}px,${ty}px,${tz}px) rotateX(${rx}deg) rotateY(${ry}deg) rotateZ(${rz}deg) scale(${scale})`}}>
      {children}
    </AbsoluteFill>
  </AbsoluteFill>
);

/** Hard beat-driven cut wrapper with flash. */
export const Cut: React.FC<{from: number; dur: number; children: React.ReactNode; hitBeat?: number}> = ({from, dur, children, hitBeat}) => {
  const f = useCurrentFrame();
  const local = f - from;
  if (local < 0 || local >= dur) return null;
  const inF = interpolate(local,[0,4],[0,1],clamp);
  const outF = interpolate(local,[dur-4,dur],[1,0],clamp);
  const flash = hitBeat !== undefined ? Math.max(0, 1 - local / 6) * 0.18 : 0;
  return (
    <AbsoluteFill style={{opacity: inF * outF}}>
      {children}
      {flash > 0.001 && <AbsoluteFill style={{backgroundColor: '#fff', opacity: flash, mixBlendMode: 'screen'}} />}
    </AbsoluteFill>
  );
};

/** QR card with metallic frame and label. */
export const QRCard: React.FC<{file: string; title: string; url: string; x: number; y: number; delay?: number; size?: number}> =
({file, title, url, x, y, delay=0, size=190}) => {
  const f = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:f-delay,fps,config:{damping:16,stiffness:110}});
  const o = interpolate(f,[delay,delay+8],[0,1],clamp);
  return (
    <div style={{position:'absolute', left:x, top:y, textAlign:'center', opacity:o, transform:`translateY(${(1-p)*50}px) scale(${0.9+p*0.1})`}}>
      <div style={{padding:16, borderRadius:22, background:'rgba(255,255,255,0.04)', border:`1px solid ${COLOR.line}`, boxShadow:'0 30px 80px rgba(0,0,0,0.5)'}}>
        <div style={{padding:10, borderRadius:14, background:'linear-gradient(135deg,#F5F5F7,#8E8E93,#D1D1D6)'}}>
          <Img src={staticFile(`qr/${file}`)} style={{width:size, height:size, display:'block', borderRadius:8, background:'#fff'}} />
        </div>
      </div>
      <div style={{fontFamily:FONT, fontSize:24, fontWeight:700, color:COLOR.ink, marginTop:16}}>{title}</div>
      <div style={{fontFamily:FONT, fontSize:21, color:COLOR.accent, marginTop:4}}>{url}</div>
    </div>
  );
};
