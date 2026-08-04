import * as cheerio from 'cheerio';

const DEFAULT_MAX_PAGES = 200;
const FETCH_TIMEOUT_MS = 15000;
const NON_PAGE_EXT = /\.(pdf|jpe?g|png|gif|svg|webp|ico|zip|mp4|css|js|json|xml|txt|woff2?|ttf)$/i;

export async function fetchText(url, { timeoutMs = FETCH_TIMEOUT_MS, method = 'GET' } = {}) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(url, { signal: controller.signal, redirect: 'follow', method });
    const text = method === 'HEAD' ? '' : await res.text().catch(() => '');
    return { ok: res.ok, status: res.status, text, url: res.url || url };
  } catch (err) {
    return { ok: false, status: 0, text: '', error: err.message, url };
  } finally {
    clearTimeout(timer);
  }
}

export async function checkLinkStatus(url) {
  let result = await fetchText(url, { method: 'HEAD' });
  if (!result.ok && (result.status === 405 || result.status === 501 || result.status === 0)) {
    result = await fetchText(url, { method: 'GET' });
  }
  return { ok: result.ok, status: result.status, error: result.error };
}

export async function discoverSitemapUrls(siteUrl) {
  const sitemapUrl = new URL('/sitemap.xml', siteUrl).toString();
  const res = await fetchText(sitemapUrl);
  if (!res.ok) return { urls: [], raw: null, url: sitemapUrl, fetched: false, status: res.status };
  const locs = [...res.text.matchAll(/<loc>\s*([^<\s]+)\s*<\/loc>/gi)].map((m) => m[1]);
  return { urls: locs, raw: res.text, url: sitemapUrl, fetched: true, status: res.status };
}

export async function fetchRobotsTxt(siteUrl) {
  const robotsUrl = new URL('/robots.txt', siteUrl).toString();
  const res = await fetchText(robotsUrl);
  return { raw: res.ok ? res.text : null, status: res.status, url: robotsUrl };
}

function normalizeUrl(u) {
  const parsed = new URL(u);
  parsed.hash = '';
  return parsed.toString();
}

/**
 * Crawls a deployed site starting from its sitemap.xml (preferred) or by following
 * same-origin links from the seed URL. Also validates every internal link it
 * encounters and reports non-2xx results as broken links.
 */
export async function crawlSite(siteUrl, { maxPages = DEFAULT_MAX_PAGES, onProgress } = {}) {
  const origin = new URL(siteUrl).origin;
  const sitemap = await discoverSitemapUrls(siteUrl);
  const seedUrls = sitemap.urls
    .filter((u) => {
      try {
        return new URL(u).origin === origin;
      } catch {
        return false;
      }
    })
    .map(normalizeUrl);

  const queue = seedUrls.length > 0 ? [...seedUrls] : [normalizeUrl(siteUrl)];
  const visited = new Set();
  const pages = [];
  const brokenLinks = [];
  const checkedLinks = new Map();

  while (queue.length > 0 && visited.size < maxPages) {
    const url = queue.shift();
    if (visited.has(url)) continue;
    visited.add(url);
    onProgress?.({ phase: 'fetch', url, count: visited.size });

    const res = await fetchText(url);
    if (!res.ok) {
      brokenLinks.push({ url, status: res.status, referrer: null });
      continue;
    }

    const html = res.text;
    pages.push({ source: 'url', url, urlPath: new URL(url).pathname, html });

    const $ = cheerio.load(html);
    const internalLinks = new Set();
    $('a[href]').each((_, el) => {
      const href = $(el).attr('href');
      if (!href || href.startsWith('#') || /^(mailto|tel):/i.test(href)) return;
      let abs;
      try {
        abs = normalizeUrl(new URL(href, url).toString());
      } catch {
        return;
      }
      if (new URL(abs).origin !== origin) return;
      internalLinks.add(abs);
    });

    if (seedUrls.length === 0) {
      for (const link of internalLinks) {
        if (!NON_PAGE_EXT.test(link) && !visited.has(link) && !queue.includes(link)) {
          queue.push(link);
        }
      }
    }

    for (const link of internalLinks) {
      if (!checkedLinks.has(link)) {
        checkedLinks.set(link, await checkLinkStatus(link));
      }
      const result = checkedLinks.get(link);
      if (!result.ok) {
        brokenLinks.push({ url: link, status: result.status, referrer: url });
      }
    }
  }

  const robots = await fetchRobotsTxt(siteUrl);

  return { pages, brokenLinks, sitemap, robots };
}
