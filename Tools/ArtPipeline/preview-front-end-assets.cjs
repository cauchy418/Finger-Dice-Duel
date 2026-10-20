const fs=require('node:fs/promises');
const path=require('node:path');
const {sharp,root}=require('./deps.cjs');
const art=path.join(root,'Art/Production/front-end-v1');
const dst=path.join(root,'Assets/Resources/Art/FrontEnd/v1');
const battle=path.join(root,'Assets/Resources/Art/Battle/v1');
const fontRoot=path.join(root,'Assets/Resources/Fonts');
const out=path.join(art,'previews');
const golden='#DCC28D',body='#D4D2C7',muted='#9CABA8';
let manifest,classes;
const escape=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
async function words(text,size=24,color=body,width,title=false,bold=false){
 const fontfile=path.join(fontRoot,title?'SourceHanSerifCN-SemiBold.otf':bold?'NotoSansCJKsc-Bold.otf':'NotoSansCJKsc-Medium.otf');
 return sharp({text:{text:`<span foreground="${color}">${escape(text)}</span>`,font:`${title?'Source Han Serif CN SemiBold':'Noto Sans CJK SC'} ${size}`,fontfile,dpi:72,rgba:true,...(width?{width}:{}),spacing:6}}).png().toBuffer();
}
class Page{
 constructor(bg){this.bg=path.join(dst,'Backgrounds',bg+'.png');this.parts=[];}
 async add(file,x,y,w,h,fit='contain'){
  this.parts.push({input:await sharp(file).resize(w,h,{fit,background:'#00000000'}).png().toBuffer(),left:Math.round(x),top:Math.round(y)});
 }
 async pic(rel,x,y,w,h){await this.add(path.join(dst,rel+'.png'),x,y,w,h);}
 async reuse(rel,x,y,w,h){await this.add(path.join(battle,rel+'.png'),x,y,w,h);}
 async text(text,x,y,size=24,color=body,width,title=false,bold=false){this.parts.push({input:await words(text,size,color,width,title,bold),left:x,top:y});}
 async center(text,cx,y,size=24,color=body,title=false,bold=false){const input=await words(text,size,color,undefined,title,bold),m=await sharp(input).metadata();this.parts.push({input,left:Math.round(cx-m.width/2),top:y});}
 async box(key,x,y,w,h){
  const a=manifest.assets.find(a=>a.key===key);if(!a)throw Error(key);
  if(!a.border){await this.pic('UI/'+key,x,y,w,h);return;}
  const [l,b,r,t]=a.border,src=path.join(dst,a.path);
  const fx=Math.min(1,w/(l+r)),fy=Math.min(1,h/(t+b));
  const dl=Math.round(l*fx),dr=Math.round(r*fx),dt=Math.round(t*fy),db=Math.round(b*fy);
  const sx=[0,l,a.width-r],sy=[0,t,a.height-b],sw=[l,a.width-l-r,r],sh=[t,a.height-t-b,b];
  const dx=[0,dl,w-dr],dy=[0,dt,h-db],dw=[dl,w-dl-dr,dr],dh=[dt,h-dt-db,db];
  for(let row=0;row<3;row++)for(let col=0;col<3;col++){
   if(dw[col]<=0||dh[row]<=0||sw[col]<=0||sh[row]<=0)continue;
   this.parts.push({input:await sharp(src).extract({left:sx[col],top:sy[row],width:sw[col],height:sh[row]}).resize(dw[col],dh[row],{fit:'fill'}).png().toBuffer(),left:x+dx[col],top:y+dy[row]});
  }
 }
 async save(file){await sharp(this.bg).resize(1920,1080).composite(this.parts).png().toFile(path.join(out,file));}
}
async function menu(bg,file){
 const p=new Page(bg);
 await p.center('手指骰子对决',960,125,108,'#E7C680',true);
 await p.center('F I N G E R   D I C E   D U E L',960,276,25,'#CEB784');
 await p.box('divider_horizontal',610,326,700,24);
 const rows=[['adventure','冒险模式','剧情与挑战（敬请期待）','normal'],['competition','竞技模式','对战：1v1 / 2v2 / 8 人','selected'],['casual','娱乐模式','双将等变体玩法','normal']];
 for(let i=0;i<rows.length;i++){
  const [key,label,sub,state]=rows[i],y=375+i*164;
  await p.box('mode_button_'+state,580,y,760,144);
  await p.pic('Illustrations/mode_'+key,598,y+6,160,132);
  await p.text(label,796,y+28,38,'#EEE1C0',undefined,true);
  await p.text(sub,796,y+84,23,state==='selected'?'#BCD4E8':muted);
 }
 for(const [i,[key,label]]of [['codex','角色图鉴'],['arcane_orb','伤害与状态'],['rules','规则说明'],['quit','退出游戏']].entries()){
  const x=535+i*225;
  await p.pic('Icons/'+key,x,925,30,30);await p.text(label,x+42,925,22,'#C6B690');
 }
 await p.save(file);
}
async function selected(){
 const p=new Page('selection_oath_hall');
 await p.box('screen_frame',10,10,1900,1060);
 await p.center('选择角色',960,34,44,golden,true);
 await p.box('panel_sidebar',30,116,158,906);
 await p.box('panel_content',206,116,1220,906);
 await p.box('panel_detail',1442,116,442,906);
 const filters=[['all','全部'],['melee','近战'],['ranged','远程'],['magic','法术'],['support','辅助'],['special','特殊']];
 for(const [i,[key,label]]of filters.entries()){
  const y=152+i*130;await p.box('icon_button_'+(i===0?'selected':'normal'),48,y,122,104);
  await p.pic('Icons/'+key,88,y+12,42,42);await p.center(label,109,y+65,23,i===0?golden:muted);
 }
 for(let i=0;i<classes.length;i++){
  const x=244+(i%6)*194,y=149+Math.floor(i/6)*210;
  if(i===0){await p.box('roster_tile_selected',x-9,y-6,180,212);await p.pic('UI/selection_halo',x-2,y+2,164,164);}
  await p.reuse('Classes/'+classes[i].key,x+6,y+8,148,148);
  await p.center(classes[i].label,x+80,y+163,23,i===0?golden:body);
 }
 await p.reuse('Classes/soldier',1536,154,250,250);
 await p.center('士兵  Soldier',1663,433,33,golden,true);
 await p.center('绝对中立',1663,490,23,'#99B994');
 await p.box('divider_horizontal',1490,538,346,24);
 await p.text('生命 20    攻击 +1',1490,596,25,body);
 await p.text('防御 +1    距离 1',1490,640,25,body);
 await p.text('攻防各 +1 的白板基准。',1490,715,24,golden,345);
 await p.text('没有特殊能力。用它练习运算与攻防节奏。',1490,770,23,muted,345);
 await p.box('mode_button_selected',1490,884,346,96);
 await p.center('选择角色',1663,914,28,'#EEE1C0',true);
 await p.save('selection-assembly.png');
}
async function overview(){
 const p=new Page('codex_scribes_desk');
 await p.box('screen_frame',10,10,1900,1060);
 await p.text('图鉴总览',64,40,40,golden,undefined,true);
 for(const [i,name]of ['角色图鉴','伤害类型','负面效果','游戏规则'].entries()){
  const x=440+i*340;await p.box('tab_'+(i===0?'selected':'normal'),x,32,310,76);
  await p.center(name,x+155,55,27,i===0?'#F1E5CB':muted,true);
 }
 await p.box('panel_content',34,128,1360,908);
 await p.box('panel_detail',1410,128,474,908);
 for(let i=0;i<classes.length;i++){
  const x=77+(i%6)*217,y=159+Math.floor(i/6)*211;
  await p.reuse('Classes/'+classes[i].key,x+13,y,156,156);
  await p.center(classes[i].label,x+91,y+164,24,i===0?golden:body);
 }
 await p.center('冒险者档案',1647,169,33,golden,true);
 await p.pic('Illustrations/codex_parchment',1434,276,426,639);
 await p.pic('Illustrations/codex_book_dice',1487,308,322,215);
 await p.text('角色',1487,557,32,'#352618',undefined,true);
 await p.text('现有 24 名角色，各有独立的技能、玩法风格和阵营归属。',1487,619,23,'#48341F',320);
 await p.text('从徽记浏览角色，再查看技能、操作指导与应对方法。',1487,737,23,'#48341F',320);
 await p.save('codex-overview-assembly.png');
}
async function detail(){
 const p=new Page('codex_scribes_desk');
 await p.box('screen_frame',10,10,1900,1060);
 await p.text('角色图鉴',58,40,42,golden,undefined,true);
 await p.box('panel_sidebar',34,122,330,902);
 await p.box('panel_content',382,122,402,902);
 await p.box('panel_detail',800,122,1084,902);
 for(let i=0;i<9;i++){
  const y=144+i*94;
  await p.box('list_row_'+(i===0?'selected':'normal'),49,y,290,88);
  await p.reuse('Classes/'+classes[i].key,60,y+9,68,68);
  await p.text(classes[i].label,143,y+13,23,i===0?golden:body);
  await p.text(classes[i].en,143,y+48,16,muted);
 }
 await p.box('scroll_track',340,148,16,824);await p.box('scroll_thumb_normal',340,151,16,90);
 await p.reuse('Classes/soldier',411,160,344,344);
 await p.center('士兵',583,552,40,golden,true);
 await p.center('Soldier',583,613,26,muted);
 await p.box('divider_horizontal',425,683,320,24);
 await p.text('攻防各 +1 的白板基准。',430,746,25,golden,305,true);
 await p.center('绝对中立',583,867,24,'#99B994');
 await p.text('士兵  Soldier',840,158,43,golden,undefined,true);
 const stats=[['health','生命 20'],['melee','攻击 +1'],['counter','防御 +1'],['ranged','距离 1']];
 for(const [i,[icon,label]]of stats.entries()){
  const x=843+i*245;await p.pic('Icons/'+icon,x,231,27,27);await p.text(label,x+40,231,23,body);
 }
 await p.box('divider_horizontal',840,288,994,24);
 const sections=[
  ['skill','技能','攻防各 +1 的白板基准。'],
  ['detail','技能详情','没有特殊能力，但攻防各 +1、攻击距离 1 —— 扎实的基础角色。'],
  ['guide','操作指导','没有技能，全靠攻防各 +1 的底子。用它练基本功：优先凑 4/6/8 打输出，血量健康时不必急着合盾。'],
  ['counter','如何针对','没有可针对的机制，正常对拼即可。注意他的攻防各 +1，换血时你会略亏，尽量用技能换回节奏。'],
  ['synergy','与其他角色的特殊交互','与咒术师：他的攻击无视护盾与正向防御，士兵的防 +1 会被吃掉。\n与巨人：巨人攻 +2 且两段，士兵的防 +1 吃不消。']
 ];
 const positions=[330,438,558,704,850];
 for(let i=0;i<sections.length;i++){
  const [icon,title,text]=sections[i],y=positions[i];await p.pic('Icons/'+icon,840,y,28,28);
  await p.text(title,882,y,25,golden);await p.text(text,882,y+42,22,body,930);
 }
 await p.pic('UI/page_left_normal',782,1018,58,46);await p.center('1 / 24',960,1024,24,golden);await p.pic('UI/page_right_normal',1080,1018,58,46);
 await p.save('codex-detail-assembly.png');
}
async function contacts(){
 const names=['menu_candle_chamber','menu_moonlit_archive','selection_oath_hall','codex_scribes_desk'];
 const labels=['主菜单 · 烛光议事厅','主菜单备选 · 月夜档案馆','选将 · 誓言大厅','图鉴 · 抄写员书桌'];
 const parts=[];
 for(let i=0;i<4;i++){
  const x=(i%2)*960,y=Math.floor(i/2)*588;
  parts.push({input:await sharp(path.join(dst,'Backgrounds',names[i]+'.png')).resize(960,540).png().toBuffer(),left:x,top:y});
  parts.push({input:await words(labels[i],24,golden),left:x+26,top:y+548});
 }
 await sharp({create:{width:1920,height:1176,channels:4,background:'#0B1116'}}).composite(parts).png().toFile(path.join(out,'backgrounds-contact.png'));
 const screens=['main-menu-assembly.png','selection-assembly.png','codex-detail-assembly.png','codex-overview-assembly.png'];
 const screenLabels=['主菜单 · 素材拼装','选将 · 24 角色布局示意','角色图鉴详情 · 素材拼装','图鉴总览 · 素材拼装'];
 const all=[];
 for(let i=0;i<4;i++){
  const x=(i%2)*960,y=Math.floor(i/2)*588;
  all.push({input:await sharp(path.join(out,screens[i])).resize(960,540).png().toBuffer(),left:x,top:y});
  all.push({input:await words(screenLabels[i],24,golden),left:x+26,top:y+548});
 }
 await sharp({create:{width:1920,height:1176,channels:4,background:'#0B1116'}}).composite(all).png().toFile(path.join(out,'front-end-assembly-contact.png'));
 for(const group of ['UI','Icons','Illustrations']){
  const list=manifest.assets.filter(a=>a.group===group),cols=group==='UI'?5:group==='Icons'?6:3;
  const cw=group==='Icons'?192:300,ch=group==='Icons'?180:210,rows=Math.ceil(list.length/cols),layers=[];
  for(let i=0;i<list.length;i++){
   const a=list[i],x=(i%cols)*cw,y=Math.floor(i/cols)*ch;
   layers.push({input:await sharp(path.join(dst,a.path)).resize(cw-32,ch-56,{fit:'contain',background:'#00000000'}).png().toBuffer(),left:x+16,top:y+6});
   layers.push({input:await words(a.key,13,golden),left:x+12,top:y+ch-36});
  }
  await sharp({create:{width:cols*cw,height:rows*ch,channels:4,background:'#141D24'}}).composite(layers).png().toFile(path.join(out,group.toLowerCase()+'-contact.png'));
 }
}
async function main(){
 await fs.mkdir(out,{recursive:true});
 manifest=JSON.parse(await fs.readFile(path.join(art,'asset-manifest.json'),'utf8'));
 classes=JSON.parse(await fs.readFile(path.join(root,'Art/Production/battle-v1/asset-manifest.json'),'utf8')).assets.filter(a=>a.group==='Classes');
 const definition=await fs.readFile(path.join(root,'Assets/Scripts/Characters/CharacterDefinition.cs'),'utf8');
 const codex=await fs.readFile(path.join(root,'Assets/Scripts/Characters/CharacterCodex.cs'),'utf8');
 const ids=new Map([...definition.matchAll(/const string (\w+) = "([^"]+)"/g)].map(m=>[m[1],m[2]]));
 const english=new Map([...codex.matchAll(/Add\(CharacterDefinition\.(\w+), "([^"]+)"/g)].map(m=>[ids.get(m[1]),m[2]]));
 classes.forEach(a=>a.en=english.get(a.key)||a.key);
 if(classes.length!==24)throw Error('Expected all 24 classes');
 await menu('menu_candle_chamber','main-menu-assembly.png');await menu('menu_moonlit_archive','main-menu-alternate-assembly.png');
 await selected();await overview();await detail();await contacts();
 console.log('Wrote 5 full-size screen assemblies and 5 contact sheets. Static compositions, not runtime captures.');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
