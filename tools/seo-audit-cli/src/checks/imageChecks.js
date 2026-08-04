import path from 'node:path';
import fs from 'node:fs';
import { makeFinding } from '../model/finding.js';
import { getImgTags, getAttr, hasAttr } from '../utils/html.js';
import { resolveLocalAsset } from '../discovery/staticScan.js';
import { fetchText } from '../discovery/crawl.js';

function altFromFilename(src) {
  const base = path.basename(src.split('?')[0].split('#')[0], path.extname(src));
  const words = base.replace(/[-_]+/g, ' ').replace(/\s+/g, ' ').trim();
  if (!words) return 'Descriptive alt text needed';
  return words.replace(/\b\w/g, (c) => c.toUpperCase());
}

async function getRemoteContentLength(url) {
  try {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 10000);
    const res = await fetch(url, { method: 'HEAD', signal: controller.signal, redirect: 'follow' });
    clearTimeout(timer);
    const len = res.headers.get('content-length');
    return len ? Number(len) : null;
  } catch {
    return null;
  }
}

export async function runImageChecksForPage(page, config) {
  const findings = [];
  const imgs = getImgTags(page.html);
  const thresholdBytes = config.thresholds.oversizedImageKb * 1024;

  for (const img of imgs) {
    const src = getAttr(img.raw, 'src');
    if (!src) continue;

    if (!hasAttr(img.raw, 'alt')) {
      findings.push(
        makeFinding({
          category: 'images',
          check: 'missing-alt-text',
          severity: 'Medium',
          effort: 'Quick fix',
          page: page.urlPath,
          file: page.filePath,
          description: `<img src="${src}"> has no alt attribute.`,
          suggestedFix: altFromFilename(src),
          fixType: page.filePath ? 'auto' : 'manual-code',
          fixData: { tag: 'img-alt', src }
        })
      );
    }

    let sizeBytes = null;
    if (page.filePath) {
      const assetPath = resolveLocalAsset(page.filePath, config.sourceDir, src);
      if (assetPath) {
        try {
          sizeBytes = fs.statSync(assetPath).size;
        } catch {
          sizeBytes = null;
        }
      }
    } else if (page.liveUrl) {
      let absUrl = null;
      try {
        absUrl = new URL(src, page.liveUrl).toString();
      } catch {
        absUrl = null;
      }
      if (absUrl) sizeBytes = await getRemoteContentLength(absUrl);
    }

    if (sizeBytes != null && sizeBytes > thresholdBytes) {
      const sizeKb = Math.round(sizeBytes / 1024);
      findings.push(
        makeFinding({
          category: 'images',
          check: 'oversized-image',
          severity: 'Medium',
          effort: 'Moderate',
          page: page.urlPath,
          file: page.filePath,
          description: `Image "${src}" is ${sizeKb}KB (threshold: ${config.thresholds.oversizedImageKb}KB), which can slow page load.`,
          suggestedFix: `Re-export "${src}" as compressed WebP/JPEG under ${config.thresholds.oversizedImageKb}KB, or serve a responsive/lazy-loaded version. Not auto-applied — recompression changes visual quality and should be reviewed by eye before committing.`,
          fixType: 'manual-code',
          fixData: { tag: 'img-size', src, sizeBytes }
        })
      );
    }
  }

  return findings;
}
