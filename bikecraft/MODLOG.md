# Bikecraft MODLOG (MX Bikes x Minecraft)

Design (agreed): host = Minecraft: Java Edition (Fabric). Companion = MX Bikes ({game:mx-bikes}).
Ride your own MX Bikes bikes in Minecraft; H toggles MX Bikes handling <-> Minecraft handling.
Multiplayer: host + up to 8 friends via Melty join link. License MIT, remix allowed.
Design change agreed (option A): bike body drawn Minecraft-style; .edf models undocumented,
stock .pkz/.wav encrypted (never decrypted). Community bikes give real specs + engine samples.

## Done
- core/: pure-Java MX Bikes reader (folders + plain-zip .pkz in place, locked .pkz by name only),
  MX Bikes handling, Minecraft handling, rpm-layered engine sample mix. 20/20 synthetic tests pass
  (`javac -d core/out $(find core/src -name '*.java') && java -cp core/out bikecraft.core.CoreTests`).

## Unverified (needs a PC with MX Bikes)
- Real cfg/engn key names (parser accepts several spellings), eng.scl layout, SoundMods path base.

## Blocked
- melty.gg, maven.fabricmc.net, meta.fabricmc.net, Mojang hosts denied by this cloud environment's network policy.
- No MX Bikes / Windows / Melty app in this container: in-game test, screenshot, test_release need the player's PC.
