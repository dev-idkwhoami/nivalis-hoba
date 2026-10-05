# HOBA — development

HOBA is a C# BepInEx 6 IL2CPP plugin for Nivalis Nights. This repository contains
runtime model generation, movement and camera handling, inventory/vendor
integration, paint tools, legendary discoveries and per-save persistence.

## Requirements

- .NET SDK 8 or newer, with support for targeting .NET 6. The plugin targets
  `net6.0`; standalone checks target `net8.0`.
- A local game installation already initialized with BepInEx 6 IL2CPP. Builds
  reference its `BepInEx/core` and generated `BepInEx/interop` assemblies.
- GNU Make, Bash and standard Unix utilities, including `sha256sum`.

Mod Companion **1.0.3** is pinned in `dependencies/`, with its checksum, license
and provenance. No sibling checkout, Python scripts or external research tools
are needed. Game assemblies and extracted game assets must not be committed.

## Build and validate

```sh
export NIVALIS_GAME_PATH="/path/to/Nivalis Nights"
make build
make test
make test-config
make check-game-assets
make package
```

Alternatively pass `GAME_PATH="..."` to each game-dependent target. Set
`DOTNET=/absolute/path/to/dotnet` when the SDK is not on PATH.
`CONFIGURATION` defaults to `Release`; `NUGET_PACKAGES` can override the local
package cache. Build output goes into ignored `bin/` directories; SDK state and
cache live under ignored `.tools/`.

| Target | Purpose |
| --- | --- |
| `build` | Verify the pinned dependency and compile against the installed game |
| `test` | Managed behavior, geometry, deformation, paint, product and persistence checks |
| `test-config` | Real BepInEx configuration round-trip checks in temporary files |
| `check-game-assets` | Verify native chest asset hashes, ranges and reconstructed geometry |
| `package` | Build, run managed/configuration checks and create the release ZIP |
| `install` | Build, run managed checks and replace the two plugin DLLs in the local game |
| `model` / `catalog` | Export OBJ/MTL geometry under `bin/models/` |
| `clean` | Remove compiled outputs and intermediate build files |

The checks do not launch the game. Changes to native hooks, animation, inventory,
shops or save lifecycle also need in-game verification. For performance changes,
compare the same board, area and activity before/after; managed timing alone does
not measure GPU cost.

## Source layout

| Location | Responsibility |
| --- | --- |
| `src/Plugin.cs` | Plugin identity, dependencies and lifecycle |
| `src/BoardConfiguration.cs`, `BoardTuning.cs`, `CompanionSettings.cs` | Configuration ownership, catalogue values and settings UI |
| `src/Ride*`, `GroundResponse.cs`, `BoardFov.cs`, `SpeedFov.cs` | Movement, input, terrain response and camera behaviour |
| `src/BoardGeometry.cs`, `BoardVariants.cs`, `BoardDesigns.cs` | Procedural meshes, model identities and palettes |
| `src/BoardModel.cs`, `BoardArticulation.cs`, `FieldDeformation.cs`, `CurvedFieldSurface.cs` | Rendering and articulated connector animation |
| `src/BoardItem.cs`, `BoardProducts.cs`, `BoardShop.cs`, `VendorController.cs` | Native inventory, product registration and dealer integration |
| `src/Spray*`, `Paint*` | Held spray-can model, hand pose and consumable paint transaction |
| `src/WorldPlacements.cs`, `HiddenChests.cs`, `LegendaryCompass.cs` | Authored placements, interaction and discovery markers |
| `src/NativeChestAsset.cs`, `ChestVisual.cs` | Verified reads of chest assets from the installed game |
| `src/HobaSave.cs`, `HobaCheckpoint*` | Save hooks and versioned companion state |
| `assets/` | Embedded inventory PNGs; source geometry lives in C# |
| `dependencies/` | Pinned Mod Companion binary, checksum and license |
| `tests/` | Automated managed regression checks and optional model exporters |
| `tools/` | Configuration checks, shell packaging and SDK ZIP task |
| `docs/` | Maintainer notes on configuration, models, vendors, native assets and persistence |

## Development boundaries

- Preserve product, dealer and chest IDs: they are persistent save keys.
  See [persistence](docs/persistence.md) and [vendor integration](docs/vendors.md).
- Keep catalogue defaults and configuration binding consistent. Board stats,
  prices and palettes load from `hoba.cfg`; live Companion controls share that
  same file. See [configuration](docs/board-configuration.md).
- Geometry is generated on deployment/load/repaint. Connector animation uses
  managed math and reusable buffers. Avoid Unity IL2CPP math calls inside
  per-vertex loops. See [models](docs/models.md).
- Inventory thumbnails are checked-in PNGs. Palette and geometry changes do not
  regenerate them; review/update the icons separately when needed.
- Native chest descriptors target Steam build **25680465**. Validate addresses,
  layouts and transforms before changing hashes for another build. See
  [native assets](docs/native-assets.md).
- Avoid native by-reference struct detours such as Cinemachine `CameraState`;
  these have caused IL2CPP crashes. Prefer the established controller hooks.

## Release packaging

Update both `Plugin.Version` and `src/Hoba.csproj`, then run `make package`.
Output is `bin/Nivalis.Hoba-VERSION.zip` plus its SHA-256 file. The ZIP contains:

```text
BepInEx/plugins/Nivalis.Hoba.dll
BepInEx/plugins/Nivalis.ModCompanion.dll
```

Settings, saves, debug symbols, research files and game assets are excluded.
Update the Companion DLL, checksum, provenance and minimum dependency declaration
together when changing its version. All mods share the root-level Companion DLL;
check compatibility before using `make install` in a development installation.

There is no automatic commit, tag, push or publication step.
