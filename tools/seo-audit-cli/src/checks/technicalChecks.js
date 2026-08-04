import fs from 'node:fs';
import path from 'node:path';
import { makeFinding } from '../model/finding.js';
import { getCanonical, getViewport } from '../utils/html.js';

export function runTechnicalChecksForPage(page, config) {
  const findings = [];
  const html = page.html;

  const canonical = getCanonical(html);
  if (canonical.length === 0) {
    const canAuto = Boolean(config.siteUrl && page.filePath);
    const suggested = config.siteUrl ? new URL(page.urlPath, config.siteUrl).toString() : null;
    findings.push(
      makeFinding({
        category: 'technical',
        check: 'missing-canonical',
        severity: 'Medium',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: 'Page has no canonical <link> tag.',
        suggestedFix: suggested,
        fixType: canAuto ? 'auto' : 'manual-code',
        fixData: { tag: 'canonical', mode: 'insert-head' }
      })
    );
  }

  const viewport = getViewport(html);
  if (viewport.length === 0) {
    findings.push(
      makeFinding({
        category: 'technical',
        check: 'missing-viewport',
        severity: 'High',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: 'Page has no mobile viewport meta tag.',
        suggestedFix: 'width=device-width, initial-scale=1',
        fixType: page.filePath ? 'auto' : 'manual-code',
        fixData: { tag: 'viewport', mode: 'insert-head' }
      })
    );
  }

  return findings;
}

function findLocalRootFile(config, filename) {
  const candidates = [
    path.join(config.sourceDir, filename),
    path.join(config.sourceDir, config.publicDir || 'public', filename)
  ];
  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) return candidate;
  }
  return null;
}

function defaultRootTarget(config, filename) {
  // Where a new robots.txt/sitemap.xml should be written when none exists yet.
  const publicDir = path.join(config.sourceDir, config.publicDir || 'public');
  const base = fs.existsSync(publicDir) ? publicDir : config.sourceDir;
  return path.join(base, filename);
}

function buildRobotsTxt(config) {
  const lines = ['User-agent: *', 'Allow: /'];
  if (config.siteUrl) {
    lines.push('', `Sitemap: ${new URL('/sitemap.xml', config.siteUrl).toString()}`);
  }
  return lines.join('\n') + '\n';
}

function buildSitemapXml(config, pages) {
  const urls = pages.map((p) => {
    const loc = config.siteUrl ? new URL(p.urlPath, config.siteUrl).toString() : p.urlPath;
    return `  <url>\n    <loc>${loc}</loc>\n  </url>`;
  });
  return (
    '<?xml version="1.0" encoding="UTF-8"?>\n' +
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' +
    urls.join('\n') +
    '\n</urlset>\n'
  );
}

