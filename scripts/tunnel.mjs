// Reaches a workload that publishes no ports, by running a throwaway socat container on the node.
// The docker CLI is a thin client, so DOCKER_HOST=ssh:// starts it there without anything being
// installed or copied. Foreground on purpose: Ctrl-C stops it and --rm removes it, so there is no
// cleanup step to forget. The range= clause is what makes this safer than publishing a port —
// only the caller's address completes a handshake.

import { spawnSync } from 'node:child_process';

const usage = 'usage: npm run tunnel -- <workload> <containerPort> [hostPort] [--any]   (TCP only)';

const argv = process.argv.slice(2);
const any = argv.includes('--any');
const [workload, containerPort, hostPort = containerPort] = argv.filter(a => a !== '--any');

if (!workload || !containerPort) {
  console.error(usage);
  process.exit(1);
}

for (const port of [containerPort, hostPort]) {
  if (!/^\d+$/.test(port) || +port < 1 || +port > 65535) {
    console.error(`'${port}' is not a port.\n${usage}`);
    process.exit(1);
  }
}

const node = process.env.FBSM_NODE;
if (!node) {
  console.error('Set FBSM_NODE to the node to tunnel through, e.g.\n  export FBSM_NODE=you@node-01');
  process.exit(1);
}

const network = process.env.FBSM_NETWORK ?? 'fbsm-overlay';
const ttl = process.env.FBSM_TUNNEL_TTL ?? '1800';
const service = workload.startsWith('fbsm--') ? workload : `fbsm--${workload}`;

const allow = any ? null : process.env.FBSM_ALLOW_IP ?? await publicIp();
if (allow?.includes(':')) {
  console.error(`Detected an IPv6 address (${allow}), which this can't lock against.`);
  console.error('Set FBSM_ALLOW_IP to your IPv4 address, or pass --any to skip the lock.');
  process.exit(1);
}

const listen = ['tcp-listen:' + containerPort, 'fork', 'reuseaddr', allow && `range=${allow}/32`]
  .filter(Boolean)
  .join(',');

const socat = `timeout ${ttl} socat ${listen} tcp-connect:${service}:${containerPort}`;

const args = [
  'run', '--rm', '--init',
  '--network', network,
  '-p', `${hostPort}:${containerPort}`,
  '--entrypoint', 'sh', 'alpine/socat',
  '-c', socat,
];

const host = node.includes('@') ? node.split('@')[1] : node;

console.log(allow
  ? `Locked to ${allow} — nothing else can connect.`
  : 'WARNING: --any, so anything that can reach the node can connect.');
console.log(`${host}:${hostPort} -> ${service}:${containerPort}`);
console.log(`Closes after ${ttl}s, or on Ctrl-C.\n`);
console.log(`DOCKER_HOST=ssh://${node} docker ${args.slice(0, -1).join(' ')} '${socat}'\n`);

const result = spawnSync('docker', args, {
  stdio: 'inherit',
  env: { ...process.env, DOCKER_HOST: `ssh://${node}` },
});

process.exit(result.status ?? 1);

async function publicIp() {
  try {
    const res = await fetch('https://api.ipify.org');
    if (!res.ok) {
      throw new Error(`HTTP ${res.status}`);
    }
    return (await res.text()).trim();
  } catch (err) {
    console.error(`Couldn't detect your public IP (${err.message}).`);
    console.error('Set FBSM_ALLOW_IP, or pass --any to skip the lock.');
    process.exit(1);
  }
}
