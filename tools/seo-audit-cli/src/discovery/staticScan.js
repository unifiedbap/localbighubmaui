import fg from 'fast-glob';
import fs from 'node:fs';
import path from 'node:path';

const DEFAULT_IGNORE = [
  '**/node_modules/**',
  '**/.git/**',
  '**/dist/**',
  '**/.next/**',
  '**/build/**',
  '**/.cache/**'
];

/** Finds every .html file under sourceDir and reads it into a Page object. */
export async function scanDirectory(sourceDir) {
  const files = await fg('**/*.html', { cwd: sourceDir, ignore: DEFAULT_IGNORE, dot: false });
  return files
    .sort()
    .map((rel) => {
      const filePath = path.join(sourceDir, rel);
      const html = fs.readFileSync(filePath, 'utf8');
      return { source: 'file', filePath, urlPath: filePathToUrlPath(rel), html };
    });
}

export function filePathToUrlPath(relFilePath) {
  let p = relFilePath.split(path.sep).join('/');
  if (p.endsWith('index.html')) {
    p = p.slice(0, -'index.html'.length);
  }
  if (!p.startsWith('/')) p = '/' + p;
  if (p !== '/' && p.endsWith('/')) p = p.slice(0, -1);
  return p || '/';
}

/**
 * Resolves an <img src> (or similar asset reference) found on `pageFilePath` to a
 * local file on disk, using `sourceDir` as the site root for absolute paths.
 * Returns null for remote URLs, data URIs, or files that don't exist locally.
 */
export function resolveLocalAsset(pageFilePath, sourceDir, src) {
  if (!src) return null;
  if (/^(https?:)?\/\//i.test(src) || src.startsWith('data:')) return null;
  const cleanSrc = src.split('?')[0].split('#')[0];
  if (!cleanSrc) return null;

  const assetPath = cleanSrc.startsWith('/')
    ? path.join(sourceDir, cleanSrc)
    : path.resolve(path.dirname(pageFilePath), cleanSrc);

  try {
    return fs.statSync(assetPath).isFile() ? assetPath : null;
  } catch {
    return null;
  }
}
