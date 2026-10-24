const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { sharp, root } = require('./deps.cjs');

// Selected exports are retained alongside their original generated sources.
// Reimporting the older painted atlas must not restore the old pathOfTheWarden badge.
async function importNewRoleIcons() {
  const base = path.join(root, 'Art/Production/new-roles-v1');
  let manifest;
  try { manifest = JSON.parse(await fs.readFile(path.join(base, 'manifest.json'), 'utf8')); }
  catch (error) { if (error.code === 'ENOENT') return; throw error; }
  const entries = manifest.assets.map(a => ({ ...a, source: a.candidate }));
  entries.push(manifest.reused);
  const prepared = [];
  for (const item of entries) {
    const bytes = await fs.readFile(path.join(root, item.source));
    const meta = await sharp(bytes).metadata();
    if (meta.width !== 512 || meta.height !== 512 || !meta.hasAlpha)
      throw new Error(`Invalid class emblem: ${item.id}`);
    const stats = await sharp(bytes).stats();
    if (stats.channels[3].max === 0 || stats.channels[3].min !== 0)
      throw new Error(`Invalid emblem alpha: ${item.id}`);
    const hash = crypto.createHash('sha256').update(bytes).digest('hex');
    if (item.sha256 && hash !== item.sha256) throw new Error(`Emblem hash mismatch: ${item.id}`);
    prepared.push({ item, bytes });
  }
  for (const { item, bytes } of prepared) await fs.writeFile(path.join(root, item.runtime), bytes);
  console.log(`Imported ${prepared.length} selected class emblems; engine .meta files untouched.`);
  await require('./import-selected-profession-icons.cjs').importSelectedProfessionIcons();
  await require('./import-wizard-school-icons.cjs').importWizardSchoolIcons();
  await require('./import-cleric-martial-icons.cjs').importClericMartialIcons();
  await require('./import-four-role-emblems.cjs').importFourRoleEmblems();
  await require('./import-martial-resource-icons.cjs').importMartialResourceIcons();
  await require('./import-druid-art.cjs').importDruidArt();
  await require('./import-ancient-role-art.cjs').importAncientRoleArt();
  await require('./import-lore-storm-illusion.cjs').importLoreStormIllusion();
}
module.exports = { importNewRoleIcons };
if (require.main === module) importNewRoleIcons().catch(error => { console.error(error); process.exitCode = 1; });
