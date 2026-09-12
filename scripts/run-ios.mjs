// Build the MAUI app for the iOS simulator, boot a simulator, install, and launch it — one command
// (`npm run dev:mobile:ios`). iOS dev is macOS-only, so this leans on `xcrun`/`open`.
//
// Convenience for the GPS demo: pre-grants location and sets a coordinate so "Capture" returns a fix
// immediately. Override the coordinate with SIM_LOCATION="lat,lon"; pick a simulator with SIM_UDID.
import { spawnSync } from 'node:child_process';
import { readdirSync } from 'node:fs';
import { join } from 'node:path';

const BUNDLE_ID = 'com.fifthbox.servermanager';
const PROJECT = 'src/Client.Mobile/Client.Mobile.csproj';
const RID = process.arch === 'arm64' ? 'iossimulator-arm64' : 'iossimulator-x64';
const LOCATION = process.env.SIM_LOCATION || '51.50073,-0.12463'; // London (Big Ben)

function run(cmd, args) {
  const res = spawnSync(cmd, args, { stdio: 'inherit' });
  if (res.status !== 0) process.exit(res.status ?? 1);
}

// Best-effort: ignore failures (e.g. "already booted", privacy pre-grant on an older Xcode).
function tryRun(cmd, args) {
  spawnSync(cmd, args, { stdio: 'ignore' });
}

function capture(cmd, args) {
  const res = spawnSync(cmd, args, { encoding: 'utf8' });
  if (res.status !== 0) throw new Error(`${cmd} ${args.join(' ')} failed:\n${res.stderr}`);
  return res.stdout;
}

console.log(`Building ${PROJECT} for ${RID}...`);
run('dotnet', ['build', PROJECT, '-p:MobileTfms=net10.0-ios', `-p:RuntimeIdentifier=${RID}`,
  '-p:UseLocalFifthBox=true', '-v', 'q', '-nologo']);

// Pick a simulator: an explicit SIM_UDID, else one already booted, else the newest available iPhone.
const devices = JSON.parse(capture('xcrun', ['simctl', 'list', 'devices', 'available', '--json'])).devices;
let udid = process.env.SIM_UDID;
if (!udid) {
  for (const list of Object.values(devices)) {
    const booted = list.find((d) => d.state === 'Booted');
    if (booted) { udid = booted.udid; break; }
  }
}
if (!udid) {
  for (const runtime of Object.keys(devices).filter((r) => r.includes('iOS')).sort().reverse()) {
    const iphone = devices[runtime].find((d) => d.name.startsWith('iPhone'));
    if (iphone) { udid = iphone.udid; break; }
  }
}
if (!udid) { console.error('No available iOS simulator found.'); process.exit(1); }

console.log(`Using simulator ${udid}`);
tryRun('xcrun', ['simctl', 'boot', udid]);
run('open', ['-a', 'Simulator']);
run('xcrun', ['simctl', 'bootstatus', udid]);

const outDir = `src/Client.Mobile/bin/Debug/net10.0-ios/${RID}`;
const appName = readdirSync(outDir).find((n) => n.endsWith('.app'));
if (!appName) { console.error(`No .app bundle in ${outDir}`); process.exit(1); }

tryRun('xcrun', ['simctl', 'terminate', udid, BUNDLE_ID]); // fresh start on re-runs
run('xcrun', ['simctl', 'install', udid, join(outDir, appName)]);
tryRun('xcrun', ['simctl', 'privacy', udid, 'grant', 'location-always', BUNDLE_ID]);
tryRun('xcrun', ['simctl', 'location', udid, 'set', LOCATION]);
console.log(`Location pre-set to ${LOCATION} (override with SIM_LOCATION="lat,lon").`);

run('xcrun', ['simctl', 'launch', udid, BUNDLE_ID]);
console.log('Launched. Sign in, open a contact, and Capture location.');
