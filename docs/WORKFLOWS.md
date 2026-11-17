# GitHub Actions Workflows

This repository uses three GitHub Actions workflows to keep pull requests and releases consistent and fully automated:

| Workflow | File | Trigger | Purpose |
| --- | --- | --- | --- |
| **Version check** | [`.github/workflows/version-check.yml`](../.github/workflows/version-check.yml) | Pull request targeting `main` | Blocks merging source changes unless the plugin version in `meta.json` has been bumped. |
| **PR template check** | [`.github/workflows/pr-template-check.yml`](../.github/workflows/pr-template-check.yml) | Pull request targeting `main` (incl. description edits) | Requires the PR description to tick a Type of Change, state the new version, and disclose AI use. |
| **Release** | [`.github/workflows/release.yml`](../.github/workflows/release.yml) | Push to `main` | Builds the plugin, publishes a GitHub Release with the zip, and updates the Jellyfin plugin repository `manifest.json`. |

Together they enforce a simple rule: **every change to the plugin that lands on `main` ships as a new version.**

```
 feature branch ──PR──▶ Version check (must pass) ──merge──▶ main
                                                              │
                                                              ▼
                                                   Release workflow
                                       build → zip → GitHub Release v<version>
                                                              │
                                                              ▼
                                       PR "chore: update manifest for v<version>"
                                         (auto-merged into main → manifest.json)
                                                              │
                                                              ▼
                                       Release workflow runs again, sees the
                                       release already exists, and skips.
```

---

## The source of truth: `JellyfinDiscordAuth/meta.json`

Both workflows read from [`JellyfinDiscordAuth/meta.json`](../JellyfinDiscordAuth/meta.json). The fields that matter to CI are:

| Field | Used by | How |
| --- | --- | --- |
| `version` | Both | Version check compares it against `main`; Release uses it for the tag (`v<version>`), release title, branch name and manifest entry. |
| `changelog` | Release | Becomes the GitHub Release notes and the `changelog` of the new manifest entry. |
| `timestamp` | Release | Overwritten with the build time (UTC) before building, so the copy inside the zip is stamped. |

Versions are four-part (`MAJOR.MINOR.PATCH.BUILD`, e.g. `1.2.0.0`), matching Jellyfin's plugin versioning.

---

## Version check (`version-check.yml`)

### When it runs

On every `pull_request` event whose base branch is `main` (opened, synchronized, reopened). There is intentionally **no `paths:` filter** on the trigger: the workflow always runs and always reports a status. That way it can be a *required* status check in branch protection without PRs that don't touch source (for example, the automated manifest PRs) getting stuck in a "pending / expected" state forever. The path filtering happens inside the job instead.

### Steps

1. **Checkout** – `actions/checkout@v4` with `fetch-depth: 0`, so the full history (including `origin/main`) is available for diffing.

2. **Detect whether source files changed** – runs
   ```bash
   git diff --name-only origin/main...HEAD
   ```
   The three-dot form lists files changed on the PR branch since it diverged from `main` (ignoring anything merged to `main` since). If any path matches
   ```
   ^JellyfinDiscordAuth/          # anything in the plugin project
   ^[^/]+\.(sln|ruleset|ps1)$     # root-level solution, ruleset, or PowerShell scripts
   ```
   the output `requires_check` is set to `true`; otherwise `false`.
   Changes to `README.md`, `docs/`, `manifest.json`, or `.github/` alone do **not** require a version bump.

3. **Get versions** *(only if `requires_check == true`)* – reads `.version` from:
   - the PR's `JellyfinDiscordAuth/meta.json` (working tree), and
   - `main`'s copy via `git show origin/main:JellyfinDiscordAuth/meta.json`.

4. **Verify version is incremented** *(only if `requires_check == true`)* – fails the check if:
   - the PR version **equals** `main`'s version, or
   - the PR version is **lower** than `main`'s version. This is determined with `sort -V` (natural version sort): if the PR version sorts first, it is the lower one.

   Failures are emitted as `::error file=JellyfinDiscordAuth/meta.json::…` annotations, so the error appears inline on `meta.json` in the PR's "Files changed" tab. On success it logs e.g. `Version check passed: 1.1.0.2 → 1.2.0.0`.

If `requires_check` is `false`, steps 3–4 are skipped and the job succeeds immediately.

### Permissions

Uses the default read-only `GITHUB_TOKEN`; it only reads the repository.

---

## PR template check (`pr-template-check.yml`)

Enforces the required fields from the [pull request template](../.github/pull_request_template.md). See [CONTRIBUTING.md](../CONTRIBUTING.md) for the contributor-facing rules.

### When it runs

