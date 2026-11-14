// Film.tsx — SalmonLauncher promo, Apple-keynote energy, 177.6s tightly synced to "3 Strikes" (108 BPM).
// Fast beat cuts, aggressive 3D camera, metallic gloss, real clean UI screenshots (v2.0.13, default/no Mica).
import React from 'react';
import {AbsoluteFill, Audio, Img, Sequence, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {AUDIO_FILE, COLOR, CREDITS, FILM, FONT, LINKS, SHOTS, toF} from './spec';
import {BEAT_COUNT, PERIOD, beatFrame, hit} from './lib/beats';
import {Big, Camera, ClickFx, LegalStrip, MetalText, Panel, QRCard, ScanLine, Shot3D, Slam, Stage, Sub, Tag, beatPunch, clamp, easeOut, snap} from './lib/fx';

const BF = (i: number) => beatFrame(i, FILM.fps);
const stepDur = (beats: number) => Math.round(beats * PERIOD * FILM.fps);
const beatOf = (l: number) => Math.floor(l / (PERIOD * FILM.fps)); // local beat index
const bprog = (l: number) => ((l / (PERIOD * FILM.fps)) % 1); // 0..1 within local beat
const clickP = (l: number, startBeat: number, durBeats = 1) => interpolate(l, [stepDur(startBeat), stepDur(startBeat + durBeats)], [0, 1], clamp);

// --- micro overlays ---
const Pulse: React.FC<{x: number; y: number; at: number; c?: string}> = ({x, y, at, c = COLOR.accent}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 22) return null;
  const s = interpolate(t, [0, 18], [0.2, 2.4], {extrapolateRight: 'clamp', easing: easeOut});
  const o = interpolate(t, [0, 2, 18], [0, 1, 0], clamp);
  const s2 = interpolate(t, [3, 20], [0.2, 1.8], {extrapolateRight: 'clamp', easing: easeOut});
  const o2 = interpolate(t, [3, 6, 20], [0, 0.7, 0], clamp);
  return <>
    <div style={{position: 'absolute', left: x - 34, top: y - 34, width: 68, height: 68, borderRadius: 999, border: `4px solid ${c}`, opacity: o, transform: `scale(${s})`, boxShadow: `0 0 40px ${c}`}} />
    <div style={{position: 'absolute', left: x - 34, top: y - 34, width: 68, height: 68, borderRadius: 999, border: `3px solid ${c}`, opacity: o2, transform: `scale(${s2})`}} />
  </>;
};

const Cursor: React.FC<{x1: number; y1: number; x2: number; y2: number; at: number; dur: number}> = ({x1, y1, x2, y2, at, dur}) => {
  const l = useCurrentFrame();
  const p = interpolate(l, [at, at + dur], [0, 1], {...clamp, easing: easeOut});
  const cx = x1 + (x2 - x1) * p;
  const cy = y1 + (y2 - y1) * p;
  const o = interpolate(l, [at - 2, at, at + dur, at + dur + 10], [0, 1, 1, 0], clamp);
  const click = l >= at + dur ? interpolate(l, [at + dur, at + dur + 3], [1, 0.85], clamp) : 1;
  return (
    <div style={{position: 'absolute', left: cx, top: cy, opacity: o, transform: `translate(-2px,-2px) scale(${click})`, filter: `drop-shadow(0 0 10px ${COLOR.accent})`}}>
      <svg width="38" height="46" viewBox="0 0 38 46" fill="none">
        <path d="M2 2L2 38L11 29L17 43L22 41L16 27L28 27L2 2Z" fill={COLOR.accent} stroke="#000" strokeWidth="2" strokeLinejoin="round"/>
      </svg>
    </div>
  );
};

const Bar: React.FC<{x: number; y: number; w: number; at: number; dur: number; label?: string; done?: string}> = ({x, y, w, at, dur, label, done}) => {
  const l = useCurrentFrame();
  const p = interpolate(l, [at, at + dur], [0, 1], clamp);
  if (l < at - 2) return null;
  const finished = p >= 1;
  return (
    <div style={{position: 'absolute', left: x, top: y, width: w, opacity: interpolate(l, [at - 2, at + 3], [0, 1], clamp)}}>
      {label && <div style={{fontFamily: FONT, fontSize: 17, color: COLOR.dim, marginBottom: 6}}>{label}</div>}
      <div style={{height: 12, borderRadius: 999, background: 'rgba(255,255,255,0.12)', overflow: 'hidden', border: `1px solid ${COLOR.line}`}}>
        <div style={{height: '100%', width: `${p * 100}%`, borderRadius: 999, background: `linear-gradient(90deg, ${COLOR.accent}, ${COLOR.cyan})`, boxShadow: `0 0 24px ${COLOR.accent}`}} />
      </div>
      {finished && done && <div style={{fontFamily: FONT, fontSize: 18, color: COLOR.accent, marginTop: 6, fontWeight: 600}}>{done}</div>}
    </div>
  );
};

const Bullet: React.FC<{text: string; x: number; y: number; delay: number; accent?: boolean}> = ({text, x, y, delay, accent}) => {
  const l = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = spring({frame: l - delay, fps, config: {damping: 16, stiffness: 150}});
  const o = interpolate(l, [delay, delay + 4], [0, 1], clamp);
  return (
    <div style={{position: 'absolute', left: x, top: y, display: 'flex', alignItems: 'center', gap: 12, opacity: o, transform: `translateX(${(1 - p) * 40}px)`}}>
      <div style={{width: 10, height: 10, borderRadius: 99, background: accent ? COLOR.accent : COLOR.cyan, boxShadow: `0 0 18px ${accent ? COLOR.accent : COLOR.cyan}`}} />
      <div style={{fontFamily: FONT, fontSize: 26, color: accent ? COLOR.ink : COLOR.dim, fontWeight: accent ? 600 : 500}}>{text}</div>
    </div>
  );
};

