const fs=require('node:fs/promises'),path=require('node:path');
const {root}=require('./deps.cjs');
async function main(){
  const base='Art/Production/arcane-five-v1';
  const manifest=JSON.parse(await fs.readFile(path.join(root,base,'selected-manifest.json'),'utf8'));
  const canonicalFile=path.join(root,'Art/Production/profession-subclass-v1/selected-manifest.json');
  const canonical=JSON.parse(await fs.readFile(canonicalFile,'utf8'));
  for(const a of manifest.assets){
    a.runtime=`Assets/Resources/Art/Battle/v1/${a.kind}/${a.id}.png`;
    await fs.mkdir(path.dirname(path.join(root,a.runtime)),{recursive:true});
    await fs.copyFile(path.join(root,a.selected),path.join(root,a.runtime));
    if(a.kind==='Subclasses'){
      const source=`Art/Production/profession-subclass-v1/selected/Subclasses/${a.id}.png`;
      await fs.copyFile(path.join(root,a.selected),path.join(root,source));
      canonical.assets=canonical.assets.filter(x=>x.runtime!==a.runtime);
      canonical.assets.push({id:a.id,source,runtime:a.runtime,sha256:a.sha256});
    }
  }
  await fs.writeFile(canonicalFile,JSON.stringify(canonical,null,2)+'\n');
  await fs.writeFile(path.join(root,base,'selected-manifest.json'),JSON.stringify(manifest,null,2)+'\n');
  console.log(`Imported ${manifest.assets.length} PNGs; canonical subclasses and professions: ${canonical.assets.length}.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
