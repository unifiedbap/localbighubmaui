import fs from 'node:fs';
import path from 'node:path';
import { SEVERITY_ORDER } from '../model/finding.js';

const MANUAL_FOLLOW_UP_CHECKLIST = [
  'Google Business Profile: verify category, hours, service areas, and photos are current.',
  'Citations: confirm NAP (name/address/phone) consistency across major directories.',
  'Backlinks: check for new toxic/broken backlinks and pursue local link-building opportunities.',
  'Reviews: monitor and respond to new Google/Yelp/Facebook reviews.'
];

function section(title, findings, { valueLabel, getValue } = {}) {
  if (findings.length === 0) return '';
  const lines = [`## ${title}`, ''];
  for (const f of findings) {
    lines.push(`- **${f.page}** — ${f.check.replace(/-/g, ' ')}: ${f.description}`);
    const value = getValue ? getValue(f) : null;
    if (valueLabel && value) {
      lines.push(`  - ${valueLabel}: ${String(value).replace(/\n/g, ' ')}`);
    }
    if (f.error) {
      lines.push(`  - Error: ${f.error}`);
    }
  }
  lines.push('');
  return lines.join('\n');
}

export function buildMarkdownReport(findings, { target, runDate = new Date() } = {}) {
  const counts = Object.fromEntries(SEVERITY_ORDER.map((s) => [s, 0]));
  for (const f of findings) counts[f.severity] = (counts[f.severity] || 0) + 1;

  const fixed = findings.filter((f) => f.status === 'fixed');
  const approvedManual = findings.filter((f) => f.status === 'approved-manual');
  const skipped = findings.filter((f) => f.status === 'skipped');
  const noted = findings.filter((f) => f.status === 'noted');
  const errors = findings.filter((f) => f.status === 'error');
  const pending = findings.filter((f) => f.status === 'pending');

  const lines = [
    '# SEO Audit Report',
    '',
    `**Target:** ${target}`,
    `**Run date:** ${runDate.toISOString()}`,
    '',
    '## Summary',
    '',
    '| Severity | Count |',
    '|---|---|',
    ...SEVERITY_ORDER.map((s) => `| ${s} | ${counts[s]} |`),
    `| **Total** | **${findings.length}** |`,
    ''
  ];

  lines.push(section('Fixed', fixed, { valueLabel: 'Applied', getValue: (f) => f.appliedValue }));
  lines.push(
    section('Approved — apply manually', approvedManual, {
      valueLabel: 'Value to apply',
      getValue: (f) => f.appliedValue
    })
  );
  lines.push(section('Skipped', skipped));
  lines.push(
    section('Needs manual follow-up (not a code fix)', noted, {
      valueLabel: 'Note',
      getValue: (f) => f.suggestedFix
    })
  );
  if (errors.length > 0) {
    lines.push(
      section('Errors while applying', errors, { valueLabel: 'Attempted value', getValue: (f) => f.appliedValue })
    );
  }
  if (pending.length > 0) {
    lines.push(
      section('Not yet reviewed (re-run to resume)', pending, {
        valueLabel: 'Suggested',
        getValue: (f) => f.suggestedFix
      })
    );
  }

  lines.push('## Manual follow-up checklist (outside this tool\'s scope)', '');
  for (const item of MANUAL_FOLLOW_UP_CHECKLIST) {
    lines.push(`- [ ] ${item}`);
  }
  lines.push('');

  return lines.join('\n');
}

export function writeMarkdownReport(markdown, outDir) {
  fs.mkdirSync(outDir, { recursive: true });
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const filePath = path.join(outDir, `seo-audit-${stamp}.md`);
  fs.writeFileSync(filePath, markdown);
  return filePath;
}
