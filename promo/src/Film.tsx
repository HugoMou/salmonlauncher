// Film.tsx — SalmonLauncher 宣传片（重排重点版）
// 355.2s / 108 BPM《3 Strikes》播放两遍，所有切换落在小节线（4 拍整数倍）。
//
// 重点顺序（按用户要求重排）：
//  1) 0-40 拍   HOOK      开场 20 秒直接抛出最惊艳画面：真实游戏画面、3D 卡片阵列、品牌三句
//  2) 40-68 拍  BRAND     品牌气质：极简 / 一键启动 / 开源免费
//  3) 68-168 拍 ONE-CLICK 主线：打开 → 选实例 → 点击启动 → 校验 → 进游戏（真实录屏）
//  4) 168-328   CONTENT   内容生态（主卖点，最长段落）：模组 / 光影 / 资源包 / 实例内容 / 3D 皮肤 / 联机
//  5) 328-368   CHARACTER 主题工坊（壁纸取色整套主题）
//  6) 368-500   COVERAGE  全功能覆盖（引导 / 安装 / 实例 / 设置，压缩快剪）
//  7) 500-536   MONTAGE   功能全景矩阵
//  8) 536-590   CLOSE     品牌收束 + 链接
//  9) 590-end   OUTRO     精简声明 + 三个二维码
import React from 'react';
import {AbsoluteFill, Audio, Img, Sequence, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {AUDIO_FILE, CLIP_MARKS, CLIPS, COLOR, FILM, FONT, LINKS, SHOTS, toF} from './spec';
import {BEAT_COUNT, PERIOD, beatFrame, hit} from './lib/beats';
import {Big, Camera, ClickFx, LegalStrip, MetalText, Panel, QRCard, RealProgress, Shot3D, Stage, Sub, Tag, VideoClip, beatPunch, clamp, easeOut} from './lib/fx';

const BF = (i: number) => beatFrame(i, FILM.fps);
const stepDur = (beats: number) => Math.round(beats * PERIOD * FILM.fps);
const LB = (i: number) => Math.round(i * PERIOD * FILM.fps);

// ---------- 基础叠加 ----------
const Pulse: React.FC<{x:number;y:number;at:number;c?:string}> = ({x,y,at,c=COLOR.accent}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 24) return null;
  const s = interpolate(t,[0,16],[0.2,2.5],{extrapolateRight:'clamp', easing:easeOut});
  const o = interpolate(t,[0,2,16],[0,1,0],clamp);
  return <div style={{position:'absolute',left:x-34,top:y-34,width:68,height:68,borderRadius:999,border:`4px solid ${c}`,opacity:o,transform:`scale(${s})`,boxShadow:`0 0 40px ${c}`,zIndex:59}}/>;
};

const ClickPoint: React.FC<{x:number;y:number;at:number;label?:string}> = ({x,y,at,label}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 42) return null;
  const flash = interpolate(t,[0,8,16],[0,0.26,0],clamp);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{backgroundColor:'#fff',opacity:flash,mixBlendMode:'screen',pointerEvents:'none'}}/>
      <ClickFx x={x} y={y} at={at} label={label} size={1.15}/>
      <Pulse x={x} y={y} at={at+stepDur(1.4)}/>
    </AbsoluteFill>
  );
};

// 极短要点（最多 2 条，减少文字讲解）
const Point: React.FC<{text:string;x:number;y:number;delay:number}> = ({text,x,y,delay}) => {
  const l = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:l-delay,fps,config:{damping:18,stiffness:160}});
  const o = interpolate(l,[delay,delay+4],[0,1],clamp);
  return (
    <div style={{position:'absolute',left:x,top:y,opacity:o,transform:`translateX(${(1-p)*36}px)`,zIndex:22}}>
      <span style={{color:COLOR.accent,fontWeight:700,marginRight:9,fontSize:20}}>▸</span>
      <span style={{fontFamily:FONT,fontSize:24,color:COLOR.ink,fontWeight:600}}>{text}</span>
    </div>
  );
};

