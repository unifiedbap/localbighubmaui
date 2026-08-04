import fs from 'node:fs';
import path from 'node:path';

/** Writes (or overwrites) a root file like robots.txt/sitemap.xml at finding.file. */
export function applyRootFileFix(finding, value) {
  if (!finding.file) throw new Error('No target file path recorded for this finding.');
  fs.mkdirSync(path.dirname(finding.file), { recursive: true });
  fs.writeFileSync(finding.file, value);
}
