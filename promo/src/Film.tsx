// Film.tsx — SalmonLauncher promo, Apple-keynote energy, 355.2s tightly synced to "3 Strikes" played twice (108 BPM).
// Fast beat cuts, aggressive 3D camera, metallic gloss, real clean UI screenshots (v2.0.13, default/no Mica).
import React from 'react';
import {AbsoluteFill, Audio, Img, Sequence, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {AUDIO_FILE, COLOR, CREDITS, FILM, FONT, LINKS, SHOTS, toF} from './spec';
import {BEAT_COUNT, PERIOD, beatFrame, hit} from './lib/beats';
import {Big, Camera, ClickFx, LegalStrip, MetalText, Panel, QRCard, ScanLine, Shot3D, Slam, Stage, Sub, Tag, beatPunch, clamp, easeOut, snap} from './lib/fx';

const BF = (i: number) => beatFrame(i, FILM.fps);
const stepDur = (beats: number) => Math.round(beats * PERIOD * FILM.fps);
const beatOf = (l: number) => Math.floor(l / (PERIOD * FILM.fps)); // local beat index

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
    <div style={{position: 'absolute', left: x, top: y, opacity: o, transform: `translateX(${(1 - p) * 30}px)`}}>
      <span style={{color: accent ? COLOR.accent : COLOR.dim, fontWeight: accent ? 700 : 500}}>▸</span>{' '}
      <span style={{fontFamily: FONT, fontSize: 20, color: COLOR.ink}}>{text}</span>
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

// ---------- 1) IMPACT INTRO (beats 0-20) ----------
const Impact: React.FC<{base: number}> = () => {
  const l = useCurrentFrame();
  const {fps} = useVideoConfig();
  const hits = [0, 2, 4, 6, 8, 10, 12, 14, 16, 18];
  return (
    <AbsoluteFill>
      {hits.map((b, i) => {
        const o = interpolate(l, [BF(b), BF(b) + 1, BF(b) + 4], [0, i % 4 === 0 ? 0.45 : 0.22, 0], clamp);
        return <AbsoluteFill key={b} style={{backgroundColor: i % 2 ? COLOR.accent : '#fff', opacity: o, mixBlendMode: 'screen'}} />;
      })}
      <Camera tz={interpolate(l, [0, stepDur(20)], [-400, 140], clamp)} rx={interpolate(l, [0, stepDur(10)], [10, 0], clamp)} ry={interpolate(l, [0, stepDur(20)], [-8, 8], clamp)}>
        {l >= BF(0) && l < BF(4) && (
          <>
            <AbsoluteFill style={{backgroundColor:'#fff',opacity: interpolate(l,[BF(0),BF(0)+2,BF(0)+7],[0,0.4,0],clamp),mixBlendMode:'screen',pointerEvents:'none'}} />
            <Slam delay={Math.max(0,BF(0)-8)} size={180} tint="silver">启动。</Slam>
          </>
        )}
        {l >= BF(8) && l < BF(12) && <Slam delay={Math.max(0,BF(8)-4)} size={150} tint="gold">极简。</Slam>}
        {l >= BF(14) && l < BF(18) && <Slam delay={Math.max(0,BF(14)-4)} size={140} tint="blue">无痕迹。</Slam>}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 2) HERO (beats 20-40) ----------
const Hero: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const ls = l - base;
  const camZ = interpolate(ls, [0, stepDur(20)], [-400, 140], {extrapolateRight: 'clamp', easing: easeOut});
  const camRy = interpolate(ls, [0, stepDur(20)], [14, -10], clamp);
  const camRx = interpolate(ls, [0, stepDur(10), stepDur(20)], [6, 2, 5], clamp);
  return (
    <AbsoluteFill>
      <Camera tz={camZ} ry={camRy} rx={camRx} scale={beatPunch(ls, 30, PERIOD, 0.05)}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 260, textAlign: 'center'}}>
          <MetalText size={110} delay={0} from="bottom" tint="silver">SalmonLauncher</MetalText>
          <div style={{marginTop: 14}}><Sub delay={4} size={36} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
        </div>
        <Shot3D file={SHOTS.home} x={100} y={520} w={520} delay={2} ry={-16} rx={6} z={-80} floatAmp={6} label="启动面板" />
        <Shot3D file={SHOTS.tasks} x={680} y={520} w={520} delay={3} ry={14} rx={-6} z={-80} floatAmp={6} label="任务中心" />
        <Shot3D file={SHOTS.theme} x={1260} y={520} w={460} delay={4} ry={-10} rx={8} z={-60} floatAmp={6} label="外观设置" />
        <FloatCard file={SHOTS.versions} x={1420} y={420} w={330} baseBeat={6} label="版本" ryBase={-12} />
        <FloatCard file={SHOTS.loaders} x={60} y={420} w={330} baseBeat={10} label="加载器" ryBase={14} />
        <div style={{position: 'absolute', left: 0, right: 0, bottom: 120, textAlign: 'center'}}>
          <Tag text={LINKS.siteFull} delay={6} accent size={20} />
          <Tag text={LINKS.githubFull} delay={7} accent size={20} />
          <Tag text={LINKS.blogFull} delay={8} accent size={20} />
        </div>
        <ClickPoint x={960} y={840} at={stepDur(10)} label="开始使用" />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 3) ONBOARDING (beats 40-120, 80 beats ~ 13 beats per step for 6 steps) ----------
