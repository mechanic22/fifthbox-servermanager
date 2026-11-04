// ships package.json's version and points the running Host service at it
//   (no arg) -> multi-arch push to the registry, the swarm pulls it
//   direct   -> registry unreachable (it sits behind the nginx this deploys): builds on the server over ssh,
//               --no-resolve-image so swarm uses that local copy instead of asking the registry
// fbsm-stats moves to the same version when it's turned on, direct can't do that since other nodes couldn't pull it
// FBSM_DOCKER_HOST (ssh://root@sm.5thbox.com), FBSM_SERVICE (fbsm-host), FBSM_IMAGE, FBSM_STATS_SERVICE (fbsm-stats)
// and FBSM_STATS_IMAGE override the defaults.
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
const statsService = process.env.FBSM_STATS_SERVICE ?? 'fbsm-stats';
const statsImage = `${process.env.FBSM_STATS_IMAGE ?? 'docker.5thbox.com/fbsm/stats'}:${version}`;
const direct = process.argv[2] === 'direct';

function statsIsOn() {
  return spawnSync('docker', ['-H', dockerHost, 'service', 'inspect', statsService], {
    stdio: 'ignore',
    shell: process.platform === 'win32',
  }).status === 0;
}

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
  if (statsIsOn()) console.log(`${statsService} left as it is, it moves on the next registry deploy`);
} else {
  run('npm', ['run', 'image:push']);
  run('docker', ['-H', dockerHost, 'service', 'update', '--image', image, service]);
  if (statsIsOn()) {
    // with-registry-auth, every node has to be able to pull this
    run('docker', ['-H', dockerHost, 'service', 'update', '--with-registry-auth', '--image', statsImage, statsService]);
  }
}

console.log(`${service} is on ${image}`);
