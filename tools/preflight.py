#!/usr/bin/env python3
"""Preflight: lay every sheet's rows and columns over each other before a build.

Lists every unfilled cell, every reference between sheets that does not resolve,
and every cell still waiting on in-game verification. Exit code 1 on any error
(unfilled or unresolved); unverified cells are reported but don't block a build.

    python3 tools/preflight.py            # human report
    python3 tools/preflight.py --strict   # also fail on unverified cells (release gate)
"""
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SHEETS = ROOT / "sheets"

# S1API 3.2.1 S1API/Properties/Property.cs public tokens (vanilla Schedule I effects).
S1API_PROPERTY_TOKENS = {
    "Munchies", "AntiGravity", "Energizing", "Focused", "Smelly", "Euphoric", "Cyclopean",
    "Slippery", "Shrinking", "Seizure", "Electrifying", "Zombifying", "Disorienting", "Sedating",
    "CalorieDense", "TropicThunder", "Toxic", "ThoughtProvoking", "Lethal", "Calming",
    "Schizophrenic", "Spicy", "Laxative", "BrightEyed", "Sneaky", "Jennerising", "Balding",
    "Glowie", "Refreshing", "Athletic", "LongFaced", "Paranoia", "Gingeritis", "Foggy", "Explosive",
}
ROLES = {"base", "effect", "corrupt"}
REACTION_RULES = {"awkward_base", "corruption"}
HOOK_KINDS = {"s1api_event", "harmony_prefix", "harmony_postfix", "melon_update", "field_write", "log_write"}
RUNS_ON = {"every_peer", "local_player"}


