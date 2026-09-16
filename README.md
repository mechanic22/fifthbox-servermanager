# FifthBox.ServerManager

A self-hosted PaaS for a small fleet: dockerized web apps fronted by nginx, dockerized services, and
native workloads on machines that can't join a Docker Swarm (Windows game servers, say) via a custom
agent. Swarm nodes and agent nodes appear as one system.

Blazor WASM client (`Client.Web`) served by a thin ASP.NET Core `Host`, domain logic in `App`, SQLite in
`Storage`. Architecture rules are in [`../CLAUDE.md`](../CLAUDE.md).

## Prerequisites

- **.NET 10 SDK**
- **Node.js 18+** (Tailwind build + task scripts)
- **Docker** with Swarm available
- Access to the **5thBox GitHub Packages feed** — a PAT with `read:packages` configured for the source
  named `GitHub` in [`nuget.config`](nuget.config). Never committed.
- The sibling repo `../fifthbox-materialcomponents` — `UseLocalMaterial` defaults to `true`, so a
  normal build compiles Material from source next door. Build with `-p:UseLocalMaterial=false` to use
  the published package instead (which is what `npm run publish` does).

## Run it locally

```bash
npm install
npm run dev
```

Open **https://localhost:7080** — browser auth needs HTTPS, the cookie is `Secure`. First run creates
the SQLite database and seeds a dev admin: **admin@demo.local** / **password123**.

Point `Docker:Endpoint` at your daemon in `src/Host/appsettings.Development.json` if it isn't at
`/var/run/docker.sock` (Rancher Desktop puts it under `~/.rd/`).

To run an agent against it: `npm run dev:agent`, after generating an enrollment key on the Cluster page (Add Node → Agent enrollment key).
Put it in `src/Agent/appsettings.Development.json` (gitignored) rather than the committed
`appsettings.json` — an enrollment key is enough to register an agent that runs arbitrary commands, so it
does not belong in source:

```json
{ "Agent": { "HostUrl": "https://localhost:7080", "EnrollmentKey": "..." } }
```

## Deploy it

### 1. Build and push the image

```bash
npm run image:push        # publish + multi-arch build (amd64/arm64) + push to the registry
```

Tags both `:<package.json version>` and `:latest`. Pin the **version** tag in production so you have a
rollback path. `npm run image:local` builds a single-arch image into this machine's Docker instead, so
you can `docker run` it here before shipping.

The build happens on your machine, not in the container — that keeps the private NuGet credential out
of the image entirely. `npm run publish` is the only sanctioned path because it forces
`-p:UseLocalFifthBox=false -p:UseLocalMaterial=false`; a bare `dotnet publish` would compile the sibling
Material working tree into the release.

### 2. Generate the two secrets — once, and keep them

```bash
openssl rand -base64 32     # Platform__Encryption__Key
openssl rand -base64 64     # Identity__Jwt__SecretKey
```

The Host refuses to start without both outside Development. **Back up the encryption key separately
from the database** — it decrypts stored registry passwords, and regenerating it orphans them
permanently. A key stored next to the data it protects isn't protecting much.

### 3. Start it

```bash
docker run -d --name fbsm-host --restart unless-stopped \
  -p 5080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v fbsm-data:/data \
  -e Platform__Encryption__Key=... \
  -e Identity__Jwt__SecretKey=... \
  -e Identity__DemoSeedAdmin__UserName=you@example.com \
  -e Identity__DemoSeedAdmin__Password='...' \
  docker.5thbox.com/fbsm/servermanager:0.1.0
```

**The seed admin is not optional on a first run.** Registration policy defaults to `AdminOnly` and
self-registration grants no roles, so without it you get a database with no administrator and every
screen that matters — cluster bootstrap included — returns 403. Seeding is idempotent and skipped when
the account already exists, so leaving the variables set does no harm; drop them once you're in if you
prefer.

The Docker socket is root-equivalent on that node — keep the Host behind nginx, never exposed raw.