const Onboarding: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(13); // 13 beats per step
  const steps = [
    {file: SHOTS.wLang, title: '选择语言', detail: '简体中文 / 繁體中文 / English / 日本語，进入即选', n: 1},
    {file: SHOTS.wTerms, title: '阅读并同意条款', detail: '用户协议与免责声明清晰展示，勾选后继续', n: 2},
    {file: SHOTS.wMigrate, title: '迁移数据（可选）', detail: '可一键识别，也可手动选择旧启动器或 .minecraft 数据文件夹', n: 3},
    {file: SHOTS.wAccount, title: '添加账户', detail: '离线账户 / 微软账号登录，之后可随时管理', n: 4},
    {file: SHOTS.wTheme, title: '外观与配色', detail: '主题与强调色；默认普通背景，云母材质按需可选开启', n: 5},
    {file: SHOTS.wDone, title: '完成引导', detail: '初始化完成，直接进入启动器开始使用', n: 6},
  ];
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
        <FloatCard file={SHOTS.wTheme} x={1260} y={620} w={330} baseBeat={idx * 13} label="外观" ryBase={-10} />
        <FloatCard file={SHOTS.wDone} x={740} y={660} w={310} baseBeat={idx * 13 + 6} label="完成" ryBase={12} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 4) LAUNCH / PLAY (beats 120-180, 60 beats ~ 15 beats per phase for 4 phases) ----------
