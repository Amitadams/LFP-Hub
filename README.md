# LFP Hub

GFNV tool for TeslaLFP password / MFA close-out after a **manual** Azure reset.

Fills a reply template → Teams DM → ITA comment → create or reuse ITA → resolve.

Not part of Deskside Hub.

## Requirements

- Windows 10/11 x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
- Node.js on PATH (or Nova / skills Node layout)
- Skills tree with `lfp-reset` (`.opencode/skills`)
- To **build** the installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`)

## Install (end users)

Download **`LfpHub-*-Setup.exe`** from [Releases](https://github.com/Amitadams/LFP-Hub/releases) and run it.

- Per-user install (no admin) → `%LocalAppData%\Programs\LfpHub\`
- Start Menu + optional Desktop shortcut
- Uninstall from Windows **Apps & features** (or Start Menu)

Do **not** run the old `install.bat` / zip PowerShell installer — that path breaks when Windows expands the zip under a temp folder with special characters.

## Build Setup.exe (developers)

```powershell
# once
winget install JRSoftware.InnoSetup

# sibling DesksideHub.Core required
.\build-release.ps1
```

Outputs:

| Artifact | Path |
|----------|------|
| App (publish) | `publish\LfpHub.exe` |
| **Installer** | `dist\LfpHub-<ver>-Setup.exe` |
| Portable zip | `dist\LfpHub-<ver>-win-x64-portable.zip` |

## First-time setup

On first launch a **setup wizard** asks for **your** tech identity (display name, username, email, signature). Nothing is pre-filled from another technician.

Identity lives under `%LocalAppData%\LfpHub\` only (not in the installer payload).

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
  Assets/              # App icon (ICO + generator)
  installer/LfpHub.iss # Inno Setup script
  Templates/           # Reply templates (token-only)
  SetupWindow.*        # First-run wizard
  build-release.ps1    # publish + ISCC → dist\*-Setup.exe
```

Depends on sibling `../DesksideHub/DesksideHub.Core`.

## Version

**0.0.12** — Bulk table paste fills rows (Ctrl+V / Paste / CSV); Run starts jobs.

**0.0.10** — Bulk CSV headers: Username, Password, Name, Ticket Type, Badge (per-row types).

**0.0.9** — Close-out type **No account** (refer to LFP provisioning; no OTP).

**0.0.8** — Bulk close-out from CSV or pasted text (username + one-time password).

**0.0.7** — Fix startup crash (NullReference in job UI while XAML loads).

**0.0.6** — Job renamed to **Open LFP Ticket** (skill `lfp-open-ticket`).

**0.0.5** — Open ticket job (create ITA with component MFA - Reset, leave open, open in browser).

**0.0.4** — first-run setup accepts any tech identity (removed accidental self-block).

**0.0.3** — Inno Setup installer (`Setup.exe`); replaces broken zip/PowerShell install path.

## License

Internal Tesla IT use.
