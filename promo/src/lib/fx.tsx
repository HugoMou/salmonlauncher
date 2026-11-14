// lib/fx.tsx — premium 3D/metal/glass primitives for beat-synced Apple-keynote motion.
// All visuals are deterministic functions of frame/time.
import React from 'react';
import {AbsoluteFill, Easing, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {COLOR, FONT} from '../spec';
import {hit} from './beats';

export const clamp = {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'} as const;
export const easeOut = Easing.bezier(0.16, 1, 0.3, 1);
export const easeInOut = Easing.bezier(0.65, 0, 0.35, 1);
export const snap = Easing.bezier(0.12, 0.95, 0.1, 1);
export const punchEase = Easing.bezier(0.34, 1.56, 0.64, 1);

export const sec = (f: number, fps: number) => f / fps;

/** Per-beat punch value (decays quickly) for local beat phase 0..1 */
export const beatPunch = (localF: number, fps: number, period: number, strength = 0.06) => {
  const p = ((localF / fps) % period) / period; // 0..1 within beat
  const k = Math.max(0, 1 - p * 2.4);
  return 1 + strength * k * k;
};

/** Cinematic black stage with stronger moving lights and depth. */
export const Stage: React.FC<{children?: React.ReactNode; energy?: number}> = ({children, energy = 1}) => {
  const f = useCurrentFrame();
  const drift = Math.sin(f / 60) * 4;
  const drift2 = Math.cos(f / 47) * 3;
  return (
    <AbsoluteFill style={{backgroundColor: '#000', perspective: 2600, overflow: 'hidden', transformStyle: 'preserve-3d'}}>
      <AbsoluteFill style={{background: `radial-gradient(ellipse 75% 55% at ${50 + drift}% ${18 + drift2}%, rgba(126,217,87,${0.13 * energy}), transparent 60%)`}} />
      <AbsoluteFill style={{background: `radial-gradient(ellipse 60% 45% at ${18 - drift}% 82%, rgba(100,210,255,${0.10 * energy}), transparent 60%)`}} />
      <AbsoluteFill style={{background: `radial-gradient(ellipse 55% 35% at ${82 + drift2}% 55%, rgba(255,214,10,${0.06 * energy}), transparent 55%)`}} />
      <AbsoluteFill style={{background: `radial-gradient(ellipse 80% 50% at 50% 110%, rgba(126,217,87,${0.08 * energy}), transparent 60%)`}} />
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 88% 78% at 50% 50%, transparent 30%, rgba(0,0,0,0.62) 100%)'}} />
      <AbsoluteFill style={{
        opacity: 0.035, mixBlendMode: 'overlay', pointerEvents: 'none',
        backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='140' height='140'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='2' seed='${(Math.floor(f / 3) % 7) + 1}'/%3E%3C/filter%3E%3Crect width='140' height='140' filter='url(%23n)' opacity='0.55'/%3E%3C/svg%3E")`,
      }} />
      {children}
    </AbsoluteFill>
  );
};

/** Metallic gradient text (chrome/steel) with stronger specular sweep. */
export const MetalText: React.FC<{
  children: React.ReactNode; size?: number; weight?: number; align?: 'center'|'left'|'right';
  delay?: number; y?: number; tracking?: number; maxWidth?: number; from?: 'bottom'|'top'|'left'|'right'|'scale';
  tint?: 'silver'|'gold'|'green'|'blue'|'white';
}> = ({children, size = 100, weight = 800, align = 'center', delay = 0, y = 0, tracking = -3, maxWidth, from = 'bottom', tint = 'silver'}) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  const local = f - delay;
  const p = spring({frame: local, fps, config: {damping: 14, mass: 0.6, stiffness: 180}});
  const o = interpolate(f, [delay, delay + 5], [0, 1], clamp);
  const blur = interpolate(p, [0, 1], [18, 0], clamp);
  const dx = from === 'left' ? (1 - p) * -120 : from === 'right' ? (1 - p) * 120 : 0;
  const dy = from === 'bottom' ? (1 - p) * 90 : from === 'top' ? (1 - p) * -90 : 0;
  const sc = from === 'scale' ? 0.7 + p * 0.3 : 0.96 + p * 0.04;
  const grads: Record<string,string> = {
    silver: 'linear-gradient(180deg, #FFFFFF 0%, #F5F5F7 16%, #AEAEB2 34%, #FFFFFF 50%, #7C7C80 70%, #E5E5EA 88%, #FFFFFF 100%)',
    gold: 'linear-gradient(180deg, #FFF8E1 0%, #FFD60A 28%, #FFF4B0 48%, #B8860B 72%, #FFE680 100%)',
    green: 'linear-gradient(180deg, #F2FFF2 0%, #7ED957 26%, #ECFFE8 48%, #2DB84D 72%, #B8F5A0 100%)',
    blue: 'linear-gradient(180deg, #F0FAFF 0%, #64D2FF 26%, #F5FCFF 48%, #007AFF 72%, #B8EAFF 100%)',
    white: 'linear-gradient(180deg, #FFFFFF 0%, #F5F5F7 40%, #D1D1D6 60%, #FFFFFF 100%)',
  };
  const shineX = interpolate(local, [0, 22], [-40, 140], clamp);
  return (
    <div style={{
      fontFamily: FONT, fontSize: size, fontWeight: weight, letterSpacing: tracking, lineHeight: 1.0,
      textAlign: align, maxWidth, whiteSpace: 'pre-line',
      opacity: o, transform: `translate3d(${dx}px, ${y + dy}px, 0) scale(${sc})`,
      filter: `blur(${blur}px) drop-shadow(0 10px 40px rgba(0,0,0,0.5))`,
      backgroundImage: `${grads[tint]}, linear-gradient(115deg, transparent ${shineX - 18}%, rgba(255,255,255,0.95) ${shineX}%, transparent ${shineX + 18}%)`,
      backgroundClip: 'text', WebkitBackgroundClip: 'text', color: 'transparent',
      WebkitTextStroke: '0.6px rgba(255,255,255,0.10)',
    } as React.CSSProperties}>{children}</div>
  );
};

