const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { sharp, root } = require('./deps.cjs');

// Only crop transparent margins, scale, and center the generated paintings.
// Preserve source PNGs and exact prompts; Unity alone owns .meta files.
async function prepare() {
  const folder = 'Art/Production/arcane-five-v1';
  const manifest = JSON.parse(await fs.readFile(path.join(root, folder, 'generation-manifest.json'), 'utf8'));
  const assets = [];
  for (const item of manifest.assets) {
    const source = `${folder}/sources/${item.id}.png`;
    const sourcePath = path.join(root, source);
    await fs.mkdir(path.dirname(sourcePath), { recursive: true });
    // Re-running on a clone uses archived sources instead of a machine-local path.
    try { await fs.access(sourcePath); }
    catch { await fs.copyFile(item.native, sourcePath); }
    const { data, info } = await sharp(sourcePath).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    let x0 = info.width, y0 = info.height, x1 = -1, y1 = -1;
    for (let y = 0; y < info.height; y++) for (let x = 0; x < info.width; x++) {
      if (data[(y * info.width + x) * 4 + 3] <= 8) continue;
      x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
    }
    if (x1 < 0) throw Error(`Empty asset: ${item.id}`);
    const crop = { left: x0, top: y0, width: x1 - x0 + 1, height: y1 - y0 + 1 };
    const side = item.status ? 448 : 480;
    const resized = await sharp(sourcePath).extract(crop)
      .resize(side, side, { fit: 'inside' }).png().toBuffer();
    const png = await sharp(resized)
      .resize(512, 512, { fit: 'contain', withoutEnlargement: true, background: '#00000000' }).png().toBuffer();
    const kind = item.status ? 'Status' : item.unit ? 'Units' : 'Subclasses';
    const selected = `${folder}/selected/${kind}/${item.id}.png`;
    await fs.mkdir(path.dirname(path.join(root, selected)), { recursive: true });
    await fs.writeFile(path.join(root, selected), png);
    assets.push({ id: item.id, source, selected, kind, sha256: crypto.createHash('sha256').update(png).digest('hex'), crop });
  }
  await fs.writeFile(path.join(root, folder, 'selected-manifest.json'), JSON.stringify({ mode: manifest.mode, assets }, null, 2) + '\n');
  console.log(`Archived and prepared ${assets.length} transparent 512px paintings; no runtime registration changed.`);
}
prepare().catch(e => { console.error(e); process.exitCode = 1; });