const LaunchPlay: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const ls = l - base;
  const phase = Math.min(3, Math.floor(ls / stepDur(15)));
  const lp = ls - phase * stepDur(15);
  const camRy = interpolate(ls, [0, stepDur(60)], [-10, 12], clamp);
  const camZ = interpolate(ls, [0, stepDur(30), stepDur(60)], [-60, 140, 40], clamp);
  const labels = ['选择实例与账户', '点击启动', '自动准备并下载依赖', '进入游戏 · 开始畅玩'];
  const notes = [
    '选择已创建的实例与登录账户，确认游戏版本与内存分配',
    '点击“启动”按钮，启动器开始读取并校验游戏文件',
    '资源校验中，请稍候… 启动器后台自动补全缺失文件',
    '游戏窗口打开，进入 Minecraft 主菜单，开始畅玩',
  ];
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls, [0, stepDur(60)], [-10, 12], clamp)} rx={interpolate(ls, [0, stepDur(60)], [4, 0], clamp)} tz={camZ} scale={beatPunch(ls, 30, PERIOD, 0.03)}>
        <div style={{position: 'absolute', left: 90, top: 100}}>
          <Tag text="从启动到畅玩 · 细讲" accent />
          <div style={{marginTop: 16}}><MetalText size={72} delay={0} from="left" tint="blue" align="left">{labels[phase]}</MetalText></div>
          <div style={{marginTop: 14}}><Sub delay={2} align="left" size={28} maxWidth={520}>{notes[phase]}</Sub></div>
        </div>
        <Panel x={90} y={360} w={520} delay={1}>
          <div style={{fontFamily: FONT, fontSize: 20, color: COLOR.accent, fontWeight: 700, marginBottom: 10}}>步骤 {phase + 1} / 4 · {labels[phase]}</div>
          <div style={{fontFamily: FONT, fontSize: 26, color: COLOR.ink, lineHeight: 1.5}}>{notes[phase]}</div>
        </Panel>
        <Shot3D file={SHOTS.home} x={700} y={130} w={1140} delay={0} ry={-14} rx={6} z={70} label="启动器主界面" enter={phase % 2 ? 'right' : 'left'} />
        {phase >= 1 && <Cursor x1={1200} y1={520} x2={1500} y2={720} at={stepDur(6)} dur={stepDur(2)} />}
        {phase >= 1 && <Pulse x={1500} y={720} at={stepDur(9)} />}
        {phase >= 1 && <ClickPoint x={1500} y={720} at={stepDur(6)} label="一键启动" />}
        {phase >= 2 && <Shot3D file={SHOTS.tasks} x={560} y={520} w={520} delay={stepDur(12)} ry={12} rx={-3} z={160} label="任务实时进度" enter="bottom" />}
        {phase >= 2 && <Bar x={600} y={880} w={440} at={stepDur(13)} dur={stepDur(8)} label="准备资源 / 检查依赖 / 补全文件…" done={phase >= 3 ? '✓ 准备完成，正在启动游戏' : undefined} />}
        {phase >= 2 && <div style={{position:'absolute',left:600,top:990,fontFamily:FONT,fontSize:18,color:COLOR.dim,opacity:interpolate(ls,[stepDur(13),stepDur(13)+2,stepDur(21)-2,stepDur(21)],[0,1,1,0],clamp)}}>资源校验中，请稍候…</div>}
        {phase >= 3 && <Panel x={1180} y={620} w={520} delay={stepDur(22)}><div style={{fontFamily: FONT, fontSize: 34, color: COLOR.accent, fontWeight: 700}}>已进入 Minecraft，开始畅玩</div><div style={{fontFamily: FONT, fontSize: 22, color: COLOR.dim, marginTop: 8}}>启动器全程可见进度与状态，出错可直达诊断</div></Panel>}
        {phase >= 3 && <Shot3D file={SHOTS.instLogs} x={1260} y={420} w={330} delay={stepDur(24)} ry={-12} rx={8} z={150} label="实例日志" />}
        <FloatCard file={SHOTS.versions} x={1260} y={500} w={330} baseBeat={phase * 15} label="版本" ryBase={-12} />
        <FloatCard file={SHOTS.loaders} x={740} y={620} w={310} baseBeat={phase * 15 + 7} label="加载器" ryBase={14} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 5) INSTALL (beats 180-260, 80 beats ~ 20 beats per stage for 4 stages) ----------
const Install: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
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
        {idx === 3 && <div style={{position:'absolute',left:760,top:900,fontFamily:FONT,fontSize:18,color:COLOR.dim,opacity:interpolate(ls,[idx * seg + 2,idx * seg + 2 + 2,idx * seg + stepDur(3.6)-2,idx * seg + stepDur(3.6)],[0,1,1,0],clamp)}}>正在下载游戏资源，请稍候…</div>}
        {idx === 3 && <div style={{position:'absolute',left:760,top:930,fontFamily:FONT,fontSize:20,color:COLOR.accent,fontWeight:700,opacity:interpolate(ls,[idx * seg + 2,idx * seg + 2 + 2,idx * seg + stepDur(3.6)-2,idx * seg + stepDur(3.6)],[0,1,1,0],clamp)}}>已下载 {Math.floor((ls - idx * seg) / stepDur(3.6) * 720)} MB / 720 MB  ·  速度 2.4 MB/s</div>}
        {idx === 3 && <ClickPoint x={1300} y={700} at={idx * seg + stepDur(4)} label="开始下载" />}
        <Pulse x={1300} y={700} at={idx * seg + stepDur(2)} />
        <FloatCard file={SHOTS.versions} x={1260} y={520} w={330} baseBeat={idx * 20} label="版本" ryBase={-12} />
        <FloatCard file={SHOTS.loaders} x={740} y={620} w={310} baseBeat={idx * 20 + 10} label="加载器" ryBase={14} />
        <div style={{position: 'absolute', left: 90, bottom: 110}}><Big size={36} delay={4} align="left" color={COLOR.accent}>{st.accent}</Big></div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 6) INSTANCES (beats 260-360, 100 beats ~ 20 beats per stage for 5 stages) ----------