/** Solid headline with slam. */
export const Big: React.FC<{
  children: React.ReactNode; size?: number; weight?: number; color?: string; delay?: number; y?: number; align?: 'center'|'left'|'right'; tracking?: number; maxWidth?: number;
  from?: 'bottom'|'scale'|'left'|'right';
}> = ({children, size=92, weight=700, color=COLOR.ink, delay=0, y=0, align='center', tracking=-2.5, maxWidth, from='bottom'}) => {
  const f = useCurrentFrame();
  const {fps} = useVideoConfig();
  const local = f - delay;
  const p = spring({frame: local, fps, config:{damping:16, mass:0.65, stiffness:160}});
  const o = interpolate(f,[delay,delay+5],[0,1],clamp);
  const dy = from==='bottom' ? (1-p)*60 : 0;
  const dx = from==='left' ? (1-p)*-80 : from==='right' ? (1-p)*80 : 0;
  const sc = from==='scale' ? 0.75+p*0.25 : 0.97+p*0.03;
  return <div style={{fontFamily:FONT,fontSize:size,fontWeight:weight,color,letterSpacing:tracking,lineHeight:1.03,textAlign:align,maxWidth,whiteSpace:'pre-line',opacity:o,transform:`translate3d(${dx}px,${y+dy}px,0) scale(${sc})`}}>{children}</div>;
};

export const Sub: React.FC<{children: React.ReactNode; delay?: number; size?: number; color?: string; align?: 'center'|'left'|'right'; y?: number; maxWidth?: number}> =
({children, delay=0, size=32, color=COLOR.dim, align='center', y=0, maxWidth}) => {
  const f = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:f-delay,fps,config:{damping:20,stiffness:140}});
  const o = interpolate(f,[delay,delay+6],[0,1],clamp);
  return <div style={{fontFamily:FONT,fontSize:size,fontWeight:500,color,lineHeight:1.4,textAlign:align,maxWidth,opacity:o,transform:`translateY(${y+(1-p)*24}px)`}}>{children}</div>;
};