// 下载/安装等待详情：随进度条展示已下载大小、速度、预计剩余时间，给用户“正在安装，请稍候”的真实感觉
const DownloadInfo: React.FC<{x:number;y:number;at:number;dur:number}> = ({x,y,at,dur}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > dur) return null;
  const p = Math.min(1, t / dur);
  const mbps = 2.4 + Math.sin(p * Math.PI) * 0.7;
  const total = 720;
  const done = Math.round(total * p);
  const remain = Math.round((total - done) / mbps);
  const mm = String(Math.floor(remain / 60)).padStart(2, '0');
  const ss = String(remain % 60).padStart(2, '0');
  return (
    <div style={{position:'absolute',left:x,top:y,fontFamily:FONT,fontSize:20,color:COLOR.dim,lineHeight:1.6,opacity:interpolate(l,[at,at+2,at+dur-2,at+dur],[0,1,1,0],clamp)}}>
      <div style={{color:COLOR.accent,fontWeight:700,marginBottom:2}}>正在下载游戏资源，请稍候…</div>
      <div>已下载 {done} MB / {total} MB</div>
      <div>速度 {mbps.toFixed(1)} MB/s · 预计剩余 {mm}:{ss}</div>
    </div>
  );
};

// 用户真实点击按钮：光标移动到按钮 + 点击光环 + 闪光，全程保持 3D 镜头语境
const ClickPoint: React.FC<{x:number;y:number;at:number;label?:string}> = ({x,y,at,label}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 36) return null;
  const flash = interpolate(t, [0, 6, 12], [0, 0.35, 0], clamp);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{position:'absolute',left:0,top:0,width:'100%',height:'100%',backgroundColor:'#fff',opacity:flash,mixBlendMode:'screen',pointerEvents:'none'}} />
      <Cursor x1={x-130} y1={y-70} x2={x} y2={y} at={at} dur={stepDur(1)} />
      <ClickFx x={x} y={y} at={at+stepDur(1)} label={label} size={1.15} />
      <Pulse x={x} y={y} at={at+stepDur(2)} />
    </AbsoluteFill>
  );
};

// 浮动 3D 剪贴图：每拍翻转角度并轻微上下浮动，作为装饰性 3D 动画，与主截图一起出镜
const FloatCard: React.FC<{file:string;x:number;y:number;w:number;baseBeat:number;label?:string;ryBase?:number}> = ({file,x,y,w,baseBeat,label,ryBase=0}) => {
  const l = useCurrentFrame();
  const b = Math.floor(l / stepDur(1)) - baseBeat;
  const bp = (l % stepDur(1)) / stepDur(1);
  const ry = ryBase + (b % 2 === 0 ? 14 : -14) * (0.7 + 0.3 * Math.sin(b));
  const rx = (b % 3 === 0 ? 4 : -3) + 6 * Math.sin(bp * Math.PI);
  const tz = 40 + (b % 2 === 0 ? 50 : 0);
  const floatY = Math.sin((l + baseBeat * 7) / 20) * 12;
  const punch = 1 + 0.05 * Math.max(0, 1 - bp * 2) ** 2;
  return <Shot3D file={file} x={x} y={y + floatY} w={w} delay={0} ry={ry} rx={rx} z={tz} scale={punch} label={label} />;
};

// 3D rotating shot that changes angle on every beat
const BeatShot: React.FC<{file: string; x: number; y: number; w: number; baseBeat: number; beats: number; label?: string; ryBase?: number; rxBase?: number; z?: number}> =
({file, x, y, w, baseBeat, beats, label, ryBase = 0, rxBase = 4, z = 0}) => {
  const l = useCurrentFrame();
  const seg = stepDur(beats);
  if (l < 0 || l > seg) return null;
  const b = Math.floor(l / stepDur(1));
  const bp = (l % stepDur(1)) / stepDur(1);
  const punch = 1 + 0.04 * Math.max(0, 1 - bp * 2) ** 2;
  const ry = ryBase + (b % 2 === 0 ? 10 : -10) * (0.6 + 0.4 * Math.sin(b));
  const rx = rxBase + (b % 3 === 0 ? 3 : -2);
  const tz = z + (b % 2 === 0 ? 60 : 0);
  return <Shot3D file={file} x={x} y={y} w={w} delay={0} ry={ry} rx={rx} z={tz} scale={punch} label={label} />;
};

