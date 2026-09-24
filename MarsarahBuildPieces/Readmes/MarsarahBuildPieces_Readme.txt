Marsarah Build Pieces v1.1.0
================================================================
Marsarah Build Pieces is a standalone collection of custom buildable pieces for Valheim, focused mainly on functional pieces, portals and light sources rather than large sets of architectural walls and floors.

The mod began as the custom build-piece section of Marsarah Tweaks and has now been separated into its own mod.

CURRENT CONTENT:
- 16 custom buildable pieces
- 1 custom craftable item (Portal Core)
- Pocket Portal
- Glacial Stone Portal
- Mystical Light Ward
- Smart Dropbox
- Small Sign
- 11 additional light-source pieces

All feature configs are synchronized using ServerSync.
MarsarahTweaks is an optional soft dependency and is NOT required to use this mod.


DEVELOPMENT NOTES
================================================================
DEEP NORTH / SPOILER NOTE:
Deep North build pieces are intentionally not included yet. I want to experience the new biome and progression myself before digging through the game files so I can play through it without spoiling the experience for myself.

After completing the Deep North, I plan to review its build pieces and other relevant content for possible additions to this mod.

AI USAGE DISCLOSURE:
The original build-piece features came from my MarsarahMod / Marsarah Tweaks projects. AI tools were later used to assist with porting and are now used as part of my development workflow for tasks such as debugging, refactoring, compatibility updates, researching game API changes, and documentation.

Development remains human-directed: I decide what features are added, how they should behave, and I write code, review and test the changes included in releases. The mod's logo was also created using generative AI.


RELATED MARSARAH MODS
================================================================
MarsarahTweaks
  Gameplay, balance, grind-reduction and quality-of-life tweaks.
  Marsarah Build Pieces includes optional integrations with several Tweaks features.

MarsarahUI
  Standalone UI and information improvements.

Neither mod is required to use Marsarah Build Pieces.


PERMISSIONS
================================================================
Reuploading this mod, whether in part or in full, is not permitted.
Anyone is free to take inspiration or implement similar features, but must do so with their own code and assets.


REQUIREMENTS
================================================================
BepInEx for Valheim:
https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/

Jotunn:
https://valheim.thunderstore.io/package/ValheimModding/Jotunn/

ServerSync is bundled with MarsarahBuildPieces.dll and does not need to be installed separately.

For multiplayer, install the mod on the SERVER and all CLIENTS so custom prefabs and synchronized configuration match.


INSTALLATION
================================================================
1. Install BepInEx for Valheim.
2. Install Jotunn.
3. Unpack the Marsarah Build Pieces .zip file.
4. Copy MarsarahBuildPieces.dll into Valheim/BepInEx/plugins.

Or use a mod manager.


CONFIGURATION
================================================================

The config file is automatically generated on first launch:
Valheim/BepInEx/config/Marsarah.MarsarahBuildPieces.cfg

All Build Pieces settings are synchronized with the server using ServerSync.
The configuration can be locked so only server administrators can change synchronized values.

Individual features can be enabled or disabled, and configurable values such as the Smart Dropbox and Mystical Light Ward radii can be adjusted through the config file or a compatible configuration manager.

Most settings can be changed during gameplay. Close and reopen the relevant build/crafting menu when needed for build or crafting menu changes to refresh.


BUILD PIECES
================================================================

======================== [SMART DROPBOX] =======================

► Description:
A functional storage piece that distributes deposited items to nearby supported storage after the Smart Dropbox is closed.

Items are only moved to containers that already contain the same item type, allowing the Dropbox to follow the storage organization you have already created instead of deciding where items belong itself.

► Build Category:
Furniture

► Build Cost:
Fine Wood: 10
Iron: 2
Thunderstone: 1

► Search Radius:
Default: 20 meters
Configurable: 5-50 meters

The radius circle is visible while positioning the placement ghost and is hidden after placement.

► Distribution Behavior:
Nearby supported storage is checked from nearest to farthest.
Only containers already containing a deposited item type are eligible for that item.
Items that cannot be transferred remain inside the Smart Dropbox.
Ward/access restrictions are respected.

