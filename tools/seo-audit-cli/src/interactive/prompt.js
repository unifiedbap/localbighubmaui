// We deliberately avoid readline/promises' rl.question() here: with piped
// (non-TTY) stdin, repeated rl.question() calls on the same interface only
// reliably resolve the first call and then hang — pulling lines from the
// interface's own async iterator instead works correctly for both TTY and
// piped input, and lets us print prompts ourselves via process.stdout.write.
export function makeLineReader(rl) {
  const iterator = rl[Symbol.asyncIterator]();
  return async function readLine(promptText) {
    process.stdout.write(promptText);
    const { value, done } = await iterator.next();
    return done ? null : value;
  };
}

/**
 * Reads a (possibly multi-line) replacement value from the terminal.
 * Type your replacement, then a lone "." to submit, or leave the very first
 * line blank to cancel.
 */
export async function promptMultiline(readLine, label) {
  console.log(label);
  console.log('(end with a single "." on its own line; leave the first line blank to cancel)');
  const lines = [];
  // eslint-disable-next-line no-constant-condition
  while (true) {
    const line = await readLine('> ');
    if (line == null) return null;
    if (lines.length === 0 && line.trim() === '') return null;
    if (line.trim() === '.') break;
    lines.push(line);
  }
  const value = lines.join('\n').trim();
  return value.length > 0 ? value : null;
}
