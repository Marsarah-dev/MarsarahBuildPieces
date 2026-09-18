## <strong> 📜 Version History </strong>

v1.0.1
- Reorganized configuration entries to remove numbered setting names and use Configuration Manager ordering instead.

v1.0.0
- **Initial standalone release.**

- **Standalone Build Pieces Mod:**
  - Moved the custom build-piece system previously included in Marsarah Tweaks into the standalone MarsarahBuildPieces mod.
  - Added Jotunn integration for custom prefabs, pieces and recipes.
  - Added ServerSync configuration support.
  - Added MarsarahTweaks as an optional soft dependency for cross-mod compatibility.

- **Pocket Portal:**
  - Added the Pocket Portal and craftable Portal Core.
  - Portal Core is crafted at a level 4 Workbench using 5 Surtling Cores, 20 Fine Wood, 5 Freeze Glands and 20 Obsidian.
  - Portal Core has a maximum stack size of 1 and weighs 10.
  - Pocket Portal costs 1 Portal Core to build.
  - Limited Pocket Portals to one placed portal per player.
  - Added a custom Raven tutorial when obtaining the Portal Core.
  - Updated Pocket Portal ownership / max-build handling.

- **Glacial Stone Portal:**
  - Added the unused vanilla stone portal as a buildable Mountain-tier portal.
  - Requires a Stonecutter.
  - Build cost: 6 Stone, 2 Freeze Glands, 2 Surtling Cores.
  - Added MarsarahTweaks Max Portals Per Player integration.

- **Mystical Light Ward:**
  - Added a local, immersive permanent-light mechanic.
  - Refuels fueled light sources inside its radius to maximum fuel.
  - Default radius is 32m and can be configured from 5m to 50m.
  - Added a placement radius indicator.
  - Removed the normal Ward protection/ownership behavior.
  - Removed the inherited vanilla Ward Raven tutorial.
  - Existing Wards become dormant and their magical glow/effects are disabled when the feature is turned off.

- **Extra Lights:**
  - Added Green Standing Brazier using Guck as fuel.
  - Added Silver Sconce variants using Resin, Greydwarf Eyes or Guck.
  - Added Silver Hanging Brazier variants using Coal, Greydwarf Eyes or Guck.
  - Added blue and green Dvergr Wall Lanterns.
  - Added blue and green Dvergr Pole Lanterns.
  - Added recolored icons, flames, emissive materials and lighting for the new variants.

- **MarsarahTweaks Compatibility:**
  - Cheaper Build Pieces Amounts adjusts relevant custom light material costs.
  - Permanent Lights adjusts custom fueled-light build costs to include maximum fuel amounts.
  - Permanent Lights and Mystical Light Ward are mutually exclusive.
  - Brighter Lanterns applies matching brighter settings to colored Dvergr Lanterns.
  - Max Portals Per Player recognizes the Glacial Stone Portal.
