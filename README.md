# LeviShare

[日本語](README_JP.md)

Share a [LeviLauncher](https://github.com/LiteLDev/LeviLauncher) setup — mods and resource/behavior packs — as a single zip.
The zip uses LeviLauncher's own instance backup format, so **the person you send it to only needs an unmodified LeviLauncher**:
they import it with LeviLauncher's "Restore backup". LeviShare is only needed on the sending side.

> Unofficial tool. Not affiliated with LiteLDev, LeviMC or Mojang.

## Download

Get `LeviShare-x.y.z-win-x64.zip` from [Releases](../../releases), unzip it and run `LeviShare.exe`.
No installation and no .NET runtime needed (Windows 10/11, 64-bit).

## Sharing a setup

1. Start LeviShare. It finds LeviLauncher's data automatically (`%APPDATA%\levilauncher.exe`, or the folder set in LeviLauncher's settings).
   If it doesn't, use **Change...** and pick the folder that contains `versions`.
2. Choose the instance.
3. Tick what to share:
   - **Mods installed with lip** – only the name and version are stored; LeviLauncher installs them again on the other PC.
   - **Mods added by hand** – copied as files. Disabled mods are unticked by default.
   - **Resource / behavior / skin packs** – from the game's shared folder. Skin packs are unticked by default.
4. Click **Export share zip...** and send the zip.
5. **Copy steps for the recipient** puts the import instructions below on the clipboard.

LeviShare only reads your LeviLauncher and game files. It writes nothing except the zip you save.

## Importing (recipient, plain LeviLauncher)

1. In LeviLauncher, open **Settings > Others** and turn on **Instance backup (experimental)**.
2. Download the same Minecraft version and create an instance.
3. Open the instance's settings > **Manage** > **Restore backup** and choose the zip.
4. Mods installed with lip are installed again automatically. lip must be installed (**Settings > Third-party components**).
5. Resource packs are copied but not switched on: turn them on in Minecraft (**Settings > Global resources**).

## What is left out

| Left out | Why |
| --- | --- |
| Worlds, screenshots, per-account folders (`Users/<id>`) | personal, and the folder names differ between PCs |
| Marketplace packs (encrypted) | may not be redistributed |
| `logs`, `cache`, `temp`, crash dumps, `*.pdb`, `*.log` inside mods | not needed, can be large |
| Files with `uuid` in the name (e.g. `ll_bstats_telemetry_uuid`) | per-PC identifiers |

Pack manifests that the game accepts but strict JSON parsers reject (comments, line breaks inside strings, text after the
last `}`) are rewritten with the same content, so LeviLauncher can match packs by UUID when restoring.

## Limitations

- The list of *active* global resource packs is not restored by LeviLauncher; the recipient switches packs on in game.
- The recipient must enable LeviLauncher's experimental instance backup.
- Only GDK instances. UWP instances cannot load mods.
- Only share mods and packs you are allowed to redistribute. A `levishare-info.json` inside the zip lists every mod, pack
  and the SHA-256 of each mod DLL so the recipient can check them.

## Zip format

The zip is a LeviLauncher instance backup (`formatVersion` 2): `backup-manifest.json`, `gameData/Users/Shared/games/com.mojang/<packs>`
and `mods/raw/<mod folders>`, with lip packages recorded in the manifest. See `internal/mcservice/instance_backup.go` in LeviLauncher.

## Building

Requires the .NET 8 SDK.

```
dotnet test tests/LeviShare.Tests
dotnet run --project src/LeviShare.App
powershell -ExecutionPolicy Bypass -File tools/publish.ps1   # release zip in artifacts/
```

## License

MIT — see [LICENSE](LICENSE).
