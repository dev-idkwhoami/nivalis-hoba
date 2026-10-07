# Releasing HOBA

The current update is `1.0.2`. Keep `src/Hoba.csproj` and `src/Plugin.cs` aligned.
Mod Companion is bundled, with a runtime dependency of `>=1.0.3`. Refresh the
bundled DLL and its checksum when taking a newer build, even if its version is
unchanged. Record its provenance in `dependencies/README.md`.

Before publishing, run `make package` and `make check-game-assets` with
`GAME_PATH` pointing to the initialized game installation. Test the resulting
package in-game, including menu open/close and switching saves.

Publish from a clean, committed checkout. Configure the intended GitHub remote,
create the `1.0.2` tag on the tested commit, then push `main` and the tag. Use that tag for the
GitHub release. Upload only these release artifacts:

- `bin/Nivalis.Hoba-1.0.2.zip`
- `bin/Nivalis.Hoba-1.0.2.zip.sha256`

The ZIP contains HOBA, pinned Mod Companion and AssetsTools.NET DLLs plus the
asset reader license under `BepInEx/plugins/`. Unity type metadata is embedded
in HOBA; no external class database or Python runtime is required.
Do not publish game assemblies, local configuration, saves or build caches.
Building or packaging does not push, tag or publish anything automatically.
