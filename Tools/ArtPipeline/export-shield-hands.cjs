const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const {sharp, root} = require('./deps.cjs');

async function bounds(file) {
  const {data, info} = await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let left=info.width, right=0, top=info.height, bottom=0;
  for(let y=0;y<info.height;y++) for(let x=0;x<info.width;x++) if(data[(y*info.width+x)*4+3]>16) {
    left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
  }
  return {left,top,width:right-left+1,height:bottom-top+1};
}
async function main() {
  const directory=path.join(root,'Assets/Resources/Art/Battle/v1/Hands');
  const source=path.join(root,'Art/Production/shield-hand-v1/shield-left.source.png');
  const model=path.join(directory,'hand_left_5.png');
  const original=await bounds(model), crop=await bounds(source), meta=await sharp(model).metadata();
  const hand=await sharp(source).extract(crop).resize({height:original.height}).png().toBuffer();
  const fitted=await sharp(hand).metadata();
  const left=await sharp({create:{width:meta.width,height:meta.height,channels:4,background:'#00000000'}})
    .composite([{input:hand,left:Math.round((meta.width-fitted.width)/2),top:original.top}]).png().toBuffer();
  const outputs=[];
  for(const side of ['left','right']) {
    const data=side==='left'?left:await sharp(left).flop().png().toBuffer();
    const name=`hand_${side}_5_shield.png`;
    await fs.writeFile(path.join(directory,name),data);
    outputs.push({name,width:meta.width,height:meta.height,sha256:crypto.createHash('sha256').update(data).digest('hex')});
  }
  await fs.writeFile(path.join(root,'Art/Production/shield-hand-v1/manifest.json'),JSON.stringify({
    source:'shield-left.source.png',reference:'Assets/Resources/Art/Battle/v1/Hands/hand_left_5.png',
    processing:'Alpha bounds crop, uniform resize to original five height, transparent padding, horizontal mirror for right hand.',
    originalBounds:original,outputs
  },null,2)+'\n');
  console.log(JSON.stringify({outputs}));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
