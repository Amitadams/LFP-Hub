# LFP Hub

GFNV tool for TeslaLFP password / MFA close-out after a **manual** Azure reset.

Fills a reply template → Teams DM → ITA comment → create or reuse ITA → resolve.

Not part of Deskside Hub.

## Requirements

- Windows 10/11 with [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (framework-dependent build)
- Node.js on PATH (or the same Node layout Deskside Hub / skills use)
- Skills tree with `lfp-reset` (`.opencode/skills`)

## Install (recommended)

From a machine that can build (with sibling `../DesksideHub`):

```powershell
.\build-release.ps1
.\publish\install.bat
```

Or after unzipping a release zip that already contains `LfpHub.exe`:

```bat
install.bat
```

This copies the app to:

`%LocalAppData%\Programs\LfpHub\`

and creates:

- Start Menu → **LFP Hub**
- Desktop shortcut (via `install.bat` / `install.ps1 -DesktopShortcut`)
- **Open LFP Hub.bat** next to the installed exe

Launch any of:

- Start Menu **LFP Hub**
- Desktop **LFP Hub**
- `%LocalAppData%\Programs\LfpHub\LfpHub.exe`
- `%LocalAppData%\Programs\LfpHub\Open LFP Hub.bat`
- Repo root `Open LFP Hub.bat` (finds install, publish, or build output)

Uninstall:

```powershell
& "$env:LOCALAPPDATA\Programs\LfpHub\uninstall.ps1"
# optional: also wipe config
& "$env:LOCALAPPDATA\Programs\LfpHub\uninstall.ps1" -RemoveConfig
```

## First-time setup

On first launch the app opens a **setup wizard**. Enter **your** tech identity:

1. Display name, username, email  
2. Site, signature title, walk-up hours  
3. **Continue**

Nothing is pre-filled from another technician. Identity is stored only under `%LocalAppData%\LfpHub\` and is never committed.

You can change identity later under **Settings → Tech identity**.

## Dev run

```bat
run-lfp-hub.bat
```

Or:

```bat
dotnet build -c Release
bin\Release\net8.0-windows\LfpHub.exe
```

## Template tokens

| Token | Meaning |
|-------|---------|
| `[User]` | First name |
| `[username]` | Tesla / LFP username |
| `[PASSWORD]` | Azure one-time password |
| `[TechName]` | Your display name |
| `[SignatureTitle]` | Signature line |
| `[WalkupHours]` | Walk-up hours |
| `[Site]` | Site code (e.g. GFNV) |

## Modes

| Type | Azure action | OTP |
|------|----------------|-----|
| Password | Password reset | Required |
| First login | New account setup | Required |
| MFA | Authenticator reset | Not needed |

Dry-run is on by default.

## Project layout

```
LfpHub/
  Templates/           # Bundled reply templates (token-only, no personal names)
  SetupWindow.*        # First-run tech identity wizard
  AppConfig.cs         # Config + identity scrub
  MainWindow.*         # Main UI
  SettingsWindow.*     # Tech identity + templates
  build-release.ps1    # Publish → ./publish + dist zip
  install.ps1/.bat     # Per-user installer
  uninstall.ps1
  Open LFP Hub.bat     # Launcher (install / publish / build)
  run-lfp-hub.bat      # Dev launcher
```

Depends on sibling `../DesksideHub/DesksideHub.Core` for job running / Node location.

## Version

**0.0.2** — app icon (LFP battery mark), first-run setup wizard, publish + per-user installer.

## License

Internal Tesla IT use.
