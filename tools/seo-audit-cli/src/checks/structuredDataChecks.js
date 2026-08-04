import { makeFinding } from '../model/finding.js';
import { getJsonLdBlocks } from '../utils/html.js';

const LOCAL_BUSINESS_TYPES = new Set([
  'LocalBusiness',
  'HomeAndConstructionBusiness',
  'Electrician',
  'Plumber',
  'RoofingContractor',
  'GeneralContractor',
  'HVACBusiness',
  'MovingCompany',
  'Locksmith',
  'PestControlBusiness'
]);

function containsLocalBusinessType(parsed) {
  if (!parsed) return false;
  const nodes = Array.isArray(parsed) ? parsed : parsed['@graph'] ? parsed['@graph'] : [parsed];
  return nodes.some((node) => {
    const type = node?.['@type'];
    if (!type) return false;
    const types = Array.isArray(type) ? type : [type];
    return types.some((t) => LOCAL_BUSINESS_TYPES.has(t));
  });
}

function buildLocalBusinessSchema(config, page) {
  const schema = {
    '@context': 'https://schema.org',
    '@type': 'LocalBusiness',
    name: config.businessName || 'REPLACE_WITH_BUSINESS_NAME',
    url: config.siteUrl ? new URL(page.urlPath, config.siteUrl).toString() : 'REPLACE_WITH_PAGE_URL'
  };
  if (config.serviceArea) {
    schema.areaServed = config.serviceArea;
  }
  if (config.phone) schema.telephone = config.phone;
  if (config.address) schema.address = config.address;
  return schema;
}

export function runStructuredDataChecksForPage(page, config) {
  const findings = [];
  const blocks = getJsonLdBlocks(page.html);

  const invalidBlocks = blocks.filter((b) => b.parseError);
  for (const block of invalidBlocks) {
    findings.push(
      makeFinding({
        category: 'structured-data',
        check: 'invalid-json-ld',
        severity: 'High',
        effort: 'Moderate',
        page: page.urlPath,
        file: page.filePath,
        description: `JSON-LD block is not valid JSON: ${block.parseError}`,
        suggestedFix: null,
        fixType: 'manual-code',
        fixData: { tag: 'json-ld', mode: 'manual-review', start: block.innerStart, end: block.innerEnd }
      })
    );
  }

  const hasValidLocalBusiness = blocks.some((b) => !b.parseError && containsLocalBusinessType(b.parsed));
  if (!hasValidLocalBusiness) {
    const schema = buildLocalBusinessSchema(config, page);
    const suggestedFix = JSON.stringify(schema, null, 2);
    findings.push(
      makeFinding({
        category: 'structured-data',
        check: 'missing-local-business-schema',
        severity: 'High',
        effort: 'Moderate',
        page: page.urlPath,
        file: page.filePath,
        description: 'Page has no LocalBusiness (or subtype) JSON-LD schema.',
        suggestedFix,
        fixType: page.filePath ? 'auto' : 'manual-code',
        fixData: { tag: 'json-ld', mode: 'insert-head' }
      })
    );
  }

  return findings;
}
