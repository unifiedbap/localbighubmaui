import path from 'node:path';
import crypto from 'node:crypto';
import os from 'node:os';

/** Mirrors defaultStateFilePath's location choice so state + reports live together. */
export function defaultReportDir(config) {
  if (config.sourceDir) {
    return path.join(config.sourceDir, '.seo-audit', 'reports');
  }
  const hash = crypto.createHash('sha1').update(config.siteUrl).digest('hex').slice(0, 10);
  return path.join(os.homedir(), '.seo-audit-cli', 'reports', hash);
}
