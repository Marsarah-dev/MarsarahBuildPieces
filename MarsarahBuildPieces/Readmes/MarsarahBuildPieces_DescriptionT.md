# <strong> Marsarah Build Pieces </strong>

**Version:** 1.0.0  
**Author:** Marsarah

---

## <strong> 🧰 About the Mod </strong>

Marsarah Build Pieces is a standalone collection of custom buildable pieces for Valheim, focused mainly on **functional pieces, portals and new light sources** rather than large sets of architectural walls and floors.

The mod began as the custom build-piece section of **Marsarah Tweaks** and has now been separated into its own mod. It currently adds **14 buildable pieces** plus the craftable **Portal Core** item, including the Pocket Portal, Glacial Stone Portal, Mystical Light Ward, Silver light sources, a Green Standing Brazier and colored Dvergr Lanterns.

All feature configs are synchronized using **ServerSync**, and the mod includes optional compatibility with MarsarahTweaks when both are installed.

---

## **Development Notes**

**Deep North / Spoiler Note:** Deep North build pieces are intentionally not included yet.  
I want to experience the new biome and progression myself before digging through the game files, so I can play through it without spoiling the experience for myself.  
After completing the Deep North, I plan to review its build pieces and other relevant content for possible additions to this mod.

**AI Usage Disclosure:** The original build-piece features came from my MarsarahMod / Marsarah Tweaks projects. AI tools were later used to assist with porting and are now used as part of my development workflow for tasks such as debugging, refactoring, compatibility updates, researching game API changes, and documentation.  
Development remains human-directed: I decide what features are added, how they should behave, and I write code, review and test the changes included in releases. The mod's logo was also created using generative AI.

### **Related Marsarah Mods**

