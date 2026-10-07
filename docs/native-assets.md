# Embedded chest asset

The box mesh and texture are embedded in HOBA as `assets/hidden-chest.bin`
(resource `Hoba.HiddenChest`). `NativeChestAsset.Read()` loads and caches this
resource once per process. There are no installed-game asset reads, asset IDs,
fixed offsets, name-lookup fallback, Unity class database or asset-reader dependency.

`ChestVisual` constructs the Unity mesh and texture from the cached data. Existing
placement offsets, interaction ranges and saved discovery IDs remain unchanged.
The 512x512 DXT1 texture includes all 10 mip levels.

## Source and format

The checked-in resource was exported once from the verified box in game build
25738165: `Detail_Objects_Foreground/Carboard_Box_Light_C_Open`. It contains the
box and both lids, excluding the shadow object. Source rotation and translation
were removed while retaining authored scale, matching the previous renderer.
This resource distributes the box geometry and texture within the mod DLL.

The little-endian binary contains:

- Four-byte magic `HBC1` followed by three int32 counts: 144 vertices,
  276 indices and 174776 texture bytes.
- Each vertex as float32 XYZ followed by float32 UV.
- Int32 triangle indices followed by the original DXT1 texture bytes.

Total length: 178776 bytes. SHA-256:
`4351ba16fb5667efe15c0a8e4c61f19311d836648b426fa2176e650ece6cffee`.
No extraction or external tools are required to build or run the mod.

## Verification

`make test` verifies the actual embedded resource against the source export's
checksum, checks geometry/UVs, placement pivot and texture bytes, and confirms
session caching. It does not require the game files.

For an in-game smoke test, restart, load a save, inspect and search an undiscovered
hidden chest, then change areas or saves. Confirm the log has no
`HOBA hidden chests stopped` error. Automated checks do not verify Unity rendering,
physics or interaction.
