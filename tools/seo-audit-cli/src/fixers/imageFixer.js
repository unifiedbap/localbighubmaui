import fs from 'node:fs';
import { findTags, getAttr, hasAttr, escapeHtmlAttr, replaceRange } from '../utils/html.js';

/** Adds an alt attribute to the first <img> matching fixData.src that still lacks one. */
export function applyImgAltFix(finding, value) {
  if (!finding.file) throw new Error('No source file to edit for this finding.');
  const src = finding.fixData?.src;
  if (!src) throw new Error('No image src recorded for this finding.');

  const html = fs.readFileSync(finding.file, 'utf8');
  const imgs = findTags(html, 'img');
  const target = imgs.find((img) => getAttr(img.raw, 'src') === src && !hasAttr(img.raw, 'alt'));
  if (!target) {
    throw new Error(`Could not find an <img src="${src}"> without alt text (file may have changed).`);
  }

  const escaped = escapeHtmlAttr(value);
  const newTag = target.raw.replace(/^<img\b/i, `<img alt="${escaped}"`);
  const next = replaceRange(html, target.start, target.end, newTag);
  fs.writeFileSync(finding.file, next);
}
