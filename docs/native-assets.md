# Installed-game chest assets

Production chest placements are defined in `WorldPlacements.cs`. Placement changes
require a source edit and rebuild. Chest data is read from the player's installation;
no game mesh or texture bytes are distributed or written to disk.

## Known-address fast path

`NativeChestAsset.ReadKnown` targets Steam build **25726588**. It reads the box's
three static-batch index ranges from `level2`, the vertex stream from `level2.resS`,
and the DXT1 texture with mipmaps from `sharedassets0.assets.resS`. Each range is
SHA-256 checked. The vertex stride is 40 bytes: float32 position at offset 0 and
float16 UV0 at offset 28. This update moved vertices out of the scene file and
changed their layout, so changing offsets alone would have been insufficient.

The source transform is undone, then the saved bottom-centre offset is applied by
`ChestVisual`. Existing placements, interaction ranges and saved discovery IDs
remain unchanged.

## Temporary fallback after game updates

If a known range is missing, truncated or fails its hash, `NativeChestResolver`
uses AssetsTools.NET to read serialized metadata from `level2`. It finds the unique
`Detail_Objects_Foreground/Carboard_Box_Light_C_Open` object, follows component and
child references, and reads the current mesh/submesh, material and texture data.
Shadow/LOD children are excluded. The object's full parent transform is used to
undo static batching. It supports inline or streamed vertices, 16/32-bit indices,
and float32/float16 position/UV channels, including separate vertex streams.

Both successful data and failed resolution are cached for the process lifetime.
A successful fallback logs one warning. No scene needs visiting or loading, no
network access is used, and no per-frame or per-area rescanning occurs. Restart
the game after installing a compatibility update or changing game files.

The fallback is intentionally bounded to Unity **2020.3.44f1**, the source object
in `level2`, uncompressed meshes and the existing single 512x512 DXT1 texture with
10 mip levels. Missing/ambiguous objects, unsupported layouts, non-finite values,
invalid indices or geometry that no longer fits the placements fail closed.
An engine upgrade, renamed/moved source, or redesigned box may still need a mod
update. Do not accept new hashes without checking layouts and transforms.

## Verification

`make check-game-assets GAME_PATH="..."` reads the known ranges directly (fallback
cannot hide stale descriptors), then forces the stale-address failure path. It
compares triangle order, all positions/UVs and texture bytes with the fast path,
and checks malformed geometry rejection plus success/failure caching.

For an in-game smoke test, restart, load Lowtown, inspect its hidden chest and
confirm the log has no `HOBA hidden chests stopped` error. Check searching on an
undiscovered save and changing areas. Automated file checks do not verify Unity
rendering, physics or interaction.

## Reader provenance

`dependencies/AssetsTools.NET.dll` is the netstandard2.0 binary from NuGet
AssetsTools.NET **3.0.5** (MIT); the package SHA-256 is
`e3b79ad8271aa8d84df541ddecba2402448408fd648b33b6a3c2e2a9a1c1d384`.
Its source is <https://github.com/nesrak1/AssetsTools.NET>.

`assets/unity-2020.3.44f1.cldb` contains Unity serialization type metadata, not
Nivalis assets. It was extracted using AssetsTools.NET `LoadClassPackage`,
`LoadClassDatabaseFromPackage("2020.3.44f1")`, and
`ClassDatabase.Write(writer, ClassFileCompressionType.Lz4)` from `classdata.tpk`
in <https://github.com/nesrak1/UABEA/releases/download/v8/uabea-ubuntu.zip>.
The source TPK SHA-256 is
`129e1f80f930415db6779fe6089afa75280cb51462bcee812beab6cd81a764c6`.
The class database is embedded in the plugin and in the test executable.
