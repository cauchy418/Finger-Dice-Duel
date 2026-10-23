const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { sharp, root } = require('./deps.cjs');
async function main() {
  const base = path.join(root, 'Art/Production/profession-subclass-v1');
  const { assets } = JSON.parse(await fs.readFile(path.join(base, 'selected-manifest.json'), 'utf8'));
  // Validate the current roster; the 27-role migration remains historical.
  const identitySource = await fs.readFile(path.join(root,'Assets/Scripts/Core/CharacterIdentity.cs'),'utf8');
  const roles = [...identitySource.matchAll(/new CharacterIdentity\("[^"]+",\s*"([^"]+)"/g)].map(m=>m[1]);
  const expected = new Set(roles.map(id => 'Subclasses/'+id+'.png'));
  const professions = ['barbarian','bard','cleric','druid','fighter','monk','paladin','ranger','rogue','sorcerer','warlock','wizard'];
  for (const id of professions) expected.add(`Professions/${id}.png`);
  if (assets.length !== expected.size) throw new Error('Expected exactly '+expected.size+' selected assets');
  const contentHashes = new Set();
  for (const item of assets) {
    const key = item.runtime.split('/v1/')[1];
    if (!expected.delete(key)) throw new Error(`Unexpected or duplicate identity ${key}`);
    const runtime = await fs.readFile(path.join(root, item.runtime));
    const selected = await fs.readFile(path.join(root, item.source));
    if (!runtime.equals(selected)) throw new Error(`Selected export mismatch ${key}`);
    if (crypto.createHash('sha256').update(runtime).digest('hex') !== item.sha256) throw new Error(`Hash mismatch ${key}`);
    if (contentHashes.has(item.sha256)) throw new Error(`Shared profession/subclass artwork ${key}`);
    contentHashes.add(item.sha256);
    const meta = await sharp(runtime).metadata(); const stats = await sharp(runtime).stats();
    if (meta.width !== 512 || meta.height !== 512 || !meta.hasAlpha || stats.channels[3].min !== 0 || stats.channels[3].max === 0)
      throw new Error(`Invalid dimensions or alpha ${key}`);
  }
  if (expected.size) throw new Error('Missing selected assets');
  console.log('PASS '+assets.length+' canonical profession/subclass PNGs, dimensions, alpha, hashes and selected/runtime consistency.');
}
main().catch(e => { console.error(e); process.exitCode = 1; });