/** 3D screenshot card with metallic bezel and animated specular sweep. */
export const Shot3D: React.FC<{
  file: string; x: number; y: number; w: number; h?: number; delay?: number;
  rx?: number; ry?: number; rz?: number; z?: number; scale?: number; label?: string; op?: number;
  floatAmp?: number; sweep?: boolean; enter?: 'bottom'|'left'|'right'|'back';
}> = ({file, x, y, w, h, delay=0, rx=0, ry=0, rz=0, z=0, scale=1, label, op=1, floatAmp=0, sweep=true, enter='back'}) => {
  const f = useCurrentFrame(); const {fps} = useVideoConfig();
  const p = spring({frame: f - delay, fps, config:{damping:14, mass:0.75, stiffness:120}});
  const o = interpolate(f,[delay,delay+5],[0,1],clamp);
  const enterZ = enter==='back' ? interpolate(p,[0,1],[-700,0]) : 0;
  const enterY = enter==='bottom' ? interpolate(p,[0,1],[140,0]) : interpolate(p,[0,1],[60,0]);
  const enterX = enter==='left' ? interpolate(p,[0,1],[-160,0]) : enter==='right' ? interpolate(p,[0,1],[160,0]) : 0;
  const enterRx = interpolate(p,[0,1],[26,0]);
  const floatY = floatAmp ? Math.sin((f-delay)/28) * floatAmp : 0;
  const shine = interpolate(f-delay,[0,28],[ -50, 150],{...clamp, easing:easeOut});
  const hh = h ?? Math.round(w * 0.72);
  return (
    <div style={{
      position:'absolute', left:x, top:y, width:w, height:hh, transformStyle:'preserve-3d',
      transform:`translate3d(${enterX}px,${enterY+floatY}px,${z+enterZ}px) rotateX(${rx+enterRx}deg) rotateY(${ry}deg) rotateZ(${rz}deg) scale(${scale*(0.88+p*0.12)})`,
      opacity: o*op,
    }}>
      <div style={{
        position:'absolute', inset:-3, borderRadius:26,
        background:'linear-gradient(135deg,#FFFFFF 0%,#AEAEB2 22%,#F5F5F7 45%,#636366 70%,#E5E5EA 100%)',
        boxShadow:'0 60px 140px rgba(0,0,0,0.85), 0 2px 0 rgba(255,255,255,0.2) inset',
      }}/>
      <div style={{position:'absolute', inset:0, borderRadius:23, overflow:'hidden', background:'#111', border:'1px solid rgba(0,0,0,0.55)'}}>
        <Img src={staticFile(`shots/${file}`)} style={{width:'100%', height:'100%', objectFit:'cover', objectPosition:'top left'}} />
        {sweep && <div style={{position:'absolute', inset:0, background:`linear-gradient(118deg, transparent ${shine-28}%, rgba(255,255,255,0.22) ${shine}%, transparent ${shine+28}%)`, mixBlendMode:'screen'}}/>}
        <div style={{position:'absolute', inset:0, boxShadow:'inset 0 0 0 1px rgba(255,255,255,0.10), inset 0 -40px 80px rgba(0,0,0,0.18)'}}/>
      </div>
      {label && <div style={{
        position:'absolute', left:20, bottom:-16, padding:'8px 18px', borderRadius:999,
        background:'rgba(28,28,30,0.94)', border:`1px solid ${COLOR.line}`,
        color:COLOR.ink, fontFamily:FONT, fontSize:18, fontWeight:600, boxShadow:'0 10px 34px rgba(0,0,0,0.45)',
      }}>{label}</div>}
    </div>
  );
};

/** Glass pill/tag. */
export const Tag: React.FC<{text: string; x?: number; y?: number; delay?: number; color?: string; accent?: boolean; size?: number}> =
({text, x, y, delay=0, color=COLOR.ink, accent, size=22}) => {
  const f = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:f-delay,fps,config:{damping:14,stiffness:170}});
  const o = interpolate(f,[delay,delay+5],[0,1],clamp);
  const pos: React.CSSProperties = x!==undefined || y!==undefined ? {position:'absolute', left:x??0, top:y??0} : {};
  return <div style={{
    ...pos, padding:`${size*0.45}px ${size}px`, borderRadius:999,
    background: accent ? 'rgba(126,217,87,0.16)' : COLOR.glass,
    border: `1px solid ${accent ? 'rgba(126,217,87,0.45)' : COLOR.line}`,
    color: accent ? COLOR.accent : color, fontFamily:FONT, fontSize:size, fontWeight:600,
    opacity:o, transform:`scale(${0.75+p*0.25}) translateY(${(1-p)*10}px)`,
    boxShadow: accent ? `0 0 30px rgba(126,217,87,0.15)` : '0 10px 30px rgba(0,0,0,0.25)',
    whiteSpace:'nowrap',
  }}>{text}</div>;
};

/** Full-frame kinetic text slam. */
export const Slam: React.FC<{children: React.ReactNode; delay?: number; size?: number; tint?: 'silver'|'gold'|'green'|'blue'|'white'}> =
({children, delay=0, size=150, tint='silver'}) => (
  <AbsoluteFill style={{justifyContent:'center', alignItems:'center'}}>
    <MetalText size={size} delay={delay} from="scale" tint={tint}>{children}</MetalText>
  </AbsoluteFill>
);

