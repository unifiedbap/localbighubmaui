import readline from 'node:readline';
import { stdin, stdout } from 'node:process';
import chalk from 'chalk';
import { applyFix, isAutoFixable } from '../fixers/index.js';
import { makeLineReader, promptMultiline } from './prompt.js';

const SEVERITY_STYLE = {
  Critical: chalk.bgRed.white.bold,
  High: chalk.red.bold,
  Medium: chalk.yellow.bold,
  Low: chalk.gray.bold
};

function formatHeader(finding, position, total) {
  const style = SEVERITY_STYLE[finding.severity] || ((s) => s);
  return `[${position}/${total}] ${style(`${finding.severity} severity`)} — ${finding.check.replace(/-/g, ' ')}`;
}

function applyDecision(finding, value, wasEdited) {
  finding.wasEdited = wasEdited;
  finding.appliedValue = value;

  if (isAutoFixable(finding)) {
    try {
      applyFix(finding, value);
      finding.status = 'fixed';
      finding.error = null;
      console.log(chalk.green(finding.file ? `Applied to ${finding.file}` : 'Applied.'));
    } catch (err) {
      finding.status = 'error';
      finding.error = err.message;
      console.log(chalk.red(`Failed to apply fix: ${err.message}`));
    }
  } else {
    // A code-level fix the tool can't safely automate (e.g. broken link target,
    // which H1 to keep) — the value is recorded for the report, but no file is
    // touched. The founder applies it by hand.
    finding.status = 'approved-manual';
    console.log(chalk.yellow('Recorded — no automatic edit available, apply this by hand.'));
  }
}

/**
 * Walks every non-pending-resolved, non-external finding one at a time.
 * Findings with fixType 'manual-external' (non-code items like GBP/citations)
 * are never prompted — per spec the tool should never attempt those, so they're
 * auto-routed to the manual follow-up log.
 */
export async function runReviewLoop(allFindings, { onProgressSave } = {}) {
  const rl = readline.createInterface({ input: stdin, output: stdout, terminal: false });
  const readLine = makeLineReader(rl);
  const total = allFindings.length;
  let quit = false;

  try {
    for (let i = 0; i < allFindings.length; i++) {
      const finding = allFindings[i];

      if (finding.fixType === 'manual-external') {
        if (finding.status === 'pending') finding.status = 'noted';
        continue;
      }
      if (finding.status !== 'pending') continue;

      console.log('');
      console.log(formatHeader(finding, i + 1, total));
      console.log(`Page: ${finding.page}`);
      console.log(`What's wrong: ${finding.description}`);
      if (finding.suggestedFix) {
        console.log(`Suggested: ${finding.suggestedFix}`);
      } else {
        console.log(chalk.dim("No automatic suggestion — this needs your judgment (use (e)dit to supply one)."));
      }
      if (!finding.file) {
        console.log(
          chalk.dim('(No local source file for this page — approving/editing records the fix, nothing is written.)')
        );
      }

      let decided = false;
      while (!decided) {
        const raw = await readLine('\n(a)pprove / (s)kip / (e)dit / (q)uit > ');
        if (raw == null) {
          quit = true;
          decided = true;
          break;
        }
        const answer = raw.trim().toLowerCase();

        if (answer === 'q') {
          quit = true;
          decided = true;
          break;
        }
        if (answer === 's') {
          finding.status = 'skipped';
          decided = true;
          break;
        }
        if (answer === 'a') {
          if (!finding.suggestedFix) {
            console.log('No suggested fix to approve — use (e)dit to supply a value, or (s)kip.');
            continue;
          }
          applyDecision(finding, finding.suggestedFix, false);
          decided = true;
          break;
        }
        if (answer === 'e') {
          const value = await promptMultiline(readLine, 'Enter your replacement:');
          if (value == null) {
            console.log('Cancelled.');
            continue;
          }
          applyDecision(finding, value, true);
          decided = true;
          break;
        }

        console.log('Please enter a, s, e, or q.');
      }

      onProgressSave?.(allFindings);
      if (quit) break;
    }
  } finally {
    rl.close();
  }

  return { quit };
}
