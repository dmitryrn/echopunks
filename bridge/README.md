# Experimental EXAPUNKS GOG bridge

This fork adds a local, token-protected HTTP bridge for experimenting with
EXAPUNKS. It is a proof of concept, not a general-purpose API. It targets one
GOG executable with SHA-256
`9e32c95075421faa26fdbeabb97e70f89f398908ee6eecfb9bf679107e43420f`.
Other versions are unsupported.

## AI-assisted playthrough

In an AI-assisted playthrough of the whole game, **GPT-6.1 Terra Fast solved
the puzzles using this bridge**, with a person selecting and opening each
level. The bridge does not navigate through the game autonomously. Puzzle
solutions were generally checked against all 100 tests. One earlier battle
result (PB019) was not captured, so its outcome was not independently
verified by the bridge.

**This fork changes Echopunks startup:** the original accessibility module is
not loaded, even on ordinary launches. The bridge only starts when explicitly
enabled by the launcher. It uses the game's normal save, and API operations can
change solutions and progress. Back up your own saves before experimenting.
The game executable and other game-derived files are not included.

## Setup

These steps describe the local setup used for development, not a verified
fresh-machine installation:

1. Install the supported Windows GOG game and the .NET build prerequisites.
   Set `$env:EXAPUNKS_DIR` to the game directory if it is not
   `C:\GOG Games\EXAPUNKS`.
2. With the game closed, prepare the build inputs using
   `tools/prepare-game.ps1 -GameDir $env:EXAPUNKS_DIR` (set the variable first).
   That upstream script needs the expected de4dot tool. Do not commit its
   output or the copied game files.
3. Build a Debug version against your game installation. The upstream
   `build.ps1` deploys files into the game directory and can overwrite
   configuration and mod files; review it and back up the installation first.
   The bridge additionally expects a generated `namemap.tsv` **in the game
   directory**, not just in its `Echopunks` subdirectory. The upstream NameMap
   tool takes the original and deobfuscated executables and an output path:
   `dotnet run --project tools/NameMap -- game/EXAPUNKS.orig.exe game/EXAPUNKS-deob.exe <output.tsv>`.
   Generation and deployment on a fresh installation have not been verified.
4. Create a private random token of at least 32 characters at
   `bridge/bridge-token.local`. For example, in PowerShell:

   ```powershell
   $bytes = New-Object byte[] 32
   $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
   try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
   [IO.File]::WriteAllText("$PWD\bridge\bridge-token.local", [Convert]::ToBase64String($bytes))
   ```

5. From the repository root, run `powershell -NoProfile -ExecutionPolicy Bypass
   -File .\bridge\start-game.ps1`. The script refuses to start if another
   EXAPUNKS process is running. It checks the executable hash and waits for
   initialization.

The server listens on `127.0.0.1:8772` and requires the `X-Exa-Token` header.
The token protects against casual local access, not other processes running as
the same Windows user. Do not expose the server to a network or publish the
token. The inherited arbitrary `/eval` route is disabled.

## API

```powershell
$token = [IO.File]::ReadAllText("$PWD\bridge\bridge-token.local")
$headers = @{ 'X-Exa-Token' = $token }
Invoke-RestMethod http://127.0.0.1:8772/health -Headers $headers
Invoke-RestMethod http://127.0.0.1:8772/gog/state -Headers $headers
```

Routes include:

- `POST /gog/desktop`, `GET /gog/tasks`, and `POST /gog/open` for progression-aware navigation.
- `GET /gog/state` for the current editor, visible network, EXAs and files.
  Hidden hosts and links are omitted; a partial snapshot does not establish
  that undisplayed information is absent.
- `POST /gog/programs` with `{"programs":["...","..."]}` replaces the initial
  programs and adjusts their count, subject to game limits. `GET`, `POST`,
  `PUT`, and `DELETE /gog/exas` provide individual initial-EXA operations.
  Mutations act on the current solution and require a reset editor.
- `POST /gog/step`, `/gog/reset`, `/gog/run`, `/gog/pause`, and
  `/gog/continue` control the simulation. Step and reset are asynchronous;
  check state before issuing the next operation.
- `GET /gog/run/status` reports test progress and completion. On a battle,
  `completed` means the results screen appeared, whereas `task_completed`
  means an overall win. Passing the size check does not establish full score
  eligibility. `GET /gog/battle-results` reads the displayed battle results.
- `POST /gog/goal-log` requests capture of the displayed goal tables;
  `GET /gog/goal-log` retrieves its status and result.

Run requests accept a timeout of 1–120 seconds. The bridge was used to
complete multiple puzzle test suites and battles, but not every level or API
path has been verified. API errors generally return `ok: false`; HTTP status
alone does not establish success.

## References

The [manual-reference](manual-reference/) directory contains notes derived
from the game's manuals, rather than full transcriptions. The game PDFs remain
authoritative. `monitor-game.ps1` can launch the game and log process exit
status and periodic memory usage to a caller-specified local path.
