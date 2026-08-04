import fs from 'node:fs';
import { insertBeforeClosingTag } from '../utils/html.js';

/** Inserts a JSON-LD <script> block before </head>. `value` must be valid JSON text. */
export function applyJsonLdFix(finding, value) {
  if (!finding.file) throw new Error('No source file to edit for this finding.');
  try {
    JSON.parse(value);
  } catch (err) {
    throw new Error(`Suggested JSON-LD is not valid JSON: ${err.message}`);
  }

  const html = fs.readFileSync(finding.file, 'utf8');
  const snippet = `<script type="application/ld+json">\n${value}\n</script>`;
  const next = insertBeforeClosingTag(html, 'head', snippet);
  if (next == null) throw new Error('No </head> tag found to insert JSON-LD before.');
  fs.writeFileSync(finding.file, next);
}
