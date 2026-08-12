# LFP Hub

GFNV tool for TeslaLFP password / MFA close-out after a **manual** Azure reset.

Fills a reply template → Teams DM → ITA comment → create or reuse ITA → resolve.

Not part of Deskside Hub.

## Requirements

- Windows with .NET 8 Desktop Runtime
- Node.js (PATH or Nova install)
- Nova skills tree with `lfp-reset` (`.opencode/skills`)

## Run

```bat
run-lfp-hub.bat
```

Or:

```bat
dotnet build -c Release
bin\Release\net8.0-windows\LfpHub.exe
```

## First-time setup

1. Open **Settings → Tech identity**
2. Set display name, username, email, signature title, walk-up hours
3. **Save**
4. Edit reply templates on the **Templates** tab if needed

Identity is stored under `%LocalAppData%\LfpHub\` and is not committed.

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
  Templates/          # Bundled reply templates (no personal names)
  AppConfig.cs        # Config + tech identity
  MainWindow.*        # Main UI
  SettingsWindow.*    # Tech identity + templates
  run-lfp-hub.bat
```

Depends on sibling `../DesksideHub/DesksideHub.Core` for job running / Node location.

## License

Internal Tesla IT use.