► Multiplayer:
Smart Dropbox supports local worlds, multiplayer and dedicated servers.

► Disabled State:
Existing placed Smart Dropboxes remain in the world and can still be used as normal storage.
Automatic distribution stops.
The Smart Dropbox's special visual effect is disabled.

► Compatibility:
Smart Dropbox is automatically disabled when MultiUserChest is detected. The two systems modify container access in incompatible ways and using them together can cause inventory desynchronization or item loss.


======================== [POCKET PORTAL] =======================

-------------------------- [Portal Core] -----------------------
► Description:
  Custom item used to build the Pocket Portal.
  Visually based on a Surtling Core with a cyan-blue magical glow and custom icon.

► Crafting Station:
  Workbench level 4

► Recipe:
  Surtling Core: 5
  Fine Wood:     20
  Freeze Gland:  5
  Obsidian:      20

► Item Properties:
  Stack size: 1
  Weight:     10

► Tutorial:
  Obtaining the Portal Core triggers a custom Raven tutorial introducing the Pocket Portal.


------------------------ [Pocket Portal] -----------------------
► Description:
  A portal designed to be convenient to carry and relocate during exploration.
  Uses custom cyan-blue portal visuals.

► Build Category:
  Misc

► Build Cost:
  Portal Core: 1

► Resource Recovery:
  The Portal Core is recoverable when the Pocket Portal is destroyed, allowing the portal to be moved elsewhere.

► Placement Limit:
  Each player can have only ONE Pocket Portal placed at a time.
  The limit is tracked by the creator of the placed portal.
  Destroying the player's existing Pocket Portal allows another to be placed.


==================== [GLACIAL STONE PORTAL] ====================

► Description:
  Enables Valheim's unused stone portal prefab as a buildable portal with Mountain-tier requirements.
  This is not the Ashlands Stone Portal.

► Build Category:
  Misc

► Required Station:
  Stonecutter

► Build Cost:
  Stone:         6
  Freeze Gland:  2
  Surtling Core: 2

► Behavior:
  Functions as a normal portal and is registered with Valheim's portal systems.

► MarsarahTweaks Compatibility:
  When both mods are installed, this portal is recognized by Tweaks' Max Portals Per Player feature as a supported portal type.


==================== [MYSTICAL LIGHT WARD] =====================

► Description:
  A small blue-glowing magical Ward that keeps fueled light sources within its configured radius permanently fueled.
  Intended as a local and more immersive alternative to making every light in the world permanent.

► Build Category:
  Misc

► Required Station:
  Workbench

► Build Cost:
  Fine Wood: 2
  Silver:    1
  Obsidian:  2

► Radius:
  Default: 32 meters
  Configurable: 5-50 meters

  The radius circle is visible while positioning the placement ghost.
  The circle is hidden after the Ward is placed.

► Light Behavior:
  Fueled light sources inside an active Ward's radius are refilled to their maximum fuel.
  The Mystical Light Ward itself does not require fuel.

► Ward Behavior:
  The normal vanilla Ward protection/ownership behavior is removed.
  The vanilla Ward Raven tutorial is also removed from this custom piece.
  The piece is not used like a normal Ward and instead exists only for its light-area mechanic.

► Disabled State:
  Existing placed Wards remain in the world.
  Their light-refueling effect stops.
  Their magical glow, particles and light effects are disabled.
  Hover text identifies the Ward as dormant.

► MarsarahTweaks Permanent Lights:
  Mystical Light Ward and Tweaks Permanent Lights are mutually exclusive.

  If both are enabled when compatibility initializes:
    - Mystical Light Ward remains enabled.
    - Tweaks Permanent Lights is disabled.

  If Permanent Lights is enabled later while the Ward is active:
    - Mystical Light Ward is disabled.

  If Mystical Light Ward is enabled while Permanent Lights is active:
    - Permanent Lights is disabled.

  Disabling one does not automatically re-enable the other.


======================== [EXTRA LIGHTS] =========================