On pull requests to `main` with types `opened`, `edited`, `synchronize`, and `reopened`. Including `edited` means fixing the PR description re-runs the check without pushing a new commit.

The job is skipped when the PR author is `github-actions[bot]`, so the Release workflow's automated manifest PRs (which don't use the template) aren't blocked. A skipped job counts as passing for a required status check.

### Steps

1. **Checkout** with `fetch-depth: 0` (needed to diff against `origin/main`).
2. **Validate PR description fields.** The PR body is passed in through the `PR_BODY` environment variable rather than interpolated into the script, so text in a PR description can't inject shell commands. Windows line endings are stripped, then:
   - **`AI used:`** – the first line beginning with `AI used:` (case-insensitive) must be answered `Yes` or `No` (case-insensitive; Markdown `*`, `_` and backticks are ignored). Missing or anything else fails.
   - **Type of Change** – the `## Type of Change` section must exist, and at least one of its checkboxes must be ticked (`- [x]` or `- [X]`). Only boxes in that section count; ticking the Checklist at the bottom doesn't satisfy it. The ticked types are printed in the job log.
   - **`New version:`** – the first line beginning with `New version:` is read (a leading `v` is allowed). Then:
     - blank fails;
     - `N/A` passes **only** if the PR doesn't change plugin source (same path rules as Version check: `JellyfinDiscordAuth/`, root `*.sln`, `*.ruleset`, `*.ps1`);
     - otherwise it must be a four-part version (`1.2.1.0`) that **exactly matches** `version` in the PR's `meta.json`.

   Each problem is reported as an `::error::` annotation, and the job fails if any check failed. Whether the new version is *higher* than `main` is left to the Version check workflow.

### Permissions

Default read-only `GITHUB_TOKEN`.

---

## Release (`release.yml`)

### When it runs

On every `push` to `main` — normally a merged PR. Because the manifest update itself is merged back into `main`, this workflow runs **twice per release**: once for the real change, and once for the manifest PR merge. The second run is harmless thanks to the idempotency check in step 4.

### Permissions

```yaml
permissions:
  contents: write        # create releases/tags, push the manifest branch
  pull-requests: write   # open and merge the manifest PR
```

All GitHub operations use the built-in `GITHUB_TOKEN` via the `gh` CLI; no extra secrets are required.

### Steps

1. **Checkout** – `actions/checkout@v4` (shallow clone of the pushed commit).

2. **Setup .NET** – `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` (the project targets `net10.0`).

3. **Read version** – `jq -r '.version' JellyfinDiscordAuth/meta.json` → exposed as `steps.meta.outputs.version`.

4. **Skip if release already exists** – runs `gh release view "v<version>"`. If a release with that tag exists, `skip=true` and **every remaining step is skipped** (each has `if: steps.check.outputs.skip == 'false'`). This makes the workflow idempotent: re-runs, docs-only merges, and the manifest PR merge don't produce duplicate releases.

5. **Update timestamp in meta.json** – sets `.timestamp` to the current UTC time (`YYYY-MM-DDTHH:MM:SSZ`) using `jq`. This edit only exists inside the runner — it is packaged into the zip but **not committed** back to the repo.
   *(Locally on Windows, the csproj's `UpdateMetaTimestamp` target does the same via `update-timestamp.ps1`; that target is gated to Windows, so it doesn't run on the Ubuntu runner.)*

6. **Build** – `dotnet build JellyfinDiscordAuth/JellyfinDiscordAuth.csproj -c Release`. `meta.json` is copied into the output directory (`CopyToOutputDirectory=Always`) and `CopyLocalLockFileAssemblies=true` puts the Discord.Net DLLs alongside the plugin DLL.

7. **Package** – creates `JellyfinDiscordAuth/artifacts/JellyfinDiscordAuth.zip` with `zip -j` (flat, no directories) containing exactly:
   - `JellyfinDiscordAuth.dll`
   - `Discord.Net.Core.dll`, `Discord.Net.Rest.dll`, `Discord.Net.WebSocket.dll`, `Discord.Net.Commands.dll`, `Discord.Net.Interactions.dll`, `Discord.Net.Webhook.dll`
   - `meta.json`

   Jellyfin's own assemblies and other framework dependencies are intentionally excluded because the server already provides them. If a new runtime dependency is added to the plugin, it must be added to this list (and to `build-debug.ps1`).

