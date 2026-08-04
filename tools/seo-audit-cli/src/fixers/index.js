import {
  applyTitleFix,
  applyMetaDescriptionFix,
  applyH1Fix,
  applyCanonicalFix,
  applyViewportFix
} from './textFixers.js';
import { applyJsonLdFix } from './schemaFixer.js';
import { applyImgAltFix } from './imageFixer.js';
import { applyRootFileFix } from './rootFileFixer.js';

const FIXERS = {
  title: applyTitleFix,
  'meta-description': applyMetaDescriptionFix,
  h1: applyH1Fix,
  canonical: applyCanonicalFix,
  viewport: applyViewportFix,
  'json-ld': applyJsonLdFix,
  'img-alt': applyImgAltFix,
  'robots-txt': applyRootFileFix,
  'sitemap-xml': applyRootFileFix
};

/** Applies an approved (or edited) fix directly to the finding's source file. */
export function applyFix(finding, value) {
  const tag = finding.fixData?.tag;
  const fixer = FIXERS[tag];
  if (!fixer) {
    throw new Error(`No automatic fixer available for "${tag}". This finding requires a manual edit.`);
  }
  fixer(finding, value);
}

export function isAutoFixable(finding) {
  return finding.fixType === 'auto' && Boolean(FIXERS[finding.fixData?.tag]);
}