The Extra Lights config controls all custom light pieces below.

------------------- [Green Standing Brazier] -------------------
► Description:
  Green-flame version of the Standing Brazier with recolored fire, glow and icon.

► Build Category:
  Furniture

► Required Station:
  Forge

► Normal Build Cost:
  Bronze:      5
  Guck:        2
  Fenris Claw: 3

► Fuel:
  Guck

► Resource Recovery:
  The fuel portion of the build cost is not recovered when destroyed.

► Tweaks Compatibility:
  Cheaper Build Pieces Amounts: Bronze 5 → 3
  Permanent Lights:             Guck 2 → 5


------------------------ [Silver Sconces] ----------------------
Three Silver Sconce variants are included:

1. Silver Sconce
   Fuel: Resin

2. Blue-burning Silver Sconce
   Fuel: Greydwarf Eye

3. Green-burning Silver Sconce
   Fuel: Guck

► Build Category:
  Furniture

► Required Station:
  Forge

► Normal Build Cost - each variant:
  Ancient Bark:     2
  Silver:           2
  Respective Fuel:  2

► Resource Recovery:
  The fuel portion of the build cost is not recovered when destroyed.

► Tweaks Compatibility:
  Cheaper Build Pieces Amounts: Silver 2 → 1
  Permanent Lights:             Respective Fuel 2 → 6


----------------- [Silver Hanging Braziers] --------------------
Three Silver Hanging Brazier variants are included:

1. Silver Hanging Brazier
   Fuel: Coal

2. Blue-burning Silver Hanging Brazier
   Fuel: Greydwarf Eye

3. Green-burning Silver Hanging Brazier
   Fuel: Guck

► Build Category:
  Furniture

► Required Station:
  Forge

► Normal Build Cost - each variant:
  Silver:           5
  Chain:            1
  Respective Fuel:  2

► Resource Recovery:
  The fuel portion of the build cost is not recovered when destroyed.

► Tweaks Compatibility:
  Cheaper Build Pieces Amounts: Silver 5 → 3
  Permanent Lights:             Respective Fuel 2 → 5


------------------- [Colored Dvergr Lanterns] ------------------
Four colored Dvergr Lantern variants are included:

1. Blue-colored Dvergr Wall Lantern
2. Green-colored Dvergr Wall Lantern
3. Blue-colored Dvergr Pole Lantern
4. Green-colored Dvergr Pole Lantern

► Build Category:
  Furniture

► Required Station:
  Black Forge

► Normal Wall Lantern Cost:
  Copper:  2
  Lantern: 1
  Chain:   1

► Normal Pole Lantern Cost:
  Copper:  3
  Lantern: 1
  Chain:   1

► Tweaks - Cheaper Build Pieces Amounts:
  Wall Lantern Copper: 2 → 1
  Pole Lantern Copper: 3 → 2

► Tweaks - Brighter Lanterns:
  The custom colored lanterns follow Tweaks' brighter-lantern behavior when that feature is enabled.

  Default custom light settings:
    Intensity: 1.5
    Range:     6

  With Brighter Lanterns:
    Intensity: 2
    Wall range: 9
    Pole range: 15
    Flicker is also reduced/slowed to match the brighter-light behavior.


========================== [SMALL SIGN] =========================

► Description:
A smaller version of the normal wooden sign for places where the vanilla sign is a bit too large.

► Build Category:
Furniture

► Size:
Approximately 75% of the normal wooden sign.

► Build Cost:
Wood: 1
Coal: 1


MARSARAHTWEAKS COMPATIBILITY SUMMARY
================================================================
MarsarahTweaks is optional. Marsarah Build Pieces works without it.

Cheaper Build Pieces Amounts
----------------------------------------------------------------
When enabled in Tweaks, relevant custom light material costs are reduced:
- Green Standing Brazier: Bronze 5 → 3
- Silver Sconces: Silver 2 → 1
- Silver Hanging Braziers: Silver 5 → 3
- Colored Dvergr Wall Lanterns: Copper 2 → 1
- Colored Dvergr Pole Lanterns: Copper 3 → 2

