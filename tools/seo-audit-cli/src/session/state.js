import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import os from 'node:os';

/** Where progress for a given target is persisted, unless overridden with --state-file. */
export function defaultStateFilePath(config) {
  if (config.sourceDir) {
    return path.join(config.sourceDir, '.seo-audit', 'state.json');
  }
  const key = config.siteUrl;
  const hash = crypto.createHash('sha1').update(key).digest('hex').slice(0, 10);
  return path.join(os.homedir(), '.seo-audit-cli', 'state', `${hash}.json`);
}

export function loadState(stateFilePath) {
  if (!fs.existsSync(stateFilePath)) return null;
  try {
    return JSON.parse(fs.readFileSync(stateFilePath, 'utf8'));
  } catch {
    return null;
  }
}

export function saveState(stateFilePath, state) {
  fs.mkdirSync(path.dirname(stateFilePath), { recursive: true });
  fs.writeFileSync(stateFilePath, JSON.stringify(state, null, 2));
}

/**
 * Carries decisions (approved/skipped/fixed/edited) forward from a previous run
 * onto a freshly re-scanned finding list, matched by stable finding id. A finding
 * that's no longer detected (because it was fixed, or the page changed) simply
 * won't appear in `newFindings` — nothing special to do, it's just gone.
 */
export function mergeWithPreviousState(newFindings, previousState) {
  if (!previousState) return newFindings;
  const prevById = new Map(previousState.findings.map((f) => [f.id, f]));
  return newFindings.map((f) => {
    const prev = prevById.get(f.id);
    if (!prev) return f;
    return { ...f, status: prev.status, appliedValue: prev.appliedValue, error: prev.error };
  });
}
