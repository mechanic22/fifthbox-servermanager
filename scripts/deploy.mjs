// ships package.json's version and points the running Host service at it
//   (no arg) -> multi-arch push to the registry, the swarm pulls it
//   direct   -> registry unreachable (it sits behind the nginx this deploys): builds on the server over ssh,
//               --no-resolve-image so swarm uses that local copy instead of asking the registry
// FBSM_DOCKER_HOST (ssh://root@sm.5thbox.com), FBSM_SERVICE (fbsm-host) and FBSM_IMAGE override the defaults.
// ssh has to work without a prompt, a password prompt shows up as "make sure the URL is valid"

import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const { version } = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

const dockerHost = process.env.FBSM_DOCKER_HOST ?? 'ssh://root@sm.5thbox.com';
const service = process.env.FBSM_SERVICE ?? 'fbsm-host';
const image = `${process.env.FBSM_IMAGE ?? 'docker.5thbox.com/fbsm/servermanager'}:${version}`;
const direct = process.argv[2] === 'direct';

function run(command, args, env = {}) {
  console.log(`> ${command} ${args.join(' ')}`);
  const result = spawnSync(command, args, {
    cwd: root,
    stdio: 'inherit',
    env: { ...process.env, ...env },
    shell: process.platform === 'win32',
  });
  if (result.status !== 0) process.exit(result.status ?? 1);
}

if (direct) {
  run('npm', ['run', 'publish']);
  run('node', ['scripts/image.mjs', 'local'], { DOCKER_HOST: dockerHost });
  run('docker', ['-H', dockerHost, 'service', 'update', '--image', image, '--no-resolve-image', service]);
} else {
  run('npm', ['run', 'image:push']);
  run('docker', ['-H', dockerHost, 'service', 'update', '--image', image, service]);
}

console.log(`${service} is on ${image}`);
