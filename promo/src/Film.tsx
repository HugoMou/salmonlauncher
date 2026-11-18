// Film.tsx — SalmonLauncher 宣传片 v2（按实测拍点重排）
//
// 卡点基准：src/lib/beatGrid.generated.ts —— 从音频实测的 634 个拍点
//   · 实测 BPM 107.07（旧版写死 108 会一路漂移，片尾偏差 >5 秒）
//   · 拼接缝 177.6s 处相位跳变已分段校正
//   · 拍点/反拍能量比 1.93x，说明网格与真实鼓点对齐
// 所有段落起点与段内切换都用 beatFrame(拍序号) 计算，因此每次切换都落在真实鼓点上。
import React from 'react';
import {AbsoluteFill, Audio, Easing, Img, Sequence, interpolate, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {AUDIO_FILE, CLIP_MARKS, CLIPS, CLIPS5, COLOR, FILM, FONT, LINKS, MARKS5, SHOTS, toF} from './spec';
import {BEAT_COUNT, PERIOD, beatFrame, hit} from './lib/beats';
import {Camera, ClickFx, LegalStrip, QRCard, RealProgress, Shot3D, Stage, Sub, Tag, VideoClip, beatPunch, clamp, easeOut} from './lib/fx';
import {
  BokehVignette, CursorTrail, ExtrudeText, LightSweep, ParticleField, ReflectFloor, RippleGrid,
  SplitCompare, SwatchExtractor, ZoomLens,
} from './lib/spectacle';
import {ChromaticEdge, GlowBands, WhipPan, ZoomBurst} from './lib/transitions';

const easeSmooth = Easing.bezier(0.65, 0, 0.35, 1);
const easeSwoop = Easing.bezier(0.16, 1, 0.3, 1);

/** 第 i 拍对应的帧（真实拍点） */
const BF = (i: number) => beatFrame(i, FILM.fps);
/** 第 bar 小节（4 拍）对应的帧 */
const BARF = (bar: number) => BF(bar * 4);

/** 拍点对齐的分段：把一段分成 count 份，每份 stepBeats 拍，返回各份起始帧（段内相对） */
const beats = (n: number) => Array.from({length: n + 1}, (_, i) => BF(i) - BF(0));

// ---------- 叠加层 ----------
const Pulse: React.FC<{x: number; y: number; at: number}> = ({x, y, at}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 24) return null;
  const s = interpolate(t, [0, 16], [0.2, 2.5], {extrapolateRight: 'clamp', easing: easeOut});
  const o = interpolate(t, [0, 2, 16], [0, 1, 0], clamp);
  return <div style={{position: 'absolute', left: x - 34, top: y - 34, width: 68, height: 68, borderRadius: 999,
    border: `4px solid ${COLOR.accent}`, opacity: o, transform: `scale(${s})`, boxShadow: `0 0 40px ${COLOR.accent}`, zIndex: 59}}/>;
};

const ClickPoint: React.FC<{x: number; y: number; at: number; label?: string}> = ({x, y, at, label}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 40) return null;
  return (
    <AbsoluteFill>
      <CursorTrail from={{x: x - 190, y: y - 110}} to={{x, y}} at={at} dur={beats(1)[1]}/>
      <ClickFx x={x} y={y} at={at} label={label} size={1.2}/>
      <Pulse x={x} y={y} at={at + beats(1)[1]}/>
    </AbsoluteFill>
  );
};

const Point: React.FC<{text: string; x: number; y: number; delay: number}> = ({text, x, y, delay}) => {
  const l = useCurrentFrame();
  const p = Math.max(0, Math.min(1, (l - delay) / 18));
  const o = interpolate(l, [delay, delay + 4], [0, 1], clamp);
  return (
    <div style={{position: 'absolute', left: x, top: y, opacity: o, transform: `translateX(${(1 - p) * 34}px)`, zIndex: 22}}>
      <span style={{color: COLOR.accent, fontWeight: 700, marginRight: 10, fontSize: 20}}>▸</span>
      <span style={{fontFamily: FONT, fontSize: 25, color: COLOR.ink, fontWeight: 600}}>{text}</span>
    </div>
  );
};

