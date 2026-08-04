import { makeFinding } from '../model/finding.js';
import { getTitle, getMetaByName, getH1s, decodeEntitiesLite } from '../utils/html.js';

function guessTopic(page, $title, h1Text) {
  if (h1Text) return h1Text;
  if ($title) return $title;
  const slug = page.urlPath.split('/').filter(Boolean).pop() || 'Home';
  return slug.replace(/[-_]/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
}

function buildSuggestedTitle(config, page, topic) {
  const parts = [topic, config.businessName].filter(Boolean);
  let title = parts.join(' | ');
  if (config.serviceArea && !title.toLowerCase().includes(config.serviceArea.toLowerCase())) {
    title = `${topic} in ${config.serviceArea} | ${config.businessName}`.replace(/^\s*\|\s*|\s*\|\s*$/g, '');
  }
  return title.length > 60 ? `${topic} | ${config.businessName}` : title;
}

function buildSuggestedDescription(config, topic) {
  const biz = config.businessName || 'us';
  const area = config.serviceArea ? ` in ${config.serviceArea}` : '';
  return `${topic}${area} from ${biz}. Licensed, insured, and locally trusted — free estimates available.`;
}

/** Runs all meta/content-level checks (title, description, H1, local keywords) for one page. */
export function runMetaChecksForPage(page, config) {
  const findings = [];
  const html = page.html;
  const title = getTitle(html);
  const descTags = getMetaByName(html, 'description');
  const h1s = getH1s(html);
  const h1Text = h1s.length > 0 ? decodeEntitiesLite(h1s[0].innerHTML.trim()) : null;
  const titleText = title.values[0] || null;
  const topic = guessTopic(page, titleText, h1Text);
  const { titleMinLength, titleMaxLength, metaDescriptionMinLength, metaDescriptionMaxLength } =
    config.thresholds;

  // --- Title ---
  if (!title.present || !titleText) {
    const suggested = buildSuggestedTitle(config, page, topic);
    findings.push(
      makeFinding({
        category: 'meta',
        check: 'missing-title',
        severity: 'Critical',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: `Page has no <title> tag (or it is empty).`,
        suggestedFix: suggested,
        fixType: page.filePath ? 'auto' : 'manual-code',
        fixData: { tag: 'title', mode: title.present ? 'replace' : 'insert-head' }
      })
    );
  } else {
    if (titleText.length < titleMinLength || titleText.length > titleMaxLength) {
      const suggested =
        titleText.length > titleMaxLength ? titleText.slice(0, titleMaxLength - 1).trim() : buildSuggestedTitle(config, page, topic);
      findings.push(
        makeFinding({
          category: 'meta',
          check: 'title-length',
          severity: 'Medium',
          effort: 'Quick fix',
          page: page.urlPath,
          file: page.filePath,
          description: `Title is ${titleText.length} characters (recommended ${titleMinLength}-${titleMaxLength}): "${titleText}"`,
          suggestedFix: suggested,
          fixType: page.filePath ? 'auto' : 'manual-code',
          fixData: { tag: 'title', mode: 'replace' }
        })
      );
    }

    if (
      config.serviceArea &&
      !titleText.toLowerCase().includes(config.serviceArea.split(',')[0].trim().toLowerCase())
    ) {
      findings.push(
        makeFinding({
          category: 'meta',
          check: 'missing-local-keyword-title',
          severity: 'Low',
          effort: 'Quick fix',
          page: page.urlPath,
          file: page.filePath,
          description: `Title doesn't mention the service area ("${config.serviceArea}"): "${titleText}"`,
          suggestedFix: buildSuggestedTitle(config, page, topic),
          fixType: page.filePath ? 'auto' : 'manual-code',
          fixData: { tag: 'title', mode: 'replace' }
        })
      );
    }
  }

  // --- Meta description ---
  const descText = descTags[0] ? decodeEntitiesLite(descTags[0].raw.match(/content\s*=\s*("([^"]*)"|'([^']*)')/i)?.[2] ?? descTags[0].raw.match(/content\s*=\s*("([^"]*)"|'([^']*)')/i)?.[3] ?? '') : null;

  if (descTags.length === 0 || !descText) {
    findings.push(
      makeFinding({
        category: 'meta',
        check: 'missing-meta-description',
        severity: 'High',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: 'Page has no meta description.',
        suggestedFix: buildSuggestedDescription(config, topic),
        fixType: page.filePath ? 'auto' : 'manual-code',
        fixData: { tag: 'meta-description', mode: descTags.length > 0 ? 'replace' : 'insert-head' }
      })
    );
  } else if (descText.length < metaDescriptionMinLength || descText.length > metaDescriptionMaxLength) {
    const suggested =
      descText.length > metaDescriptionMaxLength
        ? descText.slice(0, metaDescriptionMaxLength - 1).trim()
        : buildSuggestedDescription(config, topic);
    findings.push(
      makeFinding({
        category: 'meta',
        check: 'meta-description-length',
        severity: 'Medium',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: `Meta description is ${descText.length} characters (recommended ${metaDescriptionMinLength}-${metaDescriptionMaxLength}): "${descText}"`,
        suggestedFix: suggested,
        fixType: page.filePath ? 'auto' : 'manual-code',
        fixData: { tag: 'meta-description', mode: 'replace' }
      })
    );
  } else if (
    config.serviceArea &&
    !descText.toLowerCase().includes(config.serviceArea.split(',')[0].trim().toLowerCase())
  ) {
    findings.push(
      makeFinding({
        category: 'meta',
        check: 'missing-local-keyword-description',
        severity: 'Low',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: `Meta description doesn't mention the service area ("${config.serviceArea}").`,
        suggestedFix: buildSuggestedDescription(config, topic),
        fixType: page.filePath ? 'auto' : 'manual-code',
        fixData: { tag: 'meta-description', mode: 'replace' }
      })
    );
  }

  // --- H1 ---
  if (h1s.length === 0) {
    findings.push(
      makeFinding({
        category: 'meta',
        check: 'missing-h1',
        severity: 'High',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: 'Page has no <h1>.',
        suggestedFix: titleText || buildSuggestedTitle(config, page, topic),
        fixType: page.filePath ? 'auto' : 'manual-code',
        fixData: { tag: 'h1', mode: 'insert-body' }
      })
    );
  } else if (h1s.length > 1) {
    const texts = h1s.map((h) => decodeEntitiesLite(h.innerHTML.trim()));
    findings.push(
      makeFinding({
        category: 'meta',
        check: 'multiple-h1',
        severity: 'Medium',
        effort: 'Moderate',
        page: page.urlPath,
        file: page.filePath,
        description: `Page has ${h1s.length} <h1> tags: ${texts.map((t) => `"${t}"`).join(', ')}. Pick one and demote the rest to <h2>.`,
        suggestedFix: null,
        fixType: 'manual-code',
        fixData: { tag: 'h1', mode: 'manual-review' }
      })
    );
  } else if (
    config.serviceArea &&
    !h1Text.toLowerCase().includes(config.serviceArea.split(',')[0].trim().toLowerCase())
  ) {
    findings.push(
      makeFinding({
        category: 'meta',
        check: 'missing-local-keyword-h1',
        severity: 'Low',
        effort: 'Quick fix',
        page: page.urlPath,
        file: page.filePath,
        description: `H1 doesn't mention the service area ("${config.serviceArea}"): "${h1Text}"`,
        suggestedFix: `${h1Text} in ${config.serviceArea}`,
        fixType: page.filePath ? 'auto' : 'manual-code',
        fixData: { tag: 'h1', mode: 'replace' }
      })
    );
  }

  return findings;
}

/** Cross-page checks: duplicate titles / descriptions across the whole site. */
export function runDuplicateChecks(pages) {
  const findings = [];
  const titleGroups = new Map();
  const descGroups = new Map();

  for (const page of pages) {
    const title = getTitle(page.html).values[0];
    if (title) {
      if (!titleGroups.has(title)) titleGroups.set(title, []);
      titleGroups.get(title).push(page);
    }
    const descTag = getMetaByName(page.html, 'description')[0];
    const descMatch = descTag?.raw.match(/content\s*=\s*("([^"]*)"|'([^']*)')/i);
    const desc = descMatch ? descMatch[2] ?? descMatch[3] : null;
    if (desc) {
      if (!descGroups.has(desc)) descGroups.set(desc, []);
      descGroups.get(desc).push(page);
    }
  }

  for (const [title, group] of titleGroups) {
    if (group.length < 2) continue;
    for (const page of group) {
      findings.push(
        makeFinding({
          category: 'meta',
          check: 'duplicate-title',
          severity: 'High',
          effort: 'Moderate',
          page: page.urlPath,
          file: page.filePath,
          description: `Title "${title}" is duplicated across ${group.length} pages: ${group
            .map((p) => p.urlPath)
            .join(', ')}`,
          suggestedFix: null,
          fixType: 'manual-code',
          fixData: { tag: 'title', mode: 'manual-review' }
        })
      );
    }
  }

  for (const [desc, group] of descGroups) {
    if (group.length < 2) continue;
    for (const page of group) {
      findings.push(
        makeFinding({
          category: 'meta',
          check: 'duplicate-meta-description',
          severity: 'Medium',
          effort: 'Moderate',
          page: page.urlPath,
          file: page.filePath,
          description: `Meta description "${desc}" is duplicated across ${group.length} pages: ${group
            .map((p) => p.urlPath)
            .join(', ')}`,
          suggestedFix: null,
          fixType: 'manual-code',
          fixData: { tag: 'meta-description', mode: 'manual-review' }
        })
      );
    }
  }

  return findings;
}