/** Animated scan line. */
export const ScanLine: React.FC<{delay?: number; y?: number; color?: string}> = ({delay=0, y=540, color=COLOR.accent}) => {
  const f = useCurrentFrame();
  const w = interpolate(f,[delay,delay+14],[0,1920],{...clamp,easing:easeOut});
  const o = interpolate(f,[delay,delay+4,delay+22,delay+34],[0,1,1,0],clamp);
  return <div style={{position:'absolute', left:(1920-w)/2, top:y, width:w, height:2, background:`linear-gradient(90deg,transparent,${color},transparent)`, opacity:o, boxShadow:`0 0 40px ${color}, 0 0 80px ${color}`}}/>;
};

/** Persistent legal strip (small). */
export const LegalStrip: React.FC = () => {
  const f = useCurrentFrame();
  const o = interpolate(f,[30,50],[0,0.5],clamp);
  return (
    <div style={{position:'absolute', right:22, bottom:12, maxWidth:980, textAlign:'right', fontFamily:FONT, fontSize:12, lineHeight:1.4, color:'rgba(245,245,247,0.45)', opacity:o}}>
      SalmonLauncher · GPL-3.0 · 制作 Hugo Mou · 配乐 “3 Strikes” — Terror Jr (Bilibili) · 2.0.13 真实截图（全新默认状态）
      <div style={{fontSize:10.5, color:'rgba(245,245,247,0.3)', marginTop:2}}>Minecraft 为 Mojang Studios/Microsoft 商标，与本项目无官方关联；Modrinth/Campfire 等归各自权利人。仅作产品展示。</div>
    </div>
  );
};

/** Camera rig with 3D transform. */
export const Camera: React.FC<{children: React.ReactNode; tx?: number; ty?: number; tz?: number; rx?: number; ry?: number; rz?: number; scale?: number}> =
({children, tx=0, ty=0, tz=0, rx=0, ry=0, rz=0, scale=1}) => (
  <AbsoluteFill style={{perspective: 2400, transformStyle: 'preserve-3d'}}>
    <AbsoluteFill style={{transformStyle:'preserve-3d', transform:`translate3d(${tx}px,${ty}px,${tz}px) rotateX(${rx}deg) rotateY(${ry}deg) rotateZ(${rz}deg) scale(${scale})`}}>
      {children}
    </AbsoluteFill>
  </AbsoluteFill>
);

/** QR card with metallic frame and label. */
export const QRCard: React.FC<{file: string; title: string; url: string; x: number; y: number; delay?: number; size?: number}> =
({file, title, url, x, y, delay=0, size=190}) => {
  const f = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:f-delay,fps,config:{damping:14,stiffness:130}});
  const o = interpolate(f,[delay,delay+6],[0,1],clamp);
  return (
    <div style={{position:'absolute', left:x, top:y, textAlign:'center', opacity:o, transform:`translateY(${(1-p)*60}px) translateZ(0) scale(${0.85+p*0.15}) rotateX(${(1-p)*18}deg)`, transformStyle:'preserve-3d'}}>
      <div style={{padding:16, borderRadius:24, background:'rgba(255,255,255,0.05)', border:`1px solid ${COLOR.line}`, boxShadow:'0 40px 100px rgba(0,0,0,0.6)'}}>
        <div style={{padding:10, borderRadius:14, background:'linear-gradient(135deg,#FFFFFF,#9A9AA0,#E5E5EA,#7C7C80)'}}>
          <Img src={staticFile(`qr/${file}`)} style={{width:size, height:size, display:'block', borderRadius:8, background:'#fff'}} />
        </div>
      </div>
      <div style={{fontFamily:FONT, fontSize:26, fontWeight:700, color:COLOR.ink, marginTop:16}}>{title}</div>
      <div style={{fontFamily:FONT, fontSize:22, color:COLOR.accent, marginTop:4}}>{url}</div>
    </div>
  );
};

/** Big callout panel. */
export const Panel: React.FC<{x:number;y:number;w:number;children:React.ReactNode;delay?:number}> = ({x,y,w,children,delay=0}) => {
  const f = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:f-delay,fps,config:{damping:16,stiffness:120}});
  const o = interpolate(f,[delay,delay+6],[0,1],clamp);
  return (
    <div style={{position:'absolute', left:x, top:y, width:w, padding:'22px 28px', borderRadius:20,
      background:'rgba(20,20,22,0.92)', border:`1px solid ${COLOR.line}`,
      boxShadow:'0 30px 80px rgba(0,0,0,0.5)', opacity:o, transform:`translateY(${(1-p)*40}px) scale(${0.96+p*0.04})`}}>
      {children}
    </div>
  );
};
