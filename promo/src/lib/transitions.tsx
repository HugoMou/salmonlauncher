// transitions.tsx — 夸张转场与「流动感」组件：甩镜、推镜爆闪、色散边缘、光带、残影
// 目标：让章节切换不再生硬，镜头运动带惯性与过冲。
import React from 'react';
import {AbsoluteFill, Easing, interpolate, useCurrentFrame} from 'remotion';
import {COLOR} from '../spec';
import {clamp} from './fx';

const easeInOutCubic = Easing.bezier(0.65, 0, 0.35, 1);
const easeOutExpo = Easing.bezier(0.16, 1, 0.3, 1);

/** 甩镜转场：切点前后几帧整体横向拖拽 + 运动模糊感（多层半透明叠影） */
export const WhipPan: React.FC<{at: number; frames?: number; dir?: 1 | -1; strength?: number}> = ({
  at, frames = 8, dir = 1, strength = 1,
}) => {
  const f = useCurrentFrame();
  const t = f - at;
  if (t < -1 || t > frames) return null;
  const p = interpolate(t, [-1, frames], [0, 1], clamp);
  const peak = Math.sin(Math.PI * p) * strength;
  const dx = dir * peak * 26;
  const ghost = [0.35, 0.6, 0.85];
  return (
    <AbsoluteFill style={{pointerEvents: 'none', mixBlendMode: 'screen'}}>
      {ghost.map((g, i) => (
        <AbsoluteFill key={i} style={{
          background: `linear-gradient(${dir > 0 ? 90 : -90}deg, transparent ${20 + i * 8}%, rgba(255,255,255,${0.05 * peak}) ${50}%, transparent ${80 - i * 8}%)`,
          transform: `translateX(${dx * g}px)`,
          filter: `blur(${peak * 8}px)`,
        }}/>
      ))}
    </AbsoluteFill>
  );
};

/** 推镜爆闪：切点瞬间放大 + 白光，制造「镜头冲进来」的冲击 */
export const ZoomBurst: React.FC<{at: number; frames?: number; strength?: number; color?: string}> = ({
  at, frames = 10, strength = 1, color = '#FFFFFF',
}) => {
  const f = useCurrentFrame();
  const t = f - at;
  if (t < -1 || t > frames) return null;
  const p = interpolate(t, [-1, frames], [0, 1], clamp);
  const flash = Math.sin(Math.PI * p) * 0.34 * strength;
  const ring = interpolate(p, [0, 1], [0.4, 2.2], {...clamp, easing: easeOutExpo});
  const ringOp = interpolate(p, [0, 0.25, 1], [0, 0.5 * strength, 0], clamp);
  return (
    <AbsoluteFill style={{pointerEvents: 'none'}}>
      <AbsoluteFill style={{backgroundColor: color, opacity: flash, mixBlendMode: 'screen'}}/>
      <AbsoluteFill style={{display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
        <div style={{
          width: 900, height: 900, borderRadius: '50%',
          border: `3px solid ${COLOR.accent}`,
          opacity: ringOp,
          transform: `scale(${ring})`,
          boxShadow: `0 0 80px ${COLOR.accent}, inset 0 0 60px ${COLOR.accent}`,
        }}/>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};

/** 色散边缘：画面边缘红/青分离，模拟镜头色差，增加「电影感」 */
export const ChromaticEdge: React.FC<{strength?: number}> = ({strength = 1}) => {
  const f = useCurrentFrame();
  const pulse = 0.6 + 0.4 * Math.sin(f / 34);
  return (
    <AbsoluteFill style={{pointerEvents: 'none', mixBlendMode: 'screen'}}>
      <AbsoluteFill style={{
        background: `radial-gradient(ellipse 70% 60% at 50% 50%, transparent 55%, rgba(255,60,90,${0.10 * strength * pulse}) 100%)`,
        transform: 'translateX(4px)',
      }}/>
      <AbsoluteFill style={{
        background: `radial-gradient(ellipse 70% 60% at 50% 50%, transparent 55%, rgba(60,200,255,${0.10 * strength * pulse}) 100%)`,
        transform: 'translateX(-4px)',
      }}/>
    </AbsoluteFill>
  );
};

/** 光带：两条斜向高光带缓慢扫过，制造速度线与舞台感 */
export const GlowBands: React.FC<{speed?: number; opacity?: number}> = ({speed = 1, opacity = 0.16}) => {
  const f = useCurrentFrame();
  const x1 = ((f * 2.6 * speed) % 2600) - 600;
  const x2 = ((f * 1.4 * speed + 1200) % 2600) - 600;
  return (
    <AbsoluteFill style={{pointerEvents: 'none', mixBlendMode: 'screen', opacity}}>
      <div style={{
        position: 'absolute', left: x1, top: -200, width: 220, height: 1600,
        background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.55), transparent)',
        transform: 'rotate(18deg)',
        /* 用渐变柔边代替 blur，显著降低渲染开销 */
      }}/>
      <div style={{
        position: 'absolute', left: x2, top: -200, width: 140, height: 1600,
        background: `linear-gradient(90deg, transparent, ${COLOR.accent}88, transparent)`,
        transform: 'rotate(18deg)',

      }}/>
    </AbsoluteFill>
  );
};

/** 残影：把上一层的相同内容以低透明度、缩放偏移画出，形成拖尾（用包裹方式） */
export const GhostWrap: React.FC<{
  children: React.ReactNode; delay?: number; frames?: number; amp?: number;
}> = ({children, delay = 0, frames = 10, amp = 0.06}) => {
  const f = useCurrentFrame();
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{opacity: 0.26, filter: 'blur(5px)', transform: `scale(${1 + amp})`}}>{children}</AbsoluteFill>
      <AbsoluteFill>{children}</AbsoluteFill>
    </AbsoluteFill>
  );
};

/** 呼吸式缩放：让静止画面也有轻微推进，避免「卡住」的观感 */
export const Breathe: React.FC<{children: React.ReactNode; amp?: number; period?: number}> = ({
  children, amp = 0.02, period = 90,
}) => {
  const f = useCurrentFrame();
  const s = 1 + Math.sin(f / period * Math.PI * 2) * amp;
  return (
    <AbsoluteFill style={{transform: `scale(${s})`, transformOrigin: '50% 50%'}}>{children}</AbsoluteFill>
  );
};

export const easeSmooth = easeInOutCubic;
export const easeSwoop = easeOutExpo;
