import { runMetaChecksForPage, runDuplicateChecks } from './metaChecks.js';
import { runStructuredDataChecksForPage } from './structuredDataChecks.js';
import { runImageChecksForPage } from './imageChecks.js';
import { runTechnicalChecksForPage, runSiteLevelTechnicalChecks } from './technicalChecks.js';
import { runPerformanceCheckForPage } from './performanceChecks.js';

/** Runs every v1 audit check across every discovered page and returns the flat finding list. */
export async function runAllChecks(discoveryResult, config, { onProgress } = {}) {
  const { pages } = discoveryResult;
  const findings = [];

  for (const page of pages) {
    onProgress?.({ phase: 'check', page: page.urlPath });
    findings.push(...runMetaChecksForPage(page, config));
    findings.push(...runStructuredDataChecksForPage(page, config));
    findings.push(...(await runImageChecksForPage(page, config)));
    findings.push(...runTechnicalChecksForPage(page, config));
    findings.push(...(await runPerformanceCheckForPage(page, config)));
  }

  findings.push(...runDuplicateChecks(pages));
  findings.push(...runSiteLevelTechnicalChecks(discoveryResult, config));

  return findings;
}