// ---------- 1) IMPACT INTRO (beats 0-16) ----------
const Impact: React.FC<{base: number}> = () => {
  const l = useCurrentFrame();
  const {fps} = useVideoConfig();
  const hits = [0, 2, 4, 6, 8, 10, 12, 14];
  return (
    <AbsoluteFill>
      {hits.map((b, i) => {
        const o = interpolate(l, [BF(b), BF(b) + 1, BF(b) + 4], [0, i % 4 === 0 ? 0.45 : 0.22, 0], clamp);
        return <AbsoluteFill key={b} style={{backgroundColor: i % 2 ? COLOR.accent : '#fff', opacity: o, mixBlendMode: 'screen'}} />;
      })}
      <Camera tz={interpolate(l, [0, stepDur(16)], [-350, 120], clamp)} rx={interpolate(l, [0, stepDur(8)], [10, 0], clamp)} ry={interpolate(l, [0, stepDur(16)], [-8, 8], clamp)}>
        {l >= BF(0) && l < BF(4) && (
          <>
            <AbsoluteFill style={{backgroundColor:'#fff',opacity: interpolate(l,[BF(0),BF(0)+2,BF(0)+7],[0,0.4,0],clamp),mixBlendMode:'screen',pointerEvents:'none'}} />
            <Slam delay={Math.max(0,BF(0)-8)} size={170} tint="silver">启动。</Slam>
          </>
        )}
        {l >= BF(4) && l < BF(8) && <Slam delay={BF(4)} size={170} tint="green">安装。</Slam>}
        {l >= BF(8) && l < BF(12) && <Slam delay={BF(8)} size={170} tint="blue">畅玩。</Slam>}
        {l >= BF(12) && l < BF(14) && <Slam delay={BF(12)} size={150} tint="gold">每一步。</Slam>}
        {l >= BF(14) && (
          <AbsoluteFill style={{justifyContent: 'center', alignItems: 'center'}}>
            <MetalText size={136} delay={BF(14)} from="scale" tint="silver">SalmonLauncher</MetalText>
            <div style={{position: 'absolute', top: 665}}><Sub delay={BF(15)} size={36}>真正好用的 Minecraft Java 启动器</Sub></div>
            <ScanLine delay={BF(14)} y={625} />
          </AbsoluteFill>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 2) HERO (beats 16-32) ----------
const Hero: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const camZ = interpolate(l, [0, stepDur(16)], [-400, 140], {extrapolateRight: 'clamp', easing: easeOut});
  const camRy = interpolate(l, [0, stepDur(16)], [14, -10], clamp);
  const camRx = interpolate(l, [0, stepDur(8), stepDur(16)], [6, 2, 5], clamp);
  return (
    <AbsoluteFill>
      <Camera tz={camZ} ry={camRy} rx={camRx} scale={beatPunch(l, 30, PERIOD, 0.05)}>
        <div style={{position: 'absolute', left: 110, top: 160}}>
          <Tag text="SALMONLAUNCHER 2.0.13 · WINDOWS X64" delay={0} accent />
          <div style={{marginTop: 28}}><MetalText size={100} delay={1} from="left" tint="silver" align="left">{'从刚进入启动器\n到畅玩 Minecraft'}</MetalText></div>
          <div style={{marginTop: 22}}><Big size={48} delay={4} align="left" color={COLOR.dim} from="left">每一步都清楚。每个功能都细讲。</Big></div>
          <div style={{marginTop: 28, display: 'flex', gap: 12, flexWrap: 'wrap', maxWidth: 640}}>
            {['Windows 原生 WinUI 3', '开源免费 GPL-3.0', '.NET 10 内置', '真实界面 · 全新默认状态'].map((t, i) => <Tag key={t} text={t} delay={6 + i * 2} accent={i === 3} />)}
          </div>
        </div>
        <Shot3D file={SHOTS.home} x={820} y={110} w={1020} delay={2} ry={-18} rx={7} z={80} label="启动面板 · 真实使用画面" floatAmp={5} />
        <Shot3D file={SHOTS.about} x={640} y={560} w={380} delay={6} ry={14} rx={-4} z={160} label="关于" />
        <Shot3D file={SHOTS.tasks} x={1040} y={600} w={360} delay={8} ry={-6} rx={-3} z={140} label="任务中心" />
        <Shot3D file={SHOTS.theme} x={1160} y={540} w={350} delay={7} ry={12} rx={-4} z={150} label="主题预览 · 云母默认关闭，可选开启" />
        <FloatCard file={SHOTS.tasks} x={1260} y={600} w={320} baseBeat={4} label="任务中心" ryBase={-12} />
        <FloatCard file={SHOTS.about} x={740} y={620} w={300} baseBeat={8} label="关于" ryBase={14} />
        <ClickPoint x={1460} y={760} at={stepDur(10)} label="点击启动" />
      </Camera>
      <div style={{position: 'absolute', left: 110, bottom: 120, display: 'flex', gap: 14}}>
        <Tag text={LINKS.siteFull} delay={10} accent size={20} />
        <Tag text={LINKS.githubFull} delay={11} accent size={20} />
      </div>
    </AbsoluteFill>
  );
};

// ---------- 3) ONBOARDING (beats 32-80, 48 beats = 8 beats per step? 6 steps) ----------
const Onboarding: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const steps = [
    {file: SHOTS.wLang, title: '选择语言', detail: '简体中文 / 繁體中文 / English / 日本語，进入即选', n: 1},
    {file: SHOTS.wTerms, title: '阅读并同意条款', detail: '用户协议与免责声明清晰展示，勾选后继续', n: 2},
    {file: SHOTS.wMigrate, title: '迁移数据（可选）', detail: '可一键识别，也可手动选择旧启动器或 .minecraft 数据文件夹', n: 3},
    {file: SHOTS.wAccount, title: '添加账户', detail: '离线账户 / 微软账号登录，之后可随时管理', n: 4},
    {file: SHOTS.wTheme, title: '外观与配色', detail: '主题与强调色；默认普通背景，云母材质按需可选开启', n: 5},
    {file: SHOTS.wDone, title: '完成引导', detail: '初始化完成，直接进入启动器开始使用', n: 6},
  ];
  const seg = stepDur(4); // 4 beats per step, fast
  const idx = Math.min(steps.length - 1, Math.floor(l / seg));
  const ls = l - idx * seg;
  const b = beatOf(ls);
  const s = steps[idx];
  const camRy = interpolate(ls, [0, seg], [16, -16], clamp);
  const camRx = interpolate(ls, [0, seg / 2, seg], [3, 7, 2], clamp);
  const camZ = interpolate(ls, [0, seg / 2, seg], [-80, 120, 20], clamp);
  const angles = [ {ry: -22, rx: 8, z: 80}, {ry: 18, rx: 4, z: 160}, {ry: -14, rx: 10, z: 120}, {ry: 24, rx: 5, z: 100} ];
  const a = angles[b % angles.length];
  return (
    <AbsoluteFill>
      <Camera ry={camRy} rx={camRx} tz={camZ} scale={beatPunch(ls, 30, PERIOD, 0.025)}>
        <div style={{position: 'absolute', left: 90, top: 100}}>
          <Tag text="首次启动 · 从零开始使用" accent delay={0} />
          <div style={{marginTop: 16}}><MetalText size={68} delay={0} from="left" tint="silver" align="left">引导流程，一步到位。</MetalText></div>
        </div>
        <Shot3D key={s.file + idx} file={s.file} x={620} y={150} w={1200} delay={idx * seg} ry={a.ry} rx={a.rx} z={a.z} label={`${s.n}/6 · ${s.title}`} enter={idx % 2 ? 'right' : 'left'} />
        <Panel x={90} y={300} w={460} delay={1}>
          {steps.map((st, i) => (
            <div key={st.n} style={{display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14, opacity: i === idx ? 1 : 0.45}}>
              <div style={{width: 32, height: 32, borderRadius: 99, display: 'flex', alignItems: 'center', justifyContent: 'center', background: i === idx ? COLOR.accent : 'rgba(255,255,255,0.12)', color: i === idx ? '#000' : COLOR.ink, fontFamily: FONT, fontWeight: 700, fontSize: 16, boxShadow: i === idx ? `0 0 20px ${COLOR.accent}` : 'none'}}>{st.n}</div>
              <div style={{fontFamily: FONT, fontSize: 22, color: i === idx ? COLOR.ink : COLOR.dim, fontWeight: i === idx ? 700 : 500}}>{st.title}</div>
            </div>
          ))}
        </Panel>
        <Panel x={90} y={700} w={460} delay={3}>
          <div style={{fontFamily: FONT, fontSize: 24, color: COLOR.ink, fontWeight: 600, lineHeight: 1.5}}>{s.detail}</div>
        </Panel>
        <Cursor x1={1100} y1={480} x2={1460} y2={760} at={idx * seg + stepDur(2)} dur={stepDur(1)} />
        <Pulse x={1460} y={760} at={idx * seg + stepDur(3)} />
        <ClickPoint x={1460} y={760} at={idx * seg + stepDur(4)} label={s.title} />
        <FloatCard file={SHOTS.wTheme} x={1260} y={620} w={330} baseBeat={idx * 4} label="外观" ryBase={-10} />
        <FloatCard file={SHOTS.wDone} x={740} y={660} w={310} baseBeat={idx * 4 + 2} label="完成" ryBase={12} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 4) LAUNCH + PLAY (beats 80-104) ----------
const LaunchPlay: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const phase = Math.min(3, Math.floor(l / stepDur(6)));
  const lp = l - phase * stepDur(6);
  const camRy = interpolate(l, [0, stepDur(24)], [-10, 12], clamp);
  const camZ = interpolate(l, [0, stepDur(12), stepDur(24)], [-60, 140, 40], clamp);
  const labels = ['选择实例与账户', '点击启动', '自动准备并下载依赖', '进入游戏 · 开始畅玩'];
  const notes = ['版本、账户、Java 状态一目了然，选好实例即可', '一键启动，启动前自动检查与准备运行环境', '任务实时进度：补全资源、安装依赖、镜像回退、断点续传', '准备完成后启动游戏客户端，从启动器到畅玩全流程完成'];
  return (
    <AbsoluteFill>
      <Camera ry={camRy} rx={5} tz={camZ} scale={beatPunch(l, 30, PERIOD, 0.05)}>
        <div style={{position: 'absolute', left: 90, top: 120}}>
          <Tag text="启动与进入游戏 · 完整流程" accent />
          <div style={{marginTop: 18}}><MetalText size={76} delay={0} from="left" tint="green" align="left">{'选好实例\n一键畅玩。'}</MetalText></div>
        </div>
        <Shot3D file={SHOTS.home} x={720} y={100} w={1100} delay={0} ry={phase === 0 ? -12 : -6} rx={5} z={phase === 0 ? 80 : 30} label="启动面板" />
        {phase >= 1 && <Cursor x1={1200} y1={520} x2={1500} y2={720} at={stepDur(6)} dur={stepDur(2)} />}
        {phase >= 1 && <Pulse x={1500} y={720} at={stepDur(8)} />}
        {phase >= 1 && <ClickPoint x={1500} y={720} at={stepDur(6)} label="一键启动" />}
        {phase >= 2 && <Shot3D file={SHOTS.tasks} x={560} y={520} w={520} delay={stepDur(12)} ry={12} rx={-3} z={160} label="任务实时进度" enter="bottom" />}
        <FloatCard file={SHOTS.versions} x={1260} y={500} w={330} baseBeat={phase * 6} label="版本" ryBase={-12} />
        <FloatCard file={SHOTS.loaders} x={740} y={620} w={310} baseBeat={phase * 6 + 3} label="加载器" ryBase={14} />
        {phase >= 2 && <Bar x={600} y={880} w={440} at={stepDur(13)} dur={stepDur(8)} label="准备资源 / 检查依赖 / 补全文件…" done={phase >= 3 ? '✓ 准备完成，正在启动游戏' : undefined} />}
        {phase >= 2 && <div style={{position:'absolute',left:600,top:990,fontFamily:FONT,fontSize:18,color:COLOR.dim,opacity:interpolate(l,[stepDur(13),stepDur(13)+2,stepDur(21)-2,stepDur(21)],[0,1,1,0],clamp)}}>资源校验中，请稍候…</div>}
        {phase >= 3 && <Panel x={1180} y={620} w={520} delay={stepDur(19)}><div style={{fontFamily: FONT, fontSize: 34, color: COLOR.accent, fontWeight: 700}}>已进入 Minecraft，开始畅玩</div><div style={{fontFamily: FONT, fontSize: 22, color: COLOR.dim, marginTop: 8}}>启动器全程可见进度与状态，出错可直达诊断</div></Panel>}
        <Panel x={90} y={360} w={520} delay={1}>
          <div style={{fontFamily: FONT, fontSize: 20, color: COLOR.accent, fontWeight: 700, marginBottom: 10}}>步骤 {phase + 1} / 4 · {labels[phase]}</div>
          <div style={{fontFamily: FONT, fontSize: 26, color: COLOR.ink, lineHeight: 1.5}}>{notes[phase]}</div>
        </Panel>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 5) INSTALL (beats 104-136) ----------
const Install: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(4);
  const stages = [
    {file: SHOTS.versions, t: '选择游戏版本', note: '官方版本列表完整可选，直接挑选想玩的 Minecraft 版本创建实例', tags: ['正式版/快照', '版本清单', '一键选择'], accent: '想玩哪个版本，直接装。'},
    {file: SHOTS.loaders, t: '选择加载器', note: 'Fabric / Quilt / Forge / NeoForge 与原版自由组合，适配模组生态', tags: ['Fabric', 'Quilt', 'Forge', 'NeoForge'], accent: '模组加载器，一步配齐。'},
    {file: SHOTS.install, t: '确认安装选项', note: '实例名称、游戏版本与加载器组合、安装参数一屏确认后开始安装', tags: ['实例命名', '版本组合', '安装参数'], accent: '确认无误，一键安装。'},
    {file: SHOTS.tasks, t: '下载与安装任务', note: '多线程分段下载、镜像自动回退、断点续传，实时速度与进度可见', tags: ['多线程分段', '镜像回退', '断点续传', '实时速度'], accent: '下载过程，全程透明。'},
  ];
  const idx = Math.min(3, Math.floor(l / seg));
  const ls = l - idx * seg;
  const st = stages[idx];
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls, [0, seg], [14 - idx * 6, -14 + idx * 4], clamp)} rx={6} tz={interpolate(ls, [0, seg / 2, seg], [-40, 130, 30], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.03)}>
        <div style={{position: 'absolute', left: 90, top: 110}}>
          <Tag text="安装与下载 · 细讲" accent />
          <div style={{marginTop: 16}}><MetalText size={70} delay={0} from="left" tint="blue" align="left">{st.t}</MetalText></div>
          <div style={{marginTop: 14}}><Sub delay={2} align="left" size={28} maxWidth={520}>{st.note}</Sub></div>
          <div style={{marginTop: 18, display: 'flex', gap: 10, flexWrap: 'wrap', maxWidth: 520}}>{st.tags.map((tg, i) => <Tag key={tg} text={tg} delay={3 + i} accent={i === 0} size={20} />)}</div>
        </div>
        <Shot3D key={st.file + idx} file={st.file} x={700} y={130} w={1120} delay={idx * seg} ry={-14 + idx * 6} rx={6} z={80} label={st.t} enter={idx % 2 ? 'right' : 'left'} />
        <Bar x={760} y={820} w={980} at={idx * seg + 2} dur={stepDur(3.6)} label="下载游戏资源 · 多连接加速 · 镜像自动回退，请稍候…" done={idx === 3 ? '✓ 安装完成，实例已就绪' : undefined} />
        {idx === 2 && <ClickPoint x={1300} y={700} at={idx * seg + stepDur(2)} label="一键安装" />}
        {idx === 3 && <DownloadInfo x={760} y={900} at={idx * seg + 2} dur={stepDur(3.6)} />}
        {idx === 3 && <ClickPoint x={1300} y={700} at={idx * seg + stepDur(4)} label="开始下载" />}
        <Pulse x={1300} y={700} at={idx * seg + stepDur(2)} />
        <FloatCard file={SHOTS.versions} x={1260} y={520} w={330} baseBeat={idx * 4} label="版本" ryBase={-12} />
        <FloatCard file={SHOTS.loaders} x={740} y={620} w={310} baseBeat={idx * 4 + 2} label="加载器" ryBase={14} />
        <div style={{position: 'absolute', left: 90, bottom: 110}}><Big size={36} delay={4} align="left" color={COLOR.accent}>{st.accent}</Big></div>
      </Camera>
      <div style={{position: 'absolute', right: 80, bottom: 110, display: 'flex', gap: 10}}>{stages.map((s, i) => <Tag key={s.t} text={`${i + 1}`} delay={0} accent={i === idx} />)}</div>
    </AbsoluteFill>
  );
};

