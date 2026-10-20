// Slicing, alpha-cutout cleanup, sizing and mirroring authorized by the user.
const fs=require('node:fs/promises');
const path=require('node:path');
const {sharp,root}=require('./deps.cjs');
const base=path.join(root,'Art/Production/battle-v1');
const out=path.join(root,'Assets/Resources/Art/Battle/v1/Hands');
async function clean(input){
  const {data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const {width:w,height:h}=info, n=w*h;
  const valid=new Uint8Array(n), seen=new Uint8Array(n), queue=new Int32Array(n);
  for(let i=0;i<n;i++){
    const k=i*4,r=data[k],g=data[k+1],b=data[k+2];
    const maskColor=(r>200&&g<80&&b<65)||(r>200&&g>185&&b<65);
    valid[i]=data[k+3]>=140&&!maskColor?1:0;
  }
  let largest=[];
  for(let i=0;i<n;i++)if(valid[i]&&!seen[i]){
    let head=0,tail=1;queue[0]=i;seen[i]=1;
    while(head<tail){const j=queue[head++],x=j%w,y=Math.floor(j/w);
      for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
        const xx=x+dx,yy=y+dy,k=yy*w+xx;
        if(xx>=0&&xx<w&&yy>=0&&yy<h&&valid[k]&&!seen[k]){seen[k]=1;queue[tail++]=k;}
      }
    }
    if(tail>largest.length)largest=Array.from(queue.subarray(0,tail));
  }
  if(largest.length<500)throw new Error(`No valid hand component in ${input}`);
  const keep=new Uint8Array(n);
  for(const i of largest){const x=i%w,y=Math.floor(i/w);
    for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++){
      const xx=x+dx,yy=y+dy;if(xx>=0&&xx<w&&yy>=0&&yy<h)keep[yy*w+xx]=1;
    }
  }
  let x0=w,y0=h,x1=0,y1=0;
  for(let i=0;i<n;i++){
    const k=i*4,r=data[k],g=data[k+1],b=data[k+2];
    if(!keep[i]||(r>200&&g<80&&b<65)||(r>200&&g>185&&b<65)){data[k+3]=0;continue;}
    if(data[k+3]>16){const x=i%w,y=Math.floor(i/w);x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
  }
  const bh=y1-y0+1;
  const spans=[];
  for(let y=y1-Math.round(bh*.08);y<=y1-Math.round(bh*.035);y++){
    let lo=w,hi=-1;for(let x=x0;x<=x1;x++)if(data[(y*w+x)*4+3]>=140){lo=Math.min(lo,x);hi=x;}
    if(hi>=lo)spans.push(hi-lo+1);
  }
  spans.sort((a,b)=>a-b);const cuff=spans[Math.floor(spans.length/2)];
  const factor=196/cuff;
  const width=Math.round((x1-x0+1)*factor),height=Math.round(bh*factor);
  if(width>480||height>590)throw new Error(`Hand does not fit common canvas: ${input} ${width}x${height}`);
  const png=await sharp(data,{raw:{width:w,height:h,channels:4}}).extract({left:x0,top:y0,width:x1-x0+1,height:bh}).resize(width,height).png().toBuffer();
  return {png,width,height,cuff,sourceBounds:[x0,y0,x1,y1]};
}
async function main(){
  await fs.mkdir(out,{recursive:true});
  const manifest=[];
  for(let value=0;value<10;value++){
    const repaired=(value===4||value===6);
    const input=path.join(base,repaired?`hand-${value}-repaired.png`:`raw-hands/left-${value}.png`);
    const s=await clean(input);
    // Source cells 0 and 9 were drawn as right hands; canonicalize before export.
    const sourceNeedsMirror=(value===0||value===9);
    const canonical=sourceNeedsMirror?await sharp(s.png).flop().png().toBuffer():s.png;
    const left=await sharp({create:{width:512,height:640,channels:4,background:'#00000000'}}).composite([{input:canonical,left:Math.round((512-s.width)/2),top:610-s.height}]).png().toBuffer();
    await fs.writeFile(path.join(out,`hand_left_${value}.png`),left);
    await sharp(left).flop().png().toFile(path.join(out,`hand_right_${value}.png`));
    manifest.push({value,width:512,height:640,wristBaseline:610,nominalCuffWidth:196,source:path.relative(root,input).replaceAll('\\','/'),repaired,sourceNeedsMirror,left:`Hands/hand_left_${value}.png`,right:`Hands/hand_right_${value}.png`,sourceBounds:s.sourceBounds});
  }
  await fs.writeFile(path.join(base,'hand-manifest.json'),JSON.stringify({version:1,processing:'Grid cutouts, background speck isolation, common cuff scale and baseline; right sprites are horizontal mirrors of left sprites.',hands:manifest},null,2));
  console.log('Exported 20 transparent 512x640 hand PNGs; corrected 4 and 6 replace rejected source cells.');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
