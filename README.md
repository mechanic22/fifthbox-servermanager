# FifthBox.ServerManager

A self-hosted PaaS for a small fleet: dockerized web apps behind nginx, dockerized services, and native
workloads on machines that can't join the swarm (Windows game servers) through an agent. Architecture
rules are in [`../CLAUDE.md`](../CLAUDE.md). `npm run` lists every task.

## Prerequisites

- .NET 10 SDK, Node 18+, Docker with Swarm
- A PAT with `read:packages` for the `GitHub` source in [`nuget.config`](nuget.config). Machine config, never committed.
- `../fifthbox-materialcomponents` checked out: dev builds compile Material from it (`UseLocalMaterial`), `npm run publish` uses the package.

## Run it locally

```bash
npm install
npm run dev
```

Open <https://localhost:7080> (HTTPS is required, the cookie is `Secure`) and sign in as
**admin@demo.local** / **password123**. Set `Docker:Endpoint` in `src/Host/appsettings.Development.json` if
your daemon isn't at `/var/run/docker.sock` (Rancher Desktop: under `~/.rd/`).

For a local agent, make an enrollment key on the Cluster page and put it in the gitignored
`src/Agent/appsettings.Development.json`, then `npm run dev:agent`:

```json
{ "Agent": { "HostUrl": "https://localhost:7080", "EnrollmentKey": "..." } }
```

## First install

Push the image with `npm run image:push`, then generate two secrets and keep them:

```bash
openssl rand -base64 32     # Platform__Encryption__Key, decrypts stored registry passwords. back it up apart from the data
openssl rand -base64 64     # Identity__Jwt__SecretKey
```

```bash
docker run -d --name fbsm-host --restart unless-stopped \
  -p 5080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v fbsm-data:/data \
  -e Platform__Encryption__Key=... \
  -e Identity__Jwt__SecretKey=... \
  -e Identity__DemoSeedAdmin__UserName=you@example.com \
  -e Identity__DemoSeedAdmin__Password='...' \
  docker.5thbox.com/fbsm/servermanager:<version>
```

- The seed admin is required on a first run, without it nobody can bootstrap. It's skipped once the account exists.
- The Docker socket is root on that node. Keep the Host behind nginx, never exposed raw.
- Plain `http://<server>:5080` can't sign in (the cookie is `Secure`). Until it has TLS, tunnel:
  `ssh -L 5080:localhost:5080 you@your-server` and open <http://localhost:5080>.

Then follow the checklist on Home: **Cluster → Bootstrap**, deploy nginx, and give the Host its own route
and hostname. **Tick WebSockets on that route**, the live status and logs run over SignalR. **System**
offers a `docker service create` command to turn the container into a swarm service on the same
`fbsm-data` volume (optional). If it warns about split-brain, stop the plain container.

## Updating

Bump `version` in `package.json`, then:

```bash
npm run deploy            # push to the registry and update the fbsm-host service
npm run deploy:direct     # registry unreachable: build on the server over ssh instead
```

Both talk to `ssh://root@sm.5thbox.com` with key auth (override with `FBSM_DOCKER_HOST` / `FBSM_SERVICE`).
Migrations run at startup. The Host is single-instance by design, never scale it past 1.

## Agents (Windows)

```bash
npm run pack:agent        # -> publish-agent/fbsm-agent-win-x64-<version>.zip, self-contained
```

Unzip to somewhere permanent (`C:\fbsm-agent`), set `HostUrl` and an `EnrollmentKey` from the Agents page
in `appsettings.json`, then:

```powershell
.\fbsm-agent.exe            # console run first, check it shows online
.\fbsm-agent.exe install    # as Administrator, registers the service (uninstall removes it)
sc start FifthBoxAgent
```

- **Update:** `sc stop FifthBoxAgent`, replace the exe, `sc start FifthBoxAgent`. Running workloads keep going. Update agents with the Host.
- **Re-adding a removed agent:** delete `agent-credentials.json` and `agent-state.json` beside the exe first, it won't re-enroll on its own.
- The agent's credential is plaintext on the wire until the Host has TLS. Fine on a LAN, not across the internet.

## Backups

**Settings → Backups** snapshots the database on a timer (`Backup__IntervalHours`, keeps `Backup__Keep`,
default 14) and lets you download one. Backups don't include `Platform__Encryption__Key`, without it
registry passwords can't be recovered. To restore, stop the Host first:

```bash
docker service scale fbsm-host=0
docker run --rm -v fbsm-data:/data -w /data alpine \
  sh -c 'cp backups/<file>.db fifth-box-server-manager.db && rm -f fifth-box-server-manager.db-wal fifth-box-server-manager.db-shm'
docker service scale fbsm-host=1
```

## Notes

- Google sign-in is off unless `Authentication:Google:*` is configured.
- `src/Client.Mobile` is leftover scaffold, outside the solution.
