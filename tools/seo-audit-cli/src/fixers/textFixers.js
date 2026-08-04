import fs from 'node:fs';
import {
  findElements,
  findTags,
  getMetaByName,
  getCanonical,
  getViewport,
  insertBeforeClosingTag,
  insertAfterTag,
  replaceRange,
  escapeHtmlAttr
} from '../utils/html.js';

// All fixers re-read the file fresh at apply-time (rather than reusing the HTML
// captured during discovery) because earlier approved fixes in the same run may
// have already changed this same file, which would make cached string offsets
// stale.

function readFile(finding) {
  if (!finding.file) throw new Error('No source file to edit for this finding.');
  return fs.readFileSync(finding.file, 'utf8');
}

function writeFile(finding, html) {
  fs.writeFileSync(finding.file, html);
}

export function applyTitleFix(finding, value) {
  const html = readFile(finding);
  const escaped = escapeHtmlAttr(value);
  const elements = findElements(html, 'title');

  let next;
  if (elements.length > 0) {
    const el = elements[0];
    next = replaceRange(html, el.innerStart, el.innerEnd, escaped);
  } else {
    next = insertBeforeClosingTag(html, 'head', `<title>${escaped}</title>`);
    if (next == null) throw new Error('No </head> tag found to insert <title> before.');
  }
  writeFile(finding, next);
}

export function applyMetaDescriptionFix(finding, value) {
  const html = readFile(finding);
  const escaped = escapeHtmlAttr(value);
  const tags = getMetaByName(html, 'description');

  let next;
  if (tags.length > 0) {
    const tag = tags[0];
    next = replaceRange(html, tag.start, tag.end, `<meta name="description" content="${escaped}">`);
  } else {
    next = insertBeforeClosingTag(html, 'head', `<meta name="description" content="${escaped}">`);
    if (next == null) throw new Error('No </head> tag found to insert meta description before.');
  }
  writeFile(finding, next);
}

export function applyH1Fix(finding, value) {
  const html = readFile(finding);
  const escaped = escapeHtmlAttr(value);
  const elements = findElements(html, 'h1');

  let next;
  if (elements.length > 0) {
    const el = elements[0];
    next = replaceRange(html, el.innerStart, el.innerEnd, escaped);
  } else {
    const bodyTags = findTags(html, 'body');
    if (bodyTags.length === 0) throw new Error('No <body> tag found to insert <h1> into.');
    next = insertAfterTag(html, bodyTags[0], `<h1>${escaped}</h1>`);
  }
  writeFile(finding, next);
}

export function applyCanonicalFix(finding, value) {
  const html = readFile(finding);
  const escaped = escapeHtmlAttr(value);
  const tags = getCanonical(html);

  let next;
  if (tags.length > 0) {
    next = replaceRange(html, tags[0].start, tags[0].end, `<link rel="canonical" href="${escaped}">`);
  } else {
    next = insertBeforeClosingTag(html, 'head', `<link rel="canonical" href="${escaped}">`);
    if (next == null) throw new Error('No </head> tag found to insert canonical link before.');
  }
  writeFile(finding, next);
}

export function applyViewportFix(finding, value) {
  const html = readFile(finding);
  const escaped = escapeHtmlAttr(value);
  const tags = getViewport(html);

  let next;
  if (tags.length > 0) {
    next = replaceRange(html, tags[0].start, tags[0].end, `<meta name="viewport" content="${escaped}">`);
  } else {
    next = insertBeforeClosingTag(html, 'head', `<meta name="viewport" content="${escaped}">`);
    if (next == null) throw new Error('No </head> tag found to insert viewport meta before.');
  }
  writeFile(finding, next);
}
