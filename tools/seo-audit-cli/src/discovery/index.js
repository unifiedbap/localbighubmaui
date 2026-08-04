import { scanDirectory } from './staticScan.js';
import { crawlSite } from './crawl.js';

/**
 * Resolves the target into a unified list of Page objects, regardless of mode:
 *  - dir only: static scan, every page is fixable (has filePath)
 *  - url only: crawl, no page is fixable (no local file to edit)
 *  - both: static scan is the source of truth for content + fixes; the crawl adds
 *    liveUrl (for PageSpeed) plus broken-link / robots / sitemap data. A crawled
 *    page with no matching local file is still reported, just not auto-fixable.
 */
export async function discoverPages(config, { onProgress } = {}) {
  if (config.sourceDir && config.siteUrl) {
    const filePages = await scanDirectory(config.sourceDir);
    const crawlResult = await crawlSite(config.siteUrl, { onProgress });

    const byUrlPath = new Map(filePages.map((p) => [p.urlPath, p]));
    for (const page of filePages) {
      page.liveUrl = new URL(page.urlPath, config.siteUrl).toString();
    }
    for (const cp of crawlResult.pages) {
      if (!byUrlPath.has(cp.urlPath)) {
        filePages.push({ ...cp, filePath: null, liveUrl: cp.url });
      }
    }

    return {
      mode: 'both',
      pages: filePages,
      brokenLinks: crawlResult.brokenLinks,
      sitemap: crawlResult.sitemap,
      robots: crawlResult.robots
    };
  }

  if (config.sourceDir) {
    const pages = await scanDirectory(config.sourceDir);
    return { mode: 'dir', pages, brokenLinks: [], sitemap: null, robots: null };
  }

  const crawlResult = await crawlSite(config.siteUrl, { onProgress });
  return {
    mode: 'url',
    pages: crawlResult.pages.map((p) => ({ ...p, filePath: null, liveUrl: p.url })),
    brokenLinks: crawlResult.brokenLinks,
    sitemap: crawlResult.sitemap,
    robots: crawlResult.robots
  };
}
