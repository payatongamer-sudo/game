# Brewing Point

**Minecraft's Brewing Stand comes to Hyland Point. Nether Wart first, then brew.**

A Schedule I mashup with Minecraft: Java Edition. You play Schedule I. Minecraft brings its Brewing Stand,
its brewing ingredients and its brewing rules, all read from **your own Minecraft install** while the game runs.
Nothing from either game is shipped.

> **Status: source-only, not yet tested in the game.** The core logic (Minecraft file reading, the brewing
> stand mesh, the brewing rules) is unit-tested, and the mod compiles against the real MelonLoader and S1API packages. It still has to be built against
> Schedule I's own assemblies and played on a PC that has both games. See [MODLOG.md](MODLOG.md).

## What you get

- **A Minecraft Brewing Stand** at Dan's Hardware ($350). It's a real Schedule I mixing station that looks
  like Minecraft's: the model, textures and bottles are built from your Minecraft jar. It brews 3 bottles at a time
  (about 20 seconds) and plays Minecraft's brewing sound when it's done.
- **Ten Minecraft brewing ingredients** at both Gas-Marts, with their Minecraft names and icons:

  | Ingredient | Price | Brewed onto an Awkward batch it adds |
  |---|---|---|
  | Nether Wart | $3 | *Awkward* (the base everything else needs) |
  | Sugar | $3 | Energizing |
  | Spider Eye | $4 | Toxic |
  | Redstone Dust | $6 | Electrifying |
  | Glistering Melon Slice | $6 | Refreshing |
  | Golden Carrot | $7 | Bright-Eyed |
  | Fermented Spider Eye | $7 | *Weakness*, and corrupts: Energizing/Athletic/Anti-Gravity → Sedating, Bright-Eyed → Sneaky, Refreshing → Toxic |
  | Gunpowder | $8 | Explosive |
  | Glowstone Dust | $9 | Glowing |
  | Rabbit's Foot | $10 | Anti-Gravity |

- **Minecraft's brewing rules.** The Brewing Stand only takes Minecraft ingredients. Effect ingredients need an
  Awkward batch, so brew Nether Wart in first, and the Awkward base gets used up when the effect goes in.
  Fermented Spider Eye needs no base. Minecraft ingredients don't work in a normal Mixing Station.
  Schedule I's own mixing maths still applies on top, so existing effects can still change.

**The first minute:** buy the Brewing Stand at Dan's Hardware and some Nether Wart and Sugar at the Gas-Mart.
Place the stand in your motel room and brew OG Kush with Nether Wart, then brew that with Sugar. Out comes a new
mix with Energizing.

## What you need

- **Schedule I** on Steam (Windows, the default branch).
- **Minecraft: Java Edition** 1.14 or newer, launched at least once with any launcher (official, Prism, MultiMC
  or CurseForge), so its game files and sounds are on the PC. Melty checks you have it and tells the mod where it is.
  Otherwise the mod looks in the usual launcher folders, or you can set `minecraft_dir=` in `UserData/BrewingPoint.cfg`.

## Install and play (Windows, by hand, until it's on Melty)

1. Steam: Schedule I → Properties → Betas → **None** (the default, IL2CPP branch).
2. Install **MelonLoader 0.7.3**: run the MelonLoader installer and choose Schedule I and v0.7.3, or extract
   `MelonLoader.x64.zip` so that `version.dll` and `MelonLoader\` sit next to `Schedule I.exe`.
3. Launch Schedule I once (online). Wait for the MelonLoader console to finish and the main menu to appear, then quit.
4. Install **S1API 3.2.1**: from `S1API-Forked-3.2.1.zip`, copy **both** `Mods` and `Plugins` into the game folder.
5. Launch Minecraft: Java Edition (1.14 or newer) once, to the main menu.
6. Build and install Brewing Point (needs the .NET 10 SDK), with Schedule I closed:
   `dotnet build src\BrewingPoint -c Release -p:DeployToGame=true -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Schedule I"`
   (no backslash at the end of the path).
7. Optional: create an empty file `UserData\BrewingPoint.test` to be handed a stand, ingredients and cash on load.
   Delete it afterwards.
8. Launch from Steam, load a save, and check `UserData\BrewingPoint\checks.log`: every line should say `ok`.

## Playing together

Works in Schedule I co-op, up to 4 players. The host loads a save and invites friends through Steam, and friends
accept the invite. Everyone needs Brewing Point and their own Minecraft: Java Edition, and each player's stand is drawn from their own
Minecraft files. Brews sync through the game, since the stand is a real mixing station.

## How it's built

- `sheets/*.json`: the design, and the source of truth: ingredients, effects, brewing reactions, the station,
  shops, Minecraft models and sounds, the game hooks, and multiplayer. `python3 tools/preflight.py` checks every
  cell and cross-sheet reference. `python3 tools/codegen.py` turns each row into C# (`Sheets.g.cs`).
- `src/BrewingPoint.Core`: game-independent code. It finds Minecraft (official and Prism layouts), reads the
  jar (lang, textures, block models) and the hashed asset store (sounds), meshes Minecraft block models into
  Unity space, decodes Ogg with NVorbis, and holds the brewing rules. Run the tests with
  `dotnet test src/BrewingPoint.Core.Tests`.
- `src/BrewingPoint`: the MelonLoader IL2CPP mod, built on S1API: registration, the Brewing Stand visuals and
  sound, and the brewing-rule checks on the mixing screen. It compiles on a PC with the game:
  `dotnet build src/BrewingPoint -c Release -p:GameDir="<Schedule I folder>"`.
- `tools/typecheck`: compiles the mod here against real MelonLoader/HarmonyX/Il2CppInterop/S1API packages
  plus signature stubs, so code errors show up without the game.
- `build/package.py`: builds the release zip (MelonLoader + S1API + the mod + NVorbis + licences) on a PC with the game.

## Credits and licence

Made by payatongamer. Brewing Point's own code and data are licensed
[CC BY-NC-SA 4.0](LICENSE), and remixing is welcome. Bundled third-party software keeps its own licence;
see [CREDITS.md](CREDITS.md). Schedule I is by TVGS and Minecraft is by Mojang Studios. Neither game's content is
included: the mod reads your own copies.
