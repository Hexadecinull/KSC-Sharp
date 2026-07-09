# Windows Installer Scaffolds

Three installer scaffolds for Windows. None are wired into CI yet — treat these as a
starting point, not a finished pipeline.

- **WiX** (`WiX/Product.wxs`) — use the WiX Toolset (candle & light) to build an MSI.
  Replace the `PUT-GUID-HERE` placeholders with real GUIDs before shipping.
- **Inno Setup** (`Inno/installer.iss`) — use the Inno Setup Compiler to build an EXE installer.
- **PowerShell** (`install.ps1`) — a manual-install fallback: copies files into
  Program Files and creates a Start Menu shortcut.

## Build tips

1. Publish the app in Release:
   ```
   dotnet publish src/KSC-Sharp.App/KSC-Sharp.App.csproj -c Release -r win-x64 --self-contained false -o dist/win
   ```
2. Point whichever packaging tool you're using at `dist/win`:
   - Inno: open `installer.iss` in the Inno Setup Compiler
   - WiX: fill in real component GUIDs, then run `candle` + `light`
   - PowerShell: run `install.ps1` with elevated privileges

Code signing isn't set up here — add it to the release workflow once you have a
certificate, and keep it in GitHub Secrets rather than checked into the repo.
