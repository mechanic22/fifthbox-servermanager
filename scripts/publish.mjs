// Publishes the Host for the container image.
//
// Two publishes, deliberately. The Host's publish emits the client's _framework assets correctly, but
// hands through the *unprocessed* index.html — its `_framework/blazor.webassembly#[.{fingerprint}].js`
// placeholder and empty `<script type="importmap">` never get rewritten, so the browser asks for
// `/_framework/blazor.webassembly` and gets a 404. Only the client's own publish runs that rewrite
// (OverrideHtmlAssetPlaceholders). Fingerprints are content-derived, so the file the client's
// index.html names is byte-identical to the one in the Host's output.

import { spawnSync } from 'node:child_process';
import { copyFileSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const output = join(root, 'publish');
const clientOutput = join(root, 'publish-client');

// Never the sibling working trees: a release must not bake in whatever is uncommitted next door.
const packageRefs = [
  '-p:UseLocalFifthBox=false',
  '-p:UseLocalIdentity=false',
  '-p:UseLocalMaterial=false',
  '-p:UseLocalEncryption=false',
];

function run(command, args) {
  console.log(`${command} ${args.join(' ')}`);
  const result = spawnSync(command, args, { cwd: root, stdio: 'inherit' });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

rmSync(output, { recursive: true, force: true });
rmSync(clientOutput, { recursive: true, force: true });

run('npm', ['run', 'css:build']);
run('dotnet', ['publish', 'src/Host', '-c', 'Release', '-o', output, ...packageRefs]);
run('dotnet', ['publish', 'src/Client.Web', '-c', 'Release', '-o', clientOutput, ...packageRefs]);

copyFileSync(join(clientOutput, 'wwwroot', 'index.html'), join(output, 'wwwroot', 'index.html'));
rmSync(clientOutput, { recursive: true, force: true });

console.log('index.html taken from the client publish (placeholders resolved).');
