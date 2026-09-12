// Builds the Host image tagged with both the package.json version and :latest.
//   local -> single-arch, into this machine's Docker so you can `docker run` it
//   push  -> multi-arch (amd64 + arm64), up to the registry for a server to pull
// Two modes rather than build-then-push because a multi-arch image can't be loaded locally at all.

import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const { version } = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

const image = process.env.FBSM_IMAGE ?? 'docker.5thbox.com/fbsm/servermanager';
const mode = process.argv[2];

const tags = [`${image}:${version}`, `${image}:latest`].flatMap(t => ['-t', t]);

if (mode !== 'push' && mode !== 'local') {
  console.error('usage: node scripts/image.mjs <local|push>');
  process.exit(1);
}

const args = mode === 'push'
  ? ['buildx', 'build', '--platform', 'linux/amd64,linux/arm64', '--push', ...tags, '.']
  : ['build', '--load', ...tags, '.'];

console.log(`docker ${args.join(' ')}`);
const result = spawnSync('docker', args, { cwd: root, stdio: 'inherit' });
process.exit(result.status ?? 1);
