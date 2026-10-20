const {sharp}=require('./deps.cjs');
async function findBounds(file,cols,rows,count){
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const {width:w,height:h}=info,n=w*h,seen=new Uint8Array(n),queue=new Int32Array(n),boxes=[];
 const minArea=w*h/(cols*rows)*.15;
 for(let i=0;i<n;i++)if(!seen[i]&&data[i*4+3]>=96){
   let head=0,tail=1;queue[0]=i;seen[i]=1;let x0=w,y0=h,x1=0,y1=0;
   while(head<tail){const j=queue[head++],x=j%w,y=Math.floor(j/w);x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);
     for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
       const xx=x+dx,yy=y+dy,k=yy*w+xx;
       if(xx>=0&&xx<w&&yy>=0&&yy<h&&!seen[k]&&data[k*4+3]>=96){seen[k]=1;queue[tail++]=k;}
     }
   }
   if(tail>minArea)boxes.push({left:x0,top:y0,width:x1-x0+1,height:y1-y0+1,area:tail});
 }
 // Narrow gold diamond tips can touch between rows. Split only obvious multi-tile components.
 const widths=boxes.map(b=>b.width).sort((a,b)=>a-b),heights=boxes.map(b=>b.height).sort((a,b)=>a-b);
 const mw=Math.min(widths[Math.floor(widths.length/2)],w/cols),mh=Math.min(heights[Math.floor(heights.length/2)],h/rows),split=[];
 for(const b of boxes){const nx=b.width>mw*1.6?Math.round(b.width/mw):1,ny=b.height>mh*1.6?Math.round(b.height/mh):1;
   for(let y=0;y<ny;y++)for(let x=0;x<nx;x++){
     const l=Math.round(b.left+x*b.width/nx),t=Math.round(b.top+y*b.height/ny);
     split.push({left:l,top:t,width:Math.round(b.left+(x+1)*b.width/nx)-l,height:Math.round(b.top+(y+1)*b.height/ny)-t});
   }
 }
 if(split.length!==count)throw new Error(`Expected ${count} framed icons, got ${split.length}: ${JSON.stringify(split)}`);
 split.sort((a,b)=>(a.top+a.height/2)-(b.top+b.height/2));
 const ordered=[];
 for(let i=0;i<count;i+=cols)ordered.push(...split.slice(i,i+cols).sort((a,b)=>a.left-b.left));
 return {width:w,height:h,boxes:ordered.map(({left,top,width,height})=>({left:Math.max(0,left-3),top:Math.max(0,top-3),width:Math.min(w, left+width+3)-Math.max(0,left-3),height:Math.min(h,top+height+3)-Math.max(0,top-3)}))};
}
module.exports={findBounds};
if(require.main===module)findBounds(process.argv[2],+process.argv[3],+process.argv[4],+process.argv[5]).then(r=>console.log(JSON.stringify(r))).catch(e=>{console.error(e);process.exitCode=1;});
