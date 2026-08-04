# SEO Audit CLI (internal)

Internal-only tool for Big Local Ideas. Points at a client site (a local source
directory and/or a deployed URL), audits it for technical SEO issues, and walks
you through fixes one finding at a time. Nothing is ever applied without your
explicit approval, and nothing is ever auto-committed or auto-deployed.

This is deliberately **not** part of the Big Local Hub product — it's a
personal CLI that happens to live in this repo, in its own directory, with no
dependency on (or import from) the MAUI app.

## Install

```bash
cd tools/seo-audit-cli
npm install
```

Run it with `node bin/seo-audit.js ...`, or `npm link` to get a global
`seo-audit` command.

## Quick start

```bash
node bin/seo-audit.js init ./seo-audit.config.json   # writes a starter config
# edit seo-audit.config.json: businessName, serviceArea, sourceDir/siteUrl, pagespeedApiKey
node bin/seo-audit.js audit --config ./seo-audit.config.json
```

Or skip the config file for a quick one-off:

```bash
node bin/seo-audit.js audit --dir ../client-sites/acme-plumbing
node bin/seo-audit.js audit --url https://acmeplumbing.com
node bin/seo-audit.js audit --dir ../client-sites/acme-plumbing --url https://acmeplumbing.com
```

## Modes

- **`--dir` only** (pre-deploy / static scan): scans every `.html` file under
  the directory. Every finding is fixable — approved/edited fixes are written
  straight to the source file.
- **`--url` only** (deployed site): crawls the site (via `sitemap.xml` if
  present, otherwise by following internal links), validates every internal
  link, and runs PageSpeed Insights per page. Nothing can be auto-fixed in
  this mode — there's no local file to write to — so all findings land in the
  "approved — apply manually" bucket if you approve them.
- **`--dir` + `--url`** (recommended for an already-deployed client site): the
  local directory is the source of truth for content and fixes; the crawl
  adds broken-link checking, robots.txt/sitemap.xml reachability, and
  PageSpeed Insights (which needs a live URL to test against).

## What it checks (v1)

Meta/content (title, description, H1, local keyword presence), structured
data (LocalBusiness JSON-LD presence + validity), images (missing alt,
oversized files), technical (robots.txt, sitemap.xml, canonical tags,
viewport meta, broken internal links — crawl mode only), and performance
(Core Web Vitals via PageSpeed Insights — requires `pagespeedApiKey` and a
reachable URL).

## The approval flow

```
[8/22] HIGH severity — missing meta description
Page: /services/excavation
What's wrong: Page has no meta description.
Suggested: Professional excavation services in Denver, CO from Northstar
Excavation. Licensed, insured, and locally trusted — free estimates available.

(a)pprove / (s)kip / (e)dit / (q)uit >
```

- **(a)pprove** — apply the suggested fix as-is.
- **(s)kip** — leave it, log it as skipped in the report.
- **(e)dit** — type your own replacement (end with a line containing just
  `.`), then that value gets applied instead. For findings with no suggested
  fix (e.g. "which of these two H1s do you want to keep") this is the only
  way to supply a value.
- **(q)uit** — save progress and exit. Re-running the same command resumes
  right where you left off — findings you already decided on aren't
  re-prompted, and findings that no longer reproduce (because you fixed them)
  simply disappear from the list on the next run.

Findings are sorted by severity, then by effort within a severity tier, so
quick wins surface first.

### Findings without an automatic fix

Some findings are real code changes but need your judgment, not a guess from
the tool: which of two duplicate titles to keep, which `<h1>` to demote, what
the correct destination for a broken link is, whether malformed JSON-LD is
worth hand-repairing. For these, `suggestedFix` is `null` — you must use
**(e)dit** to record what you did (or plan to do); approving/editing never
silently writes to the file, since there's no fixer registered for that kind
of change. The value you type is captured in the report as
"apply manually," not as "fixed."

Oversized images work the same way on purpose: the tool tells you the file is
too big and by how much, but never recompresses it. Changing image quality
isn't something that should happen without you looking at the result.

Non-code items (Google Business Profile, citations, backlinks, reviews) are
never prompted at all — the tool doesn't check them, and every report ends
with a static manual-follow-up checklist for them.

## Output

- **State**: `<sourceDir>/.seo-audit/state.json` (or, for URL-only mode,
  `~/.seo-audit-cli/state/<hash>.json`). This is what `--fresh` bypasses and
  what a plain re-run resumes from.
- **Report**: a markdown file per run under `<sourceDir>/.seo-audit/reports/`
  (or `~/.seo-audit-cli/reports/<hash>/` for URL-only mode), listing what was
  fixed, what needs manual application, what was skipped, and the standing
  manual-follow-up checklist.

Both directories are safe to add to the client site's `.gitignore` — they're
audit-tool bookkeeping, not site content.

## Git awareness

If the target directory is a git repo, fixes are left as uncommitted changes
so you can review the diff (`git diff`) before committing — the tool never
commits for you. If it isn't a repo, you'll get a warning, since there's no
built-in undo for the edits it makes.

## Fix philosophy

Fixes are applied with targeted string surgery (find the exact tag, splice in
the new value) rather than parsing and reserializing the whole HTML document,
so a fix only touches the lines it needs to — no reformatting, no reordering,
no unrelated whitespace changes. New content (a missing `<title>`, a JSON-LD
block) is indented to match its surrounding siblings.

## Config file

See `config.example.json`. All fields are optional except that you need at
least one of `sourceDir` / `siteUrl`. CLI flags (`--dir`, `--url`,
`--pagespeed-key`, `--performance-threshold`) override the config file.

## Limitations (v1)

- HTML-only static scanning — no JSX/Vue/templating-engine awareness. For a
  site built with a framework, run the tool against the built/rendered HTML
  output, or use `--url` against a deployed preview.
- The regex-based HTML reader assumes reasonably well-formed markup (one
  `<title>`/`<head>`/`<body>` etc.) — it's built for typical local-business
  marketing sites, not arbitrary HTML.
- PageSpeed Insights requires a real, publicly reachable URL — it can't test
  localhost or unpublished files.
