#!/usr/bin/env python3
"""Build the Brewing Point release zip (installs into the Schedule I folder).

Run on a PC that has Schedule I with MelonLoader run once (its Il2CppAssemblies are the compile references):

    python build/package.py --game-dir "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Schedule I"

Steps: preflight the sheets, check generated code is current, run the core tests, build the mod, download the
pinned MelonLoader and S1API releases from their official GitHub releases (SHA-256 pinned in deps.lock.json on
first download), stage, zip to dist/BrewingPoint-<version>.zip, and check nothing from a game is inside.
"""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
BUILD = ROOT / "build"
DIST = ROOT / "dist"
DEPS = json.loads((BUILD / "deps.json").read_text(encoding="utf-8"))
LOCK = BUILD / "deps.lock.json"

# Anything with these extensions in the stage would mean a game's content leaked in.
GAME_CONTENT = {".assets", ".bundle", ".resS", ".resource", ".ogg", ".png", ".jar", ".mca", ".nbt"}
ALLOWED_TOP = {"version.dll", "MelonLoader", "Plugins", "Mods", "UserLibs", "UserData", "dobby.dll", "NOTICE.txt"}


def run(cmd, **kw):
    print("+", " ".join(str(c) for c in cmd))
    subprocess.run(cmd, check=True, **kw)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def download(name, url, cache):
    cache.mkdir(parents=True, exist_ok=True)
    target = cache / url.rsplit("/", 1)[-1]
    if not target.exists():
        print(f"downloading {name}: {url}")
        req = urllib.request.Request(url, headers={"User-Agent": "BrewingPoint-packager"})
        with urllib.request.urlopen(req) as r, open(target, "wb") as f:
            shutil.copyfileobj(r, f)
    lock = json.loads(LOCK.read_text()) if LOCK.exists() else {}
    digest = sha256(target)
    if url in lock and lock[url] != digest:
        sys.exit(f"{name}: SHA-256 {digest} does not match the pinned {lock[url]} - refusing to package")
    if url not in lock:
        lock[url] = digest
        LOCK.write_text(json.dumps(lock, indent=2) + "\n")
        print(f"pinned {name} sha256 {digest} in {LOCK.name}")
    return target


def s1api_asset_url(dep):
    """Finds the S1API release zip on the official GitHub release for the pinned version."""
    if dep.get("url"):
        return dep["url"]
    for tag in (dep["version"], "v" + dep["version"]):
        api = f"https://api.github.com/repos/{dep['repo']}/releases/tags/{tag}"
        try:
            req = urllib.request.Request(api, headers={"User-Agent": "BrewingPoint-packager", "Accept": "application/vnd.github+json"})
            with urllib.request.urlopen(req) as r:
                release = json.load(r)
        except Exception:
            continue
        for asset in release.get("assets", []):
            if asset["name"].lower().endswith(".zip") and "s1api" in asset["name"].lower():
                return asset["browser_download_url"]
    sys.exit(f"S1API {dep['version']}: no release zip found on github.com/{dep['repo']}; set deps.json s1api.url")


def extract(zip_path, dest, strip_root_dirs=()):
    with zipfile.ZipFile(zip_path) as z:
        for info in z.infolist():
            name = info.filename
            for prefix in strip_root_dirs:
                if name.startswith(prefix):
                    name = name[len(prefix):]
            if not name or name.endswith("/"):
                continue
            out = dest / name
            out.parent.mkdir(parents=True, exist_ok=True)
            with z.open(info) as src, open(out, "wb") as dst:
                shutil.copyfileobj(src, dst)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--game-dir", required=True, help="Schedule I folder with MelonLoader run once")
    ap.add_argument("--skip-tests", action="store_true")
    ap.add_argument("--release", action="store_true", help="also require every sheet cell verified in game")
    args = ap.parse_args()

    run([sys.executable, str(ROOT / "tools" / "preflight.py")] + (["--strict"] if args.release else []))
    generated = ROOT / "src" / "BrewingPoint.Core" / "Generated" / "Sheets.g.cs"
    before = generated.read_text(encoding="utf-8")
    run([sys.executable, str(ROOT / "tools" / "codegen.py")])
    if generated.read_text(encoding="utf-8") != before:
        sys.exit("Sheets.g.cs was out of date with the sheets; regenerated - review and commit it, then package again")
    if not args.skip_tests:
        run(["dotnet", "test", str(ROOT / "src" / "BrewingPoint.Core.Tests"), "-c", "Release"])
    run(["dotnet", "build", str(ROOT / "src" / "BrewingPoint"), "-c", "Release", f"-p:GameDir={args.game_dir}"])
    out = ROOT / "src" / "BrewingPoint" / "bin" / "Release"
    version = DEPS["mod"]["version"]

    cache = BUILD / ".cache"
    ml = download("MelonLoader", DEPS["melonloader"]["url"], cache)
    s1 = download("S1API", s1api_asset_url(DEPS["s1api"]), cache)

    stage = DIST / "stage"
    if stage.exists():
        shutil.rmtree(stage)
    stage.mkdir(parents=True)
    extract(ml, stage)
    s1_tmp = DIST / "s1api"
    if s1_tmp.exists():
        shutil.rmtree(s1_tmp)
    extract(s1, s1_tmp)
    plugins = next((p for p in s1_tmp.rglob("Plugins") if p.is_dir()), None)
    if plugins is None:
        sys.exit("S1API zip has no Plugins folder; check its layout")
    shutil.copytree(plugins, stage / "Plugins", dirs_exist_ok=True)
    (stage / "Mods").mkdir(exist_ok=True)
    shutil.copy2(out / "BrewingPoint.dll", stage / "Mods" / "BrewingPoint.dll")
    (stage / "UserLibs").mkdir(exist_ok=True)
    shutil.copy2(out / "NVorbis.dll", stage / "UserLibs" / "NVorbis.dll")
    notes = stage / "UserData" / "BrewingPoint" / "licenses"
    notes.mkdir(parents=True)
    for f in (BUILD / "licenses").glob("*.txt"):
        shutil.copy2(f, notes / f.name)
    shutil.copy2(ROOT / "LICENSE", notes / "BrewingPoint-CC-BY-NC-SA-4.0.txt")
    shutil.copy2(ROOT / "CREDITS.md", stage / "UserData" / "BrewingPoint" / "CREDITS.md")

    problems = []
    for p in stage.rglob("*"):
        rel = p.relative_to(stage)
        if rel.parts[0] not in ALLOWED_TOP:
            problems.append(f"unexpected top-level entry {rel}")
        if p.is_file() and p.suffix in GAME_CONTENT:
            problems.append(f"game-content-like file {rel}")
    if problems:
        sys.exit("refusing to package:\n  " + "\n  ".join(problems))

    DIST.mkdir(exist_ok=True)
    zip_path = DIST / f"BrewingPoint-{version}.zip"
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for p in sorted(stage.rglob("*")):
            if p.is_file():
                z.write(p, p.relative_to(stage).as_posix())
    print(f"\n{zip_path.relative_to(ROOT)}  {zip_path.stat().st_size} bytes  sha256 {sha256(zip_path)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