8. **Compute MD5 checksum** – `md5sum` of the zip, upper-cased (Jellyfin's manifest convention) → `steps.checksum.outputs.md5`. Jellyfin verifies this checksum when installing the plugin.

9. **Create GitHub release** – `gh release create "v<version>"` with:
   - title `v<version>`,
   - notes from `meta.json`'s `changelog`,
   - the zip as the release asset.

   This also creates the `v<version>` git tag on the pushed commit.

10. **Update manifest.json** – rewrites the root [`manifest.json`](../manifest.json) (the Jellyfin plugin repository file users add to their server):
    - builds a new version entry:
      ```json
      {
        "changelog": "<meta.json changelog>",
        "checksum": "<MD5>",
        "sourceUrl": "https://github.com/<owner>/<repo>/releases/download/v<version>/JellyfinDiscordAuth.zip",
        "targetAbi": "12.0.0.0",
        "timestamp": "<now, UTC>",
        "version": "<version>"
      }
      ```
    - removes any existing entry with the same version from `.[0].versions`,
    - prepends the new entry so the newest version is first,
    - writes the result back with `jq` (`.[0].versions = …`). Plugin-level fields (name, guid, description, etc.) are left untouched.

11. **Push manifest branch** – as `github-actions[bot]`, creates branch `ci/manifest-v<version>`, stages `manifest.json`, and:
    - if there is no actual diff, sets `pushed=false` and stops (nothing to merge);
    - otherwise commits `chore: update manifest for v<version>`, pushes the branch, and sets `pushed=true`.

    The manifest is updated via a PR rather than a direct push because `main` is branch-protected.

12. **Create and auto-merge manifest PR** *(only if `pushed == true`)* –
    - `gh pr create --base main --head ci/manifest-v<version>` with the same title;
    - then tries an **immediate** squash merge (`gh pr merge --squash --delete-branch`);
    - if that fails (e.g. the required "Version check" status hasn't reported yet), falls back to **auto-merge** (`--auto --squash --delete-branch`), so GitHub merges it as soon as the requirements are met.

    The manifest PR only touches `manifest.json`, so the Version check passes it without a version bump. Once merged, users pulling `https://raw.githubusercontent.com/EvanTrow/Jellyfin-Discord-Auth/main/manifest.json` see the new version.

### Rollback on failure

If any step fails, two cleanup steps run (in reverse dependency order) so a half-finished release doesn't linger:

| Step | Runs when | Action |
| --- | --- | --- |
| **Rollback – delete manifest branch** | `failure()` and the manifest branch was pushed | `git push origin --delete ci/manifest-v<version>` (errors ignored). Deleting the head branch also closes any PR opened from it. |
| **Rollback – delete GitHub release and tag** | `failure()` and the release step succeeded | `gh release delete "v<version>" --yes --cleanup-tag`, removing both the release and its tag. |

Because the release is deleted, simply re-running the workflow (or pushing again) will retry the whole release from scratch — the idempotency check in step 4 won't skip it.

---

## Required repository settings

For the workflows to function as designed:

- **Settings → Actions → General → Workflow permissions:** "Allow GitHub Actions to create and approve pull requests" must be enabled (needed for the manifest PR).
- **Settings → General → Pull Requests:** "Allow auto-merge" must be enabled (needed for the step 12 fallback).
- **Branch protection on `main`:** require a pull request and the **Version check** and **PR template check** status checks. Don't require approving reviews, or the bot's manifest PR will never auto-merge.

---

## Releasing a new version (maintainer checklist)

1. On a feature branch, make your changes.
2. In `JellyfinDiscordAuth/meta.json`, bump `version` (higher than `main`'s) and update `changelog`.
3. Open a PR to `main`, filling in `New version:` and `AI used:` in the template; **Version check** and **PR template check** must pass.
4. Merge the PR. The **Release** workflow builds, publishes `v<version>`, and opens + merges the manifest PR automatically.
5. Verify the new release under **Releases** and the new entry at the top of `manifest.json` on `main`.

---

## Notes and caveats

- **`targetAbi` is hard-coded** to `"12.0.0.0"` in the manifest step rather than read from `meta.json` (which currently says `"12.0.0"`). When targeting a new Jellyfin ABI, update the workflow as well as `meta.json`.
- **Timestamps differ slightly:** the zipped `meta.json` and the manifest entry are each stamped with the time their step ran, so they can be a few seconds apart. The committed `meta.json` keeps whatever timestamp was last committed.
- **No concurrency group:** two pushes to `main` in quick succession run in parallel. With different versions this is fine; with the same version, both may pass the "release exists?" check before either creates it, and the second `gh release create` will fail. That run's rollback won't delete the first run's release (its own release step didn't succeed), but it will show as a failed run. Adding `concurrency: release` to the workflow would serialize runs.
- **Rollback doesn't revert a merged manifest PR.** Merging is the last step, so in practice a failure after merge can't happen, but if the step order changes, keep this in mind.
- **Version check only compares against `main`'s current version.** Two open PRs that both bump to the same version will each pass; the second one merged will then be skipped by the Release workflow (the release already exists). Rebase and bump again if that happens.
