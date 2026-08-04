import { execSync } from 'node:child_process';

function run(cmd, cwd) {
  return execSync(cmd, { cwd, stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim();
}

/** Detects whether the target directory is inside a git repo, and its state before we touch it. */
export function getGitInfo(sourceDir) {
  if (!sourceDir) return { isRepo: false };
  try {
    run('git rev-parse --is-inside-work-tree', sourceDir);
  } catch {
    return { isRepo: false };
  }

  let branch = null;
  try {
    branch = run('git rev-parse --abbrev-ref HEAD', sourceDir);
  } catch {
    branch = null;
  }

  let dirtyBeforeRun = false;
  try {
    dirtyBeforeRun = run('git status --porcelain', sourceDir).length > 0;
  } catch {
    dirtyBeforeRun = false;
  }

  return { isRepo: true, branch, dirtyBeforeRun };
}
