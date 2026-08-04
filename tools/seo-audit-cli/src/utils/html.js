// Lightweight, regex-based HTML reading/writing helpers.
//
// We deliberately avoid parsing-then-reserializing the whole document (e.g. with
// cheerio) when we *write* a fix: full reserialization tends to reformat quotes,
// self-closing slashes, and whitespace across the whole file, which violates the
// "no unrelated formatting changes" requirement. Instead we locate the exact
// substring to change with a targeted regex and splice just that range back into
// the original text. cheerio is used elsewhere for read-only analysis where
// reformatting doesn't matter.

const ENTITY_MAP = {
  amp: '&',
  lt: '<',
  gt: '>',
  quot: '"',
  apos: "'",
  '#39': "'",
  nbsp: ' '
};

export function decodeEntitiesLite(str) {
  return String(str).replace(/&(#?\w+);/g, (m, ent) => {
    const key = ent.toLowerCase();
    return ENTITY_MAP[key] !== undefined ? ENTITY_MAP[key] : m;
  });
}

export function escapeHtmlAttr(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

/** Matches every opening tag `<tagName ...>` (void or not). */
export function findTags(html, tagName) {
  const re = new RegExp(`<${tagName}\\b[^>]*>`, 'gi');
  const results = [];
  let m;
  while ((m = re.exec(html))) {
    results.push({ raw: m[0], start: m.index, end: m.index + m[0].length });
  }
  return results;
}

/** Matches full `<tagName ...>...</tagName>` elements, non-greedy on inner content. */
export function findElements(html, tagName) {
  const re = new RegExp(`<${tagName}\\b[^>]*>([\\s\\S]*?)<\\/${tagName}>`, 'gi');
  const results = [];
  let m;
  while ((m = re.exec(html))) {
    const openTag = m[0].slice(0, m[0].indexOf('>') + 1);
    results.push({
      raw: m[0],
      openTag,
      innerHTML: m[1],
      start: m.index,
      end: m.index + m[0].length,
      innerStart: m.index + openTag.length,
      innerEnd: m.index + m[0].length - `</${tagName}>`.length
    });
  }
  return results;
}

export function getAttr(tag, attrName) {
  const re = new RegExp(`\\b${attrName}\\s*=\\s*("([^"]*)"|'([^']*)'|([^\\s>]+))`, 'i');
  const m = tag.match(re);
  if (!m) return null;
  return m[2] !== undefined ? m[2] : m[3] !== undefined ? m[3] : m[4];
}

export function hasAttr(tag, attrName) {
  return new RegExp(`\\b${attrName}\\s*=`, 'i').test(tag);
}

export function getTitle(html) {
  const elements = findElements(html, 'title');
  return {
    present: elements.length > 0,
    values: elements.map((e) => decodeEntitiesLite(e.innerHTML.trim())),
    elements
  };
}

export function getMetaByName(html, name) {
  return findTags(html, 'meta').filter((m) => {
    const n = getAttr(m.raw, 'name') || getAttr(m.raw, 'property');
    return n && n.toLowerCase() === name.toLowerCase();
  });
}

export function getH1s(html) {
  return findElements(html, 'h1');
}

export function getCanonical(html) {
  return findTags(html, 'link').filter(
    (l) => (getAttr(l.raw, 'rel') || '').toLowerCase() === 'canonical'
  );
}

export function getViewport(html) {
  return getMetaByName(html, 'viewport');
}

export function getJsonLdBlocks(html) {
  const scripts = findElements(html, 'script').filter((s) => {
    const type = getAttr(s.openTag, 'type') || '';
    return type.toLowerCase() === 'application/ld+json';
  });
  return scripts.map((s) => {
    let parsed = null;
    let parseError = null;
    try {
      parsed = JSON.parse(s.innerHTML);
    } catch (e) {
      parseError = e.message;
    }
    return { ...s, parsed, parseError };
  });
}

export function getImgTags(html) {
  return findTags(html, 'img');
}

export function replaceRange(html, start, end, replacement) {
  return html.slice(0, start) + replacement + html.slice(end);
}

/** Detects the indentation of the line containing `index` so inserted lines match it. */
function indentAt(html, index) {
  const lineStart = html.lastIndexOf('\n', index - 1) + 1;
  const match = html.slice(lineStart, index).match(/^[ \t]*/);
  return match ? match[0] : '';
}

/**
 * Indentation to use for a new line inserted just before `index`. New siblings
 * should match the *previous content line's* indentation, not the indentation
 * of whatever comes right after `index` (e.g. a closing tag that's flush left).
 */
function indentForInsertionBefore(html, index) {
  let before = html.slice(0, index);
  // Drop exactly one trailing line break, if any, to get past the closing
  // tag's own (possibly unindented) line and down to the sibling above it.
  if (before.endsWith('\r\n')) before = before.slice(0, -2);
  else if (before.endsWith('\n')) before = before.slice(0, -1);

  const prevLineStart = before.lastIndexOf('\n') + 1;
  const prevLine = before.slice(prevLineStart);
  if (prevLine.trim().length > 0) {
    return prevLine.match(/^[ \t]*/)[0];
  }
  return indentAt(html, index);
}

/** Inserts `snippet` as its own line, indented like the previous sibling line. */
export function insertBeforeClosingTag(html, tagName, snippet) {
  const re = new RegExp(`</${tagName}>`, 'i');
  const m = html.match(re);
  if (!m) return null;
  const idx = m.index;
  const indent = indentForInsertionBefore(html, idx);
  return html.slice(0, idx) + `${indent}${snippet}\n` + html.slice(idx);
}

/** Inserts `snippet` as its own line, indented like the line after the matched opening tag. */
export function insertAfterTag(html, openTagMatch, snippet) {
  const idx = openTagMatch.end;
  const indent = indentAt(html, openTagMatch.start);
  return html.slice(0, idx) + `\n${indent}${snippet}` + html.slice(idx);
}
