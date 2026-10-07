# Board configuration

`BoardConfiguration.Load` binds all model, price and palette entries into the
same `BepInEx/config/hoba.cfg` used by Companion. Missing entries are generated
on startup. Restart after editing this catalogue: it is read before item database
registration and model construction. Hotkey, audio and discovery controls remain live.

## Quick-deploy hotkey

Press **Q** to deploy and mount an owned board without opening the inventory.
HOBA uses the last selected board during the current session, falling back to
an available board in your inventory. An already deployed board stays where it is.

In Mod Companion → HOBA → General, turn **Enable board hotkey** off to disable
the shortcut; inventory use still works. The toggle defaults to on. Rebind
**Take out board** in the Controls tab; the gamepad binding is initially empty.
Both settings persist in `hoba.cfg` and take effect without restarting.
The shortcut is suppressed during menus, loading and rebinding, and uses the
same riding and inventory checks as deploying from the inventory.

## Ride profiles and board prices

Every design has a section: `Board.hoba-mk1` through `Board.hoba-mk3`,
`Board.sc-mk1` through `Board.sc-mk3`, `Board.fc-mk1` through `Board.fc-mk3`,
plus `Board.hoba-foundry`, `Board.sc-eclipse` and `Board.fc-aurora`.
All finishes of a model share its profile and price. Defaults intentionally
retain identical handling across models until balance changes are chosen.

| Key | Default | Units |
| --- | --- | --- |
| MaxSpeed | 9 | m/s, both forward and reverse |
| Acceleration | 4.5 | m/s² |
| Braking | 12 | m/s² while braking to reverse |
| CoastDeceleration | 3 | m/s², including parked momentum |
| TurnRate | 160 | degrees/s |
| CrouchedTurnRate | 260 | degrees/s |
| CrouchSpeedBonus | 0.15 | fraction added to top speed |
| BankAngle | 8 | degrees at full speed |
| CrouchedBankAngle | 14 | degrees at full speed |
| HoverHeight | 0.38 | metres, loaded |
| UnloadedHeight | 0.65 | metres, parked |
| HoverMotionScale | 1 | vertical animation multiplier |
| Price | 2000 / 3000 / 4000 | Mark I / II / III base price |

Native economy adjustments still apply. Legendary prices are reference values;
legendaries remain unavailable for purchase/sale. Config cannot change persistent
IDs, family membership, chest claims or legendary restrictions.

## Paint prices and colors

`Paint.<finish>.Price` defaults to 300 for each of the nine paint consumables.
Finish IDs: industrial, porcelain, copperwork, obsidian, rescue, glacier, reactor,
relic and crimson.

`Palette.<finish>` controls each finish across all marks in its family.
`Palette.neutral` controls unpainted boards; legendary palettes are
`Palette.hoba-foundry`, `Palette.sc-eclipse`, `Palette.fc-aurora`.

Colors are invariant RGB triples in the 0–1 range, e.g.:

```ini
[Palette.industrial]
shell = 0.39, 0.51, 0.045
yellow = 1, 0.72, 0.025
```

Generated keys include shell, edge, grip, metal, cyan, core, mark, yellow (trim),
red (cable), copper, signal, field, exhaust, exhaustBody, spillLight, fieldSleeve
and fieldSleeveEmission. Legendary palettes also include paintA, paintB,
paintPaper and paintInk. Emission animation retains its intensity modulation but
uses configured colors. Spray cans use their paint's palette. Inventory icons
remain baked PNGs; color changes do not regenerate thumbnails.

Values are bounded; malformed colors return to defaults with a warning. Deleting
an entry restores its code-defined default on next startup. Defaults in code
bootstrap missing configuration; runtime values come from the config snapshot.

## Validation

`make test` checks profile behavior, product identity, prices and palette routing.
`make test-config GAME_PATH="..."` exercises the real BepInEx ConfigFile with
isolated temporary files, persistence/reload, invalid values and audio preservation.
`make package` includes both checks. The latter requires local BepInEx.Core.dll,
but does not launch the game or modify its configuration.

Normal paint entries matching the pre-0.1.35 defaults are upgraded to the brighter
palette in `BoardDesigns`; other configured colors are preserved.
