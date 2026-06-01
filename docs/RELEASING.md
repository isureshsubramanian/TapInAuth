# Releasing TapInAuth

End-to-end runbook for cutting a public NuGet release. This document is the source of truth — update it whenever the process changes.

The release pipeline is driven by `.github/workflows/release.yml`. On a `v*` tag push (or a `workflow_dispatch` rerun), the workflow restores, builds with warnings-as-errors, runs tests, packs every project marked `IsPackable=true`, pushes to nuget.org AND GitHub Packages, and creates a GitHub Release with auto-generated notes.

Versioning is driven by [MinVer](https://github.com/adamralph/minver) reading the most recent `v*` tag. Pre-release tags (`v1.0.0-rc.1`) get marked as pre-release on GitHub automatically.

---

## T-1 day — pre-flight

A release the day before the marketing push gives you 24 hours to catch a regression with no audience watching.

- [ ] **Bump and freeze the changelog.** Edit `CHANGELOG.md` (if you maintain one) or `marketing/launch/release-notes-vX.Y.Z.md` to match the actual shipping scope. No "TBD" entries.
- [ ] **Run the full local validation pass.**
  ```bash
  dotnet restore
  dotnet build -c Release -warnaserror
  dotnet test  -c Release
  dotnet pack  -c Release --output ./artifacts
  ```
  All three must succeed cleanly. If anything is yellow, treat it as red.
- [ ] **Verify packages.** `ls ./artifacts/*.nupkg` should list one `.nupkg` (and one `.snupkg`) per package in `src/`. Open one in 7zip / dotPeek and confirm the `README.md` and license are present.
- [ ] **Run every sample.** At minimum:
  ```bash
  dotnet run --project samples/Mvc.Quickstart
  dotnet run --project samples/SaaS.MultiTenant
  dotnet run --project samples/Identity.Sample
  dotnet run --project samples/BlazorServer.Quickstart
  ```
  Sign in once via magic-link, once via OTP, once via passkey (if you have a Yubikey/Touch ID/Windows Hello). The Hermex inbox at `/hermex` shows the captured email.
- [ ] **Check the docs build.** Read every page in `docs/` top to bottom. Broken `[link](...)` references and stale code snippets are embarrassing on launch day.
- [ ] **NUGET_API_KEY secret is set** under `Settings → Secrets → Actions`. The workflow no-ops the push step if it's missing (we'd rather skip than fail), so you must verify it's present.
- [ ] **Dry-run the release workflow against a personal NuGet account first.** Create a tag like `v0.5.0-test.1` and push to a fork or feature branch with `NUGET_API_KEY` pointing at a sandbox account. The workflow will:
  - Build + test
  - Pack
  - Push (with `--skip-duplicate`) — confirms credentials work and packages are accepted
  - Create a draft GitHub Release
- [ ] If the dry-run pushed anything to nuget.org by accident, **unlist** those packages immediately (nuget.org has no delete; unlist is the closest).

## Release day

- [ ] **Final `main` commit landed.** No PRs in flight that touch shipping code.
- [ ] **Tag the release.**
  ```bash
  git tag -a v0.5.0 -m "TapInAuth v0.5.0 — first public release"
  git push origin v0.5.0
  ```
- [ ] **Watch the workflow.** [Actions tab](https://github.com/tapinauth/tapinauth/actions). It should take ~5 min on a quiet runner. If anything fails:
  - Build/test failure → fix on main, delete the tag (`git tag -d v0.5.0 && git push origin :v0.5.0`), retag.
  - Push failure (API key, rate limit, package collision) → fix the secret/conflict, then `workflow_dispatch` the same tag from the Actions tab.
- [ ] **Verify nuget.org.** Each package indexed within ~5 min. Look for the package page on `nuget.org/packages/TapInAuth.AspNetCore` etc.
- [ ] **Verify the GitHub Release.** [Releases tab](https://github.com/tapinauth/tapinauth/releases). Auto-generated notes should be reasonable; if they're rough, paste over with the curated text from `marketing/launch/release-notes-vX.Y.Z.md`.
- [ ] **Install test.** From a fresh `dotnet new web` project:
  ```bash
  dotnet add package TapInAuth.AspNetCore
  dotnet add package TapInAuth.Store.EntityFrameworkCore
  dotnet add package TapInAuth.UI
  dotnet add package TapInAuth.Email.Smtp
  dotnet restore
  ```
  Must succeed without "package not found" or version-conflict errors.

## Launch comms (T+0 to T+2 hr)

- [ ] **Twitter/X thread** — `marketing/launch/twitter-thread.md`. Schedule for ~9am PT.
- [ ] **LinkedIn post** — `marketing/launch/linkedin-post.md`. Post simultaneously with Twitter.
- [ ] **Blog announcement** — `marketing/launch/blog-announcement.md`. Publish on dev.to / Medium / personal blog. Quote-tweet the link from the dev account.
- [ ] **Show HN** — submit at T+2 hr (~11am PT for best traction). Use the title/URL/body from `marketing/launch/hacker-news.md` exactly. Do NOT ask for upvotes anywhere.
- [ ] **/r/dotnet** — post in the afternoon (the morning crowd has moved on; the EU/late-PT crowd will see it fresh).

## Post-launch (T+1 day)

- [ ] **Reply to every comment** on HN, Reddit, Twitter, LinkedIn. First 24 hours is when momentum compounds.
- [ ] **Open a GitHub Discussion** titled "v0.5.0 feedback" to give long-form comments a home.
- [ ] **DM .NET podcasters / newsletter editors**: `dotnet-weekly`, `Daily.dev`, `The .NET Newsletter`. Pitch the angle (UI included, multi-tenant default).
- [ ] **Email the .NET Foundation** at `info@dotnetfoundation.org` describing the project for incubation conversations. Reference the [governance doc](https://github.com/dotnet/foundation/blob/main/governance/PROJECTS.md).

## Rollback

If a critical bug surfaces post-release (security issue, broken DI graph, broken EF migration):

1. **Unlist** the affected package versions on nuget.org. Each package page has an "Unlist" link visible to owners.
2. **Push a patch** (`v0.5.1`) ASAP — the unlist is invisible to existing installs but blocks new ones.
3. **Add a deprecation notice** to the GitHub Release.
4. **Postmortem** in `docs/postmortems/` (create the directory if it doesn't exist).

We do not have a `delete` option on nuget.org — only `unlist`. Anyone who pinned the bad version still has it. Patch fast.

## What's automated vs manual

| Step                                  | Automated? |
| ------------------------------------- | ---------- |
| Build + test on every PR              | ✅ CI workflow |
| Build + test on every tag push        | ✅ Release workflow |
| NuGet pack                            | ✅ Release workflow |
| Push to nuget.org                     | ✅ Release workflow (skipped if `NUGET_API_KEY` missing) |
| Push to GitHub Packages               | ✅ Release workflow |
| GitHub Release creation               | ✅ Release workflow |
| Release notes drafting                | ⬜ Manual — write `release-notes-vX.Y.Z.md` first |
| Social posts                          | ⬜ Manual — copy from `marketing/launch/` |
| Sample-app sanity check               | ⬜ Manual — listed in pre-flight |
| Incident response on bad release      | ⬜ Manual — runbook above |