### Signing in the first time

**The auth cookie is `Secure`, so signing in over plain `http://<server>:5080` will not work** — the
browser accepts the login and then refuses to send the cookie back, which looks exactly like being
logged straight out again. Until the Host is behind TLS, reach it over an SSH tunnel, because browsers
treat `localhost` as a secure origin:

```bash
ssh -L 5080:localhost:5080 you@your-server
```

Then open **http://localhost:5080**. Once you've bootstrapped and deployed nginx you can give the Host
its own route and hostname like any other workload — the managed install joins `fbsm-overlay`, so nginx
resolves it by service name. **Tick WebSockets on that route**: the UI runs three SignalR hubs (nodes,
workloads, agents) and without the upgrade headers live status and log streaming quietly stop working.

### 4. Bootstrap the cluster, then hand over to a swarm service

Sign in. Home shows a setup checklist and walks you through the order; the first step is
**Cluster → Bootstrap** (initialises the swarm if there isn't one, adopts it if there is, and creates
the overlay network — idempotent, never runs at startup).

**System** then reports this instance as unmanaged and prints a `docker service create` command with
your node id filled in. It removes the plain container first and reuses the same `fbsm-data` volume, so
your database and login survive the move. Substitute the two keys from step 2.

Running as a swarm service is optional — `docker run --restart unless-stopped` is a perfectly good
end state. It buys you the /system view of the Host alongside nginx; it does **not** buy availability,
because SQLite pins the Host to one node either way.

If both ever run at once, System shows a **split-brain** warning: two Hosts driving one Docker socket
and one database. Stop the unmanaged one.

### 5. Add a Windows machine (the agent)

Machines that can't join the swarm run workloads through the agent instead. Build the package on your
machine — it's self-contained, so the target needs no .NET installed:

```bash
npm run pack:agent          # -> publish-agent/fbsm-agent-win-x64-<version>.zip
```

On the Windows machine: unzip somewhere permanent (`C:\fbsm-agent`), then edit `appsettings.json` beside
the exe — set `HostUrl` and paste an `EnrollmentKey` generated on the **Agents** page.

```powershell
.\fbsm-agent.exe            # run once in a console: confirm it appears online on the Cluster page
.\fbsm-agent.exe install    # then, as Administrator, register it as a service
sc start FifthBoxAgent
```

The same executable is both a console app and a service — `install` only registers it, so the console run
is a genuine dry run. Enrolling writes `agent-credentials.json` **beside the exe**, and the service picks
up that same file, so testing first doesn't cost you a duplicate agent. `uninstall` removes the service.

**Updating an agent:** `sc stop FifthBoxAgent`, replace `fbsm-agent.exe`, `sc start FifthBoxAgent`. It
can't replace itself while running. Credentials and `agent-state.json` survive, so the agent keeps its
identity and re-attaches to workloads that are still running — **updating the agent doesn't restart your
game servers.** There's no Host↔agent version handshake yet, so update agents when you update the Host.

**Re-adding an agent you removed:** the credential file is what makes an upgrade painless, and it's the
same thing that makes removal a two-step job. The agent treats a stored credential as "already enrolled"
and never enrolls again, so if the Host no longer has its row — you removed it from the Cluster page, or
the database was reset or restored from an older backup — you have to delete `agent-credentials.json`
(and `agent-state.json` beside it) before it can enroll again. The Host rejects the stale credential and
the agent logs exactly that, then stops rather than reconnecting forever with something that can't work.

Re-enrolling is deliberately not automatic. The enrollment key stays valid, so an agent that re-enrolled
on rejection would reappear moments after you removed it, and Remove would only work if you got to the
machine first.

The agent authenticates with a bearer credential, so until the Host is behind TLS that credential is
plaintext on the wire. Fine on a LAN; don't run it across the internet before then.

### Updating

```bash
npm run image:push                                                      # from your machine
docker service update --image docker.5thbox.com/fbsm/servermanager:0.2.0 fbsm-host   # if managed
docker rm -f fbsm-host && docker run ... :0.2.0                         # if a plain container
```

