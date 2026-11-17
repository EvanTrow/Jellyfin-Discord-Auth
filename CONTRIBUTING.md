# Contributing to Jellyfin Discord Authentication

Thanks for your interest in improving this plugin! This guide covers how to report issues, set up a dev environment, and get a pull request merged.

## Reporting bugs and requesting features

[Open a new issue](https://github.com/EvanTrow/Jellyfin-Discord-Auth/issues/new/choose) and pick the **Bug report** or **Feature request** form. Bug reports ask for:

- Jellyfin server version and install type (Docker, Windows, Linux, ...)
- Plugin version (from **Dashboard → Plugins**)
- Whether Jellyfin sits behind a reverse proxy (Cloudflare, Traefik, nginx, ...)
- Steps to reproduce, what you expected, and what happened
- Relevant Jellyfin log lines (search for `DiscordAuth`)

Blank issues are disabled, so please use one of the forms.

**Never post your Discord Client Secret, Bot Token, or Jellyfin API keys.** Redact them from logs and screenshots.

### Security issues

Please don't open public issues for vulnerabilities (auth bypass, token leaks, XSS, etc.). Use GitHub's [private vulnerability reporting](https://github.com/EvanTrow/Jellyfin-Discord-Auth/security/advisories/new) instead.

## Development setup

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A local Jellyfin 12.4.x server for testing
- A Discord application and bot (see the [README](README.md) for setup)

### Building

```bash
dotnet build JellyfinDiscordAuth/JellyfinDiscordAuth.csproj -c Release
```

The project builds with `TreatWarningsAsErrors`, StyleCop, and the rules in [`jellyfin.ruleset`](jellyfin.ruleset), so **any analyzer warning fails the build**. Fix warnings rather than suppressing them; if a suppression is genuinely needed, scope it narrowly and explain why in a comment.

### Testing against a local Jellyfin (Windows)

[`build-debug.ps1`](build-debug.ps1) builds in Debug, stops Jellyfin, removes any old copies of the plugin, copies the new DLLs into your Jellyfin plugins folder, and restarts Jellyfin:

```powershell
./build-debug.ps1
```

Override the default locations with `-JellyfinExe` / `-JellyfinDataPath`, or the `JELLYFIN_EXE_PATH` / `JELLYFIN_DATA_PATH` environment variables.

On other platforms, build in Debug and copy `JellyfinDiscordAuth.dll`, `meta.json`, and the `Discord.Net.*.dll` files from `JellyfinDiscordAuth/bin/Debug/net10.0/` into `<jellyfin data>/plugins/Discord Authentication_<version>/`, then restart Jellyfin.

## Making changes

1. **Fork** the repo and create a branch off `main` (e.g. `fix/redirect-uri`, `feat/role-mapping`).
2. **Keep PRs focused.** One fix or feature per PR is much easier to review than a grab bag.
3. **Follow the existing style.** The analyzers and [`.editorconfig`](.editorconfig) enforce most of it; public members need XML doc comments.
4. **Write clear commit messages.** We loosely follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`.
5. **Test on a real Jellyfin server.** Exercise the Discord login flow and anything touching role sync.
6. **Update docs.** If you change setup steps, configuration, or behavior, update the [README](README.md).

### Versioning and changelog

Every PR that changes plugin source must bump the version, because each merge to `main` publishes a release automatically. In [`JellyfinDiscordAuth/meta.json`](JellyfinDiscordAuth/meta.json):

- Increase `version` (four parts: `MAJOR.MINOR.PATCH.BUILD`), higher than the version currently on `main`:
  - `MAJOR`: breaking changes (e.g. a new Jellyfin ABI, config that must be redone)
  - `MINOR`: new features
  - `PATCH`: bug fixes
  - `BUILD`: small follow-up fixes or rebuilds
- Replace `changelog` with a short, user-facing summary of the changes in this version. It becomes the release notes and appears in Jellyfin's plugin catalog.
- Don't edit `timestamp` (it's set at build time) or `manifest.json` (CI updates it after release).

Docs-only changes (README, `docs/`, `.github/`) don't need a version bump.

See [docs/WORKFLOWS.md](docs/WORKFLOWS.md) for how the release pipeline works.

## Pull requests

Opening a PR pre-fills the [pull request template](.github/pull_request_template.md). Fill in every section. These fields are **required and checked automatically** by the [PR template check](.github/workflows/pr-template-check.yml) workflow:

| Field | What to type | Rules |
| --- | --- | --- |
| Type of Change | Tick one or more boxes (`[x]`) | At least one box in the Type of Change section must be ticked. |
| `New version:` | The new version, e.g. `1.2.1.0` | Must exactly match `version` in your `meta.json`. Use `N/A` only if the PR doesn't touch plugin source. |
| `AI used:` | `Yes` or `No` | See [AI use disclosure](#ai-use-disclosure) below. |

If any of these is missing or invalid, the check fails. Edit the PR description to fix it; the check re-runs automatically.

Before merging, PRs must also pass:

- **Version check**: `meta.json`'s version is higher than `main`'s (for source changes).
- **Maintainer review**.

## AI use disclosure

AI-assisted contributions are welcome, but **you must disclose them**. Every PR has to answer `AI used: Yes` or `AI used: No`.

### When to answer "Yes"

Answer **Yes** if an AI tool (GitHub Copilot, ChatGPT, Claude, Cursor, Gemini, etc.) did any of the following:

- wrote or generated code, tests, or docs in the PR (beyond single-line autocomplete)
- substantially refactored or rewrote existing code
- wrote the PR description or commit messages
- diagnosed the bug or designed the approach you implemented

Answer **No** only if you wrote the change yourself. Using AI purely as a search engine, or accepting trivial one-line autocompletions, can be answered No. If in doubt, answer Yes; disclosure won't count against your PR.

### If you answer "Yes"

Under the `AI used:` line, briefly describe:

- which tool(s) you used
- which parts of the PR they produced
- what you reviewed, tested, or changed yourself

### Your responsibilities

AI disclosure doesn't change the bar for merging. As the contributor, you are responsible for every line in your PR:

- **Understand it.** You should be able to explain any change in review. "The AI wrote it" isn't an answer.
- **Test it.** Run the change against a real Jellyfin server; don't rely on the AI saying it works.
- **Check security-sensitive code carefully.** This plugin handles authentication, OAuth tokens, and bot credentials. Pay extra attention to anything touching login, redirects, token handling, or HTML/JS output.
- **Check licensing.** Don't submit code that an AI may have reproduced from incompatibly licensed sources.
- **Don't paste secrets** (tokens, client secrets, server URLs) into AI tools.

PRs that are clearly AI-generated but answered `No`, or that the author can't explain, may be closed.

## Code of conduct

Be respectful and constructive. Assume good intent, keep feedback about the code, and help newcomers along.
