// Packages the node agent for a target machine: self-contained single-file publish, then a zip.
// Self-contained so the target needs no .NET install and can't drift from the Host's runtime version.

import { spawnSync } from 'node:child_process';
import { readFileSync, rmSync, mkdirSync, existsSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import AdmZip from 'adm-zip';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const { version } = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

const rid = process.argv[2] ?? 'win-x64';
const outDir = join(root, 'publish-agent', rid);
const zipPath = join(root, 'publish-agent', `fbsm-agent-${rid}-${version}.zip`);

rmSync(outDir, { recursive: true, force: true });
mkdirSync(outDir, { recursive: true });

const publish = spawnSync('dotnet', [
  'publish', 'src/Agent',
  '-c', 'Release',
  '-r', rid,
  '--self-contained', 'true',
  '-p:PublishSingleFile=true',
  '-p:UseLocalFifthBox=false',
  '-o', outDir,
], { cwd: root, stdio: 'inherit' });

if (publish.status !== 0) {
  process.exit(publish.status ?? 1);
}

// Refuse to ship this developer's enrolled identity. The csproj already excludes these, but a package
// that leaks a credential to every target machine is not something to leave resting on one MSBuild line.
for (const leaked of ['agent-credentials.json', 'agent-state.json']) {
  if (existsSync(join(outDir, leaked))) {
    console.error(`\nRefusing to package: ${leaked} is in the publish output.`);
    process.exit(1);
  }
}

// Flat archive — the exe and appsettings.json sit at the root so there's nothing to unnest on the target.
const zip = new AdmZip();
zip.addLocalFolder(outDir, '', name => !name.endsWith('.pdb'));
zip.writeZip(zipPath);

console.log(`\n${zipPath}`);
console.log(readdirSync(outDir).filter(f => !f.endsWith('.pdb')).join('  '));