#### Without the registry

When the registry is down or unreachable (it runs behind the same nginx this deploys, so a broken edge
can block the push that would fix it), build straight into the server's Docker instead. The `docker` CLI
can drive a remote daemon over SSH, so nothing is pushed or copied by hand:

```bash
npm run publish
DOCKER_HOST=ssh://you@your-server node scripts/image.mjs local
docker -H ssh://you@your-server service update \
  --image docker.5thbox.com/fbsm/servermanager:0.2.4 --no-resolve-image fbsm-host
```

The build runs on the server, so it's native to that machine's architecture, and the image lands in its
local store under the usual tag. SSH has to work non-interactively — key auth, and
`ssh you@your-server docker version` should succeed without a prompt — or the connection fails with a
misleading "make sure the URL is valid" error.

`--no-resolve-image` matters: without it the manager asks the registry what the tag points to and can pin
the old image. With it, the node's pull fails and it falls back to the local copy — which only exists on
the node you built on, so this suits a Host pinned to one node (it is, by SQLite). A plain container
just needs `docker run` with that tag. Push properly with `npm run image:push` once the registry is
reachable again.

Migrations run automatically at startup. The Host is **single-instance by design** — `AgentRegistry` is
in-memory with no SignalR backplane, so a second replica would break agent presence and command
dispatch.

### Backing up

Everything is in the `fbsm-data` volume — one SQLite file holding workloads, routes, agents, registries
and users, plus `/data/keys`, the data-protection keys that encrypt auth cookies. Those keys live on the
volume deliberately: left to the framework they go under `$HOME`, which in a container is the writable
layer, so every restart would issue new keys and sign everyone out. **Settings → Backups** takes a consistent snapshot of the live database, keeps the last
`Backup__Keep` of them (default 14) under `Backup__Directory` (`/data/backups` in the container), and
runs itself every `Backup__IntervalHours`. Download one from the same screen.

**A backup does not contain `Platform__Encryption__Key`.** Registry passwords are stored encrypted, so a
restore without the original key recovers the rows but not their contents. Keep the key with your other
credentials, not only in the service definition.

To restore, stop the Host first — replacing the file underneath a running process corrupts it:

```bash
docker service scale fbsm-host=0                       # or: docker rm -f fbsm-host
docker run --rm -v fbsm-data:/data -w /data alpine \
  sh -c 'cp backups/fbsm-20260101-030000.db fifth-box-server-manager.db && rm -f fifth-box-server-manager.db-wal fifth-box-server-manager.db-shm'
docker service scale fbsm-host=1
```

Deleting the stale `-wal`/`-shm` sidecars matters: left behind, SQLite replays them over the file you
just restored.

## Scripts

| Script | Does |
| --- | --- |
| `npm run dev` | Tailwind watch + `dotnet watch` on the Host (https profile) |
| `npm run dev:agent` | Run the node agent against a local Host |
| `npm run pack:agent` | Self-contained single-file agent for Windows, zipped for a target machine |
| `npm run build` | Tailwind CSS, then the whole solution |
| `npm run test` | Unit tests (MSTest + Moq) |
| `npm run image:push` | Publish + multi-arch image (amd64/arm64) pushed to the registry — the deploy step |
| `npm run image:local` | Publish + single-arch image into this machine's Docker, to test with `docker run` |
| `npm run publish` | Release publish of the Host into `./publish` (what both image scripts call) |
| `npm run build:mobile` / `dev:mobile` | MAUI head — needs mobile workloads |

## Notes

- Google OAuth is wired but off unless `Authentication:Google:*` is configured.
- TLS is terminated by the nginx ServerManager deploys, not by the Host — the Host speaks plain HTTP
  and honours forwarded headers.
- `src/Client.Mobile` is inherited scaffold from the app this repo started as, kept out of the solution
  and not yet reshaped for ServerManager. It doesn't affect the build.
