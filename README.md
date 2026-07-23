# GithubScout

GithubScout watches a list of GitHub pull requests and branches, polls them on an interval,
shows you what changed (state, CI checks, reviews, new commits), and keeps your machine
from sleeping / dropping its internet connection while it runs.

## Why this exists

This started from a genuinely annoying real-world moment: I was waiting on a PR review, the
review actually came in late at night, my PC went to sleep / lost its connection, and I
completely missed it — the PR sat there overnight looking "done" when it was actually still
blocked. GithubScout is the "nicer" fix for that: point it at the PRs/branches you care about,
leave it running, and it will (a) keep your machine awake so you don't silently drop off the
grid, and (b) keep polling GitHub and surfacing exactly what's still blocking a merge — down to
*which* CODEOWNERS team a stale-looking "Approved" badge is actually still waiting on — so you
never again mistake "somebody approved it" for "it's actually mergeable."

It ships three ways to see what's going on:

- **Console dashboard** — a live-updating table in the terminal (via Spectre.Console).
- **Local website** — `http://localhost:5232` (or whatever port you run it on), auto-refreshing,
  lets you add/remove watched items from the browser (including by just pasting a PR URL).
- **Activity log** — structured log entries for every detected change (state/CI/review/commit).
- **Accurate, CODEOWNERS-aware review state** — uses GitHub's own authoritative
  `reviewDecision` (via GraphQL), not just "did anyone approve it," so a stale-looking
  "Approved" badge never hides a still-pending required CODEOWNERS team review. When that
  happens, GithubScout names the exact blocking team (e.g. `org/some-team`) by matching the
  PR's changed files against the repo's `CODEOWNERS` file.

## Architecture

- **GithubScout.Core** — everything platform/host-agnostic: models, the GitHub polling
  service, change detection, the watchlist store, and the keep-awake abstraction. All the
  interesting logic lives here and is unit tested.
- **GithubScout.Web** — the ASP.NET Core host: minimal API, static website, and the console
  dashboard renderer. Composition root wiring everything from `GithubScout.Core` together.
- **GithubScout.Tests** — xUnit tests covering change detection, the watchlist store, the
  in-memory state repository, container/OS detection, token resolution, and the polling
  orchestration logic (via mocked dependencies — no real network/OS calls in tests).

Everything depends on interfaces (`IGitHubWatcherClient`, `IWatchlistStore`,
`IWatchStateRepository`, `IKeepAwakeService`, `INotifier`, `ITokenProvider`) so each concern
can be tested and swapped independently (SOLID).

## Cross-platform behavior

| Feature | Windows | Linux | macOS | Docker container |
|---|---|---|---|---|
| Polling / change detection | ✅ | ✅ | ✅ | ✅ |
| Console dashboard | ✅ | ✅ | ✅ | ✅ (skipped if output isn't a real terminal) |
| Local website | ✅ | ✅ | ✅ | ✅ |
| Toast notifications | ✅ | ➖ no-op | ➖ no-op | ➖ no-op |
| Keep machine awake | ✅ `SetThreadExecutionState` | ✅ `systemd-inhibit` | ✅ `caffeinate` | ❌ no-op (a container has no access to the host's power state) |

Because keep-awake needs the host OS, run GithubScout **natively** when you specifically need
"don't let my PC sleep / drop the connection." Docker is great for the polling + dashboard
experience during development/testing, but won't stop the host machine from sleeping.

## Getting started

### 1. Get a GitHub token

Create a [personal access token](https://github.com/settings/tokens) (classic or fine-grained,
`repo` read access is enough for private repos; none needed for public repos, but you'll hit
much lower unauthenticated rate limits without one).

### 2. Store the token (cross-platform, never touches the repo)

**Option A — .NET User Secrets (recommended):**

```powershell
cd src/GithubScout.Web
dotnet user-secrets init
dotnet user-secrets set "GithubScout:GitHubToken" "ghp_xxxxxxxx"
```

This stores the token outside the project folder, in your OS user profile:
- Windows: `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`
- Linux/macOS: `~/.microsoft/usersecrets/<id>/secrets.json`

**Option B — environment variable (used as a fallback, e.g. in Docker/CI):**

```powershell
$env:GITHUB_TOKEN = "ghp_xxxxxxxx"     # PowerShell
```
```bash
export GITHUB_TOKEN=ghp_xxxxxxxx        # bash/zsh
```

### 3. Set up your watchlist

Copy `src/GithubScout.Web/watchlist.sample.json` to `src/GithubScout.Web/watchlist.json`
(gitignored) and edit it:

```json
[
  { "id": "dotnet/runtime/pr/12345", "owner": "dotnet", "repo": "runtime", "kind": "PullRequest", "pullRequestNumber": 12345 },
  { "id": "dotnet/runtime/branch/main", "owner": "dotnet", "repo": "runtime", "kind": "Branch", "branchName": "main", "alias": "runtime main branch" }
]
```

**Easier: just paste a URL.** From the website's "+ Add item" dialog, paste a full PR URL
(`https://github.com/owner/repo/pull/123`) or branch URL
(`https://github.com/owner/repo/tree/branch-name`) into the main field — GithubScout parses
owner/repo/number for you, so there's no manual field-entry (and no risk of the
owner/repo/number typo mismatch that plain manual entry is prone to). The old manual
owner/repo/kind/number fields are still there under "Advanced" if you ever need them.

### 4. Run it

**Recommended — just use the default launch profile:**

```powershell
cd src/GithubScout.Web
dotnet run
```

That's it. The default launch profile sets `ASPNETCORE_ENVIRONMENT=Development`, which is what
makes `dotnet user-secrets` actually load your token — so no extra steps needed.

**If instead you run with `--no-launch-profile`** (or otherwise bypass `launchSettings.json`),
you must set the Development environment yourself first, or your user-secrets token silently
won't load (this shows up confusingly as a GitHub rate-limit/`NotFoundException` error, not an
auth error):