/** 通用场景（拍点对齐版） */
const Scene: React.FC<{
  local: number; dur: number; head: string; sub?: string;
  clip?: {src: string; start?: number}; file?: string;
  cardW?: number; cardH?: number; cardY?: number;
  ry?: number; rx?: number; z?: number;
  camRy?: number; camRx?: number; camZ?: number;
  click?: {x: number; y: number; label: string; atBeat: number};
  lens?: {x: number; y: number; r?: number; label?: string; atBeat: number};
  points?: string[];
  progress?: {pct: number; label: string; detail?: string; atBeat: number};
  deco?: boolean; closeup?: boolean; entryBeats?: number; floor?: boolean;
}> = ({local: l, dur, head, sub, clip, file, cardW = 1060, cardH, cardY = 470, ry = -12, rx = 6, z = 40,
      camRy = 0, camRx = 5, camZ = 120, click, lens, points, progress, deco = true, closeup = false,
      entryBeats = 4, floor = true}) => {
  const entry = BF(entryBeats) - BF(0);
  // 相机三段式：掠过 → 推近 → 落定（全部带缓动）
  const camRyT = interpolate(l, [0, entry, dur], [camRy - 42, camRy, camRy + 22], {...clamp, easing: easeSmooth});
  const camRxT = interpolate(l, [0, entry, dur], [camRx + 16, camRx, camRx - 6], {...clamp, easing: easeSmooth});
  const camZT = interpolate(l, [0, entry, dur], [camZ - 620, camZ + 40, camZ - 30], {...clamp, easing: easeSwoop});
  const eZ = interpolate(l, [0, entry], [z - 900, z], {...clamp, easing: easeSwoop});
  const eRy = interpolate(l, [0, entry], [ry + 72, ry], {...clamp, easing: easeSmooth});
  const eRx = interpolate(l, [0, entry], [rx + 34, rx], {...clamp, easing: easeSmooth});
  const eS = interpolate(l, [0, entry], [0.34, 1.06], {...clamp, easing: easeSwoop});
  const ryT = interpolate(l, [entry, dur], [ry - 20, ry + 20], {...clamp, easing: easeSmooth});
  const rxT = interpolate(l, [entry, dur], [rx - 7, rx + 7], {...clamp, easing: easeSmooth});
  const punch = beatPunch(l, FILM.fps, PERIOD, 0.06);
  const isClose = closeup && l > dur * 0.55;
  const cw = isClose ? cardW * 1.24 : cardW;
  const cy = isClose ? cardY - 28 : cardY;
  const opacity = interpolate(l, [0, Math.max(1, entry / 2)], [0, 1], clamp);
  const pStart = entry;
  return (
    <AbsoluteFill style={{opacity}}>
      <Camera ry={camRyT} rx={camRxT} tz={camZT} scale={punch}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 96, textAlign: 'center', zIndex: 26}}>
          <ExtrudeText size={96} delay={0} tint="silver">{head}</ExtrudeText>
          {sub && <div style={{marginTop: 8}}><Sub delay={2} size={26} color={COLOR.dim} maxWidth={780}>{sub}</Sub></div>}
        </div>

        {floor && <ReflectFloor x={960 - cw / 2} y={cy + (cardH ?? cw * 0.68) - 30} w={cw} h={210} opacity={0.55}/>}

        {clip ? (
          <VideoClip src={clip.src} start={clip.start ?? 0} x={960 - cw / 2} y={cy} w={cw} h={cardH}
            delay={0} ry={l < entry ? eRy : ryT} rx={l < entry ? eRx : rxT} z={l < entry ? eZ : (isClose ? z - 120 : z)}
            floatAmp={10} scale={(l < entry ? eS : 1) * 1.02} mute/>
        ) : file ? (
          <Shot3D file={file} x={960 - cw / 2} y={cy} w={cw} h={cardH}
            delay={0} ry={l < entry ? eRy : ryT} rx={l < entry ? eRx : rxT} z={l < entry ? eZ : (isClose ? z - 120 : z)}
            floatAmp={12} scale={(l < entry ? eS : 1) * 1.02}/>
        ) : null}

        <LightSweep delay={0} periodBeats={8} strength={0.14}/>

        {deco && (
          <>
            <Shot3D file={SHOTS.versions} x={150} y={cardY - 210} w={300} delay={pStart} ry={-24} rx={10} z={-150} floatAmp={8}/>
            <Shot3D file={SHOTS.tasks}    x={1470} y={cardY - 210} w={290} delay={pStart + BF(1) - BF(0)} ry={24} rx={-8} z={-140} floatAmp={8}/>
            <Shot3D file={SHOTS.repoMods} x={210} y={cardY + 280} w={280} delay={pStart + 2 * (BF(1) - BF(0))} ry={-14} rx={14} z={-165} floatAmp={7}/>
            <Shot3D file={SHOTS.online}   x={1500} y={cardY + 280} w={270} delay={pStart + 3 * (BF(1) - BF(0))} ry={18} rx={-12} z={-150} floatAmp={7}/>
          </>
        )}

        {lens && <ZoomLens x={lens.x} y={lens.y} r={lens.r ?? 108} delay={BF(lens.atBeat) - BF(0)} label={lens.label}/>}

        {points && points.map((t, i) => (
          <Point key={i} text={t} x={80} y={700 + i * 52} delay={BF(1 + i) - BF(0)}/>
        ))}

        {progress && <RealProgress x={72} y={652} w={1320} pct={progress.pct} label={progress.label}
          detail={progress.detail} delay={BF(progress.atBeat) - BF(0)}/>}

        {click && <ClickPoint x={click.x} y={click.y} at={BF(click.atBeat) - BF(0)} label={click.label}/>}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 1) HOOK ----------
