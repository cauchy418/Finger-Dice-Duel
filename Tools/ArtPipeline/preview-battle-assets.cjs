const fs=require('node:fs/promises');
const path=require('node:path');
const {sharp,root}=require('./deps.cjs');
const art=path.join(root,'Art/Production/battle-v1');
const dst=path.join(root,'Assets/Resources/Art/Battle/v1');
const fontfile=path.join(root,'Assets/Resources/Fonts/NotoSansCJKsc-Medium.otf');
async function label(text,size=20,color='#D6C49D'){
  return sharp({text:{text:`<span foreground="${color}">${text}</span>`,font:`Noto Sans CJK SC ${size}`,fontfile,dpi:72,rgba:true}}).png().toBuffer();
}
async function contact(group,keys,cols,cellW,cellH,imgW,imgH,filename){
  const rows=Math.ceil(keys.length/cols),parts=[];
  for(let i=0;i<keys.length;i++){
    const x=(i%cols)*cellW,y=Math.floor(i/cols)*cellH;
    const file=path.join(dst,group,`${keys[i]}.png`);
    const pic=await sharp(file).resize(imgW,imgH,{fit:'contain',background:'#00000000'}).png().toBuffer();
    parts.push({input:pic,left:x+Math.floor((cellW-imgW)/2),top:y+12});
    const text=await label(keys[i],16);
    const m=await sharp(text).metadata();
    parts.push({input:text,left:x+Math.max(4,Math.floor((cellW-m.width)/2)),top:y+cellH-28});
  }
  await sharp({create:{width:cols*cellW,height:rows*cellH,channels:4,background:'#181C22'}}).composite(parts).png().toFile(path.join(art,filename));
}
async function assembly(){
 const p=[];
 async function add(rel,x,y,w,h){p.push({input:await sharp(path.join(dst,rel)).resize(w,h,{fit:'fill'}).png().toBuffer(),left:x,top:y});}
 async function text(t,x,y,size=24,c){p.push({input:await label(t,size,c),left:x,top:y});}
 await add('UI/d20_sigil.png',592,330,500,500);
 await add('UI/panel_log.png',1410,215,450,560);
 await text('战报',1445,246,30);
 await add('UI/divider.png',1440,290,390,24);
 for(const [i,t] of ['法师运算结果为 6','士兵受到 4 点伤害','法师的回合结束','轮到士兵行动'].entries())await text(t,1450,340+i*65,21);
 await text('手指骰子对决',70,58,48);
 await text('第 3 轮',115,144,28);
 for(const [y,key,name,hp] of [[30,'wizard','法师','18 / 20'],[628,'soldier','士兵','16 / 20']]){
   await add('UI/badge_ring.png',690,y,108,108);
   await add(`Classes/${key}.png`,703,y+13,82,82);
   await text(name,820,y+7,29);
   await add('UI/health_track.png',814,y+55,340,33);
   await add('UI/health_fill.png',826,y+61,key==='wizard'?288:255,20);
   await text(hp,939,y+62,17,'#F5E9CD');
 }
 for(const [x,y,side,n,state] of [[740,148,'left',3,'available'],[958,148,'right',5,'normal'],[740,742,'left',4,'selected'],[958,742,'right',2,'normal']]){
   await add('UI/hand_plate.png',x,y,192,269);
   await add(`Hands/hand_${side}_${n}.png`,x+1,y+2,190,238);
   await add('UI/label_strip.png',x+9,y+224,174,35);
   await text(`${n}  ${side==='left'?'左手':'右手'}`,x+59,y+230,21);
   await add(`UI/hand_frame_${state}.png`,x,y,192,269);
 }
 await text('4 + 3 = 7',848,501,35);
 await text('选择加数',859,1031,26);
 await add('UI/button_normal.png',1560,945,260,72);
 await text('取消',1660,966,26);
 const bg=await sharp(path.join(dst,'Backgrounds/battle_table.png')).resize(1920,1080).png().toBuffer();
 await sharp(bg).composite(p).png().toFile(path.join(art,'battle-assembly-preview.png'));
}
async function main(){
 await fs.mkdir(art,{recursive:true});
 await contact('Hands',[...Array.from({length:10},(_,i)=>`hand_left_${i}`),...Array.from({length:10},(_,i)=>`hand_right_${i}`)],10,160,240,154,193,'hands-contact.png');
 const m=JSON.parse(await fs.readFile(path.join(art,'vector-manifest.json'),'utf8'));
 for(const g of ['Classes','Damage','Status','Overlay'])await contact(g,m.assets.filter(a=>a.group===g).map(a=>a.key),6,176,180,132,132,`${g.toLowerCase()}-contact.png`);
 const ui=m.assets.filter(a=>a.group==='UI');
 await contact('UI',ui.map(a=>a.key),4,300,220,264,168,'ui-contact.png');
 const tiny=m.assets.filter(a=>['Damage','Status','Classes'].includes(a.group)),parts=[];
 for(let i=0;i<tiny.length;i++){
   const x=i%8*200,y=Math.floor(i/8)*112;
   for(const [j,size]of [28,40,64].entries())parts.push({input:await sharp(path.join(dst,tiny[i].path)).resize(size,size).png().toBuffer(),left:x+[8,49,104][j],top:y+12+(64-size)});
   parts.push({input:await label(tiny[i].key,14),left:x+8,top:y+90});
 }
 await sharp({create:{width:1600,height:Math.ceil(tiny.length/8)*112,channels:4,background:'#181C22'}}).composite(parts).png().toFile(path.join(art,'small-size-check.png'));
 await assembly();
 console.log('Created hand, icon and UI contact sheets plus a battle assembly preview from actual exported assets.');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
