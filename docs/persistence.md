# Settings and saved state

Companion registers one existing `ConfigFile` at `BepInEx/config/hoba.cfg`.
Stable entries: Audio/BaseVolume (0.20), Audio/AccelerationVolumeBoost (0.075),
Discovery/ShowLegendariesOnCompass (false). Sliders span 0–1 in 0.001 increments.
Gameplay reads the typed settings live; UI changes are persisted by Companion.
Do not add a second owner for this configuration file.

The native save stores inventory instances, including the private deployed-board
inventory. A successful native save writes additional HOBA state beside the game
saves under `HOBA/<SHA256-of-native-save>.bin`. It contains the deployed product,
area, ground position, orientation and chest-to-legendary discoveries. Binary
version 1 starts with the HOBA magic and version. Legacy JSON remains readable;
binary takes precedence. Writes use a temporary file and atomic replacement.

Loading first clears session state and then restores only the selected save's
checkpoint. A board visual is restored only when its corresponding native
inventory instance exists in the deployed inventory. Unsaved discoveries and
parking changes are discarded on title return/reload. Missing old checkpoints
recover deployed items to inventory rather than inventing a parking position.
Backups must include the save-side HOBA directory. The file format is not encrypted.

Chest IDs and product IDs are persistent keys. Never change existing IDs when
renaming display labels or adjusting geometry. Three chests award three distinct
random legendaries within a saved playthrough. Reward failure leaves the chest
unclaimed. The compass marker reflects that same per-save claim state.

Version 0.1.30 also binds startup-only board profiles, base prices and material
palettes into this same config file before Companion registration. Those entries
are not additional Companion UI controls. See [board configuration](board-configuration.md).

Version 0.1.31 adds `Camera/FovIncrease`, default 10 degrees, range 0–30,
to Companion's Camera tab. It is the additive full-speed bonus, not an absolute
FOV. Speed uses the current model's normal top speed (crouching cannot exceed the
configured maximum bonus); forward and reverse use speed magnitude. A smooth
speed curve and time-based blend avoid snapping. Zero fades the effect out.
The native output camera is adjusted after Cinemachine updates, with the prior
bonus removed before the next update to prevent accumulation. Dismount/session
cleanup restores the underlying view. Native zoom remains the base calculation.

`Discovery/ShowParkedHobaOnCompass` defaults to true and updates live. A native
compass marker follows the deployed board's transform and area visibility. It is
disabled while mounted and removed on pickup/disposal; the scanner is independent.
