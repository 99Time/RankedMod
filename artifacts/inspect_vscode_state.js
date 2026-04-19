const { DatabaseSync } = require('node:sqlite');

const dbPath = process.argv[2];
const mode = process.argv[3] || 'keys';

if (!dbPath) {
  console.error('Usage: node --experimental-sqlite inspect_vscode_state.js <dbPath> [schema|keys|key <name>]');
  process.exit(1);
}

const db = new DatabaseSync(dbPath, { readonly: true });

if (mode === 'schema') {
  const rows = db.prepare("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name").all();
  console.log(JSON.stringify(rows, null, 2));
} else if (mode === 'keys') {
  const rows = db.prepare("SELECT key, length(value) as len FROM ItemTable WHERE key LIKE '%copilot%' OR key LIKE '%chat%' ORDER BY len DESC, key LIMIT 500").all();
  console.log(JSON.stringify(rows, null, 2));
} else if (mode === 'key') {
  const key = process.argv[4];
  const row = db.prepare('SELECT key, value, length(value) as len FROM ItemTable WHERE key = ?').get(key);
  console.log(JSON.stringify(row, null, 2));
} else if (mode === 'search') {
  const text = process.argv[4];
  const rows = db.prepare("SELECT key, length(value) as len FROM ItemTable WHERE key LIKE '%' || ? || '%' OR value LIKE '%' || ? || '%' ORDER BY len DESC, key").all(text, text);
  console.log(JSON.stringify(rows, null, 2));
} else if (mode === 'top') {
  const rows = db.prepare('SELECT key, length(value) as len FROM ItemTable ORDER BY len DESC LIMIT 50').all();
  console.log(JSON.stringify(rows, null, 2));
} else {
  console.error(`Unknown mode: ${mode}`);
  process.exit(1);
}
