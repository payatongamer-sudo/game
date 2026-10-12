using System;
using System.Collections.Generic;
using BrewingPoint.Brewing;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppTMPro;
using S1API.Building;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrewingPoint
{
    /// <summary>
    /// Placed Brewing Stands. Each is a real (networked) Mixing Station whose own meshes are hidden and
    /// replaced by Minecraft's brewing stand model built from the player's Minecraft jar, with bottles that
    /// fill while a batch is in and Minecraft's brew sound when it finishes.
    /// </summary>
    internal static class BrewingStands
    {
        private sealed class Stand
        {
            public MixingStation Station = null!;
            public GameObject Model = null!;
            public GameObject?[] Full = Array.Empty<GameObject?>();
            public GameObject?[] Empty = Array.Empty<GameObject?>();
            public AudioSource? Audio;
            public bool WasDone;
            public int Shown = -1;
        }

        private static readonly List<Stand> Stands = new List<Stand>();
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static bool _reportedPlacement;

        public static void Reset() => Stands.Clear();

        public static bool IsBrewingStand(MixingStation? station)
        {
            if (station == null)
                return false;
            foreach (var s in Stands)
                if (s.Station != null && s.Station.Pointer == station.Pointer)
                    return true;
            try
            {
                return station.ItemInstance != null && station.ItemInstance.ID == Catalog.BrewingStand.Id;
            }
            catch (Exception)
            {
                return false; // ItemInstance not initialised yet
            }
        }

        /// <summary>S1API BuildEvents.OnBuildableItemInitialized: fires on every peer when a placed item is set up.</summary>
        public static void OnBuildableInitialized(BuildEventArgs args)
        {
            if (args.ItemId != Catalog.BrewingStand.Id || args.GameObject == null || BrewingPointMod.Minecraft == null)
                return;
            try
            {
                var station = args.GameObject.GetComponent<MixingStation>() ?? args.GameObject.GetComponentInChildren<MixingStation>(true);
                if (station == null)
                {
                    Checks.Fail("stand_visuals", "placed Brewing Stand has no MixingStation");
                    return;
                }
                foreach (var s in Stands)
                    if (s.Station != null && s.Station.Pointer == station.Pointer)
                        return; // host path and observer RPC can both initialise it
                ApplyLimits(station);
                var stand = Build(station);
                Stands.Add(stand);
                if (!_reportedPlacement)
                {
                    _reportedPlacement = true;
                    Checks.Ok("stand_visuals", $"Minecraft brewing stand model on a placed station ({stand.Full.Length} bottle slots, sound {(stand.Audio != null ? "ready" : "missing")})");
                }
            }
            catch (Exception ex)
            {
                Checks.Fail("stand_visuals", ex.Message);
                BrewingPointMod.Log.Error(ex.ToString());
            }
        }

        private static void ApplyLimits(MixingStation station)
        {
            var row = Catalog.BrewingStand;
            station.MaxMixQuantity = row.MaxBottles;
            station.MixTimePerItem = row.MinutesPerBottle;
            try
            {
                var threshold = station.stationConfiguration?.StartThrehold;
                if (threshold != null)
                {
                    threshold.Configure(1f, row.MaxBottles, true);
                    if (threshold.Value > row.MaxBottles)
                        threshold.SetValue(row.MaxBottles, true);
                }
            }
            catch (Exception ex)
            {
                BrewingPointMod.Log.Warning("Brewing Stand employee threshold not resized: " + ex.Message);
            }
        }

        private static Stand Build(MixingStation station)
        {
            var row = Catalog.BrewingStand;
            var keepUnder = station.Clock != null ? station.Clock.transform : null;
            var existingAudio = station.GetComponentInChildren<AudioSource>(true);

            // Measure, then hide, the mixing station's own meshes (its UI text and clock stay).
            Bounds? bounds = null;
            Material? template = null;
            foreach (var r in station.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || (r.TryCast<MeshRenderer>() == null && r.TryCast<SkinnedMeshRenderer>() == null))
                    continue;
                if (r.GetComponent<TMP_Text>() != null || (keepUnder != null && r.transform.IsChildOf(keepUnder)))
                    continue;
                if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    if (bounds == null)
                        bounds = r.bounds;
                    else
                    {
                        var b = bounds.Value;
                        b.Encapsulate(r.bounds);
                        bounds = b;
                    }
                    if (template == null && r.sharedMaterial != null)
                        template = r.sharedMaterial;
                }
                r.enabled = false;
                r.forceRenderingOff = true; // stays hidden if the game re-enables the renderer
            }
            template ??= FallbackMaterial();

            var root = new GameObject("BrewingPoint Minecraft Brewing Stand");
            root.transform.SetParent(station.transform, false);
            var centre = bounds?.center ?? station.transform.position;
            root.transform.position = new Vector3(centre.x, bounds?.min.y ?? station.transform.position.y, centre.z);
            root.transform.rotation = station.transform.rotation;
            var parentScale = station.transform.lossyScale;
            root.transform.localScale = new Vector3(1f / NonZero(parentScale.x), 1f / NonZero(parentScale.y), 1f / NonZero(parentScale.z));

            var stand = new Stand { Station = station, Model = root };
            Part(root, row.McModelBase, template);
            stand.Full = new GameObject?[row.MaxBottles];
            stand.Empty = new GameObject?[row.MaxBottles];
            for (int i = 0; i < row.MaxBottles; i++)
            {
                stand.Full[i] = Part(root, row.McModelBottleFull[i], template);
                stand.Empty[i] = Part(root, row.McModelBottleEmpty[i], template);
            }

            var sound = Catalog.SoundById[row.McSoundDone];
            if (UnityAssets.Sound(sound.Id).Count > 0)
            {
                var audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.minDistance = 1f;
                audio.maxDistance = sound.MaxDistance;
                audio.volume = sound.Volume;
                if (existingAudio != null)
                    audio.outputAudioMixerGroup = existingAudio.outputAudioMixerGroup;
                stand.Audio = audio;
            }
            Refresh(stand);
            return stand;
        }

        private static float NonZero(float v) => Mathf.Abs(v) < 1e-4f ? 1f : v;

        private static GameObject? Part(GameObject parent, string modelId, Material template)
        {
            var mesh = UnityAssets.Mesh(modelId, out var textures);
            if (mesh == null)
                return null;
            var go = new GameObject(modelId);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var mats = new Material[textures.Count];
            for (int i = 0; i < textures.Count; i++)
            {
                if (!Materials.TryGetValue(textures[i], out var mat) || mat == null)
                {
                    var tex = UnityAssets.Texture(textures[i]);
                    mat = tex != null ? UnityAssets.Material(template, tex) : template;
                    Materials[textures[i]] = mat;
                }
                mats[i] = mat;
            }
            renderer.sharedMaterials = mats;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        private static Material FallbackMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader);
        }

        /// <summary>Bottles to show: the batch being brewed, the finished output, or what's loaded.</summary>
        private static int BottlesIn(MixingStation station)
        {
            int max = Catalog.BrewingStand.MaxBottles;
            int n;
            if (station.CurrentMixOperation != null)
                n = station.CurrentMixOperation.Quantity;
            else if (station.OutputSlot != null && station.OutputSlot.Quantity > 0)
                n = station.OutputSlot.Quantity;
            else
                n = station.ProductSlot != null ? station.ProductSlot.Quantity : 0;
            return Mathf.Clamp(n, 0, max);
        }

        private static void Refresh(Stand s)
        {
            int filled = BottlesIn(s.Station);
            if (filled == s.Shown)
                return;
            for (int b = 0; b < s.Full.Length; b++)
            {
                s.Full[b]?.SetActive(b < filled);
                s.Empty[b]?.SetActive(b >= filled);
            }
            s.Shown = filled;
        }

        /// <summary>Called a few times a second: bottles follow the station, and a finished brew plays Minecraft's sound.</summary>
        public static void Poll()
        {
            for (int i = Stands.Count - 1; i >= 0; i--)
            {
                var s = Stands[i];
                if (s.Station == null || s.Model == null)
                {
                    Stands.RemoveAt(i);
                    continue;
                }
                try
                {
                    Refresh(s);
                    bool done = s.Station.CurrentMixOperation != null && s.Station.IsMixingDone;
                    if (done && !s.WasDone && s.Audio != null)
                    {
                        var clips = UnityAssets.Sound(Catalog.BrewingStand.McSoundDone);
                        if (clips.Count > 0)
                            s.Audio.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Count)]);
                    }
                    s.WasDone = done;
                }
                catch (Exception ex)
                {
                    BrewingPointMod.Log.Warning("Brewing Stand update failed: " + ex.Message);
                    Stands.RemoveAt(i);
                }
            }
        }
    }
}