const Instances: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
  const stages = [
    {file: SHOTS.overview, t: '实例概览', note: '所有实例一览，版本、加载器、状态一目了然，支持快速切换与启动', tags: ['实例列表', '状态指示', '快速启动'], accent: '实例尽在掌握。'},
    {file: SHOTS.mods, t: '模组管理', note: '从 Modrinth 浏览、搜索、安装模组，支持版本筛选与依赖检查', tags: ['Modrinth 仓库', '模组分类', '依赖检查'], accent: '模组随心装。'},
    {file: SHOTS.saves, t: '存档管理', note: '备份、恢复、重命名存档，自定义存档路径，数据安全无忧', tags: ['一键备份', '存档恢复', '路径自定义'], accent: '存档随心存。'},
    {file: SHOTS.instCfg, t: '实例设置', note: '实例名称、游戏目录、Java 路径、启动参数等详细配置', tags: ['实例命名', '目录设置', '启动参数'], accent: '设置随心改。'},
    {file: SHOTS.instLogs, t: '实例日志', note: '实时读取启动日志与崩溃报告，关键词过滤，问题定位更精准', tags: ['日志实时读取', '崩溃诊断', '关键词过滤'], accent: '日志看得清。'},
  ];
  const idx = Math.min(stages.length - 1, Math.floor(l / seg));
  const ls = l - idx * seg;
  const st = stages[idx];
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls, [0, seg], [14 - idx * 6, -14 + idx * 4], clamp)} rx={6} tz={interpolate(ls, [0, seg / 2, seg], [-40, 130, 30], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.03)}>
        <div style={{position: 'absolute', left: 90, top: 110}}>
          <Tag text="实例管理 · 细讲" accent />
          <div style={{marginTop: 16}}><MetalText size={70} delay={0} from="left" tint="blue" align="left">{st.t}</MetalText></div>
          <div style={{marginTop: 14}}><Sub delay={2} align="left" size={28} maxWidth={520}>{st.note}</Sub></div>
          <div style={{marginTop: 18, display: 'flex', gap: 10, flexWrap: 'wrap', maxWidth: 520}}>{st.tags.map((tg, i) => <Tag key={tg} text={tg} delay={3 + i} accent={i === 0} size={20} />)}</div>
        </div>
        <Shot3D key={st.file + idx} file={st.file} x={700} y={120} w={1120} delay={idx * seg} ry={idx % 2 ? 14 : -14} rx={6} z={70} label={st.t} enter={idx % 2 ? 'left' : 'right'} />

        <Pulse x={1280} y={620} at={stepDur(5)} />
        {idx === 0 && <ClickPoint x={1280} y={520} at={idx * seg + stepDur(2)} label="健康检查" />}
        {idx === 1 && <ClickPoint x={1280} y={560} at={idx * seg + stepDur(2)} label="启用模组" />}
        {idx === 2 && <ClickPoint x={1280} y={620} at={idx * seg + stepDur(2)} label="备份存档" />}
        {idx === 3 && <ClickPoint x={1280} y={660} at={idx * seg + stepDur(2)} label="打开目录" />}
        {idx === 4 && <ClickPoint x={1280} y={560} at={idx * seg + stepDur(2)} label="查看日志" />}
        <FloatCard file={SHOTS.mods} x={1260} y={520} w={330} baseBeat={idx * 20} label="模组" ryBase={-12} />
        <FloatCard file={SHOTS.saves} x={740} y={620} w={310} baseBeat={idx * 20 + 10} label="存档" ryBase={14} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 7) CONTENT (beats 360-440, 80 beats ~ 20 beats per stage for 4 stages) ----------
