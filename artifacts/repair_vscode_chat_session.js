const fs = require('node:fs');
const path = require('node:path');
const { DatabaseSync } = require('node:sqlite');

const dbPath = process.argv[2];
const sessionId = process.argv[3];

if (!dbPath || !sessionId) {
  console.error('Usage: node --experimental-sqlite repair_vscode_chat_session.js <dbPath> <sessionId>');
  process.exit(1);
}

const containsSessionId = (value) => {
  if (typeof value === 'string') {
    return value.includes(sessionId);
  }
  if (Array.isArray(value)) {
    return value.some(containsSessionId);
  }
  if (value && typeof value === 'object') {
    return Object.values(value).some(containsSessionId);
  }
  return false;
};

const pruneSessionReferences = (value) => {
  if (Array.isArray(value)) {
    return value
      .filter((item) => !containsSessionId(item))
      .map(pruneSessionReferences);
  }

  if (value && typeof value === 'object') {
    const result = {};
    for (const [key, child] of Object.entries(value)) {
      if (!containsSessionId(child)) {
        result[key] = pruneSessionReferences(child);
      }
    }
    return result;
  }

  return value;
};

const db = new DatabaseSync(dbPath);
const updatedKeys = [];

const updateValue = (key, nextValue) => {
  db.prepare('UPDATE ItemTable SET value = ? WHERE key = ?').run(nextValue, key);
  updatedKeys.push(key);
};

const deleteKey = (key) => {
  db.prepare('DELETE FROM ItemTable WHERE key = ?').run(key);
  updatedKeys.push(`${key} (deleted)`);
};

const indexRow = db.prepare('SELECT value FROM ItemTable WHERE key = ?').get('chat.ChatSessionStore.index');
if (indexRow) {
  const parsed = JSON.parse(indexRow.value);
  if (parsed.entries && parsed.entries[sessionId]) {
    delete parsed.entries[sessionId];
    updateValue('chat.ChatSessionStore.index', JSON.stringify(parsed));
  }
}

const rows = db.prepare("SELECT key, value FROM ItemTable WHERE value LIKE '%' || ? || '%' OR key LIKE '%' || ? || '%' ORDER BY key").all(sessionId, sessionId);
for (const row of rows) {
  if (row.key === 'chat.ChatSessionStore.index') {
    continue;
  }

  if (row.key.includes(sessionId)) {
    deleteKey(row.key);
    continue;
  }

  try {
    const parsed = JSON.parse(row.value);
    const pruned = pruneSessionReferences(parsed);
    if (JSON.stringify(pruned) !== JSON.stringify(parsed)) {
      updateValue(row.key, JSON.stringify(pruned));
    }
  } catch {
    if (typeof row.value === 'string' && row.value.includes(sessionId)) {
      deleteKey(row.key);
    }
  }
}

console.log(JSON.stringify({ sessionId, updatedKeys }, null, 2));

const workspaceRoot = path.dirname(dbPath);
const editingPath = path.join(workspaceRoot, 'chatEditingSessions', sessionId);
if (fs.existsSync(editingPath)) {
  const backupPath = `${editingPath}.bak`;
  if (!fs.existsSync(backupPath)) {
    fs.renameSync(editingPath, backupPath);
    console.log(`renamed:${editingPath} -> ${backupPath}`);
  } else {
    fs.rmSync(editingPath, { recursive: true, force: true });
    console.log(`deleted:${editingPath}`);
  }
}
