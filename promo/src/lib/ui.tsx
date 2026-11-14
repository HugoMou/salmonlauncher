// lib/ui.tsx — Apple-keynote visual primitives with 3D motion for SalmonLauncher promo.
import React from 'react';
import {
  AbsoluteFill, Audio, Easing, Img, interpolate, spring, staticFile,
  useCurrentFrame, useVideoConfig,
} from 'remotion';
import {COLOR, FONT, CREDITS} from '../spec';

const FONT_I = FONT;

export const clamp = {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'} as const;

export const useIn = (delay = 0, dur = 18) => {
  const frame = useCurrentFrame();
  return interpolate(frame, [delay, delay + dur], [0, 1], clamp);
};

export const useSpring = (delay = 0, config?: {damping?: number; mass?: number; stiffness?: number}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  return spring({frame: frame - delay, fps, config: config ?? {damping: 18, mass: 0.9, stiffness: 120}});
};

/** Pure black stage with subtle cinematic vignette and accent bloom. */
export const Stage: React.FC<{children?: React.ReactNode}> = ({children}) => (
  <AbsoluteFill style={{backgroundColor: COLOR.bg, perspective: 2200}}>
    <AbsoluteFill style={{background: 'radial-gradient(ellipse 80% 55% at 50% 0%, rgba(126,217,87,0.10), transparent 70%)'}} />
    <AbsoluteFill style={{background: 'radial-gradient(ellipse 60% 50% at 80% 100%, rgba(41,151,255,0.06), transparent 70%)'}} />
    <AbsoluteFill style={{boxShadow: 'inset 0 0 220px rgba(0,0,0,0.85)'}} />
    {children}
  </AbsoluteFill>
);

/** Apple-style giant headline. */
export const Big: React.FC<{
  children: React.ReactNode;
  size?: number;
  weight?: number;
  color?: string;
  delay?: number;
  y?: number;
  tracking?: number;
  align?: 'center' | 'left' | 'right';
  maxWidth?: number;
  italic?: boolean;
}> = ({children, size = 96, weight = 700, color = COLOR.ink, delay = 0, y = 0, tracking = -2.5, align = 'center', maxWidth, italic}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: frame - delay, fps, config: {damping: 22, mass: 0.8, stiffness: 110}});
  const o = interpolate(frame, [delay, delay + 12], [0, 1], clamp);
  const blur = interpolate(p, [0, 1], [18, 0], clamp);
  return (
    <div style={{
      fontFamily: italic ? FONT_I : FONT, fontSize: size, fontWeight: weight, color,
      letterSpacing: tracking, lineHeight: 1.05, textAlign: align, maxWidth,
      opacity: o,
      transform: `translateY(${y + (1 - p) * 50}px) scale(${0.96 + p * 0.04})`,
      filter: `blur(${blur}px)`,
      whiteSpace: 'pre-line',
    }}>{children}</div>
  );
};

/** Secondary line. */
export const Sub: React.FC<{children: React.ReactNode; delay?: number; size?: number; color?: string; y?: number; align?: 'center'|'left'}> =
({children, delay = 0, size = 34, color = COLOR.dim, y = 0, align = 'center'}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: frame - delay, fps, config: {damping: 24, mass: 0.8, stiffness: 100}});
  const o = interpolate(frame, [delay, delay + 14], [0, 1], clamp);
  return (
    <div style={{
      fontFamily: FONT, fontSize: size, fontWeight: 500, color, lineHeight: 1.4, textAlign: align,
      opacity: o, transform: `translateY(${y + (1 - p) * 30}px)`,
    }}>{children}</div>
  );
};

