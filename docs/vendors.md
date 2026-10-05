# Dealer and inventory implementation

`WorldPlacements.Vendors` defines the production dealer position, appearance and
stable shop ID. `VendorController` spawns that dealer when its area is ready and
cleans it up on travel/session changes. There is no runtime dealer editor,
appearance cycling, JSON placement import/export or placement keyboard shortcut.

`BoardShop` clones the native vendor interaction and floating label, fits the
trade bounds to the NPC and opens the game's shop UI. The catalogue offers nine
neutral boards and nine family-specific paints. Legendaries cannot be bought or
sold. Base prices come from `hoba.cfg`; native economy adjustments still apply.

Inventory Use and assigned quick actions share `BoardItem.QueueUse` and
`RideController.UseBoard`: transfer the owned item into the deployed inventory,
create its visual and mount directly. No numeric-key deployment shortcut exists.
Pickup transfers that same instance back. Paint completion performs a guarded
item exchange; cancellation never consumes a can. See `PaintTransaction` checks.

Keep product and dealer IDs stable. Save ownership remains native; deployed poses
and chest discoveries use the per-save binary state described in
[persistence](persistence.md). Placement or appearance changes require source
edits and a rebuild. See [board configuration](board-configuration.md) for balance
and palette configuration.