const Content: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
  const stages = [
    {file: SHOTS.repoMods, t: '模组仓库', note: 'Modrinth 模组仓库集成，搜索、筛选、安装模组，支持版本与依赖管理', tags: ['模组搜索', '版本筛选', '依赖管理'], accent: '模组仓库，一站到底。'},
    {file: SHOTS.repoShaders, t: '光影与材质', note: '光影包与材质包预览与安装，让游戏画面更出彩', tags: ['光影预览', '材质安装', '画质提升'], accent: '光影，让世界更美。'},
    {file: SHOTS.repoPacks, t: '资源包管理', note: '资源包浏览与切换，自定义游戏纹理与音效', tags: ['资源包预览', '一键切换', '自定义纹理'], accent: '资源包，换个风格。'},
    {file: SHOTS.instPacks, t: '实例内容', note: '在实例内统一管理已安装的模组、光影、资源包与数据包', tags: ['实例内容统一管理', '启用/禁用', '版本控制'], accent: '实例内容，统一掌控。'},
  ];
  const idx = Math.min(stages.length - 1, Math.floor(l / seg));
  const ls = l - idx * seg;
  const st = stages[idx];
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls, [0, seg], [14 - idx * 6, -14 + idx * 4], clamp)} rx={6} tz={interpolate(ls, [0, seg / 2, seg], [-40, 130, 30], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.03)}>
        <div style={{position: 'absolute', left: 90, top: 110}}>
          <Tag text="内容生态 · 细讲" accent />
          <div style={{marginTop: 16}}><MetalText size={70} delay={0} from="left" tint="blue" align="left">{st.t}</MetalText></div>
          <div style={{marginTop: 14}}><Sub delay={2} align="left" size={28} maxWidth={520}>{st.note}</Sub></div>
          <div style={{marginTop: 18, display: 'flex', gap: 10, flexWrap: 'wrap', maxWidth: 520}}>{st.tags.map((tg, i) => <Tag key={tg} text={tg} delay={3 + i} accent={i === 0} size={20} />)}</div>
        </div>
        <Shot3D key={st.file + idx} file={st.file} x={360} y={290} w={1200} delay={idx * seg} ry={idx % 2 ? -18 : 18} rx={6} z={90} label={st.t} />
        <Pulse x={1200} y={420} at={idx * seg + stepDur(2)} />
        <FloatCard file={SHOTS.repoMods} x={1260} y={420} w={330} baseBeat={idx * 20} label="仓库" ryBase={-12} />
        <FloatCard file={SHOTS.instPacks} x={740} y={620} w={310} baseBeat={idx * 20 + 10} label="实例内容" ryBase={14} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 8) ONLINE + SKINS (beats 440-500, 60 beats ~ 30 beats per half for 2 halves) ----------
const Online: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const half = stepDur(30);
  const p2 = l >= half;
  const ls = p2 ? l - half : l;
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(l, [0, stepDur(60)], [-14, 14], clamp)} rx={5} tz={interpolate(ls, [0, half], [-40, 120], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.03)}>
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

// ---------- 9) TOOLS & SETTINGS EVERY PAGE (beats 500-580, 80 beats ~ 10 beats per item for 8 items) ----------
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
    {file: SHOTS.wLang, t: '语言设置', note: '多语言界面切换，支持简体/繁体中文、英文、日文', tint: 'blue' as const},
  ];
  const seg = stepDur(10); // 10 beats each
  const idx = Math.min(items.length - 1, Math.floor(l / seg));
  const ls = l - idx * seg;
  const it = items[idx];
  const dirs = [ {ry: 14, rx: 6}, {ry: -16, rx: 5}, {ry: 12, rx: 7}, {ry: -14, rx: 4}, {ry: 16, rx: 6}, {ry: -12, rx: 5}, {ry: 14, rx: 5}, {ry: -10, rx: 6} ];
  const d = dirs[idx];
  const camRy = interpolate(ls, [0, seg], [d.ry, -d.ry], clamp);
  return (
    <AbsoluteFill>
      <Camera ry={camRy} rx={d.rx} tz={interpolate(ls, [0, seg / 2, seg], [-120, 100, -20], clamp)} scale={beatPunch(ls, 30, PERIOD, 0.035)}>
        <div style={{position: 'absolute', left: 90, top: 110}}>
          <Tag text="工具与设置 · 每一页" accent />
          <div style={{marginTop: 16}}><MetalText size={70} delay={0} from="left" tint={it.tint} align="left">{it.t}</MetalText></div>
          <div style={{marginTop: 14}}><Sub delay={2} align="left" size={26} maxWidth={520}>{it.note}</Sub></div>
        </div>
        <Shot3D key={it.file + idx} file={it.file} x={690} y={130} w={1140} delay={idx * seg} ry={idx % 2 ? 16 : -16} rx={6} z={80} label={it.t} enter={idx % 2 ? 'right' : 'left'} />
        {idx === 0 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="扫描诊断" />}
        {idx === 1 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="打开日志" />}
        {idx === 2 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="应用设置" />}
        {idx === 3 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="启动参数" />}
        {idx === 4 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="切换主题" />}
        {idx === 5 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="检查更新" />}
        {idx === 6 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="阅读条款" />}
        {idx === 7 && <ClickPoint x={1300} y={520} at={idx * seg + stepDur(1)} label="切换语言" />}
        <Pulse x={1300} y={620} at={stepDur(1)} />
        <FloatCard file={SHOTS.diag} x={1260} y={560} w={330} baseBeat={idx * 10} label="诊断" ryBase={-12} />
        <FloatCard file={SHOTS.theme} x={740} y={620} w={310} baseBeat={idx * 10 + 5} label="外观" ryBase={14} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 10) MONTAGE (beats 580-620, 40 beats rapid fire) ----------
