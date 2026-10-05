# Model implementation

`BoardDesigns` defines three families, three standard marks per family, and three
legendary IDs. Standard marks each accept three family-specific finishes. The
catalogue therefore has 12 models and 39 board appearances. `BoardProducts`
registers those appearances and nine paint consumables with stable IDs.

`BoardGeometry` and `BoardVariants` generate the mesh data at deployment, save
restoration or repaint. `BoardModel` constructs the Unity objects and groups
geometry by material and articulated node. Source geometry uses metres, +Y up
and +Z forward. Neutral bodywork is grey; metal, rubber and emitters retain their
material colours. `BoardConfiguration` supplies configurable palette overrides.

SC uses split foot platforms joined by curved energy links. FC adds articulated
spine segments. `BoardArticulation` provides the steering arc and delayed segment
motion. `FieldDeformation` evaluates connector vertices in managed math, sharing
curve frames across longitudinal sections. `CurvedFieldSurface` reuses the Unity
mesh and vertex buffer, skips unchanged poses, and recalculates normals/bounds
when uploading a changed mesh. Avoid introducing IL2CPP vector/quaternion calls
inside the per-vertex loop.

Legendary models have exclusive geometry/art and cannot accept normal paint.
Their world rewards are defined by `WorldPlacements` and per-save discovery state.

`make model` exports the base mesh to `bin/models/base`; `make catalog` exports
all appearances and a manifest to `bin/models/catalog`. These are disposable,
ignored developer outputs, not runtime dependencies. Exporters do not render
inventory thumbnails. The required PNG resources remain under `assets/`.
