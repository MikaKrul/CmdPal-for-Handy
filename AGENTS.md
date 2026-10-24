# HandyExtension

PowerToys Command Palette extension for [Handy](https://github.com/cjpais/Handy), the speech-to-text app. Out-of-process COM server (Shmuelie.WinRTServer) built on `Microsoft.CommandPalette.Extensions.Toolkit`. C# / .NET 10. Reads Handy's own local files (SQLite history, JSON settings); writes settings and custom words back to them.

## Build and test

The .NET 10 SDK is not on PATH on this machine. Call it by full path (`C:\Users\MikaK\AppData\Local\Microsoft\dotnet\dotnet.exe`) and run everything from the repository root:

```powershell
dotnet restore HandyExtension.sln
dotnet test HandyExtension.Tests/HandyExtension.Tests.csproj --configuration Release -p:Platform=x64
dotnet build HandyExtension.sln --configuration Release --no-restore -p:Platform=x64
```

- Always pass `-p:Platform=x64`. The packaging setup expects it.
- The extension targets `net10.0-windows10.0.26100.0` with `TargetPlatformMinVersion` 10.0.19041; the test project targets plain `net10.0`.
- Release builds publish trimmed and AOT-compatible. Keep the build at zero warnings.

## Test project layout

There is no project reference. The test project compiles four source files directly:

- `HandyExecutableLocator.cs`
- `HandyMenuItems.cs`
- `HandyMenuConfig.cs`
- `HandyLocalData.cs`

Code that needs unit tests must be WinRT-free and live in one of those files, or be added to the `Compile Include` list in `HandyExtension.Tests.csproj`. Pages, commands, and anything touching `Microsoft.CommandPalette.Extensions` or `Windows.*` cannot run under MSTest; keep logic in the data layer and the UI layer thin. `HandyLocalData` must stay free of WinRT dependencies for exactly this reason.

Facts that keep biting:

- MSTest 4.5.1 removed `Assert.ThrowsException`. Use `Assert.ThrowsExactly<T>` for the exact type, `Assert.Throws<T>` when subclasses count (needed for `JsonException`).
- Every SQLite connection string in tests must set `Pooling = false`. Pooled connections keep the database file locked, so `Directory.Delete` in cleanup fails.
- Package versions live in `Directory.Packages.props` (central package management). Do not pin versions in the csproj files.
- Tests never touch the real profile. `HandyLocalData` has an internal constructor taking a data-directory override; use it.
- `HandyExecutableLocator` caches discovery; the locator tests change PATH between runs and call `ClearDiscoveryCache()` in cleanup.

## Data layer rules

The extension reads and writes files that Handy owns. Treat these rules as load-bearing:

- Open a fresh read-only SQLite connection per query and dispose it. Never hold one open: Handy writes to the same database and an open connection can block it. `Pooling = false` in the extension too.
- The history schema is probed, not assumed: `post_processed_text` may or may not exist. Cache the probe result per database file, but never cache a probe that returned zero rows (the table did not exist yet). `ReadHistoryEmptyDatabaseThenCreatedTableSeesNewColumns` guards this.
- Full-text search uses `LIKE` with `ESCAPE '\'` on title, raw text, and post-processed text, so user input is wildcard-safe.
- `ReadHistory(beforeId, out hasMore, search, limit)` reads `limit + 1` rows to compute `hasMore`. Single-entry callers pass `limit: 1`.
- The apparent double Handy-process check in settings updates is the concurrency guard: one check decides whether to refuse, the re-read catches a file that changed mid-edit. Do not collapse it.
- Settings writes are transactional in three steps: refuse while Handy runs, re-read and compare just before saving, then swap atomically via `File.Replace`, which leaves the previous file as `settings_store.json.HandyExtension.bak`.
- The discovery, portable-marker, and schema caches are static because they describe the file system, not an instance. They validate before use: a cached executable path is re-probed with `File.Exists` so installing or removing Handy mid-session is picked up.
- Handy's data locations: roaming `%APPDATA%\com.pais.handy\` unless the executable directory contains a `portable` marker file with content starting `Handy Portable Mode`, in which case `<exe>\Data\` wins.

## Command Palette Toolkit gotchas

Toolkit 0.9.260303001:

- The host filters plain `ListPage` items itself on Title/Subtitle. That filtering only sees the preview, so full-transcript search lives in a `DynamicListPage` that filters via `SearchText` / `UpdateSearchText`.
- `ListPage.SetSearchNoUpdate` is protected; call it from inside a page subclass only.
- `CommandProvider.RaiseItemsChanged` is protected. Subscribe to the `ItemsChanged` event where the provider must react.
- The extension's COM class registration lives in `Package.appxmanifest`; the csproj target `SkipCommandPaletteContractWinmdHarvest` keeps MSIX tooling from harvesting activatable classes from the contract-only WinMD. Do not delete it.

## Conventions

- UI strings, toasts, code comments, and docs are English. Keep toasts full sentences that say what happened or what to do.
- Code comments explain why, not what.
- Prose (README, docs) follows the deslop skill: short active sentences, no filler, no bold-first bullets, no em dashes, only details a reader needs.
- Commit subjects are a single imperative line. Never push, tag, release, or install anything without explicit user approval in the session; committing locally is fine.
- New user-facing behavior gets a test where the layer allows it, plus a line in the manual end-to-end checklist in the README when a recording is needed.

## Release

`.github/workflows/release.yml` is `workflow_dispatch` with a four-part version input (for example `1.6.1.0`); it builds and signs the MSIX on Actions and publishes tag `vMAJOR.MINOR.PATCH` with the package, the public certificate, and a SHA-256 checksum. The signing key is an encrypted Actions secret and never lands in the repository. CI runs tests plus an x64 Release build on pushes and PRs to `main` and `develop`.

Installing a release locally requires exiting PowerToys first (error `0x80073CF8` otherwise), trusting the publisher certificate once in `Cert:\LocalMachine\TrustedPeople`, then `Add-AppxPackage`.
