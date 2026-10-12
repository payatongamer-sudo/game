using System.Collections.Generic;
using BrewingPoint.Minecraft;
using UnityEngine;

namespace BrewingPoint
{
    /// <summary>Turns the Minecraft data read at start into Unity objects (in memory only, cached).</summary>
    internal static class UnityAssets
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, List<AudioClip>> Clips = new Dictionary<string, List<AudioClip>>();

        private static MinecraftContent Mc => BrewingPointMod.Minecraft!;

        /// <summary>A Minecraft texture with crisp pixels.</summary>
        public static Texture2D? Texture(string jarPath)
        {
            if (Textures.TryGetValue(jarPath, out var cached) && cached != null)
                return cached;
            if (!Mc.Textures.TryGetValue(jarPath, out var png))
                return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, png, false))
                return null;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "BrewingPoint " + jarPath;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Textures[jarPath] = tex;
            return tex;
        }

        /// <summary>An inventory icon from a Minecraft item texture (first frame of animated ones).</summary>
        public static Sprite? Icon(string jarPath)
        {
            if (Sprites.TryGetValue(jarPath, out var cached) && cached != null)
                return cached;
            var tex = Texture(jarPath);
            if (tex == null)
                return null;
            int size = Mathf.Min(tex.width, tex.height);
            var sprite = Sprite.Create(tex, new Rect(0, tex.height - size, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Sprites[jarPath] = sprite;
            return sprite;
        }

        /// <summary>A Unity mesh for one model id; <paramref name="textures"/> lists each submesh's texture.</summary>
        public static Mesh? Mesh(string modelId, out List<string> textures)
        {
            textures = new List<string>();
            if (!Mc.Models.TryGetValue(modelId, out var data))
                return null;
            textures.AddRange(data.Submeshes.Keys);
            if (Meshes.TryGetValue(modelId, out var cached) && cached != null)
                return cached;

            var vertices = new Vector3[data.Vertices.Count];
            var normals = new Vector3[data.Normals.Count];
            var uvs = new Vector2[data.Uvs.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3(data.Vertices[i].X, data.Vertices[i].Y, data.Vertices[i].Z);
                normals[i] = new Vector3(data.Normals[i].X, data.Normals[i].Y, data.Normals[i].Z);
                uvs[i] = new Vector2(data.Uvs[i].U, data.Uvs[i].V);
            }
            var mesh = new Mesh { name = "BrewingPoint " + modelId };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.subMeshCount = textures.Count;
            for (int s = 0; s < textures.Count; s++)
                mesh.SetTriangles(data.Submeshes[textures[s]].ToArray(), s);
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Meshes[modelId] = mesh;
            return mesh;
        }

        /// <summary>
        /// A material that uses the game's own lit shader (copied from <paramref name="template"/>) with a Minecraft
        /// texture, alpha-cutout and double-sided so the stand's thin parts and bottles draw like in Minecraft.
        /// </summary>
        public static Material Material(Material template, Texture2D texture)
        {
            var mat = new Material(template) { name = "BrewingPoint " + texture.name };
            mat.mainTexture = texture;
            foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                if (mat.HasProperty(prop))
                    mat.SetTexture(prop, texture);
            foreach (var prop in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap", "_ParallaxMap" })
                if (mat.HasProperty(prop))
                    mat.SetTexture(prop, null);
            foreach (var kw in new[] { "_NORMALMAP", "_EMISSION", "_METALLICGLOSSMAP", "_METALLICSPECGLOSSMAP", "_PARALLAXMAP", "_OCCLUSIONMAP", "_DETAIL_MULX2" })
                mat.DisableKeyword(kw);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", Color.white);
            foreach (var prop in new[] { "_Smoothness", "_Glossiness", "_Metallic" })
                if (mat.HasProperty(prop))
                    mat.SetFloat(prop, 0f);
            if (mat.HasProperty("_Cutoff"))
                mat.SetFloat("_Cutoff", 0.5f);
            if (mat.HasProperty("_AlphaClip"))
                mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Cull"))
                mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;
            mat.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return mat;
        }

        /// <summary>Audio clips for a sounds-sheet id, from the player's Minecraft .ogg files.</summary>
        public static List<AudioClip> Sound(string soundId)
        {
            if (Clips.TryGetValue(soundId, out var cached))
                return cached;
            var list = new List<AudioClip>();
            if (Mc.Sounds.TryGetValue(soundId, out var pcms))
            {
                int n = 0;
                foreach (var pcm in pcms)
                {
                    if (pcm.FramesPerChannel == 0)
                        continue;
                    var clip = AudioClip.Create($"BrewingPoint {soundId} {n++}", pcm.FramesPerChannel, pcm.Channels, pcm.SampleRate, false);
                    clip.SetData(pcm.Samples, 0);
                    clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    list.Add(clip);
                }
            }
            Clips[soundId] = list;
            return list;
        }
    }
}
