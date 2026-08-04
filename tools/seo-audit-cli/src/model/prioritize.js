import { SEVERITY_ORDER, EFFORT_ORDER } from './finding.js';

/** Severity first, then effort (quick wins surface first within a severity tier). */
export function prioritizeFindings(findings) {
  return [...findings].sort((a, b) => {
    const sevDiff = SEVERITY_ORDER.indexOf(a.severity) - SEVERITY_ORDER.indexOf(b.severity);
    if (sevDiff !== 0) return sevDiff;
    const effDiff = EFFORT_ORDER.indexOf(a.effort) - EFFORT_ORDER.indexOf(b.effort);
    if (effDiff !== 0) return effDiff;
    return a.page.localeCompare(b.page);
  });
}