/** 3D screenshot card with depth, tilt and entrance. */
export const Card3D: React.FC<{
  file: string;
  x: number; y: number; w: number; h: number;
  delay?: number;
  rotateY?: number; rotateX?: number; rotateZ?: number;
  scale?: number; z?: number;
  label?: string;
  shadow?: boolean;
  opacity?: number;
}> = ({file, x, y, w, h, delay = 0, rotateY = 0, rotateX = 0, rotateZ = 0, scale = 1, z = 0, label, shadow = true, opacity = 1}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: frame - delay, fps, config: {damping: 16, mass: 0.85, stiffness: 90}});
  const o = interpolate(frame, [delay, delay + 10], [0, 1], clamp);
  const enterZ = interpolate(p, [0, 1], [-420, 0]);
  const enterY = interpolate(p, [0, 1], [120, 0]);
  const enterRX = interpolate(p, [0, 1], [18, 0]);
  return (
    <div style={{
      position: 'absolute', left: x, top: y, width: w, height: h,
      transformStyle: 'preserve-3d',
      transform: `translateZ(${z + enterZ}px) translateY(${enterY}px) rotateY(${rotateY}deg) rotateX(${rotateX + enterRX}deg) rotateZ(${rotateZ}deg) scale(${scale * (0.92 + p * 0.08)})`,
      opacity: o * opacity,
    }}>
      <div style={{
        width: '100%', height: '100%', borderRadius: 22, overflow: 'hidden',
        border: `1px solid ${COLOR.line}`,
        boxShadow: shadow ? '0 60px 140px rgba(0,0,0,0.75), 0 0 0 1px rgba(255,255,255,0.04) inset' : 'none',
        background: '#0A0A0C',
      }}>
        <Img src={staticFile(`shots/${file}`)} style={{width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top left'}} />
      </div>
      {label ? (
        <div style={{
          position: 'absolute', left: 18, bottom: -14,
          padding: '8px 16px', borderRadius: 999,
          background: 'rgba(20,20,22,0.82)', backdropFilter: 'blur(20px)',
          border: `1px solid ${COLOR.line}`,
          color: COLOR.ink, fontFamily: FONT, fontSize: 18, fontWeight: 600,
        }}>{label}</div>
      ) : null}
    </div>
  );
};

/** Floating pill/tag. */
export const Pill: React.FC<{text: string; x: number; y: number; delay?: number; color?: string}> =
({text, x, y, delay = 0, color = COLOR.accent}) => {
  const o = useIn(delay, 14);
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: frame - delay, fps, config: {damping: 18, stiffness: 140}});
  return (
    <div style={{
      position: 'absolute', left: x, top: y,
      padding: '10px 22px', borderRadius: 999,
      background: 'rgba(126,217,87,0.12)', border: `1px solid rgba(126,217,87,0.35)`,
      color, fontFamily: FONT, fontSize: 22, fontWeight: 600, letterSpacing: 0.2,
      opacity: o, transform: `scale(${0.8 + p * 0.2})`,
    }}>{text}</div>
  );
};

/** Persistent bottom-right disclaimer + credits overlay (subtle). */
export const LegalOverlay: React.FC = () => {
  const frame = useCurrentFrame();
  const o = interpolate(frame, [30, 60], [0, 1], clamp);
  return (
    <div style={{
      position: 'absolute', right: 28, bottom: 18, width: 620,
      textAlign: 'right', fontFamily: FONT, fontSize: 13, lineHeight: 1.45,
      color: 'rgba(245,245,247,0.38)', opacity: o, pointerEvents: 'none',
    }}>
      <div>SalmonLauncher · GPL-3.0-or-later　{CREDITS.maker}</div>
      <div>{CREDITS.music}　{CREDITS.assets}</div>
      <div style={{fontSize: 11, color: 'rgba(245,245,247,0.28)', marginTop: 2}}>{CREDITS.disclaimer}</div>
    </div>
  );
};

/** Chapter transition: big word wipe. */
export const Kicker: React.FC<{text: string; delay?: number; x?: number; y?: number}> = ({text, delay = 0, x = 140, y = 120}) => {
  const o = useIn(delay, 12);
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: frame - delay, fps, config: {damping: 20, stiffness: 120}});
  return (
    <div style={{
      position: 'absolute', left: x, top: y,
      fontFamily: FONT, fontSize: 22, fontWeight: 700, letterSpacing: 6,
      color: COLOR.accent, textTransform: 'uppercase',
      opacity: o, transform: `translateX(${(1 - p) * -30}px)`,
    }}>{text}</div>
  );
};

/** Scene fade wrapper (in/out). */
export const SceneWrap: React.FC<{from: number; dur: number; children: React.ReactNode}> = ({from, dur, children}) => {
  const frame = useCurrentFrame();
  const local = frame - from;
  if (local < 0 || local > dur) return null;
  const fadeIn = interpolate(local, [0, 10], [0, 1], clamp);
  const fadeOut = interpolate(local, [dur - 12, dur], [1, 0], clamp);
  return <AbsoluteFill style={{opacity: fadeIn * fadeOut}}>{children}</AbsoluteFill>;
};
