# Mod Companion

`Nivalis.ModCompanion.dll` is pinned to **1.0.3**, built from the local Mod Companion
1.0.3 project. It is an intentional version-controlled binary dependency; building
HOBA does not require that source checkout. `SHA256SUMS` pins its exact bytes.
The companion's MIT license is included in `ModCompanion.LICENSE`.

The compiler reference and release archive use this same file. Update the binary,
checksum and HOBA's minimum BepInEx dependency version together. Never add another
copy in a nested plugin directory: all mods share the root-level companion DLL.

Source tag: `1.0.3`, commit `39df058e5bac1761fc93a16a554d85ba3fe08cc9`.
The bundled DLL was checked by building HOBA against it; the source checkout is
not consulted by any HOBA build or packaging target.

Bundled 1.0.3 matches the tagged release build prepared on 2026-10-05, including
the independent settings window, input/rebinding and icon-lifetime fixes.
Its exact bytes are identified by `SHA256SUMS`.
Companion's build and 60 configuration checks pass.

# AssetsTools.NET

`AssetsTools.NET.dll` is pinned to **3.0.5**, using the netstandard2.0 DLL from
NuGet's `assetstools.net/3.0.5` package. It has no additional runtime package
dependencies. `SHA256SUMS` pins the DLL and its MIT license. Both are included in
the install/archive. This reader is used only when HOBA's known chest ranges fail.
See [native assets](../docs/native-assets.md) for the embedded Unity metadata's
provenance and the fallback's compatibility limits.