// ---------- 6) INSTANCES (beats 136-176) ----------
const Instances: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(4);
  const stages = [
    {file: SHOTS.overview, t: '实例概览', note: '版本与加载器信息、快捷操作、健康检查、复制实例、打开文件夹，一页掌握', bullets: ['版本/加载器信息', '健康检查与快捷操作', '复制实例 · 打开目录']},
    {file: SHOTS.mods, t: '模组管理', note: '每个实例独立管理模组：启用/禁用、安装与移除，互不影响', bullets: ['启用 / 禁用切换', '安装与移除模组', '按实例隔离']},
    {file: SHOTS.saves, t: '存档管理', note: '备份存档、从历史备份还原，世界数据安全可回退', bullets: ['一键备份存档', '从历史备份还原', '数据可回退不丢失']},
    {file: SHOTS.instCfg, t: '实例设置', note: '为单个实例单独配置内存、Java、窗口与启动参数，覆盖全局设置', bullets: ['独立内存/Java 配置', '窗口与参数', '覆盖全局设置'], alt: undefined as string|undefined},
    {file: SHOTS.instLogs, t: '实例日志', note: '查看该实例运行日志，定位问题有据可查，配合诊断快速排查', bullets: ['运行日志查看', '问题定位', '与诊断联动'], alt: undefined as string|undefined},
  ];
  const idx = Math.min(stages.length - 1, Math.floor(l / seg));
  const ls = l - idx * seg;
  const st = stages[idx];
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls, [0, seg], [16, -16], clamp)} rx={5} tz={interpolate(ls, [0, seg / 2, seg], [-30, 120, 20], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.025)}>
        <div style={{position: 'absolute', left: 90, top: 110}}>
          <Tag text="实例管理 · 每个世界独立一间房" accent />
          <div style={{marginTop: 16}}><MetalText size={72} delay={0} from="left" tint="silver" align="left">{st.t}</MetalText></div>
          <div style={{marginTop: 12}}><Sub delay={2} align="left" size={28} maxWidth={500}>{st.note}</Sub></div>
        </div>
        <Shot3D key={st.file + idx} file={st.file} x={700} y={120} w={1120} delay={idx * seg} ry={idx % 2 ? 14 : -14} rx={6} z={70} label={st.t} enter={idx % 2 ? 'left' : 'right'} />
        {st.bullets.map((b, i) => <Bullet key={b} text={b} x={90} y={420 + i * 54} delay={idx * seg + 2 + i} accent={i === 0} />)}
        <Pulse x={1280} y={620} at={stepDur(5)} />
        {idx === 0 && <ClickPoint x={1280} y={520} at={idx * seg + stepDur(2)} label="健康检查" />}
        {idx === 1 && <ClickPoint x={1280} y={560} at={idx * seg + stepDur(2)} label="启用模组" />}
        {idx === 2 && <ClickPoint x={1280} y={620} at={idx * seg + stepDur(2)} label="备份存档" />}
        {idx === 3 && <ClickPoint x={1280} y={660} at={idx * seg + stepDur(2)} label="打开目录" />}
        {idx === 4 && <ClickPoint x={1280} y={560} at={idx * seg + stepDur(2)} label="查看日志" />}
        <FloatCard file={SHOTS.mods} x={1260} y={520} w={330} baseBeat={idx * 4} label="模组" ryBase={-12} />
        <FloatCard file={SHOTS.saves} x={740} y={620} w={310} baseBeat={idx * 4 + 2} label="存档" ryBase={14} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 7) CONTENT (beats 176-208) ----------
