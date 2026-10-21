// Generated art only: authorized transparent crop and resize. Never writes engine .meta files.
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { sharp, root } = require('./deps.cjs');
const art = path.join(root, 'Art/Production/status-icons-v3');
const dst = path.join(root, 'Assets/Resources/Art/Battle/v1/Status');
const names = [
  ['resonance','残响'], ['acid','强酸'], ['poison','中毒'], ['blind','目盲'],
  ['decay','腐朽'], ['burn','燃烧'], ['frost','覆霜'], ['shock','感电'],
  ['panic','恐慌'], ['bleed','流血'], ['wet','潮湿'], ['frozen','冻结'],
  ['stunned','震慑'], ['limp','跛脚'], ['knocked_down','倒伏'],
  ['charm','魅惑'], ['disabled','失能'], ['dizzy','眩晕']
];
const generated = new Set(['frost','bleed','wet','stunned','limp','knocked_down','charm','disabled','dizzy']);
const fontfile = path.join(root, 'Assets/Resources/Fonts/NotoSansCJKsc-Medium.otf');
async function label(text, size = 18) {
  const buffer = await sharp({text:{text:`<span foreground="#DDC18C">${text}</span>`,font:`Noto Sans CJK SC ${size}`,fontfile,dpi:72,rgba:true,width:170,align:'centre'}}).png().toBuffer();
  const meta = await sharp(buffer).metadata();
  return sharp({create:{width:170,height:30,channels:4,background:'#00000000'}})
    .composite([{input:buffer,left:Math.floor((170-meta.width)/2),top:Math.floor((30-meta.height)/2)}]).png().toBuffer();
}
async function main() {
  for (const key of generated) await fs.access(path.join(art, 'source', key + '.png'));
  for (const key of generated) {
    await sharp(path.join(art, 'source', key + '.png')).trim({background:'#00000000',threshold:2})
      .resize(448,448,{fit:'contain',background:'#00000000'})
      .extend({top:32,bottom:32,left:32,right:32,background:'#00000000'}).png().toFile(path.join(dst,key+'.png'));
  }
  // Old complete ice block now represents Frozen; the old psychic distress painting represents Panic.
  await fs.copyFile(path.join(dst,'freeze.png'),path.join(dst,'frozen.png'));
  await fs.copyFile(path.join(dst,'stun.png'),path.join(dst,'panic.png'));
  const assets = [], hashes = new Set(), contact = [], small = [];
  for (let i = 0; i < names.length; i++) {
    const [key,name] = names[i], file = path.join(dst,key+'.png');
    const bytes = await fs.readFile(file), meta = await sharp(bytes).metadata(), stats = await sharp(bytes).stats();
    if(meta.width!==512 || meta.height!==512 || !meta.hasAlpha || stats.channels[3].min!==0 || stats.channels[3].max!==255)
      throw new Error('Invalid dimensions or transparency: '+key);
    const hash=crypto.createHash('sha256').update(bytes).digest('hex');
    if(hashes.has(hash)) throw new Error('Duplicate active status art: '+key);
    hashes.add(hash);
    assets.push({key,name,path:'Assets/Resources/Art/Battle/v1/Status/'+key+'.png',width:512,height:512,sha256:hash,
      source:generated.has(key)?'Art/Production/status-icons-v3/source/'+key+'.png':key==='frozen'?'Assets/Resources/Art/Battle/v1/Status/freeze.png':key==='panic'?'Assets/Resources/Art/Battle/v1/Status/stun.png':'Art/Production/painted-icons-v2/native/Status/'+key+'.png',
      method:generated.has(key)?'Built-in image_gen, alpha crop and uniform resize':'Existing painted icon preserved; canonical status resource key',frameIncluded:true});
    const x=i%6*180,y=Math.floor(i/6)*204;
    contact.push({input:await sharp(bytes).resize(152,152).png().toBuffer(),left:x+14,top:y+8});
    contact.push({input:await label(name),left:x+5,top:y+170});
    const sx=i%3*360,sy=Math.floor(i/3)*98;
    small.push({input:await label(name),left:sx+3,top:sy+60});
    for(const [j,size] of [28,40,64].entries())
      small.push({input:await sharp(bytes).resize(size,size).png().toBuffer(),left:sx+55+j*84,top:sy+4+(64-size)/2});
  }
  await fs.writeFile(path.join(art,'manifest.json'),JSON.stringify({version:3,statusCount:18,newPaintings:9,tool:'Built-in image_gen',assets},null,2));
  await sharp({create:{width:1080,height:612,channels:4,background:'#12181E'}}).composite(contact).png().toFile(path.join(art,'contact-sheet.png'));
  await sharp({create:{width:1080,height:588,channels:4,background:'#12181E'}}).composite(small).png().toFile(path.join(art,'small-size-check.png'));
  console.log('Validated 18 distinct painted icons, 512x512 with transparent margins; generated 9 new subjects.');
}
main().catch(error=>{console.error(error);process.exitCode=1;});
