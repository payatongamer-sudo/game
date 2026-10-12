# Compile check without the game

`dotnet build tools/typecheck` compiles the real mod sources (`src/BrewingPoint` + `src/BrewingPoint.Core`)
against the real MelonLoader, HarmonyX, Il2CppInterop and S1API packages, and against **signature stubs** for the
Unity and Schedule I types the mod touches (`UnityStubs/`, `GameStubs/`). It catches mistakes in the mod's own
code on any machine. It does **not** prove the stubs match the game: the release build compiles against the
game's own generated assemblies (`MelonLoader/Il2CppAssemblies`) on a PC with Schedule I. Nothing here ships.