const Content: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(4);
  const stages = [
    {file: SHOTS.repoMods, t: '模组仓库', note: '对接 Modrinth：搜索安装模组，空搜索显示热门推荐，可“换一批”快速发现', extra: '一眼看到大家在用什么'},
    {file: SHOTS.repoShaders, t: '光影仓库', note: '浏览与搜索光影包，按实例安装，让画面风格自由切换', extra: '搜得到，装得上'},
    {file: SHOTS.repoPacks, t: '资源包仓库', note: '浏览与安装资源包，丰富材质与界面表现，按实例管理', extra: '材质风格随心选'},
    {file: SHOTS.instPacks, t: '实例内资源/光影', note: '已安装的资源包、光影在实例内独立管理与开关，每个实例互不影响', extra: '实例隔离，管理清晰', alt: SHOTS.instShaders},
  ];
  const idx = Math.min(3, Math.floor(l / seg));
  const ls = l - idx * seg;
  const st = stages[idx];
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls, [0, seg], [18, -18], clamp)} rx={4} tz={interpolate(ls, [0, seg / 2, seg], [-60, 110, 10], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.03)}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 80, textAlign: 'center'}}>
          <Tag text="内容生态 · Modrinth" accent />
          <div style={{marginTop: 14}}><MetalText size={74} delay={0} tint="green">{st.t}</MetalText></div>
          <Sub delay={3} size={30} color={COLOR.ink}>{st.note}</Sub>
        </div>
        <Shot3D key={st.file + idx} file={st.file} x={360} y={290} w={1200} delay={idx * seg} ry={idx % 2 ? -18 : 18} rx={6} z={90} label={st.t} />
        {st.alt && <Shot3D file={st.alt} x={80} y={520} w={440} delay={idx * seg + stepDur(2)} ry={22} rx={-5} z={180} label="实例内光影" enter="left" />}
        {idx === 0 && <ClickPoint x={1200} y={520} at={idx * seg + stepDur(2)} label="安装模组" />}
        {idx === 1 && <ClickPoint x={1200} y={520} at={idx * seg + stepDur(2)} label="安装光影" />}
        {idx === 2 && <ClickPoint x={1200} y={520} at={idx * seg + stepDur(2)} label="安装资源包" />}
        {idx === 3 && <ClickPoint x={1200} y={620} at={idx * seg + stepDur(2)} label="开关资源包" />}
        <div style={{position: 'absolute', left: 0, right: 0, bottom: 115, textAlign: 'center'}}><Big size={40} delay={4} color={COLOR.accent}>{st.extra}</Big></div>
        <Pulse x={1200} y={420} at={idx * seg + stepDur(2)} />
        <FloatCard file={SHOTS.repoMods} x={1260} y={420} w={330} baseBeat={idx * 4} label="仓库" ryBase={-12} />
        <FloatCard file={SHOTS.instPacks} x={740} y={620} w={310} baseBeat={idx * 4 + 2} label="实例内容" ryBase={14} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 8) ONLINE + SKINS (beats 208-232) ----------