const Montage: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const ls = l - base;
  const files = [SHOTS.home, SHOTS.tasks, SHOTS.versions, SHOTS.loaders, SHOTS.install, SHOTS.overview, SHOTS.mods, SHOTS.saves, SHOTS.instCfg, SHOTS.instLogs, SHOTS.repoMods, SHOTS.repoShaders, SHOTS.repoPacks, SHOTS.instPacks, SHOTS.online, SHOTS.skins, SHOTS.diag, SHOTS.appLog, SHOTS.global, SHOTS.launchSet, SHOTS.theme, SHOTS.about, SHOTS.terms];
  const cardW = 320, cardH = 230, gap = 24;
  const cols = 3;
  const rows = Math.ceil(files.length / cols);
  const totalDur = stepDur(40);
  const perCard = Math.floor(totalDur / files.length);
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(ls, [0, stepDur(40)], [-500, 240], clamp)} ry={interpolate(ls, [0, stepDur(40)], [-22, 22], clamp)} rx={7} scale={beatPunch(ls, 30, PERIOD, 0.06)}>
        <div style={{position: 'absolute', left: 90, top: 80}}>
          <Tag text="功能全景 · 快速剪辑" accent delay={0} />
          <div style={{marginTop: 14}}><MetalText size={80} delay={1} from="bottom" tint="silver">全部功能，一应俱全。</MetalText></div>
        </div>
        {files.map((file, i) => {
          const d = i * perCard;
          const row = Math.floor(i / cols);
          const col = i % cols;
          const p = interpolate(ls, [d, d + 20], [0, 1], clamp);
          const wave = Math.sin((ls + i * 7) / 15) * 20;
          const beatK = 1 + 0.03 * Math.max(0, 1 - ((ls % stepDur(1)) / stepDur(1)) * 2);
          return (
            <div key={file + i} style={{position: 'absolute', left: col * (cardW + gap), top: row * (cardH + gap), width: cardW, height: cardH, transformStyle: 'preserve-3d', transform: `translateZ(${(1 - p) * -600 + wave}px) rotateY(${(col - 2.5) * 4}deg) rotateX(${(1 - p) * 35}deg) scale(${(0.75 + p * 0.25) * beatK})`, opacity: interpolate(ls, [d, d + 3], [0, 1], clamp)}}>
              <div style={{width: '100%', height: '100%', borderRadius: 14, overflow: 'hidden', border: `1px solid ${COLOR.line}`, boxShadow: '0 20px 60px rgba(0,0,0,0.75)'}}>
                <Img src={staticFile(`shots/${file}`)} style={{width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top left'}} />
              </div>
            </div>
          );
        })}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 11) OUTRO + CREDITS (beat 620 - end) ----------
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
  // 0-20 impact, 20-40 hero, 40-120 onboarding(80), 120-180 launch(60),
  // 180-260 install(80), 260-360 instances(100), 360-440 content(80),
  // 440-500 online(60), 500-580 tools/settings(80), 580-620 montage(40), 620-end outro
  const S = {
    impact: seg(0, 20),
    hero: seg(20, 20),
    onboard: seg(40, 80),
    launch: seg(120, 60),
    install: seg(180, 80),
    instance: seg(260, 100),
    content: seg(360, 80),
    online: seg(440, 60),
    tools: seg(500, 80),
    montage: seg(580, 40),
  };
  const outroFrom = BF(620);
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

      {flash > 0.001 && <AbsoluteFill style={{backgroundColor: '#fff', opacity: flash * 0.28, mixBlendMode: 'screen', pointerEvents:'none'}} />}
      <AbsoluteFill style={{boxShadow: 'inset 0 0 180px rgba(0,0,0,0.45)', pointerEvents:'none'}} />
      <LegalStrip />
    </Stage>
  );
};