- [**MarsarahTweaks**](https://old.thunderstore.io/c/valheim/p/Marsarah/MarsarahTweaks/) - Gameplay, balance, grind-reduction and quality-of-life tweaks. BuildPieces includes optional compatibility with several Tweaks features.
- [**MarsarahUI**](https://old.thunderstore.io/c/valheim/p/Marsarah/MarsarahUI/) - Standalone UI and information improvements.
- Neither mod is required to use Marsarah Build Pieces.

---

## <strong> 🔒 Permissions </strong>

Reuploading this mod, whether in part or in full, is **not permitted**.  
Anyone is free to take inspiration or implement similar features, but must do so with their own code and assets.

---

## <strong> 🧱 Requirements </strong>

This mod requires:
- **BepInEx for Valheim**  
  https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/
- **Jotunn**  
  https://valheim.thunderstore.io/package/ValheimModding/Jotunn/

For multiplayer, install the mod on the **server** and **all clients** so synchronized configuration and custom prefabs match.

---

## <strong> 📦 Installation </strong>

- Unpack the `.zip` file and copy `MarsarahBuildPieces.dll` into your `Valheim/BepInEx/plugins` folder.
- Or use a mod manager.

Make sure **Jotunn** is also installed.

---

## <strong> ⚙️ Configuration </strong>

A config file is generated on first launch:  
`Valheim/BepInEx/config/Marsarah.MarsarahBuildPieces.cfg`

All BuildPieces settings are synchronized with the server. The configuration can also be locked so only server administrators can change synchronized values.

Main settings:
- **Pocket Portal**
- **Glacial Stone Portal**
- **Extra Lights**
- **Mystical Light Ward**
- **Mystical Light Ward Radius:** 5-50 meters, default **32m**

Feature toggles can be changed during gameplay. Close and reopen the relevant build/crafting menu when needed for the menu to refresh.

---

## <strong> 💰 Donations </strong>

My mods are and will always be free to use. If you'd like to support my work, you can donate here:  
https://paypal.me/Marsarah9

---

## <strong> ⚡ Main Features </strong>

### <strong>🔧 Pocket Portal</strong>

A portable portal designed to make exploration more convenient.

- Adds a craftable **Portal Core** item.
- Portal Core is crafted at a **level 4 Workbench** using:
  - 5 Surtling Cores
  - 20 Fine Wood
  - 5 Freeze Glands
  - 20 Obsidian
- Portal Core has a maximum stack size of **1** and weighs **10**.
- Building a Pocket Portal consumes **1 Portal Core**.
- Each player can only have **one Pocket Portal** placed at a time.
- Destroying the existing portal allows that player to place it somewhere else.
- Crafting the Portal Core introduces the Pocket Portal through a custom Raven tutorial.

---

### <strong>🔧 Glacial Stone Portal</strong>

Enables an unused vanilla stone portal as a proper buildable portal with a cold/glacial progression theme.

- Available from Mountain-tier materials.
- Requires a **Stonecutter** nearby.
- Build cost:
  - 6 Stone
  - 2 Freeze Glands
  - 2 Surtling Cores
- Functions as a normal portal.
- This is **not** the Ashlands Stone Portal.
- If MarsarahTweaks is installed, it integrates with **Max Portals Per Player** as its own supported portal type.

---

### <strong>🔧 Mystical Light Ward</strong>

A smaller, blue-glowing magical Ward that keeps fueled light sources inside its area permanently lit.

- Available from Mountain-tier materials.
- Requires a **Workbench** nearby.
- Build cost:
  - 2 Fine Wood
  - 1 Silver
  - 2 Obsidian
- Default effect radius: **32 meters**.
- Configurable from **5 to 50 meters**.
- The effect radius is shown while placing the Ward and hidden after placement.
- Refuels fueled light sources inside its radius to their maximum fuel.
- Has **no normal Ward protection/ownership behavior**.
- Existing Wards become dormant and lose their glow/effect if the feature is disabled, rather than disappearing from the world.

---

### <strong>🔧 Extra Lights</strong>

Adds **11 additional light-source build pieces** across Mountain and Mistlands progression.

#### **Green Standing Brazier**
- Green-flame version of the Standing Brazier.
- Requires a **Forge**.
- Build cost: 5 Bronze, 2 Guck, 3 Fenris Claws.
- Uses **Guck** as fuel.

#### **Silver Sconces**
Three Silver Sconce variants:
- Silver Sconce - Resin
- Blue-burning Silver Sconce - Greydwarf Eye
- Green-burning Silver Sconce - Guck

Each requires a **Forge** and normally costs 2 Ancient Bark, 2 Silver, and 2 of the respective fuel.

#### **Silver Hanging Braziers**
Three Silver Hanging Brazier variants:
- Silver Hanging Brazier - Coal
- Blue-burning Silver Hanging Brazier - Greydwarf Eye
- Green-burning Silver Hanging Brazier - Guck

Each requires a **Forge** and normally costs 5 Silver, 1 Chain, and 2 of the respective fuel.

#### **Colored Dvergr Lanterns**
Adds blue and green versions of both Dvergr Lantern styles:
- Blue-colored Dvergr Wall Lantern
- Green-colored Dvergr Wall Lantern
- Blue-colored Dvergr Pole Lantern
- Green-colored Dvergr Pole Lantern

Requires a **Black Forge**.

Wall Lantern cost: 2 Copper, 1 Lantern, 1 Chain.  
Pole Lantern cost: 3 Copper, 1 Lantern, 1 Chain.

---

## <strong> 🔗 MarsarahTweaks Compatibility </strong>

MarsarahTweaks is an **optional soft dependency**. BuildPieces works normally without it.

### **Cheaper Build Pieces Amounts**
Custom light costs follow relevant Tweaks reductions:
- Green Standing Brazier: Bronze **5 → 3**
- Silver Sconces: Silver **2 → 1**
- Silver Hanging Braziers: Silver **5 → 3**
- Colored Dvergr Wall Lanterns: Copper **2 → 1**
- Colored Dvergr Pole Lanterns: Copper **3 → 2**

### **Permanent Lights**
Custom fueled light build costs follow Tweaks' Permanent Lights philosophy by including their maximum fuel amount:
- Green Standing Brazier fuel: **2 → 5**
- Silver Hanging Brazier fuel: **2 → 5**
- Silver Sconce fuel: **2 → 6**

**Permanent Lights and Mystical Light Ward are mutually exclusive.** Enabling one while the other is active disables the other option.

### **Brighter Lanterns**
The custom blue and green Dvergr Lanterns also use the brighter light settings when Tweaks' **Brighter Lanterns** feature is enabled.

### **Max Portals Per Player**
The Glacial Stone Portal is recognized by Tweaks' **Max Portals Per Player** feature when both mods are installed.

---

## <strong> 🔮 Future Plans </strong>

A planned future feature is the **Smart Dropbox**: a functional container that automatically distributes deposited items into nearby containers that already contain the same item/type.

Deep North build pieces and other relevant content will be reviewed after I have completed the biome myself.

The above plans may change as development continues.

---

## <strong> 💬 Feedback </strong>

Suggestions and bug reports are welcome on the **Posts** or **Bugs** tabs of the Nexusmods page.  
Thanks for checking out Marsarah Build Pieces!

## <strong> 🧑‍🤝‍🧑 Credits </strong>

- ValheimModding team - for Jotunn
- Blaxxun-bloop - for ServerSync

## <strong> 📜 Version History </strong>

Check the Changelog tab.
