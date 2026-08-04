import { makeFinding } from '../model/finding.js';

const PSI_ENDPOINT = 'https://www.googleapis.com/pagespeedonline/v5/runPagespeed';

async function fetchPageSpeed(url, apiKey, strategy) {
  const params = new URLSearchParams({
    url,
    strategy,
    category: 'PERFORMANCE'
  });
  if (apiKey) params.set('key', apiKey);

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 30000);
  try {
    const res = await fetch(`${PSI_ENDPOINT}?${params.toString()}`, { signal: controller.signal });
    const body = await res.json();
    if (!res.ok) {
      return { error: body?.error?.message || `PageSpeed API returned ${res.status}` };
    }
    return { data: body };
  } catch (err) {
    return { error: err.message };
  } finally {
    clearTimeout(timer);
  }
}

function extractVitals(psiData) {
  const lighthouse = psiData?.lighthouseResult;
  const score = lighthouse?.categories?.performance?.score;
  const audits = lighthouse?.audits || {};
  return {
    score: score != null ? Math.round(score * 100) : null,
    lcp: audits['largest-contentful-paint']?.displayValue ?? null,
    cls: audits['cumulative-layout-shift']?.displayValue ?? null,
    inp: audits['interaction-to-next-paint']?.displayValue ?? audits['experimental-interaction-to-next-paint']?.displayValue ?? null
  };
}

/**
 * Runs Google PageSpeed Insights (mobile) for a page and flags it if the
 * performance score falls below config.thresholds.performanceMobile.
 * Requires a public URL — silently skipped for pages with no liveUrl (e.g. a
 * pre-deploy local-only scan), since PSI can't reach localhost/unpublished files.
 */
export async function runPerformanceCheckForPage(page, config) {
  const url = page.liveUrl;
  if (!url) return [];
  if (!config.pagespeedApiKey) {
    return [
      makeFinding({
        category: 'performance',
        check: 'pagespeed-skipped-no-api-key',
        severity: 'Low',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description:
          'Core Web Vitals not checked: no PageSpeed Insights API key configured (set "pagespeedApiKey" in the config file).',
        suggestedFix: null,
        fixType: 'manual-external',
        fixData: null
      })
    ];
  }

  const { data, error } = await fetchPageSpeed(url, config.pagespeedApiKey, 'mobile');
  if (error) {
    return [
      makeFinding({
        category: 'performance',
        check: 'pagespeed-api-error',
        severity: 'Low',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: `Could not fetch PageSpeed Insights data: ${error}`,
        suggestedFix: null,
        fixType: 'manual-external',
        fixData: null
      })
    ];
  }

  const vitals = extractVitals(data);
  if (vitals.score == null) return [];

  const threshold = config.thresholds.performanceMobile;
  if (vitals.score >= threshold) return [];

  const severity = vitals.score < threshold * 0.6 ? 'Critical' : 'High';
  const vitalsList = [
    vitals.lcp ? `LCP: ${vitals.lcp}` : null,
    vitals.cls ? `CLS: ${vitals.cls}` : null,
    vitals.inp ? `INP: ${vitals.inp}` : null
  ]
    .filter(Boolean)
    .join(', ');

  return [
    makeFinding({
      category: 'performance',
      check: 'low-core-web-vitals-score',
      severity,
      effort: 'Involved',
      page: page.urlPath,
      file: page.filePath,
      description: `Mobile PageSpeed performance score is ${vitals.score}/100 (threshold: ${threshold}).${
        vitalsList ? ` ${vitalsList}` : ''
      }`,
      suggestedFix:
        'No single-file code fix — investigate render-blocking resources, image sizes/formats, and JS bundle size in the full PageSpeed Insights report, then re-run this audit.',
      fixType: 'manual-code',
      fixData: { tag: 'core-web-vitals', score: vitals.score }
    })
  ];
}