// 章节切点的高光脉冲：让画面切换正好压在拍点/小节线上
const CutAccent: React.FC<{frames:number[];strength?:number}> = ({frames,strength=0.5}) => {
  const l = useCurrentFrame();
  let o = 0;
  for (const cf of frames) {
    const t = l - cf;
    if (t < 0 || t > 10) continue;
    o = Math.max(o, interpolate(t,[0,10],[strength,0],clamp));
  }
  if (o <= 0.001) return null;
  return <AbsoluteFill style={{backgroundColor:'#fff',opacity:o,mixBlendMode:'screen',pointerEvents:'none'}}/>;
};

// ---------- 章节主视觉：3D 屏（截图或录屏），强调相机运动与卡点 ----------
const Chapter: React.FC<{
  dur:number; head:string; sub?:string;
  file?:string; clip?:{src:string; start?:number};
  cardX?:number; cardY?:number; cardW?:number; cardH?:number;
  ry?:number; rx?:number; z?:number;
  camRy?:number; camRx?:number; camZ?:number;
  click?:{x:number;y:number;label:string;atRel:number};
  points?:string[];
  progress?:{pct:number;label:string;detail?:string;atRel:number;x:number;y:number;w:number};
  deco?:boolean; closeup?:boolean; enterBeats?:number;
}> = ({dur,head,sub,file,clip,cardX=960,cardY=470,cardW=1060,cardH,rx=6,ry=-10,z=40,
      camRy=0,camRx=5,camZ=120,click,points,progress,deco=false,closeup=false,enterBeats=3}) => {
  const l = useCurrentFrame();
  const entry = stepDur(enterBeats);
  // 相机从远处推到近处并缓慢环绕
  const camRyT = interpolate(l,[0,entry,dur],[camRy-26,camRy,camRy+12],clamp);
  const camRxT = interpolate(l,[0,entry,dur],[camRx+10,camRx,camRx-2],clamp);
  const camZT = interpolate(l,[0,entry,dur],[camZ-340,camZ+20,camZ-14],clamp);
  // 卡片飞入定格
  const eZ = interpolate(l,[0,entry],[z-560,z],clamp);
  const eRy = interpolate(l,[0,entry],[ry+44,ry],clamp);
  const eRx = interpolate(l,[0,entry],[rx+22,rx],clamp);
  const eS = interpolate(l,[0,entry],[0.5,1],clamp);
  // 定格后的缓慢漂移
  const ryT = interpolate(l,[entry,dur],[ry-11,ry+11],clamp);
  const rxT = interpolate(l,[entry,dur],[rx-3,rx+3],clamp);
  // 拍点脉冲
  const punch = beatPunch(l, FILM.fps, PERIOD, 0.035);
  const flyScale = l < entry ? eS : 1 + 0.03*Math.max(0,1-((l-entry)%stepDur(1))/(stepDur(1)*0.6))**2;
  // 产品特写：后半段推近
  const isClose = closeup && l > dur*0.55;
  const cw = isClose ? cardW*1.24 : cardW;
  const cy = isClose ? cardY-30 : cardY;
  const cz = isClose ? z-130 : z;
  const opacity = interpolate(l,[0,Math.max(1,entry/2)],[0,1],clamp);
  const pStart = entry + stepDur(1);
  return (
    <AbsoluteFill style={{opacity}}>
      <Camera ry={camRyT} rx={camRxT} tz={camZT} scale={punch}>
        <div style={{position:'absolute',left:0,right:0,top:112,textAlign:'center',zIndex:26}}>
          <MetalText size={100} delay={0} from="scale" tint="silver">{head}</MetalText>
          {sub && <div style={{marginTop:10}}><Sub delay={2} size={27} color={COLOR.dim} maxWidth={780}>{sub}</Sub></div>}
        </div>

        {clip ? (
          <VideoClip src={clip.src} start={clip.start ?? 0} x={cardX} y={cy} w={cw} h={cardH}
            delay={0} ry={l<entry?eRy:ryT} rx={l<entry?eRx:rxT} z={l<entry?eZ:cz}
            floatAmp={11} scale={flyScale*1.02} mute/>
        ) : file ? (
          <Shot3D file={file} x={cardX} y={cy} w={cw} h={cardH}
            delay={0} ry={l<entry?eRy:ryT} rx={l<entry?eRx:rxT} z={l<entry?eZ:cz}
            floatAmp={13} scale={flyScale*1.02}/>
        ) : null}

        {deco && (file || clip) && (
          <>
            <Shot3D file={SHOTS.versions} x={cardX-470} y={cardY-250} w={300} delay={pStart} ry={-26} rx={10} z={-150} floatAmp={9}/>
            <Shot3D file={SHOTS.tasks}    x={cardX+470} y={cardY-250} w={290} delay={pStart+stepDur(1)} ry={26} rx={-8} z={-140} floatAmp={9}/>
            <Shot3D file={SHOTS.mods}     x={cardX-380} y={cardY+300} w={300} delay={pStart+stepDur(2)} ry={-14} rx={14} z={-165} floatAmp={8}/>
            <Shot3D file={SHOTS.online}   x={cardX+380} y={cardY+300} w={290} delay={pStart+stepDur(3)} ry={18} rx={-12} z={-150} floatAmp={8}/>
          </>
        )}

        {points && points.map((t,i)=>(
          <Point key={i} text={t} x={80} y={dur*0 + 700 + i*54} delay={pStart + i*stepDur(1)}/>
        ))}

        {progress && <RealProgress x={progress.x} y={progress.y} w={progress.w} pct={progress.pct}
          label={progress.label} detail={progress.detail} delay={pStart + stepDur(progress.atRel)}/>}

        {click && <ClickPoint x={click.x} y={click.y} at={click.atRel} label={click.label}/>}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 1) HOOK：开场 20 秒抛出最惊艳画面 ----------
const Hook: React.FC = () => {
  const l = useCurrentFrame();
  const b = l / stepDur(1); // 相对拍数
  const g = CLIP_MARKS.game;
  // 3D 卡片阵列：随拍点旋转
  const cards: {file:string;x:number;y:number;w:number}[] = [
    {file:SHOTS.repoShaders,x:180,y:150,w:360},
    {file:SHOTS.repoMods,x:1520,y:120,w:340},
    {file:SHOTS.skins,x:120,y:640,w:330},
    {file:SHOTS.online,x:1560,y:600,w:340},
    {file:SHOTS.repoPacks,x:330,y:820,w:300},
    {file:SHOTS.instPacks,x:1330,y:830,w:300},
  ];
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(l,[0,stepDur(40)],[-520,170],clamp)}
        ry={interpolate(l,[0,stepDur(20),stepDur(40)],[-16,10,-14],clamp)}
        rx={interpolate(l,[0,stepDur(40)],[12,-4],clamp)}
        scale={beatPunch(l,30,PERIOD,0.07)}>
        {cards.map((c,i)=>{
          const ph = Math.sin((l/stepDur(1) + i*1.7));
          const on = b >= 6 + i*1.1;
          return (
            <div key={c.file} style={{position:'absolute',left:c.x,top:c.y,width:c.w,opacity:on?interpolate(b,[6+i*1.1,7.4+i*1.1],[0,1],clamp):0,
              transformStyle:'preserve-3d',
              transform:`translateZ(${-160 + ph*90}px) rotateY(${ph*26}deg) rotateX(${ph*8}deg) scale(${0.9 + ph*0.06})`}}>
              <Shot3D file={c.file} x={0} y={0} w={c.w} delay={0} floatAmp={0} scale={1} sweep/>
            </div>
          );
        })}

        {b>=0 && b<4 && (
          <AbsoluteFill style={{display:'flex',alignItems:'center',justifyContent:'center'}}>
            <MetalText size={150} delay={0} from="scale" tint="silver">启动。</MetalText>
          </AbsoluteFill>
        )}
        {b>=4 && b<8 && (
          <VideoClip src={CLIPS.game} start={g.menu} x={560} y={230} w={800} h={470} delay={0} ry={-12} rx={7} z={-40} floatAmp={12} scale={1.05}/>
        )}
        {b>=8 && b<12 && (
          <AbsoluteFill style={{display:'flex',alignItems:'center',justifyContent:'center'}}>
            <MetalText size={132} delay={0} from="scale" tint="gold">一键。</MetalText>
          </AbsoluteFill>
        )}
        {b>=12 && b<16 && (
          <AbsoluteFill style={{display:'flex',alignItems:'center',justifyContent:'center'}}>
            <MetalText size={134} delay={0} from="scale" tint="blue">全部功能。</MetalText>
          </AbsoluteFill>
        )}
        {b>=16 && b<20 && (
          <AbsoluteFill style={{display:'flex',alignItems:'center',justifyContent:'center'}}>
            <MetalText size={140} delay={0} from="scale" tint="green">开源免费。</MetalText>
          </AbsoluteFill>
        )}
        {b>=20 && (
          <AbsoluteFill style={{display:'flex',flexDirection:'column',alignItems:'center',justifyContent:'center'}}>
            <MetalText size={116} delay={0} from="scale" tint="silver">SalmonLauncher</MetalText>
            <div style={{marginTop:18}}><Sub delay={2} size={34} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
          </AbsoluteFill>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 2) BRAND：极简 / 一键启动 / 开源免费 ----------
const Brand: React.FC<{base:number; dur:number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = stepDur(9);
  const idx = Math.min(2, Math.floor(l/seg));
  const shots = [
    {file:SHOTS.home, head:'极简',  sub:'一个启动面板，选好实例与账户即可进入世界'},
    {file:SHOTS.home, head:'一键启动', sub:'一个按钮完成启动、校验与进入游戏'},
    {file:SHOTS.about, head:'开源免费', sub:'GPL-3.0-or-later 开源，Windows x64 免费使用'},
  ];
  const s = shots[idx];
  return (
    <Chapter dur={seg} head={s.head} sub={s.sub} file={s.file}
      cardX={960} cardY={470} cardW={1080} ry={idx%2?12:-12} rx={6} z={40}
      camRy={idx%2?-10:10} camRx={5} camZ={120} deco/>
  );
};

// ---------- 3) ONE-CLICK：主线（真实录屏） ----------
const OneClick: React.FC<{base:number; dur:number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
  const m = CLIP_MARKS.launch;
  const g = CLIP_MARKS.game;
  const idx = Math.min(4, Math.floor(l/seg));
  const local = l - idx*seg;
  const stages = [
    {clip:CLIPS.launch, start:m.click, head:'打开即用', sub:'启动面板一览：实例、账户、状态',
     points:['实例与账户一目了然'], pct:[0.02,0.18], label:'准备启动', detail:'真实录屏 · 启动面板', ry:-12},
    {clip:CLIPS.launch, start:m.click+2.2, head:'点击启动', sub:'一个按钮，交给启动器',
     points:['点击「启动 Minecraft」'], pct:[0.18,0.34], label:'启动准备', detail:'真实录屏 · 点击启动', ry:12,
     click:{x:1180,y:470,label:'启动 Minecraft',atRel:stepDur(3)}},
    {clip:CLIPS.launch, start:m.downloading, head:'自动校验资源', sub:'首次启动自动补齐，进度实时可见',
     points:['分段下载 · 失败重试','逐文件校验完整性'], pct:[0.34,0.92], label:'资源下载与校验', detail:'真实录屏 · 进度与速度实时刷新', ry:-12},
    {clip:CLIPS.game, start:g.menu, head:'进入游戏', sub:'游戏窗口打开，直达主菜单',
     points:['窗口自动打开 · 无需手动'], pct:[0.92,1], label:'启动完成', detail:'真实录屏 · Minecraft 1.21.11 主菜单', ry:12, game:true},
    {file:SHOTS.home, head:'不到一分钟', sub:'从点击到进入世界，全程无需手动干预',
     points:['一次点击，进入世界'], ry:-10, closeup:false},
  ];
  const s = stages[idx];
  const pct = interpolate(local,[stepDur(3),seg-4],(s.pct ?? [0,1]) as [number,number],clamp);
  return (
    <Chapter dur={seg} head={s.head} sub={s.sub}
      clip={(s as any).clip ? {src:(s as any).clip, start:(s as any).start} : undefined}
      file={(s as any).file}
      cardX={960} cardY={470} cardW={1080} cardH={(s as any).game ? 620 : undefined}
      ry={s.ry} rx={6} z={40} camRy={0} camRx={5} camZ={120}
      click={(s as any).click} points={s.points}
      progress={{pct,label:s.label ?? '',detail:s.detail,atRel:2,x:70,y:650,w:1320}} deco/>
  );
};

// ---------- 4) CONTENT：内容生态（主卖点） ----------
const Content: React.FC<{base:number; dur:number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
  const t = CLIP_MARKS.tour;
  const t2 = CLIP_MARKS.tour2;
  const idx = Math.min(7, Math.floor(l/seg));
  const stages = [
    {clip:CLIPS.tour2, start:t2.repoMods, head:'模组仓库', sub:'集成 Modrinth，搜索、筛选、一键安装', points:['搜索 · 筛选 · 依赖检查'], closeup:false},
    {clip:CLIPS.tour2, start:t2.shaders,  head:'光影仓库', sub:'光影包浏览与安装', points:['让画面更出彩'], closeup:true},
    {clip:CLIPS.tour2, start:t2.packs,    head:'资源包仓库', sub:'纹理与音效自由替换', points:['自定义游戏外观'], closeup:false},
    {clip:CLIPS.tour,  start:t.mods,      head:'实例模组管理', sub:'启用 / 停用即时生效', points:['按实例隔离，互不干扰'], closeup:false},
    {clip:CLIPS.tour2, start:t2.instPacks,head:'实例内容总览', sub:'模组 · 光影 · 资源包集中管理', points:['一个实例，一处管理'], closeup:true},
    {clip:CLIPS.tour,  start:t.skins,     head:'3D 皮肤预览', sub:'皮肤站集成，所见即所得', points:['360° 预览后再换'], closeup:false},
    {clip:CLIPS.tour,  start:t.online,    head:'联机 · Campfire', sub:'进入世界即开放，邀请码发给好友', points:['随包附带 · 无需端口映射'], closeup:false},
    {file:SHOTS.instShaders, head:'内容生态', sub:'模组 · 光影 · 资源包 · 联机，全部内置', points:['一个启动器，玩遍全部内容'], closeup:true},
  ];
  const s = stages[idx];
  return (
    <Chapter dur={seg} head={s.head} sub={s.sub}
      clip={(s as any).clip ? {src:(s as any).clip, start:(s as any).start} : undefined}
      file={(s as any).file}
      cardX={960} cardY={470} cardW={1100}
      ry={idx%2?13:-13} rx={6} z={50} camRy={idx%2?-11:11} camRx={6} camZ={130}
      points={s.points} closeup={s.closeup} deco/>
  );
};

// ---------- 5) CHARACTER：主题工坊 ----------
const Character: React.FC<{base:number; dur:number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
  const m = CLIP_MARKS.theme;
  const idx = l < seg ? 0 : 1;
  if (idx === 0) {
    return (
      <Chapter dur={seg} head="主题工坊" sub="从 Minecraft 风格画面取色，整套配色与壁纸一键应用"
        clip={{src:CLIPS.theme, start:m.scroll}}
        cardX={960} cardY={470} cardW={1100} ry={-10} rx={6} z={40}
        camRy={0} camRx={6} camZ={120}
        click={{x:188,y:622,label:'主题工坊',atRel:stepDur(3)}}
        points={['壁纸自动提取配色']} deco/>
    );
  }
  return (
    <Chapter dur={seg} head="一键整套主题" sub="壁纸 + 配色 + 强调色同时应用"
      file={SHOTS.theme}
      cardX={960} cardY={470} cardW={1100} ry={12} rx={6} z={40}
      camRy={0} camRx={6} camZ={120}
      points={['10 张内置场景 · 也可用本地图片']} closeup deco/>
  );
};

// ---------- 6) COVERAGE：全功能覆盖（压缩快剪） ----------
const Coverage: React.FC<{base:number; dur:number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = stepDur(12);
  const m = CLIP_MARKS.onboarding;
  const i = CLIP_MARKS.install;
  const t = CLIP_MARKS.tour;
  const items = [
    {clip:CLIPS.onboarding, start:m.lang,    head:'首次引导 · 语言'},
    {clip:CLIPS.onboarding, start:m.terms,   head:'首次引导 · 条款'},
    {clip:CLIPS.onboarding, start:m.migrate, head:'首次引导 · 迁移'},
    {clip:CLIPS.onboarding, start:m.account, head:'首次引导 · 账户'},
    {clip:CLIPS.install,    start:i.versions,head:'安装 · 选版本'},
    {clip:CLIPS.install,    start:i.loaders, head:'安装 · 选加载器'},
    {clip:CLIPS.install,    start:i.downloading, head:'安装 · 下载进度'},
    {clip:CLIPS.tour,       start:t.overview,head:'实例概览'},
    {clip:CLIPS.tour,       start:t.saves,   head:'存档管理'},
    {clip:CLIPS.tour,       start:t.logs,    head:'实例日志'},
    {clip:CLIPS.tour,       start:t.global,  head:'全局偏好与诊断'},
  ];
  const idx = Math.min(items.length-1, Math.floor(l/seg));
  const it = items[idx];
  return (
    <Chapter dur={seg} head={it.head} sub="全功能覆盖 · 快速带过"
      clip={{src:it.clip, start:it.start}}
      cardX={960} cardY={470} cardW={1120} ry={idx%2?10:-10} rx={6} z={40}
      camRy={idx%2?-8:8} camRx={5} camZ={130} enterBeats={2}/>
  );
};

// ---------- 7) MONTAGE：功能全景 ----------
const Montage: React.FC<{base:number; dur:number}> = ({dur}) => {
  const l = useCurrentFrame();
  const files = [SHOTS.home,SHOTS.tasks,SHOTS.versions,SHOTS.loaders,SHOTS.install,SHOTS.overview,SHOTS.mods,SHOTS.saves,SHOTS.instCfg,SHOTS.instLogs,SHOTS.repoMods,SHOTS.repoShaders,SHOTS.repoPacks,SHOTS.instPacks,SHOTS.instShaders,SHOTS.online,SHOTS.skins,SHOTS.diag,SHOTS.appLog,SHOTS.global,SHOTS.launchSet,SHOTS.theme,SHOTS.about,SHOTS.terms];
  const perCard = Math.floor(dur / files.length);
  const cardW = 306, gap = 16, cols = 4;
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(l,[0,dur],[-560,220],clamp)} ry={interpolate(l,[0,dur],[-26,26],clamp)} rx={8}
        scale={beatPunch(l,30,PERIOD,0.09)}>
        <div style={{position:'absolute',left:88,top:78,zIndex:30}}>
          <Tag text="功能全景 · 快速剪辑" accent/>
          <div style={{marginTop:12}}><MetalText size={74} delay={1} from="bottom" tint="silver" align="left">全部功能，一应俱全。</MetalText></div>
        </div>
        {files.map((file,i)=>{
          const d = i*perCard;
          const row = Math.floor(i/cols), col = i%cols;
          const p = interpolate(l,[d,d+14],[0,1],clamp);
          return (
            <div key={file+i} style={{position:'absolute',left:col*(cardW+gap),top:250+row*(220+gap),width:cardW,height:220,
              transformStyle:'preserve-3d',
              transform:`translateZ(${(1-p)*-640}px) rotateY(${(col-1.5)*6}deg) rotateX(${(1-p)*34}deg) scale(${(0.72+p*0.28)*beatPunch(l-d,30,PERIOD,0.06)})`,
              opacity:interpolate(l,[d,d+3],[0,1],clamp)}}>
              <div style={{width:'100%',height:'100%',borderRadius:12,overflow:'hidden',border:`1px solid ${COLOR.line}`,boxShadow:'0 22px 60px rgba(0,0,0,0.78)'}}>
                <Img src={staticFile(`shots/${file}`)} style={{width:'100%',height:'100%',objectFit:'cover',objectPosition:'top left'}}/>
              </div>
            </div>
          );
        })}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 8) CLOSE：品牌收束 ----------
const Close: React.FC<{base:number; dur:number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = stepDur(18);
  const idx = Math.min(2, Math.floor(l/seg));
  const lines = ['极简。','一键启动。','全部功能。'];
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background:'radial-gradient(ellipse 70% 50% at 50% 45%, rgba(126,217,87,0.16), transparent 70%)'}}/>
      <Camera tz={interpolate(l,[0,dur],[-260,80],clamp)} rx={interpolate(l,[0,dur],[6,0],clamp)}
        ry={interpolate(l,[0,dur],[-10,10],clamp)} scale={beatPunch(l,30,PERIOD,0.05)}>
        <AbsoluteFill style={{display:'flex',alignItems:'center',justifyContent:'center'}}>
          <MetalText size={138} delay={0} from="scale" tint={idx===1?'blue':idx===2?'gold':'silver'}>{lines[idx]}</MetalText>
        </AbsoluteFill>
        <div style={{position:'absolute',left:0,right:0,bottom:150,textAlign:'center',opacity:interpolate(l,[seg*2,seg*2+14],[0,1],clamp)}}>
          <MetalText size={64} delay={0} from="bottom" tint="silver">SalmonLauncher</MetalText>
          <div style={{marginTop:10}}><Sub delay={2} size={26} color={COLOR.dim}>{LINKS.siteFull}　·　{LINKS.githubFull}</Sub></div>
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 9) OUTRO：精简声明 + 二维码 ----------
const Outro: React.FC<{total:number}> = ({total}) => {
  const l = useCurrentFrame();
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background:'radial-gradient(ellipse 75% 55% at 50% 34%, rgba(126,217,87,0.18), transparent 70%)'}}/>
      <Camera tz={interpolate(l,[0,Math.min(total,stepDur(20))],[-170,50],clamp)}
        rx={interpolate(l,[0,total],[4,0],clamp)} scale={beatPunch(l,30,PERIOD,0.04)}>
        <div style={{position:'absolute',left:0,right:0,top:56,textAlign:'center'}}>
          <MetalText size={96} delay={0} from="scale" tint="silver">SalmonLauncher</MetalText>
          <div style={{marginTop:8}}><Sub delay={3} size={30} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
        </div>
        <QRCard file="site.png"   title="官方网站"   url={LINKS.site}   x={300} y={250} delay={4} size={170}/>
        <QRCard file="github.png" title="GitHub 仓库" url={LINKS.github} x={700} y={250} delay={7} size={170}/>
        <QRCard file="blog.png"   title="作者博客"   url={LINKS.blog}   x={1100} y={250} delay={10} size={170}/>

        <div style={{position:'absolute',left:90,right:90,bottom:96,padding:'16px 22px',borderRadius:16,
          background:'rgba(20,20,22,0.82)',border:`1px solid ${COLOR.line}`,
          opacity:interpolate(l,[12,20],[0,1],clamp),transform:`translateY(${interpolate(l,[12,20],[24,0],clamp)}px)`}}>
          <div style={{fontFamily:FONT,fontSize:19,color:COLOR.ink,fontWeight:700,marginBottom:7}}>视频声明 · 制作人员 · 致谢名单 · 版权解释</div>
          <div style={{fontFamily:FONT,fontSize:15.5,color:COLOR.dim,lineHeight:1.62}}>
            <div><span style={{color:COLOR.ink,fontWeight:600}}>制作人员：</span>Hugo Mou（产品设计 / 开发 / 维护）　<span style={{color:COLOR.ink,fontWeight:600}}>致谢：</span>Modrinth 与社区作者 · Campfire 联机组件 · .NET / Windows App SDK / WinUI 3 · Mojang 官方版本与资源生态</div>
            <div><span style={{color:COLOR.ink,fontWeight:600}}>配乐与素材：</span>“3 Strikes” — Terror Jr（Bilibili BV1tek2YsEYY，仅作展示） · 画面为 SalmonLauncher 2.0.14 真实录屏与界面截图（默认状态，云母默认关闭）</div>
            <div><span style={{color:COLOR.accent,fontWeight:700}}>本软件由 AI 辅助开发制作　·　本宣传片由 AI 生成制作</span></div>
            <div><span style={{color:COLOR.ink,fontWeight:600}}>项目许可：</span>GPL-3.0-or-later（开源免费，Windows x64）　官网 {LINKS.siteFull}　GitHub {LINKS.githubFull}</div>
            <div style={{marginTop:5,fontSize:14,color:'rgba(134,134,139,0.92)'}}>版权与声明：Minecraft 为 Mojang Studios / Microsoft 的商标，本项目与 Mojang/Microsoft 无官方关联；Modrinth、Campfire 等名称与服务归各自权利人所有。本视频仅用于 SalmonLauncher 产品展示与介绍，不构成对第三方商标或内容的授权声明。</div>
          </div>
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 时间线（全部落在 4 拍整数倍的小节线上） ----------
export const Film: React.FC = () => {
  const seg = (startBeat:number, durBeats:number) => ({from:BF(startBeat), dur:stepDur(durBeats)});
  const S = {
    hook:     seg(0,40),     // 0-40    开场 20s 高能
    brand:    seg(40,28),    // 40-68   品牌气质
    oneclick: seg(68,100),   // 68-168  主线：一键启动→进游戏
    content:  seg(168,160),  // 168-328 内容生态（最长）
    character:seg(328,40),   // 328-368 主题工坊
    coverage: seg(368,132),  // 368-500 全功能覆盖
    montage:  seg(500,36),   // 500-536 功能矩阵
    close:    seg(536,54),   // 536-590 品牌收束
  };
  const outroFrom = BF(590);
  const outroDur = Math.max(1, toF(FILM.duration) - outroFrom);

  const f = useCurrentFrame();
  const t = f / FILM.fps;
  // 全局拍点闪光（强拍最亮）
  let flash = 0;
  for (let i=0;i<BEAT_COUNT;i+=1) {
    const down = i%4===0;
    const d = down ? 0.2 : i%2===0 ? 0.1 : 0.05;
    const w = down ? 0.1 : 0.06;
    flash = Math.max(flash, hit(t,i,w)*d);
  }
  // 章节切点额外高光，让切换正好压在拍点
  const cuts = [S.brand.from, S.oneclick.from, S.content.from, S.character.from, S.coverage.from, S.montage.from, S.close.from, outroFrom];

  return (
    <Stage>
      <Audio src={staticFile(AUDIO_FILE)}/>
      <Sequence from={S.hook.from}     durationInFrames={S.hook.dur}><Hook/></Sequence>
      <Sequence from={S.brand.from}    durationInFrames={S.brand.dur}><Brand base={0} dur={S.brand.dur}/></Sequence>
      <Sequence from={S.oneclick.from} durationInFrames={S.oneclick.dur}><OneClick base={0} dur={S.oneclick.dur}/></Sequence>
      <Sequence from={S.content.from}  durationInFrames={S.content.dur}><Content base={0} dur={S.content.dur}/></Sequence>
      <Sequence from={S.character.from} durationInFrames={S.character.dur}><Character base={0} dur={S.character.dur}/></Sequence>
      <Sequence from={S.coverage.from} durationInFrames={S.coverage.dur}><Coverage base={0} dur={S.coverage.dur}/></Sequence>
      <Sequence from={S.montage.from}  durationInFrames={S.montage.dur}><Montage base={0} dur={S.montage.dur}/></Sequence>
      <Sequence from={S.close.from}    durationInFrames={S.close.dur}><Close base={0} dur={S.close.dur}/></Sequence>
      <Sequence from={outroFrom}       durationInFrames={outroDur}><Outro total={outroDur}/></Sequence>

      {flash > 0.001 && <AbsoluteFill style={{backgroundColor:'#fff',opacity:flash*0.26,mixBlendMode:'screen',pointerEvents:'none'}}/>}
      <CutAccent frames={cuts} strength={0.26}/>
      <AbsoluteFill style={{boxShadow:'inset 0 0 190px rgba(0,0,0,0.5)',pointerEvents:'none'}}/>
      <LegalStrip/>
    </Stage>
  );
};
