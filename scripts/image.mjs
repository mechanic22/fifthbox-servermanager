// Builds the images tagged with both the package.json version and :latest.
//   local -> single-arch, into this machine's Docker so you can `docker run` it
//   push  -> multi-arch (amd64 + arm64), up to the registry for a server to pull. fbsm-stats too, every node pulls it
//   push-stats -> just fbsm-stats
// Two modes rather than build-then-push because a multi-arch image can't be loaded locally at all.

import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const { version } = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

const image = process.env.FBSM_IMAGE ?? 'docker.5thbox.com/fbsm/servermanager';
const statsImage = process.env.FBSM_STATS_IMAGE ?? 'docker.5thbox.com/fbsm/stats';
const mode = process.argv[2];

const tagsFor = name => [`${name}:${version}`, `${name}:latest`].flatMap(t => ['-t', t]);

function build(args) {
  console.log(`docker ${args.join(' ')}`);
  const result = spawnSync('docker', args, { cwd: root, stdio: 'inherit' });
  if (result.status !== 0) process.exit(result.status ?? 1);
}

if (mode !== 'push' && mode !== 'push-stats' && mode !== 'local') {
  console.error('usage: node scripts/image.mjs <local|push|push-stats>');
  process.exit(1);
}

const push = ['buildx', 'build', '--platform', 'linux/amd64,linux/arm64', '--push'];

if (mode === 'push') {
  build([...push, ...tagsFor(image), '.']);
  build([...push, ...tagsFor(statsImage), '-f', 'Dockerfile.stats', '.']);
} else if (mode === 'push-stats') {
  build([...push, ...tagsFor(statsImage), '-f', 'Dockerfile.stats', '.']);
} else {
  build(['build', '--load', ...tagsFor(image), '.']);
}