const Online: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const half = stepDur(6);
  const p2 = l >= half;
  const ls = p2 ? l - half : l;
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(l, [0, stepDur(24)], [-14, 14], clamp)} rx={5} tz={interpolate(ls, [0, half], [-40, 120], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.03)}>
        {!p2 ? (
          <>
            <div style={{position: 'absolute', left: 90, top: 120}}>
              <Tag text="联机 · Campfire" accent />
              <div style={{marginTop: 16}}><MetalText size={78} delay={0} from="left" tint="blue" align="left">{'把世界\n开给朋友。'}</MetalText></div>
              <Sub delay={3} align="left" size={28} maxWidth={520}>随附 Campfire 联机组件，集成在启动器内</Sub>
            </div>
            <Shot3D file={SHOTS.online} x={700} y={110} w={1120} delay={0} ry={-14} rx={6} z={70} label="联机中心 · Campfire" />
            <Bullet text="一键创建房间 / 生成邀请码" x={90} y={430} delay={4} accent />
            <Bullet text="复制邀请码分享给好友加入" x={90} y={484} delay={6} />
            <Bullet text="支持检查与更新 Campfire" x={90} y={538} delay={8} />
            <ClickPoint x={1260} y={430} at={stepDur(4)} label="创建房间" />
            <ClickPoint x={1260} y={484} at={stepDur(6)} label="复制邀请码" />
            <Pulse x={1260} y={560} at={stepDur(3)} />
          </>
        ) : (
          <>
            <div style={{position: 'absolute', left: 90, top: 130}}>
              <Tag text="皮肤站与 3D 预览" accent />
              <div style={{marginTop: 16}}><MetalText size={76} delay={0} from="left" tint="gold" align="left">皮肤管理</MetalText></div>
              <Sub delay={3} align="left" size={28} maxWidth={520}>皮肤站集成与 3D 皮肤预览，外观所见即所得</Sub>
            </div>
            <Shot3D file={SHOTS.skins} x={680} y={100} w={1140} delay={half} ry={14} rx={6} z={80} label="皮肤站 · 3D 预览" />
            <Bullet text="皮肤站账号与皮肤管理" x={90} y={420} delay={4} accent />
            <Bullet text="3D 实时预览皮肤效果" x={90} y={474} delay={6} />
            <ClickPoint x={1260} y={520} at={stepDur(4)} label="更换皮肤" />
          </>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 9) TOOLS & SETTINGS EVERY PAGE (beats 232-260, 4 beats each for 7 items) ----------
const ToolsSettings: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const items = [
    {file: SHOTS.diag, t: '崩溃诊断', note: '解析崩溃报告并给出建议，启动失败可一键直达诊断', tint: 'blue' as const},
    {file: SHOTS.appLog, t: '应用日志', note: '日志尾部实时读取、关键词过滤，问题可追踪可定位', tint: 'silver' as const},
    {file: SHOTS.global, t: '全局设置', note: '内存分配、Java、下载源与镜像等全局参数，即时生效', tint: 'green' as const},
    {file: SHOTS.launchSet, t: '启动设置', note: '游戏窗口大小、JVM 参数与启动行为按需配置', tint: 'silver' as const},
    {file: SHOTS.theme, t: '主题与外观', note: '深浅色/强调色/主题工坊配色；云母材质默认关闭，可选开启', tint: 'gold' as const},
    {file: SHOTS.about, t: '关于与更新', note: '版本信息、开源许可、检查启动器更新', tint: 'silver' as const},
    {file: SHOTS.terms, t: '协议与条款', note: '用户协议与免责声明完整可查', tint: 'silver' as const},
  ];
  const seg = stepDur(2); // 2 beats each, hard cuts
  const idx = Math.min(items.length - 1, Math.floor(l / seg));
  const ls = l - idx * seg;
  const it = items[idx];
  const dirs = [ {ry: 14, rx: 6}, {ry: -16, rx: 5}, {ry: 12, rx: 7}, {ry: -14, rx: 4}, {ry: 16, rx: 6}, {ry: -12, rx: 5}, {ry: 14, rx: 5} ];
  const d = dirs[idx];
  const camRy = interpolate(ls, [0, seg], [d.ry, -d.ry], clamp);
  return (
    <AbsoluteFill>
      <Camera ry={camRy} rx={d.rx} tz={interpolate(ls, [0, seg / 2, seg], [-120, 100, -20], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.035)}>
        <div style={{position: 'absolute', left: 90, top: 120, zIndex: 5, maxWidth: 560}}>
          <Tag text="诊断 · 日志 · 设置 · 关于" accent />
          <div style={{marginTop: 14}}><MetalText size={74} delay={0} from="left" tint={it.tint} align="left">{it.t}</MetalText></div>
          <div style={{marginTop: 12}}><Sub delay={1} align="left" size={30}>{it.note}</Sub></div>
          <div style={{marginTop: 18, fontFamily: FONT, fontSize: 22, color: COLOR.accent, fontWeight: 600}}>{idx + 1} / {items.length} 功能页</div>
        </div>
        <Shot3D key={it.file + idx} file={it.file} x={690} y={130} w={1140} delay={idx * seg} ry={idx % 2 ? 16 : -16} rx={6} z={80} label={it.t} enter={idx % 2 ? 'right' : 'left'} />
        {idx === 0 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="扫描诊断" />}
        {idx === 1 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="打开日志" />}
        {idx === 2 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="应用设置" />}
        {idx === 3 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="启动参数" />}
        {idx === 4 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="切换主题" />}
        {idx === 5 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="检查更新" />}
        {idx === 6 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="阅读条款" />}
        <Pulse x={1300} y={620} at={stepDur(1)} />
        <FloatCard file={SHOTS.diag} x={1260} y={560} w={330} baseBeat={idx * 2} label="诊断" ryBase={-12} />
        <FloatCard file={SHOTS.theme} x={740} y={620} w={310} baseBeat={idx * 2 + 1} label="外观" ryBase={14} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 11) MONTAGE (beats 260-288) rapid fire ----------
const Montage: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const all = Object.values(SHOTS);
  const cols = 6; const cardW = 275; const cardH = 185; const gap = 14;
  const totalW = cols * cardW + (cols - 1) * gap; const sx = (1920 - totalW) / 2;
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(l, [0, stepDur(12)], [-500, 220], clamp)} ry={interpolate(l, [0, stepDur(12)], [-20, 20], clamp)} rx={6} scale={beatPunch(l, 30, PERIOD, 0.06)}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 55, textAlign: 'center', zIndex: 10}}>
          <MetalText size={76} delay={0} tint="silver">从启动页到畅玩 Minecraft</MetalText>
          <Sub delay={2} size={34} color={COLOR.accent}>每一个功能，全部讲到。</Sub>
        </div>
        <div style={{position: 'absolute', left: sx, top: 195, width: totalW, transformStyle: 'preserve-3d'}}>
          {all.map((file, i) => {
            const col = i % cols; const row = Math.floor(i / cols);
            const d = Math.floor(i * 0.6);
            const p = spring({frame: l - d, fps: 30, config: {damping: 14, stiffness: 160}});
            const wave = Math.sin((l + i * 5) / 8) * 10;
            const beatK = 1 + 0.03 * Math.max(0, 1 - ((l % stepDur(1)) / stepDur(1)) * 2);
            return (
              <div key={file + i} style={{position: 'absolute', left: col * (cardW + gap), top: row * (cardH + gap), width: cardW, height: cardH, transformStyle: 'preserve-3d', transform: `translateZ(${(1 - p) * -600 + wave}px) rotateY(${(col - 2.5) * 4}deg) rotateX(${(1 - p) * 35}deg) scale(${(0.75 + p * 0.25) * beatK})`, opacity: interpolate(l, [d, d + 3], [0, 1], clamp)}}>
                <div style={{width: '100%', height: '100%', borderRadius: 14, overflow: 'hidden', border: `1px solid ${COLOR.line}`, boxShadow: '0 20px 60px rgba(0,0,0,0.75)'}}>
                  <Img src={staticFile(`shots/${file}`)} style={{width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top left'}} />
                </div>
              </div>
            );
          })}
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 12) OUTRO + CREDITS (beat 288 - end) ----------
const Outro: React.FC<{base: number; total: number}> = ({base, total}) => {
  const l = useCurrentFrame();
  const {fps} = useVideoConfig();
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 75% 55% at 50% 38%, rgba(126,217,87,0.18), transparent 70%)'}} />
      <Camera tz={interpolate(l, [0, Math.min(total, stepDur(24))], [-160, 50], clamp)} rx={interpolate(l, [0, total], [4, 0], clamp)} scale={beatPunch(l, 30, PERIOD, 0.04)}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 55, textAlign: 'center'}}>
          <MetalText size={100} delay={0} from="scale" tint="silver">SalmonLauncher</MetalText>
          <div style={{marginTop: 12}}><Sub delay={4} size={32} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
        </div>

        <QRCard file="site.png" title="官方网站" url={LINKS.site} x={260} y={285} delay={6} size={170} />
        <QRCard file="github.png" title="GitHub 仓库" url={LINKS.github} x={660} y={285} delay={10} size={170} />
        <QRCard file="blog.png" title="作者博客" url={LINKS.blog} x={1060} y={285} delay={14} size={170} />

        <div style={{position: 'absolute', left: 100, right: 100, bottom: 138, padding: '20px 26px', borderRadius: 18, background: 'rgba(20,20,22,0.76)', border: `1px solid ${COLOR.line}`, opacity: interpolate(l, [18, 28], [0, 1], clamp), transform: `translateY(${interpolate(l, [18, 28], [30, 0], clamp)}px)`}}>
          <div style={{fontFamily: FONT, fontSize: 21, color: COLOR.ink, fontWeight: 700, marginBottom: 8}}>视频声明 · 制作人员 · 致谢名单 · 版权解释</div>
          <div style={{fontFamily: FONT, fontSize: 17, color: COLOR.dim, lineHeight: 1.7}}>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>制作人员：</span>Hugo Mou（产品设计 / 开发 / 维护）</div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>致谢名单：</span>Modrinth 内容平台与社区作者 · Campfire 联机组件 · .NET / Windows App SDK / WinUI 3 · Mojang 官方版本与资源生态</div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>配乐与素材：</span>“3 Strikes” — Terror Jr（来源：Bilibili BV1tek2YsEYY，本片仅作展示） · 画面为 SalmonLauncher 2.0.13 真实界面截图（全新默认状态，云母默认关闭，无个人使用痕迹）</div>
            <div style={{color: COLOR.accent, fontWeight: 600, marginTop: 2}}>本软件由 AI 辅助开发制作</div>
            <div style={{color: COLOR.accent, fontWeight: 600}}>本宣传片由 AI 生成制作</div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>项目许可：</span>GPL-3.0-or-later（开源免费，Windows x64）　官网 {LINKS.siteFull}　GitHub {LINKS.githubFull}</div>
            <div style={{marginTop: 6, fontSize: 15, color: 'rgba(134,134,139,0.95)'}}>版权与声明：Minecraft 为 Mojang Studios / Microsoft 的商标，本项目与 Mojang/Microsoft 无官方关联；Modrinth、Campfire 等名称与服务归各自权利人所有。本视频仅用于 SalmonLauncher 产品展示与介绍，不构成对第三方商标或内容的授权声明。</div>
          </div>
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- timeline ----------
export const Film: React.FC = () => {
  const seg = (startBeat: number, durBeats: number) => ({from: BF(startBeat), dur: stepDur(durBeats)});
  // 0-16 impact, 16-32 hero, 32-80 onboarding(48), 80-104 launch/play(24), 104-136 install(32),
  // 136-176 instances(40), 176-208 content(32), 208-232 online(24), 232-260 tools/settings(28),
  // 260-276 blog(16), 276-288 montage(12), 288-end outro
  const S = {
    impact: seg(0, 16),
    hero: seg(16, 16),
    onboard: seg(32, 48),
    launch: seg(80, 24),
    install: seg(104, 32),
    instance: seg(136, 40),
    content: seg(176, 32),
    online: seg(208, 24),
    tools: seg(232, 28),
    montage: seg(260, 28),
  };
  const outroFrom = BF(288);
  const outroDur = Math.max(1, toF(FILM.duration) - outroFrom);

  // global beat punch/flash
  const f = useCurrentFrame();
  const t = f / FILM.fps;
  let flash = 0;
  for (let i = 0; i < BEAT_COUNT; i += 1) {
    const down = i % 4 === 0;
    const d = down ? 0.22 : i % 2 === 0 ? 0.12 : 0.06;
    const w = down ? 0.1 : 0.06;
    flash = Math.max(flash, hit(t, i, w) * d);
  }

  return (
    <Stage>
      <Audio src={staticFile(AUDIO_FILE)} />
      <Sequence from={S.impact.from} durationInFrames={S.impact.dur}><Impact base={S.impact.from} /></Sequence>
      <Sequence from={S.hero.from} durationInFrames={S.hero.dur}><Hero base={S.hero.from} /></Sequence>
      <Sequence from={S.onboard.from} durationInFrames={S.onboard.dur}><Onboarding base={S.onboard.from} /></Sequence>
      <Sequence from={S.launch.from} durationInFrames={S.launch.dur}><LaunchPlay base={S.launch.from} /></Sequence>
      <Sequence from={S.install.from} durationInFrames={S.install.dur}><Install base={S.install.from} /></Sequence>
      <Sequence from={S.instance.from} durationInFrames={S.instance.dur}><Instances base={S.instance.from} /></Sequence>
      <Sequence from={S.content.from} durationInFrames={S.content.dur}><Content base={S.content.from} /></Sequence>
      <Sequence from={S.online.from} durationInFrames={S.online.dur}><Online base={S.online.from} /></Sequence>
      <Sequence from={S.tools.from} durationInFrames={S.tools.dur}><ToolsSettings base={S.tools.from} /></Sequence>
      <Sequence from={S.montage.from} durationInFrames={S.montage.dur}><Montage base={S.montage.from} /></Sequence>
      <Sequence from={outroFrom} durationInFrames={outroDur}><Outro base={outroFrom} total={outroDur} /></Sequence>

      {flash > 0.001 && <AbsoluteFill style={{backgroundColor: '#fff', opacity: flash * 0.28, mixBlendMode: 'screen', pointerEvents: 'none'}} />}
      <AbsoluteFill style={{boxShadow: 'inset 0 0 180px rgba(0,0,0,0.45)', pointerEvents: 'none'}} />
      <LegalStrip />
    </Stage>
  );
};
