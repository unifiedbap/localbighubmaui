import crypto from 'node:crypto';

export const SEVERITY_ORDER = ['Critical', 'High', 'Medium', 'Low'];
export const EFFORT_ORDER = ['Quick fix', 'Moderate', 'Involved'];

// fixType:
//   'auto'           - tool can construct the exact replacement and apply it on approve
//   'manual-code'     - a code change, but the tool won't guess the value safely;
//                        requires (e)dit to supply the value, or is logged for manual follow-up
//   'manual-external' - not a code fix at all (GBP, citations, backlinks, reviews); never
//                        touched by the tool, always routed to the manual follow-up log
export function makeFinding({
  category,
  check,
  severity,
  effort,
  page,
  file = null,
  description,
  suggestedFix = null,
  fixType,
  fixData = null
}) {
  if (!SEVERITY_ORDER.includes(severity)) {
    throw new Error(`Invalid severity: ${severity}`);
  }
  if (!EFFORT_ORDER.includes(effort)) {
    throw new Error(`Invalid effort: ${effort}`);
  }

  const idSource = `${category}|${check}|${page}|${description}`;
  const id = crypto.createHash('sha1').update(idSource).digest('hex').slice(0, 12);

  return {
    id,
    category,
    check,
    severity,
    effort,
    page,
    file,
    description,
    suggestedFix,
    fixType,
    fixData,
    // pending | fixed | approved-manual | skipped | noted | error
    status: 'pending',
    wasEdited: false,
    appliedValue: null,
    error: null
  };
}
