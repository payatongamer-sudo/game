# MODLOG: Brewing Point (Schedule I × Minecraft: Java Edition)

Journal for whoever continues this, human or agent. Read this before touching code.

## The design (agreed with the creator)

- **What it is:** Minecraft's Brewing Stand and brewing ingredients in Schedule I, with Minecraft's brewing rules.
- **Host (primary):** Schedule I. **Companion:** Minecraft: Java Edition, read from the player's own install (jar plus
  asset store) while Schedule I runs. Minecraft doesn't need to be running, so there's no `recipe.together`.
- **First minute:** buy the Brewing Stand at Dan's Hardware, then Nether Wart and Sugar at the Gas-Mart. Place
  the stand, brew OG Kush + Nether Wart, then + Sugar, and the mix comes out Energizing, with Minecraft's brewing sound.
- **Players:** co-op through Schedule I's own Steam co-op, up to 4 players.
- **Licence:** CC BY-NC-SA 4.0, remix allowed.
- First version (decided): the 10 ingredients in `sheets/ingredients.json`, one station, and the Awkward-base,
  consumption and corruption rules. Out of scope: modifiers (Redstone/Glowstone as duration/level), splash
  behaviour, Steve the supplier, and custom 3D models for ingredients in the world (they reuse Cuke's stored body).

## Route and why

- **Schedule I** is Unity IL2CPP on the default branch (0.4.7). Its established route is **MelonLoader** (the
  `version.dll` proxy) plus **S1API** (MIT) for items, effects, mixing reactions, shops and build events, and
  **Harmony** patches on `MixingStationInterface` for the brewing-rule gate. Schedule I isn't in Melty's catalogue,
  so no loader comes with it: MelonLoader 0.7.3 (Apache-2.0) and S1API 3.2.1 are bundled in the release with credit.
- **Minecraft** is read, not run: the client jar is a zip (en_us lang, item textures, block models), and sounds
  go through `sounds.json`, then the asset index, then `assets/objects/<hash>`. Both the official launcher layout
  (`versions/<v>/<v>.jar`) and the Prism/MultiMC layout (`libraries/com/mojang/minecraft/<v>/minecraft-<v>-client.jar`)
  are handled. Melty passes `{game:minecraft-java}` as `--brewingpoint-minecraft=` and `BREWINGPOINT_MINECRAFT_DIR`.
- **Brewing Stand** = `BuildableItemCreator.CloneFrom("mixingstation")`. That keeps the networked Mixing Station
  prefab, so co-op sync is the game's own. On `BuildEvents.OnBuildableItemInitialized` (every peer), the station's
  meshes are hidden and Minecraft's `brewing_stand` + `brewing_stand_bottle0-2` / `empty0-2` block models are meshed
  in their place. `MaxMixQuantity` = 3 and `MixTimePerItem` = 7 (1 in-game minute = 1 real second).
- **Brewing rules:** the custom effects Awkward and Weakness (S1API `EffectCreator`, zero mix push) plus
  `MixReactions` rules from `sheets/reactions.json`, which run after the game's own `EffectMixCalculator.MixProperties`,
  identically on every peer. The "Nether Wart first" gate is on the mixing screen (`UpdateInstruction`,
  `UpdateBeginButton`, `BeginButtonPressed`).

## Versions

| Thing | Version | Where it comes from |
|---|---|---|
| Schedule I | 0.4.7 IL2CPP (default branch) | player's Steam install |
| MelonLoader | 0.7.3 | github.com/LavaGang/MelonLoader releases (`MelonLoader.x64.zip`) |
| S1API | 3.2.1 | github.com/ifBars/S1API releases (Plugins folder) / NuGet `S1API.Forked` for compiling |
| NVorbis | 0.10.5 | NuGet |
| Minecraft: Java | 1.14+ (any launcher) | player's install |

## Verified so far (cloud session, no games present)

- `python3 tools/preflight.py`: 10 sheets, every cell filled, every cross-sheet reference resolves.
  The 8 hook rows are **unverified in game**.
- `dotnet test src/BrewingPoint.Core.Tests`: **21/21 pass**. They cover the locator (official/Prism/instance
  folder, Melty arg > env > config > defaults), lang fallback keys, sound event → asset index → hashed object,
  Ogg decode (a generated sine tone), the block-model mesher (outward winding, Minecraft→Unity z-mirror, UV
  orientation, parent textures, element rotation), the brewing gate, and the reactions (Awkward use-up, corruption).
  All fixtures are synthetic; no Minecraft content was used.
- `dotnet build tools/typecheck`: the mod compiles against the **real** MelonLoader 0.7.3, HarmonyX, Il2CppInterop 1.5.3
  and S1API 3.2.1 packages, plus **stubs** for the Unity and Schedule I types (shapes taken from S1API and PocketPlug,
  which compile against 0.4.7). It proves the mod's own code, not that the stubs match the game.

## Not verified yet (needs a PC with Schedule I + Minecraft: Java)

1. Build `src/BrewingPoint` against `MelonLoader/Il2CppAssemblies` and fix any name drift (likely suspects:
   `MixingStation.Clock`, `MixTimePerItem` setter, `InstructionLabel` type, `ItemInstance.Definition.Name`).
2. In game: `UserData/BrewingPoint/checks.log` should show `minecraft_found`, `minecraft_content`, `effects`,
   `ingredients`, `brewing_stand`, `reactions`, `shops` and `brew_order` all **ok**. `brew_order` runs the game's
   own mix maths: OG Kush + Nether Wart → has Awkward; + Sugar → Energizing, no Awkward; + Fermented Spider Eye →
   Weakness, Energizing corrupted.
3. Test mode (`UserData/BrewingPoint.test` or `--brewingpoint-test`) gives a stand, OG Kush, ingredients and $2000.
   Place the stand: check the Minecraft model sits on the floor, faces the right way, and that the bottles fill and the
   brew sound plays (`stand_visuals ok`). Brew Sugar without Nether Wart: Begin is disabled with the message.
4. Product ID `ogkush` and the shop anchor items `cuke` and `mixingstation` are assumed. Check them in the log.
5. Co-op: host + one joiner (two Steam accounts/PCs). Both see the stand model and the brew syncs.
6. Screenshot/clip for the listing: the stand in the motel room mid-brew, and the mixing screen showing Awkward → Energizing.

## Known gaps and risks

- The gate is on the mixing screen only. Chemist employees running a Brewing Stand aren't gated (yet).
- IL2CPP (default branch) only. The Mono `alternate` beta branch would need a second build.
- If the mod is removed, saves with placed Brewing Stands or Awkward/Weakness products reference unknown IDs.
- Ingredients reuse Cuke's world model when placed on shelves (S1API default stored body).

## Publishing (Melty)

- `melty.gg` (and `maven.fabricmc.net`, `meta.fabricmc.net`, `piston-meta.mojang.com`) are **blocked by this
  cloud environment's network policy**, so no Melty tool has been called yet: no game_info, search_mashups,
  inspect_package, one_click_check or draft.
- `docs/melty-recipe.draft.json` is a starting point to check with `inspect_package` / `validate_recipe` /
  `one_click_check`. It isn't a validated recipe, so it's not saved as `melty.json`.
- `docs/LISTING.md` holds the draft listing text.
