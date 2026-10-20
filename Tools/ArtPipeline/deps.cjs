const path = require('node:path');
const os = require('node:os');
const fs = require('node:fs');
const bundled = path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
module.exports = {
  sharp: require(process.env.FDD_SHARP_PATH || (fs.existsSync(bundled) ? bundled : 'sharp')),
  root: path.resolve(__dirname, '../..')
};
