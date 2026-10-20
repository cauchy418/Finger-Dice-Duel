// User authorized grid slicing, resizing and left/right mirroring on 2026-10-20.
const fs = require('node:fs/promises');
const path = require('node:path');
const {sharp, root} = require('./deps.cjs');
async function main() {
  const source = process.argv[2];
  if (!source) throw new Error('Usage: node prepare-hands.cjs <generated-sheet.png>');
  const out = path.join(root, 'Art/Production/battle-v1/raw-hands');
  await fs.mkdir(out, {recursive:true});
  await fs.copyFile(source, path.join(root, 'Art/Production/battle-v1/hands-source-v1.png'));
  const meta = await sharp(source).metadata();
  if (!meta.hasAlpha) throw new Error('Source sheet must have alpha transparency.');
  for(let value=0; value<10; value++) {
    const c=value%5, r=Math.floor(value/5);
    const left=Math.round(c*meta.width/5), top=Math.round(r*meta.height/2);
    const width=Math.round((c+1)*meta.width/5)-left;
    const height=Math.round((r+1)*meta.height/2)-top;
    await sharp(source).extract({left,top,width,height}).png().toFile(path.join(out, `left-${value}.png`));
  }
  console.log(JSON.stringify({width:meta.width,height:meta.height,hasAlpha:meta.hasAlpha,rawCrops:10}));
}
main().catch(e=>{console.error(e); process.exitCode=1;});