const Hook: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const g = CLIP_MARKS.game;
  // 段落切换点都用真实拍点：0 / 3 / 5.5 / 7.5 / 9 拍…这里按整拍切
  const t1 = BF(3), t2 = BF(5), t3 = BF(7), t4 = BF(9);
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(l, [0, dur], [-900, 260], {...clamp, easing: easeSwoop})}
        ry={interpolate(l, [0, dur / 2, dur], [-30, 18, -24], {...clamp, easing: easeSmooth})}
        rx={interpolate(l, [0, dur], [22, -8], {...clamp, easing: easeSmooth})}
        scale={beatPunch(l, FILM.fps, PERIOD, 0.12)}>
        {l < t1 && (
          <>
            <ReflectFloor x={440} y={560} w={1040} h={200} opacity={0.6}/>
            <VideoClip src={CLIPS.game} start={g.menu} x={530} y={200} w={860} h={486} delay={0} ry={-12} rx={7} z={-30} floatAmp={10} scale={1.04}/>
          </>
        )}
        {l >= t1 && l < t2 && (
          <AbsoluteFill style={{display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
            <ExtrudeText size={172} delay={0} tint="gold" depth={14} tilt={6}>一键。</ExtrudeText>
          </AbsoluteFill>
        )}
        {l >= t2 && l < t3 && (
          <AbsoluteFill style={{display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
            <ExtrudeText size={156} delay={0} tint="blue" depth={14} tilt={6}>全部功能。</ExtrudeText>
          </AbsoluteFill>
        )}
        {l >= t3 && l < t4 && (
          <AbsoluteFill style={{display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center'}}>
            <ExtrudeText size={152} delay={0} tint="green" depth={14} tilt={6}>开源免费。</ExtrudeText>
            <div style={{marginTop: 18}}><Sub delay={1} size={30} color={COLOR.dim}>GPL-3.0-or-later · Windows x64</Sub></div>
          </AbsoluteFill>
        )}
        {l >= t4 && (
          <AbsoluteFill style={{display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center'}}>
            <ExtrudeText size={116} delay={0} tint="silver">SalmonLauncher</ExtrudeText>
            <div style={{marginTop: 18}}><Sub delay={1} size={32} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
          </AbsoluteFill>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

/** 多段场景：按拍点分段 */
const useStep = (baseBeat: number, stepBeats: number, count: number, totalDur: number) => {
  const l = useCurrentFrame();
  const bounds = Array.from({length: count + 1}, (_, k) => BF(baseBeat + k * stepBeats) - BF(baseBeat));
  let idx = 0;
  for (let k = 0; k < count; k++) if (l >= bounds[k]) idx = k;
  const start = bounds[idx];
  const end = bounds[idx + 1] ?? totalDur;
  return {idx, local: l - start, stepDur: end - start, l};
};

// ---------- 2) BRAND ----------
const Brand: React.FC<{dur: number; baseBeat: number; stepBeats: number}> = ({dur, baseBeat, stepBeats}) => {
  const {idx, local, stepDur} = useStep(baseBeat, stepBeats, 3, dur);
  const items = [
    {file: SHOTS.home, head: '极简', sub: '一个启动面板：实例、账户、状态一目了然', pt: ['选好实例与账户即可开始']},
    {file: SHOTS.tasks, head: '一键启动', sub: '一个按钮完成启动、校验与进入游戏', pt: ['进度与状态实时可见']},
    {file: SHOTS.about, head: '开源免费', sub: 'GPL-3.0-or-later，Windows x64 免费使用', pt: ['源码与构建全部公开']},
  ];
  const it = items[idx];
  return <Scene local={local} dur={stepDur} head={it.head} sub={it.sub} file={it.file} points={it.pt}
    cardW={1040} ry={idx % 2 ? 12 : -12} camRy={idx % 2 ? -9 : 9} camZ={130}
    lens={idx === 1 ? {x: 980, y: 560, r: 96, label: '2.2×', atBeat: 2} : undefined}/>;
};

// ---------- 3) ONE-CLICK 主线 ----------
const OneClick: React.FC<{dur: number; baseBeat: number; stepBeats: number}> = ({dur, baseBeat, stepBeats}) => {
  const {idx, local, stepDur} = useStep(baseBeat, stepBeats, 6, dur);
  const m5 = MARKS5.launch;
  const g = CLIP_MARKS.game;
  const stages = [
    {clip: CLIPS5.launch, start: m5.nav, head: '打开即用', sub: '实例、账户、版本一目了然',
     pt: ['无需配置，直接进入'], pct: [0.02, 0.16], label: '准备启动', detail: '真实录屏 · 启动面板'},
    {clip: CLIPS5.install, start: MARKS5.install.start, head: '一键安装', sub: '选好版本与加载器，交给启动器',
     pt: ['版本 · 加载器 · 安装位置'], pct: [0.16, 0.5], label: '下载与安装', detail: '真实录屏 · 进度实时刷新'},
    {clip: CLIPS.launch, start: CLIP_MARKS.launch.downloading, head: '自动校验资源', sub: '首次启动自动补齐，逐文件校验',
     pt: ['分段下载 · 失败重试'], pct: [0.5, 0.94], label: '资源下载与校验', detail: '真实录屏 · 速度与进度可见'},
    {clip: CLIPS.game, start: g.menu, head: '进入游戏', sub: '游戏窗口打开，直达主菜单',
     pt: ['窗口自动打开 · 无需手动'], pct: [0.94, 1], label: '启动完成', detail: '真实游戏画面', cardH: 620},
    {clip: CLIPS.game, start: g.move, head: '实机畅玩', sub: '进去，就是世界', pt: ['启动器不参与游戏运行'], cardH: 620},
    {file: SHOTS.home, head: '不到一分钟', sub: '从点击到进入世界，全程无需手动干预', pt: ['一次点击，进入世界']},
  ];
  const s = stages[idx] as any;
  const pct = s.pct ? interpolate(local, [BF(2) - BF(0), Math.max(1, stepDur - 4)], s.pct as [number, number], clamp) : 0;
  return (
    <Scene local={local} dur={stepDur} head={s.head} sub={s.sub}
      clip={s.clip ? {src: s.clip, start: s.start} : undefined} file={s.file}
      cardH={s.cardH} cardW={1080} ry={idx % 2 ? 12 : -12} camRy={idx % 2 ? -9 : 9} camZ={130}
      click={idx === 0 ? {x: 1150, y: 470, label: '启动 Minecraft', atBeat: 1} : undefined}
      points={s.pt} closeup={idx === 3}
      lens={idx === 2 ? {x: 1180, y: 620, r: 104, label: '2.2×', atBeat: 1} : undefined}
      progress={s.pct ? {pct, label: s.label, detail: s.detail, atBeat: 2} : undefined}/>
  );
};

// ---------- 4) GAME HERO ----------
const GameHero: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const g = CLIP_MARKS.game;
  const scale = interpolate(l, [0, dur], [1.14, 1.0], {...clamp, easing: easeSwoop});
  const drift = interpolate(l, [0, dur], [-18, 18], {...clamp, easing: easeSmooth});
  const badgeOp = interpolate(l, [BF(1), BF(2)], [0, 1], clamp);
  return (
    <AbsoluteFill style={{overflow: 'hidden'}}>
      <AbsoluteFill style={{transform: `scale(${scale}) translateX(${drift}px)`, transformOrigin: '50% 50%'}}>
        <VideoClip src={CLIPS.game} start={g.menu} x={-140} y={-90} w={2200} h={1260} delay={0} floatAmp={0} mute/>
      </AbsoluteFill>
      <AbsoluteFill style={{background: 'linear-gradient(180deg, rgba(0,0,0,0.62) 0%, transparent 26%, transparent 62%, rgba(0,0,0,0.78) 100%)'}}/>
      <LightSweep delay={0} periodBeats={6} strength={0.2}/>
      <div style={{position: 'absolute', left: 96, bottom: 92, opacity: badgeOp}}>
        <div style={{display: 'inline-flex', alignItems: 'center', gap: 12, padding: '10px 18px', borderRadius: 999,
          background: 'rgba(0,0,0,0.55)', border: '1px solid rgba(255,255,255,0.22)'}}>
          <span style={{width: 10, height: 10, borderRadius: 999, background: '#FF3B30', boxShadow: '0 0 12px #FF3B30'}}/>
          <span style={{fontFamily: FONT, fontSize: 20, fontWeight: 700, color: '#fff', letterSpacing: 0.6}}>真实游戏画面 · Minecraft 1.21.11</span>
        </div>
        <div style={{marginTop: 16}}>
          <ExtrudeText size={84} delay={BF(1)} tint="silver" align="left">进去，就是世界。</ExtrudeText>
        </div>
      </div>
      <AbsoluteFill style={{boxShadow: 'inset 0 0 220px rgba(0,0,0,0.55)', pointerEvents: 'none'}}/>
    </AbsoluteFill>
  );
};

// ---------- 5) CONTENT 内容生态 ----------
const Content: React.FC<{dur: number; baseBeat: number; stepBeats: number}> = ({dur, baseBeat, stepBeats}) => {
  const {idx, local, stepDur} = useStep(baseBeat, stepBeats, 10, dur);
  const m5 = MARKS5;
  const stages = [
    {clip: CLIPS5.mods, start: m5.mods.search, head: '模组仓库', sub: '集成 Modrinth：搜索、筛选、一键安装', pt: ['依赖与版本自动匹配'],
     lens: {x: 900, y: 300, r: 96, label: '2.2×', atBeat: 1}},
    {clip: CLIPS5.shaders, start: m5.shaders.search, head: '光影仓库', sub: '搜索并安装光影包，画质立竿见影', pt: ['下载进度实时可见'],
     lens: {x: 1260, y: 420, r: 100, label: '2.2×', atBeat: 1}},
    {clip: CLIPS5.packs, start: m5.packs.search, head: '资源包仓库', sub: '纹理、音效、动画资源自由替换', pt: ['热门与搜索双模式']},
    {clip: CLIPS5.tour, start: m5.tour.mods, head: '实例模组管理', sub: '按实例隔离，启用停用即时生效', pt: ['互不干扰 · 一键切换']},
    {clip: CLIPS5.skins, start: m5.skins.nav, head: '3D 皮肤预览', sub: '皮肤站集成，拖动即可 360° 查看', pt: ['所见即所得'], closeup: true},
    {clip: CLIPS5.online, start: m5.online.nav, head: '联机 · Campfire', sub: '进入世界即开放局域网，邀请码发给好友', pt: ['随包附带 · 无需端口映射']},
    {clip: CLIPS5.saves, start: m5.saves.nav, head: '存档管理', sub: '备份、恢复、重命名与自定义路径', pt: ['数据安全无忧']},
    {clip: CLIPS5.logs, start: m5.logs.nav, head: '实例日志', sub: '实时读取、关键词过滤、崩溃报告解析', pt: ['问题定位更精准']},
    {clip: CLIPS5.overview, start: m5.overview.nav, head: '实例概览', sub: '版本、加载器、状态集中呈现', pt: ['快速切换与启动']},
    {file: SHOTS.instShaders, head: '内容生态', sub: '模组 · 光影 · 资源包 · 皮肤 · 联机，全部内置', pt: ['一个启动器，玩遍全部内容'], closeup: true},
  ];
  const s = stages[idx] as any;
  return (
    <Scene local={local} dur={stepDur} head={s.head} sub={s.sub}
      clip={s.clip ? {src: s.clip, start: s.start} : undefined} file={s.file}
      cardW={1100} ry={idx % 2 ? 13 : -13} camRy={idx % 2 ? -10 : 10} camZ={132}
      points={s.pt} closeup={s.closeup} lens={s.lens}
      progress={idx === 1 ? {pct: interpolate(local, [BF(2) - BF(0), Math.max(1, stepDur - 4)], [0.12, 1], clamp),
        label: '光影包下载与安装', detail: '真实录屏 · 进度实时刷新', atBeat: 1} : undefined}/>
  );
};

// ---------- 6) CHARACTER 主题工坊 ----------
const Character: React.FC<{dur: number; baseBeat: number; stepBeats: number}> = ({dur, baseBeat, stepBeats}) => {
  const {idx, local, stepDur} = useStep(baseBeat, stepBeats, 2, dur);
  const m5 = MARKS5.theme;
  if (idx === 0) {
    return <Scene local={local} dur={stepDur} head="主题工坊" sub="从 Minecraft 风格画面取色，整套配色一键应用"
      clip={{src: CLIPS5.theme, start: m5.nav}} cardW={1100} ry={-11}
      click={{x: 190, y: 620, label: '主题工坊', atBeat: 1}}
      points={['壁纸自动提取配色']}
      lens={{x: 520, y: 560, r: 100, label: '色卡', atBeat: 2}}/>;
  }
  return <Scene local={local} dur={stepDur} head="一键整套主题" sub="壁纸 + 配色 + 强调色同时应用"
    file={SHOTS.theme} cardW={1100} ry={11} closeup points={['10 张内置场景 · 也支持本地图片']}/>;
};

// ---------- 7) COVERAGE 全功能快剪 ----------
const Coverage: React.FC<{dur: number; baseBeat: number; stepBeats: number}> = ({dur, baseBeat, stepBeats}) => {
  const {idx, local, stepDur} = useStep(baseBeat, stepBeats, 12, dur);
  const m = CLIP_MARKS.onboarding;
  const i5 = MARKS5.install;
  const t = CLIP_MARKS.tour;
  const items = [
    {clip: CLIPS.onboarding, start: m.lang, head: '引导 · 语言'},
    {clip: CLIPS.onboarding, start: m.terms, head: '引导 · 条款'},
    {clip: CLIPS.onboarding, start: m.migrate, head: '引导 · 迁移'},
    {clip: CLIPS.onboarding, start: m.account, head: '引导 · 账户'},
    {clip: CLIPS5.tour2, start: MARKS5.tour2.versions, head: '安装 · 版本'},
    {clip: CLIPS5.tour2, start: MARKS5.tour2.loaders, head: '安装 · 加载器'},
    {clip: CLIPS5.install, start: i5.start, head: '安装 · 下载'},
    {clip: CLIPS5.tour, start: MARKS5.tour.overview, head: '实例概览'},
    {clip: CLIPS5.saves, start: MARKS5.saves.nav, head: '存档管理'},
    {clip: CLIPS5.logs, start: MARKS5.logs.nav, head: '实例日志'},
    {clip: CLIPS5.tour, start: t.global, head: '全局偏好'},
    {file: SHOTS.launchSet, head: '启动设置'},
  ];
  const it = items[idx] as any;
  return (
    <Scene local={local} dur={stepDur} head={it.head} sub="全功能覆盖 · 快速带过"
      clip={it.clip ? {src: it.clip, start: it.start} : undefined} file={it.file}
      cardW={1120} ry={idx % 2 ? 9 : -9} camRy={idx % 2 ? -7 : 7} camZ={128} deco={false} floor={false} entryBeats={2}/>
  );
};

// ---------- 8) MONTAGE ----------
const Montage: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const files = [SHOTS.home, SHOTS.tasks, SHOTS.versions, SHOTS.loaders, SHOTS.install, SHOTS.overview, SHOTS.mods, SHOTS.saves,
    SHOTS.instCfg, SHOTS.instLogs, SHOTS.repoMods, SHOTS.repoShaders, SHOTS.repoPacks, SHOTS.instPacks, SHOTS.instShaders,
    SHOTS.online, SHOTS.skins, SHOTS.diag, SHOTS.appLog, SHOTS.global, SHOTS.launchSet, SHOTS.theme, SHOTS.about, SHOTS.terms];
  const perBeat = files.length / 40;  // 40 拍内铺满
  const cardW = 306, gap = 16, cols = 4;
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(l, [0, dur], [-560, 220], clamp)} ry={interpolate(l, [0, dur], [-26, 26], clamp)} rx={8}
        scale={beatPunch(l, 30, PERIOD, 0.09)}>
        <div style={{position: 'absolute', left: 88, top: 78, zIndex: 30}}>
          <Tag text="功能全景 · 快速剪辑" accent/>
          <div style={{marginTop: 12}}><ExtrudeText size={72} delay={1} align="left" tint="silver">全部功能，一应俱全。</ExtrudeText></div>
        </div>
        {files.map((file, i) => {
          const d = BF(i * perBeat) - BF(0);
          const row = Math.floor(i / cols), col = i % cols;
          const p = interpolate(l, [d, d + 8], [0, 1], clamp);
          return (
            <div key={file + i} style={{position: 'absolute', left: col * (cardW + gap), top: 250 + row * (220 + gap),
              width: cardW, height: 220, transformStyle: 'preserve-3d',
              transform: `translateZ(${(1 - p) * -640}px) rotateY(${(col - 1.5) * 6}deg) rotateX(${(1 - p) * 34}deg) scale(${(0.72 + p * 0.28) * beatPunch(l - d, 30, PERIOD, 0.06)})`,
              opacity: interpolate(l, [d, d + 3], [0, 1], clamp)}}>
              <div style={{width: '100%', height: '100%', borderRadius: 12, overflow: 'hidden',
                border: `1px solid ${COLOR.line}`, boxShadow: '0 22px 60px rgba(0,0,0,0.78)'}}>
                <Img src={staticFile(`shots-v4/${file}`)} style={{width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top left'}}/>
              </div>
            </div>
          );
        })}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 9) CLOSE ----------
const Close: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const stepBeats = 3;
  const bounds = [0, BF(stepBeats) - BF(0), BF(stepBeats * 2) - BF(0), BF(stepBeats * 3) - BF(0)];
  let idx = 0;
  for (let k = 0; k < 4; k++) if (l >= bounds[k]) idx = k;
  const lines: {t: string; tint: 'silver' | 'blue' | 'gold' | 'green'}[] = [
    {t: '极简。', tint: 'silver'},
    {t: '一键启动。', tint: 'green'},
    {t: '全部功能。', tint: 'gold'},
    {t: 'SalmonLauncher', tint: 'silver'},
  ];
  const cur = lines[idx];
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 70% 50% at 50% 45%, rgba(126,217,87,0.16), transparent 70%)'}}/>
      <Camera tz={interpolate(l, [0, dur], [-280, 90], clamp)} rx={interpolate(l, [0, dur], [6, 0], clamp)}
        ry={interpolate(l, [0, dur], [-10, 10], clamp)} scale={beatPunch(l, 30, PERIOD, 0.05)}>
        <AbsoluteFill style={{display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
          <ExtrudeText size={idx === 3 ? 118 : 168} delay={0} tint={cur.tint} depth={12} tilt={5}>{cur.t}</ExtrudeText>
        </AbsoluteFill>
        {idx === 3 && (
          <div style={{position: 'absolute', left: 0, right: 0, bottom: 170, textAlign: 'center'}}>
            <Sub delay={2} size={26} color={COLOR.dim}>{LINKS.siteFull}　·　{LINKS.githubFull}</Sub>
          </div>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 10) OUTRO ----------
const Outro: React.FC<{total: number}> = ({total}) => {
  const l = useCurrentFrame();
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 75% 55% at 50% 34%, rgba(126,217,87,0.18), transparent 70%)'}}/>
      <Camera tz={interpolate(l, [0, Math.min(total, BF(16))], [-160, 50], clamp)}
        rx={interpolate(l, [0, total], [4, 0], clamp)} scale={beatPunch(l, 30, PERIOD, 0.04)}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 52, textAlign: 'center'}}>
          <ExtrudeText size={92} delay={0} tint="silver">SalmonLauncher</ExtrudeText>
          <div style={{marginTop: 8}}><Sub delay={3} size={29} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
        </div>
        {/* 三张二维码按等宽栅格排布，中心 588 / 960 / 1332 */}
        <QRCard file="site.png" title="官方网站" url={LINKS.site} x={418} y={226} delay={BF(4)} size={200} colW={340}/>
        <QRCard file="github.png" title="GitHub 仓库" url={LINKS.github} x={790} y={226} delay={BF(7)} size={200} colW={340}/>
        <QRCard file="blog.png" title="作者博客" url={LINKS.blog} x={1162} y={226} delay={BF(10)} size={200} colW={340}/>

        <div style={{position: 'absolute', left: 90, right: 90, bottom: 92, padding: '15px 22px', borderRadius: 16,
          background: 'rgba(20,20,22,0.84)', border: `1px solid ${COLOR.line}`,
          opacity: interpolate(l, [BF(12), BF(14)], [0, 1], clamp), transform: `translateY(${interpolate(l, [BF(12), BF(14)], [22, 0], clamp)}px)`}}>
          <div style={{fontFamily: FONT, fontSize: 18, color: COLOR.ink, fontWeight: 700, marginBottom: 6}}>视频声明 · 制作人员 · 致谢名单 · 版权解释</div>
          <div style={{fontFamily: FONT, fontSize: 15, color: COLOR.dim, lineHeight: 1.6}}>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>制作人员：</span>Hugo Mou（产品设计 / 开发 / 维护）　<span style={{color: COLOR.ink, fontWeight: 600}}>致谢：</span>Modrinth 与社区作者 · Campfire 联机组件 · .NET / Windows App SDK / WinUI 3 · Mojang 官方版本与资源生态</div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>配乐与素材：</span>“3 Strikes” — Terror Jr（Bilibili BV1tek2YsEYY，仅作展示） · 画面为 SalmonLauncher 2.0.15 真实录屏与界面截图</div>
            <div><span style={{color: COLOR.accent, fontWeight: 700}}>本软件由 AI 辅助开发制作　·　本宣传片由 AI 生成制作</span></div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>项目许可：</span>GPL-3.0-or-later（开源免费，Windows x64）　官网 {LINKS.siteFull}　GitHub {LINKS.githubFull}</div>
            <div style={{marginTop: 4, fontSize: 13.5, color: 'rgba(134,134,139,0.92)'}}>版权与声明：Minecraft 为 Mojang Studios / Microsoft 的商标，本项目与 Mojang/Microsoft 无官方关联；Modrinth、Campfire 等名称与服务归各自权利人所有。本视频仅用于 SalmonLauncher 产品展示与介绍，不构成对第三方商标或内容的授权声明。</div>
          </div>
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 时间线（全部以拍为单位，用真实拍点换算帧） ----------
export const Film: React.FC = () => {
  const seg = (startBar: number, barCount: number) => ({from: BARF(startBar), dur: BARF(startBar + barCount) - BARF(startBar)});
  const S = {
    hook:      seg(0, 9),      //  0–9   小节
    brand:     seg(9, 7),      //  9–16
    oneclick:  seg(16, 24),    // 16–40
    gamehero:  seg(40, 4),     // 40–44
    content:   seg(44, 38),    // 44–82
    character: seg(82, 10),    // 82–92
    coverage:  seg(92, 32),    // 92–124
    montage:   seg(124, 10),   // 124–134
    close:     seg(134, 12),   // 134–146
  };
  const outroFrom = BARF(146);
  const outroDur = Math.max(1, toF(FILM.duration) - outroFrom);

  const f = useCurrentFrame();
  const t = f / FILM.fps;
  let flash = 0;
  for (let i = 0; i < BEAT_COUNT; i += 1) {
    const down = i % 4 === 0;
    const d = down ? 0.26 : i % 2 === 0 ? 0.13 : 0.06;
    const w = down ? 0.1 : 0.06;
    flash = Math.max(flash, hit(t, i, w) * d);
  }
  const cuts = [S.brand.from, S.oneclick.from, S.gamehero.from, S.content.from, S.character.from,
    S.coverage.from, S.montage.from, S.close.from, outroFrom];
  const whipFrames = Math.max(4, BF(1) - BF(0));

  return (
    <Stage>
      <Audio src={staticFile(AUDIO_FILE)}/>
      <ParticleField count={260} energy={1.1}/>
      <GlowBands speed={1.4} opacity={0.2}/>

      <Sequence from={S.hook.from}      durationInFrames={S.hook.dur}><Hook dur={S.hook.dur}/></Sequence>
      <Sequence from={S.brand.from}     durationInFrames={S.brand.dur}><Brand dur={S.brand.dur} baseBeat={36} stepBeats={9}/></Sequence>
      <Sequence from={S.oneclick.from}  durationInFrames={S.oneclick.dur}><OneClick dur={S.oneclick.dur} baseBeat={64} stepBeats={16}/></Sequence>
      <Sequence from={S.gamehero.from}  durationInFrames={S.gamehero.dur}><GameHero dur={S.gamehero.dur}/></Sequence>
      <Sequence from={S.content.from}   durationInFrames={S.content.dur}><Content dur={S.content.dur} baseBeat={176} stepBeats={15}/></Sequence>
      <Sequence from={S.character.from} durationInFrames={S.character.dur}><Character dur={S.character.dur} baseBeat={328} stepBeats={20}/></Sequence>
      <Sequence from={S.coverage.from}  durationInFrames={S.coverage.dur}><Coverage dur={S.coverage.dur} baseBeat={368} stepBeats={11}/></Sequence>
      <Sequence from={S.montage.from}   durationInFrames={S.montage.dur}><Montage dur={S.montage.dur}/></Sequence>
      <Sequence from={S.close.from}     durationInFrames={S.close.dur}><Close dur={S.close.dur}/></Sequence>
      <Sequence from={outroFrom}        durationInFrames={outroDur}><Outro total={outroDur}/></Sequence>

      {cuts.map((c, i) => (
        <React.Fragment key={`cut-${i}`}>
          <WhipPan at={c} frames={whipFrames} dir={i % 2 === 0 ? 1 : -1} strength={1.5}/>
          <ZoomBurst at={c} frames={Math.round(BF(1) - BF(0))} strength={1.2}/>
          <RippleGrid x={960} y={540} at={c} count={3}/>
        </React.Fragment>
      ))}

      <ChromaticEdge strength={f >= outroFrom ? 0 : 1.2}/>
      <BokehVignette strength={f >= outroFrom ? 0.22 : 1.0}/>
      {flash > 0.001 && f < outroFrom && (
        <AbsoluteFill style={{backgroundColor: '#fff', opacity: flash * 0.3, mixBlendMode: 'screen', pointerEvents: 'none'}}/>
      )}
      <AbsoluteFill style={{boxShadow: 'inset 0 0 220px rgba(0,0,0,0.6)', pointerEvents: 'none'}}/>
      <LegalStrip/>
    </Stage>
  );
};