/** Site-wide checks: robots.txt, sitemap.xml, and (crawl mode) broken internal links. */
export function runSiteLevelTechnicalChecks(discoveryResult, config) {
  const findings = [];
  const { pages, brokenLinks, sitemap, robots } = discoveryResult;

  // --- robots.txt ---
  const localRobotsPath = config.sourceDir ? findLocalRootFile(config, 'robots.txt') : null;
  const robotsContent = localRobotsPath
    ? fs.readFileSync(localRobotsPath, 'utf8')
    : robots?.raw ?? null;
  const robotsMissing = config.sourceDir ? !localRobotsPath : !robots || !robots.raw;
  const robotsMalformed = robotsContent != null && !/user-agent\s*:/i.test(robotsContent);

  if (config.sourceDir) {
    if (robotsMissing) {
      findings.push(
        makeFinding({
          category: 'technical',
          check: 'missing-robots-txt',
          severity: 'High',
          effort: 'Quick fix',
          page: '/robots.txt',
          file: defaultRootTarget(config, 'robots.txt'),
          description: 'Site has no robots.txt.',
          suggestedFix: buildRobotsTxt(config),
          fixType: 'auto',
          fixData: { tag: 'robots-txt', mode: 'write-file' }
        })
      );
    } else if (robotsMalformed) {
      findings.push(
        makeFinding({
          category: 'technical',
          check: 'malformed-robots-txt',
          severity: 'High',
          effort: 'Quick fix',
          page: '/robots.txt',
          file: localRobotsPath,
          description: `robots.txt exists but has no "User-agent:" directive: ${localRobotsPath}`,
          suggestedFix: buildRobotsTxt(config),
          fixType: 'auto',
          fixData: { tag: 'robots-txt', mode: 'write-file' }
        })
      );
    }
  } else {
    if (robotsMissing) {
      findings.push(
        makeFinding({
          category: 'technical',
          check: 'missing-robots-txt',
          severity: 'High',
          effort: 'Quick fix',
          page: '/robots.txt',
          file: null,
          description: `No robots.txt found at ${robots?.url ?? '/robots.txt'}.`,
          suggestedFix: buildRobotsTxt(config),
          fixType: 'manual-code',
          fixData: { tag: 'robots-txt', mode: 'manual-review' }
        })
      );
    } else if (robotsMalformed) {
      findings.push(
        makeFinding({
          category: 'technical',
          check: 'malformed-robots-txt',
          severity: 'High',
          effort: 'Quick fix',
          page: '/robots.txt',
          file: null,
          description: `robots.txt at ${robots.url} has no "User-agent:" directive.`,
          suggestedFix: buildRobotsTxt(config),
          fixType: 'manual-code',
          fixData: { tag: 'robots-txt', mode: 'manual-review' }
        })
      );
    }
  }

  // --- sitemap.xml ---
  const localSitemapPath = config.sourceDir ? findLocalRootFile(config, 'sitemap.xml') : null;
  const sitemapContent = localSitemapPath
    ? fs.readFileSync(localSitemapPath, 'utf8')
    : sitemap?.raw ?? null;
  const sitemapMissing = !localSitemapPath && (!sitemap || !sitemap.fetched);
  const sitemapMalformed = sitemapContent != null && !/<urlset[\s>]/i.test(sitemapContent);

  if (config.sourceDir) {
    if (!localSitemapPath) {
      findings.push(
        makeFinding({
          category: 'technical',
          check: 'missing-sitemap-xml',
          severity: 'High',
          effort: 'Moderate',
          page: '/sitemap.xml',
          file: defaultRootTarget(config, 'sitemap.xml'),
          description: 'Site has no sitemap.xml.',
          suggestedFix: buildSitemapXml(config, pages),
          fixType: 'auto',
          fixData: { tag: 'sitemap-xml', mode: 'write-file' }
        })
      );
    } else if (sitemapMalformed) {
      findings.push(
        makeFinding({
          category: 'technical',
          check: 'malformed-sitemap-xml',
          severity: 'High',
          effort: 'Moderate',
          page: '/sitemap.xml',
          file: localSitemapPath,
          description: `sitemap.xml exists but has no <urlset> element: ${localSitemapPath}`,
          suggestedFix: buildSitemapXml(config, pages),
          fixType: 'auto',
          fixData: { tag: 'sitemap-xml', mode: 'write-file' }
        })
      );
    }
  } else if (sitemapMissing) {
    findings.push(
      makeFinding({
        category: 'technical',
        check: 'missing-sitemap-xml',
        severity: 'High',
        effort: 'Moderate',
        page: '/sitemap.xml',
        file: null,
        description: `No sitemap.xml found at ${sitemap?.url ?? '/sitemap.xml'}.`,
        suggestedFix: buildSitemapXml(config, pages),
        fixType: 'manual-code',
        fixData: { tag: 'sitemap-xml', mode: 'manual-review' }
      })
    );
  } else if (sitemapMalformed) {
    findings.push(
      makeFinding({
        category: 'technical',
        check: 'malformed-sitemap-xml',
        severity: 'High',
        effort: 'Moderate',
        page: '/sitemap.xml',
        file: null,
        description: `sitemap.xml at ${sitemap.url} has no <urlset> element.`,
        suggestedFix: buildSitemapXml(config, pages),
        fixType: 'manual-code',
        fixData: { tag: 'sitemap-xml', mode: 'manual-review' }
      })
    );
  }

  // --- broken internal links (crawl mode only) ---
  const pagesByUrlPath = new Map(pages.map((p) => [p.urlPath, p]));
  for (const broken of brokenLinks || []) {
    let referrerPage = null;
    if (broken.referrer) {
      try {
        referrerPage = pagesByUrlPath.get(new URL(broken.referrer).pathname) || null;
      } catch {
        referrerPage = null;
      }
    }
    findings.push(
      makeFinding({
        category: 'technical',
        check: 'broken-internal-link',
        severity: 'High',
        effort: 'Involved',
        page: referrerPage ? referrerPage.urlPath : broken.referrer || broken.url,
        file: referrerPage?.filePath ?? null,
        description: `Link to "${broken.url}" returns ${broken.status || 'a network error'}${
          broken.referrer ? ` (found on ${broken.referrer})` : ''
        }.`,
        suggestedFix: null,
        fixType: 'manual-code',
        fixData: { tag: 'broken-link', href: broken.url }
      })
    );
  }

  return findings;
}