Changes are refreshed when the Tweaks config changes.

Permanent Lights
----------------------------------------------------------------
When enabled in Tweaks, fueled custom lights use increased build fuel costs matching their respective maximum fuel amounts:
- Green Standing Brazier: fuel 2 → 5
- Silver Hanging Braziers: fuel 2 → 5
- Silver Sconces: fuel 2 → 6

Permanent Lights and Mystical Light Ward cannot remain enabled at the same time.

Brighter Lanterns
----------------------------------------------------------------
Colored Dvergr Lanterns automatically follow Tweaks' Brighter Lanterns setting and update if that setting changes.

Max Portals Per Player
----------------------------------------------------------------
Tweaks recognizes the Glacial Stone Portal and can enforce its configured per-player portal limit when both mods are installed.


FUTURE PLANS
================================================================

DEEP NORTH
----------------------------------------------------------------
Deep North build pieces and other relevant content are intentionally deferred until I have completed the biome myself to avoid spoilers.

These are development plans and may change as the mods and game evolve.


FEEDBACK
================================================================
Suggestions and bug reports are welcome on the Posts or Bugs tabs of the Nexusmods page.
Thanks for checking out Marsarah Build Pieces!


CREDITS
================================================================
ValheimModding team - Jotunn
Blaxxun-bloop - ServerSync


VERSION HISTORY
================================================================

v1.1.0
- General:
  - Reorganized configuration entries to remove numbered setting names and use Configuration Manager ordering instead.
- Smart Dropbox:
  - Added a new functional storage piece that distributes deposited items to nearby supported storage containing the same item type.
- Small Sign:
  - Added a smaller version of the vanilla wooden sign.
- Mystical Light Ward:
  - Recolored the Mystical Light Ward icon.

v1.0.0
- Initial standalone release.

- Standalone Build Pieces Mod:
  - Moved the custom build-piece system previously included in Marsarah Tweaks into MarsarahBuildPieces.
  - Added Jotunn integration for custom prefabs, pieces and recipes.
  - Added ServerSync configuration support.
  - Added MarsarahTweaks as an optional soft dependency.

- Pocket Portal:
  - Added the Pocket Portal and craftable Portal Core.
  - Portal Core recipe: 5 Surtling Cores, 20 Fine Wood, 5 Freeze Glands and 20 Obsidian at a level 4 Workbench.
  - Portal Core stack size 1 and weight 10.
  - Pocket Portal costs 1 Portal Core.
  - Limited Pocket Portals to one placed portal per player.
  - Added a custom Raven tutorial.
  - Updated Pocket Portal ownership / max-build handling.

- Glacial Stone Portal:
  - Added the unused vanilla stone portal as a buildable Mountain-tier portal.
  - Requires a Stonecutter.
  - Build cost: 6 Stone, 2 Freeze Glands, 2 Surtling Cores.
  - Added MarsarahTweaks Max Portals Per Player integration.

- Mystical Light Ward:
  - Added a local permanent-light mechanic that refuels fueled lights inside its radius.
  - Default radius 32m, configurable from 5m to 50m.
  - Added placement radius visualization.
  - Removed normal Ward protection/ownership behavior.
  - Removed the inherited vanilla Ward Raven tutorial.
  - Existing Wards become dormant when the feature is disabled.

- Extra Lights:
  - Added Green Standing Brazier using Guck as fuel.
  - Added Silver Sconce variants using Resin, Greydwarf Eyes and Guck.
  - Added Silver Hanging Brazier variants using Coal, Greydwarf Eyes and Guck.
  - Added blue/green Dvergr Wall Lanterns.
  - Added blue/green Dvergr Pole Lanterns.

- MarsarahTweaks Compatibility:
  - Cheaper Build Pieces Amounts integration for relevant custom light costs.
  - Permanent Lights integration for fueled custom-light costs.
  - Permanent Lights and Mystical Light Ward are mutually exclusive.
  - Brighter Lanterns integration for colored Dvergr Lanterns.
  - Max Portals Per Player support for Glacial Stone Portal.