def load():
    sheets = {}
    for path in sorted(SHEETS.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        sheets[data["sheet"]] = data
    return sheets


def filled(value):
    if value is None:
        return False
    if isinstance(value, str) and value.strip() == "":
        return False
    if isinstance(value, list) and len(value) == 0:
        return False
    return True


def main():
    strict = "--strict" in sys.argv
    sheets = load()
    errors, unverified, cells = [], [], 0

    def ids(name):
        return {row["id"] for row in sheets.get(name, {}).get("rows", [])}

    def ref(sheet, row, col, target_sheet, value):
        if value not in ids(target_sheet):
            errors.append(f"{sheet}:{row['id']}.{col} -> {target_sheet}:{value} does not resolve")

    # 1. every row x column crossing is filled, no stray columns, unique ids
    for name, data in sheets.items():
        columns = data.get("columns", {})
        seen = set()
        for row in data.get("rows", []):
            rid = row.get("id", "<no id>")
            if rid in seen:
                errors.append(f"{name}:{rid} duplicate id")
            seen.add(rid)
            for col in columns:
                cells += 1
                if col not in row or not filled(row[col]):
                    errors.append(f"{name}:{rid}.{col} unfilled")
            for col in row:
                if col not in columns:
                    errors.append(f"{name}:{rid}.{col} is not a declared column")

    # 2. references and enumerations
    for row in sheets["ingredients"]["rows"]:
        ref("ingredients", row, "effect", "effects", row["effect"])
        ref("ingredients", row, "shop", "shops", row["shop"])
        if row["role"] not in ROLES:
            errors.append(f"ingredients:{row['id']}.role '{row['role']}' not in {sorted(ROLES)}")
        if not row["mc_texture"].startswith("assets/minecraft/textures/"):
            errors.append(f"ingredients:{row['id']}.mc_texture is not a jar texture path")

    for row in sheets["effects"]["rows"]:
        if row["kind"] == "vanilla":
            if row["s1api_token"] not in S1API_PROPERTY_TOKENS:
                errors.append(f"effects:{row['id']}.s1api_token '{row['s1api_token']}' is not an S1API Property token")
            for col in ("tier", "value_change", "value_multiplier", "addictiveness", "product_color", "label_color", "name", "mix_push"):
                if row[col] != "game":
                    errors.append(f"effects:{row['id']}.{col} must be 'game' for a vanilla effect")
        elif row["kind"] == "custom":
            if row["custom_id"] == "none" or row["s1api_token"] != "none":
                errors.append(f"effects:{row['id']} custom effect needs custom_id and s1api_token 'none'")
            if row["mix_push"] != "zero":
                errors.append(f"effects:{row['id']}.mix_push must be 'zero' for a custom effect")
            if not (isinstance(row["tier"], int) and 1 <= row["tier"] <= 5):
                errors.append(f"effects:{row['id']}.tier must be 1-5")
            for col in ("value_change", "value_multiplier", "addictiveness"):
                if not isinstance(row[col], (int, float)):
                    errors.append(f"effects:{row['id']}.{col} must be a number")
            for col in ("product_color", "label_color"):
                if not (isinstance(row[col], str) and row[col].startswith("#") and len(row[col]) == 7):
                    errors.append(f"effects:{row['id']}.{col} must be #RRGGBB")
        else:
            errors.append(f"effects:{row['id']}.kind '{row['kind']}' not vanilla|custom")

    ingredient_effects = {r["effect"]: r for r in sheets["ingredients"]["rows"]}
    for row in sheets["reactions"]["rows"]:
        for col in ("mixer_effect", "when_contains", "result"):
            ref("reactions", row, col, "effects", row[col])
        if row["rule"] not in REACTION_RULES:
            errors.append(f"reactions:{row['id']}.rule '{row['rule']}' unknown")
        if row["mixer_effect"] not in ingredient_effects:
            errors.append(f"reactions:{row['id']}.mixer_effect '{row['mixer_effect']}' is imprinted by no ingredient")
        if not isinstance(row["replace"], bool):
            errors.append(f"reactions:{row['id']}.replace must be true/false")
    # every effect ingredient's effect must use up the Awkward base
    awkward_mixers = {r["mixer_effect"] for r in sheets["reactions"]["rows"] if r["rule"] == "awkward_base"}
    for row in sheets["ingredients"]["rows"]:
        if row["role"] in ("effect", "corrupt") and row["effect"] not in awkward_mixers:
            errors.append(f"ingredients:{row['id']} has no awkward_base reaction for '{row['effect']}'")

    for row in sheets["stations"]["rows"]:
        ref("stations", row, "mc_model_base", "models", row["mc_model_base"])
        for col in ("mc_model_bottle_full", "mc_model_bottle_empty"):
            if len(row[col]) != row["max_bottles"]:
                errors.append(f"stations:{row['id']}.{col} needs one model per bottle ({row['max_bottles']})")
            for value in row[col]:
                ref("stations", row, col, "models", value)
        ref("stations", row, "mc_sound_done", "sounds", row["mc_sound_done"])
        ref("stations", row, "shop", "shops", row["shop"])
        for col in ("accepts_roles", "needs_awkward_roles", "vanilla_stations_reject_roles"):
            for value in row[col]:
                if value not in ROLES:
                    errors.append(f"stations:{row['id']}.{col} role '{value}' unknown")
        if not row["mc_icon"].startswith("assets/minecraft/textures/"):
            errors.append(f"stations:{row['id']}.mc_icon is not a jar texture path")

    for row in sheets["models"]["rows"]:
        if not row["jar_path"].startswith("assets/minecraft/models/"):
            errors.append(f"models:{row['id']}.jar_path is not a jar model path")

    for row in sheets["hooks"]["rows"]:
        if row["kind"] not in HOOK_KINDS:
            errors.append(f"hooks:{row['id']}.kind '{row['kind']}' unknown")
        if row["runs_on"] not in RUNS_ON:
            errors.append(f"hooks:{row['id']}.runs_on '{row['runs_on']}' unknown")
        for use in row["uses"]:
            sheet, _, rid = use.partition(":")
            if sheet not in sheets:
                errors.append(f"hooks:{row['id']}.uses sheet '{sheet}' does not exist")
            elif rid != "*" and rid not in ids(sheet):
                errors.append(f"hooks:{row['id']}.uses {use} does not resolve")
        if row["verified_in_game"] is not True:
            unverified.append(f"hooks:{row['id']} ({row['target']}) not yet seen working in the running game")

    print(f"preflight: {len(sheets)} sheets, {cells} cells checked")
    for e in errors:
        print("  ERROR      " + e)
    for u in unverified:
        print("  UNVERIFIED " + u)
    if errors:
        print(f"preflight: {len(errors)} error(s) - fix the sheets before building")
        return 1
    if strict and unverified:
        print(f"preflight: clean, but {len(unverified)} cell(s) unverified - not releasable yet")
        return 1
    print("preflight: clean" + (f" ({len(unverified)} unverified, need a run in the game)" if unverified else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