```powershell
cd src/GithubScout.Web
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-launch-profile
```

**A few rules for a smooth run, learned the hard way:**

- Only run **one instance at a time** per machine. A second `dotnet run`/`dotnet build` while
  one is already running will fail with a file-lock error (`MSB3027`/`MSB3021`,
  `GithubScout.Core.dll` in use) — stop the previous process first
  (`Get-Process -Name dotnet`, then `Stop-Process -Id <pid>`).
- Double-check `GithubScout:GitHubToken` is spelled exactly right in user secrets
  (`dotnet user-secrets list`) — a typo'd key silently means "no token," which Octokit reports
  as a confusing 404 on private repos instead of an auth error.
- `watchlist.json` fields must be the actual `owner`/`repo`/`pullRequestNumber` — don't paste a
  full PR URL into the `repo` field. Use the URL-paste "+ Add item" flow above to avoid this.
- Keep-awake only works when run natively (Windows/Linux/macOS) — it's a deliberate no-op
  inside Docker, since a container can't touch the host's power state. Run it natively if
  "don't let my PC sleep tonight" is the whole point.
- The token needs at least `repo` read access (and for GraphQL review-decision checks on
  private repos, that's included in `repo` scope already — no extra scope needed).

Then open the printed local URL (e.g. `http://localhost:5232`) in a browser, and watch the
console for the live dashboard.

### 5. Configuration (`appsettings.json` → `GithubScout` section)

| Setting | Default | Meaning |
|---|---|---|
| `PollIntervalSeconds` | 120 | How often to re-check every watched item |
| `WatchlistFilePath` | `watchlist.json` | Path to the watchlist file (relative to content root) |
| `EnableToastNotifications` | true | Windows-only toast popups on change |
| `EnableKeepAwake` | true | Keep the host machine awake while running |
| `MaxActivityEntries` | 200 | How many activity feed entries to retain in memory |

## Running in Docker

```bash
cp src/GithubScout.Web/watchlist.sample.json watchlist.json
export GITHUB_TOKEN=ghp_xxxxxxxx
docker compose up --build
```

The website will be available at `http://localhost:8080`. Remember: **keep-awake is a no-op
inside the container** — run natively on Windows alongside/instead of Docker if you need that.

## Running the tests

```powershell
dotnet test
```

## Project layout

```
GithubScout.sln
src/
  GithubScout.Core/     # models, abstractions, services (platform-agnostic, unit tested)
  GithubScout.Web/      # ASP.NET Core host: API + website + console dashboard
tests/
  GithubScout.Tests/    # xUnit tests
Dockerfile
docker-compose.yml
```
