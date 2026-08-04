import fs from 'node:fs';
import path from 'node:path';

const DEFAULTS = {
  businessName: '',
  serviceArea: '',
  siteUrl: '',
  sourceDir: '',
  publicDir: 'public',
  pagespeedApiKey: '',
  thresholds: {
    performanceMobile: 50,
    titleMinLength: 30,
    titleMaxLength: 60,
    metaDescriptionMinLength: 50,
    metaDescriptionMaxLength: 160,
    oversizedImageKb: 500
  }
};

export function loadConfig({ configPath, cliOverrides = {} }) {
  let fileConfig = {};

  if (configPath) {
    const resolved = path.resolve(configPath);
    if (!fs.existsSync(resolved)) {
      throw new Error(`Config file not found: ${resolved}`);
    }
    fileConfig = JSON.parse(fs.readFileSync(resolved, 'utf8'));
  }

  const merged = {
    ...DEFAULTS,
    ...fileConfig,
    thresholds: { ...DEFAULTS.thresholds, ...(fileConfig.thresholds || {}) }
  };

  if (cliOverrides.dir) merged.sourceDir = cliOverrides.dir;
  if (cliOverrides.url) merged.siteUrl = cliOverrides.url;
  if (cliOverrides.pagespeedApiKey) merged.pagespeedApiKey = cliOverrides.pagespeedApiKey;
  if (cliOverrides.performanceThreshold != null) {
    merged.thresholds.performanceMobile = Number(cliOverrides.performanceThreshold);
  }

  if (merged.sourceDir) {
    merged.sourceDir = path.resolve(merged.sourceDir);
  }

  if (!merged.sourceDir && !merged.siteUrl) {
    throw new Error(
      'No target specified. Provide --dir <path>, --url <url>, or both, either on the ' +
        'command line or in the config file.'
    );
  }

  return merged;
}
