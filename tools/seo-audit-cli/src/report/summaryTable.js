import chalk from 'chalk';
import { SEVERITY_ORDER } from '../model/finding.js';

const SEVERITY_STYLE = {
  Critical: chalk.bgRed.white.bold,
  High: chalk.red.bold,
  Medium: chalk.yellow.bold,
  Low: chalk.gray.bold
};

export function printSummaryTable(findings) {
  const counts = Object.fromEntries(SEVERITY_ORDER.map((s) => [s, 0]));
  for (const f of findings) counts[f.severity] = (counts[f.severity] || 0) + 1;

  console.log('');
  console.log(chalk.bold('SEO Audit Summary'));
  console.log('-'.repeat(28));
  for (const sev of SEVERITY_ORDER) {
    const style = SEVERITY_STYLE[sev] || ((s) => s);
    console.log(`${style(sev.padEnd(10))} ${String(counts[sev]).padStart(4)}`);
  }
  console.log('-'.repeat(28));
  console.log(`${'Total'.padEnd(10)} ${String(findings.length).padStart(4)}`);
  console.log('');
}
