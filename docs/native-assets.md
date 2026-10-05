# Installed-game chest assets

Production chest placements are defined in `WorldPlacements.cs`. Placement changes
require a source edit and rebuild.

`NativeChestAsset.Read` opens two files under Unity's `Application.dataPath`.
It reads the cardboard box's three static-batch index ranges, its vertex stream
and the DXT1 texture with mipmaps. The source transform is undone, then the saved
bottom-centre offset is applied by `ChestVisual`. This preserves existing world
placements, collider bounds and interaction ranges.

Only range addresses, format information, transforms and SHA-256 fingerprints
are stored in code. Mesh/texture bytes are read from the user's game at runtime,
not distributed or written to disk. The loader needs no previously visited scene.
Reads occur when spawning the current area's chest, not each frame.

This is deliberately specific to the inspected game build, not a general Unity
asset parser. `make check-game-assets GAME_PATH="..."` verifies each required range
and the reconstructed pivot. A game update that changes these bytes requires
re-identifying the source mesh/submeshes/texture and updating the descriptors.
Do not simply replace hashes without rechecking offsets, layouts and transforms.
Unsupported data fails closed and logs once for the current area.
