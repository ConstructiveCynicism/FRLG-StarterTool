## Setting up development environment

Before starting, fork the repository on GitHub (use the "Fork" button on the top right of the page).

The source code must be obtained with [git](https://git-scm.com/download). Once git is obtained, the repository can be cloned using:
```sh
git clone https://github.com/yourusername/FRLG-StarterTool
```
Where yourusername is replaced with your GitHub username.

Building requires Windows 10 2004 or later and the [dotnet8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). [VS Community 2022](https://visualstudio.microsoft.com/downloads/) (or any other edition) with the ".NET desktop development" workload installs it; [Rider](https://www.jetbrains.com/rider/download/) or [VS Code](https://code.visualstudio.com/download) with the `dotnet` CLI work as well.

- `dotnet build` builds the solution.
- `dotnet run --project src/FRLG.StarterTool.App` runs the tool.
- `dotnet publish src/FRLG.StarterTool.App -p:PublishProfile=win-x64` makes the single self-contained exe the releases ship.

The first build downloads the Vosk speech model (39 MB) into `obj\` and checks its hash. Without network the build still succeeds, with a warning, and that build has no voice entry.

The solution is two projects:
- `src/FRLG.StarterTool.App` - the WinForms window, the global keyboard hook, the timer loop, audio, capture, the updater.
- `src/FRLG.StarterTool.Core` - everything that is not UI: the RNG port, timing arithmetic, the searches, settings, the NPC simulation, the encounter tables. It has no UI dependency and must not grow one.

## LLM/AI Usage

LLM disclosure is preferred but not required if code is human reviewed and verified. Otherwise, mostly/fully AI contributions must be clearly designated as such. Do not send code you don't understand or have copyright over without clearly stating you don't understand it / have copyright.

## Code style

> Standard [dotnet coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) are used.

- Use four spaces for indentation.
- Use "Allman" style braces.
- File-scoped namespaces, nullable enabled.
- Comments are welcome where they say why a choice was made. Keep them brief and concise, LLM comments should be tagged so they can be removed later.
- The route constants, frame windows and seed tables in `Core` are measurements off real captures, not tuning knobs. A change to one needs the capture or the sweep that measured it, not a value that makes a case look right.
- Settings changes stay additive. A new key with a safe default needs no version bump; when a key's meaning changes, add a step to `SettingsMigrations` and bump `CurrentVersion`.
- The keyboard hook only stamps and posts; it never does work or touches the window. Nothing opens a socket without a visible switch and an offline fallback.

Pull requests go against `main`, one change each, with a line under `### unreleased` at the top of `CHANGELOG.md` written for a runner. CI builds and publishes every pull request; the artifact it uploads is a test build with `ci.<n>` on its menu bar and in `Help ▸ About`, so a reviewer can run exactly what the pull request produces.

## Copyright/Licensing

All original FRLG Starter Tool code shall be licensed as MIT and copyrighted to ConstructiveCynicism.

Any code contributions you make thus must either:
- Be derived from an open-source, publicly-licensed codebase, compatible with the MIT License, with proper attributions. Copyright statements should be modified to include the original author.
- Be authored by you, and you are willing to transfer all transferable rights to ConstructiveCynicism, including but not limited to, re-licensing the code, modifying the code, and distributing it in source or binary forms. Note that this transfer is on a nonexclusive basis only and it does not take away your own copyright.

The released exe as a whole is distributed under GPL-3.0 because of the FFmpeg build it bundles - see `LICENSE.md` and `THIRD-PARTY-NOTICES.md`.
