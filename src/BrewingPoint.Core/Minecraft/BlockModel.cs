using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace BrewingPoint.Minecraft
{
    public readonly struct Vec3
    {
        public readonly float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }

    public readonly struct Vec2
    {
        public readonly float U, V;
        public Vec2(float u, float v) { U = u; V = v; }
    }

    public sealed class ModelFace
    {
        public string Direction = "";
        public float[]? Uv;          // u1 v1 u2 v2 in 0..16, or null for the default
        public string Texture = "";  // "#var" or a texture id
        public int Rotation;         // 0, 90, 180, 270
    }

    public sealed class ModelElement
    {
        public Vec3 From, To;
        public string? RotationAxis;
        public float RotationAngle;
        public Vec3 RotationOrigin;
        public bool Rescale;
        public List<ModelFace> Faces = new List<ModelFace>();
    }

    /// <summary>A Minecraft block model with its parent chain merged and texture variables resolved.</summary>
    public sealed class ResolvedModel
    {
        public string Id = "";
        public List<ModelElement> Elements = new List<ModelElement>();
        public Dictionary<string, string> Textures = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Jar path of the PNG a face texture ("#stand") ends at, or null if it doesn't resolve.</summary>
        public string? TexturePath(string reference)
        {
            var current = reference;
            for (int i = 0; i < 16 && current.StartsWith("#", StringComparison.Ordinal); i++)
            {
                if (!Textures.TryGetValue(current.Substring(1), out var next))
                    return null;
                current = next;
            }
            if (current.StartsWith("#", StringComparison.Ordinal))
                return null;
            return BlockModelLoader.TextureJarPath(current);
        }
    }

    public static class BlockModelLoader
    {
        public static string ModelJarPath(string id) => "assets/" + Namespace(id) + "/models/" + Path(id) + ".json";
        public static string TextureJarPath(string id) => "assets/" + Namespace(id) + "/textures/" + Path(id) + ".png";

        private static string Namespace(string id) { int c = id.IndexOf(':'); return c >= 0 ? id.Substring(0, c) : "minecraft"; }
        private static string Path(string id) { int c = id.IndexOf(':'); return c >= 0 ? id.Substring(c + 1) : id; }

        /// <summary>Loads a model by id ("block/brewing_stand") following its parents through <paramref name="readJarText"/>.</summary>
        public static ResolvedModel Load(string id, Func<string, string?> readJarText)
        {
            var chain = new List<JsonElement>();
            var docs = new List<JsonDocument>();
            try
            {
                string? current = id;
                for (int depth = 0; current != null && depth < 16; depth++)
                {
                    if (current.StartsWith("builtin/", StringComparison.Ordinal) || current.StartsWith("minecraft:builtin/", StringComparison.Ordinal))
                        break;
                    var text = readJarText(ModelJarPath(current));
                    if (text == null)
                    {
                        if (depth == 0)
                            throw new InvalidOperationException("model not found in the Minecraft jar: " + ModelJarPath(current));
                        break;
                    }
                    var doc = JsonDocument.Parse(text);
                    docs.Add(doc);
                    chain.Add(doc.RootElement);
                    current = doc.RootElement.TryGetProperty("parent", out var parent) ? parent.GetString() : null;
                }

                var model = new ResolvedModel { Id = id };
                // textures: parents first, children override
                for (int i = chain.Count - 1; i >= 0; i--)
                    if (chain[i].TryGetProperty("textures", out var textures))
                        foreach (var p in textures.EnumerateObject())
                            model.Textures[p.Name] = p.Value.GetString() ?? "";
                // elements: the nearest model that defines any
                foreach (var m in chain)
                {
                    if (!m.TryGetProperty("elements", out var elements))
                        continue;
                    foreach (var e in elements.EnumerateArray())
                        model.Elements.Add(ParseElement(e));
                    break;
                }
                return model;
            }
            finally
            {
                foreach (var d in docs)
                    d.Dispose();
            }
        }

        private static Vec3 ReadVec3(JsonElement a) => new Vec3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());

        private static ModelElement ParseElement(JsonElement e)
        {
            var el = new ModelElement { From = ReadVec3(e.GetProperty("from")), To = ReadVec3(e.GetProperty("to")) };
            if (e.TryGetProperty("rotation", out var r))
            {
                el.RotationAxis = r.GetProperty("axis").GetString();
                el.RotationAngle = r.GetProperty("angle").GetSingle();
                el.RotationOrigin = r.TryGetProperty("origin", out var o) ? ReadVec3(o) : new Vec3(8, 8, 8);
                el.Rescale = r.TryGetProperty("rescale", out var rs) && rs.GetBoolean();
            }
            if (e.TryGetProperty("faces", out var faces))
            {
                foreach (var f in faces.EnumerateObject())
                {
                    var face = new ModelFace { Direction = f.Name, Texture = f.Value.GetProperty("texture").GetString() ?? "" };
                    if (f.Value.TryGetProperty("uv", out var uv))
                        face.Uv = uv.EnumerateArray().Select(x => x.GetSingle()).ToArray();
                    if (f.Value.TryGetProperty("rotation", out var fr))
                        face.Rotation = fr.GetInt32();
                    el.Faces.Add(face);
                }
            }
            return el;
        }
    }

    /// <summary>Mesh data ready for UnityEngine.Mesh: one shared vertex list, one triangle list per texture.</summary>
    public sealed class MeshData
    {
        public readonly List<Vec3> Vertices = new List<Vec3>();
        public readonly List<Vec3> Normals = new List<Vec3>();
        public readonly List<Vec2> Uvs = new List<Vec2>();
        /// <summary>Texture jar path -> triangle indices into Vertices.</summary>
        public readonly Dictionary<string, List<int>> Submeshes = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        public readonly List<string> Problems = new List<string>();
    }

    /// <summary>
    /// Builds Unity-space geometry from a Minecraft block model. Minecraft's model space is right-handed
    /// (x east, y up, z south, 0..16 per block); Unity is left-handed, so z is mirrored and triangles are
    /// wound clockwise as seen from outside (Unity's front face). The block's bottom centre sits at the origin.
    /// </summary>
    public static class BlockMesher
    {
        public static MeshData Build(ResolvedModel model, float metresPerBlock, Func<string, int>? framesOf = null, MeshData? into = null)
        {
            var mesh = into ?? new MeshData();
            foreach (var el in model.Elements)
            {
                foreach (var face in el.Faces)
                {
                    var texture = model.TexturePath(face.Texture);
                    if (texture == null)
                    {
                        mesh.Problems.Add($"{model.Id}: texture {face.Texture} does not resolve");
                        continue;
                    }
                    if (!FaceCorners(el, face.Direction, out var corners, out var normal))
                    {
                        mesh.Problems.Add($"{model.Id}: unknown face direction {face.Direction}");
                        continue;
                    }
                    var uv = face.Uv ?? DefaultUv(el, face.Direction);
                    var uvCorners = new[]
                    {
                        new Vec2(uv[0], uv[1]), new Vec2(uv[2], uv[1]), new Vec2(uv[2], uv[3]), new Vec2(uv[0], uv[3]),
                    };
                    int k = ((face.Rotation / 90) % 4 + 4) % 4;
                    int frames = Math.Max(1, framesOf?.Invoke(texture) ?? 1);

                    int start = mesh.Vertices.Count;
                    var unityNormal = ToUnityDirection(Rotate(el, normal, true));
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Rotate(el, corners[i], false);
                        mesh.Vertices.Add(ToUnity(p, metresPerBlock));
                        mesh.Normals.Add(unityNormal);
                        var t = uvCorners[(i - k + 4) % 4];
                        mesh.Uvs.Add(new Vec2(t.U / 16f, 1f - (t.V / 16f) / frames));
                    }
                    if (!mesh.Submeshes.TryGetValue(texture, out var tris))
                        mesh.Submeshes[texture] = tris = new List<int>();
                    AddTriangle(mesh, tris, start, start + 1, start + 2, unityNormal);
                    AddTriangle(mesh, tris, start, start + 2, start + 3, unityNormal);
                }
            }
            return mesh;
        }

        /// <summary>Adds a triangle wound so Unity sees its front from the side the face points to.</summary>
        private static void AddTriangle(MeshData mesh, List<int> tris, int a, int b, int c, Vec3 outward)
        {
            var n = Vec3.Cross(mesh.Vertices[b] - mesh.Vertices[a], mesh.Vertices[c] - mesh.Vertices[a]);
            if (Vec3.Dot(n, outward) >= 0)
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            else
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
        }

        public static Vec3 ToUnity(Vec3 mc, float metresPerBlock) =>
            new Vec3((mc.X / 16f - 0.5f) * metresPerBlock, mc.Y / 16f * metresPerBlock, -(mc.Z / 16f - 0.5f) * metresPerBlock);

        private static Vec3 ToUnityDirection(Vec3 d) => new Vec3(d.X, d.Y, -d.Z);

        /// <summary>Corners top-left, top-right, bottom-right, bottom-left as seen from outside, plus the outward normal.</summary>
        public static bool FaceCorners(ModelElement e, string direction, out Vec3[] corners, out Vec3 normal)
        {
            var f = e.From;
            var t = e.To;
            switch (direction)
            {
                case "north":
                    corners = new[] { new Vec3(t.X, t.Y, f.Z), new Vec3(f.X, t.Y, f.Z), new Vec3(f.X, f.Y, f.Z), new Vec3(t.X, f.Y, f.Z) };
                    normal = new Vec3(0, 0, -1); return true;
                case "south":
                    corners = new[] { new Vec3(f.X, t.Y, t.Z), new Vec3(t.X, t.Y, t.Z), new Vec3(t.X, f.Y, t.Z), new Vec3(f.X, f.Y, t.Z) };
                    normal = new Vec3(0, 0, 1); return true;
                case "west":
                    corners = new[] { new Vec3(f.X, t.Y, f.Z), new Vec3(f.X, t.Y, t.Z), new Vec3(f.X, f.Y, t.Z), new Vec3(f.X, f.Y, f.Z) };
                    normal = new Vec3(-1, 0, 0); return true;
                case "east":
                    corners = new[] { new Vec3(t.X, t.Y, t.Z), new Vec3(t.X, t.Y, f.Z), new Vec3(t.X, f.Y, f.Z), new Vec3(t.X, f.Y, t.Z) };
                    normal = new Vec3(1, 0, 0); return true;
                case "up":
                    corners = new[] { new Vec3(f.X, t.Y, f.Z), new Vec3(t.X, t.Y, f.Z), new Vec3(t.X, t.Y, t.Z), new Vec3(f.X, t.Y, t.Z) };
                    normal = new Vec3(0, 1, 0); return true;
                case "down":
                    corners = new[] { new Vec3(f.X, f.Y, t.Z), new Vec3(t.X, f.Y, t.Z), new Vec3(t.X, f.Y, f.Z), new Vec3(f.X, f.Y, f.Z) };
                    normal = new Vec3(0, -1, 0); return true;
                default:
                    corners = Array.Empty<Vec3>();
                    normal = default;
                    return false;
            }
        }

        /// <summary>Minecraft's default face UVs (BlockElement), in 0..16 with v down from the top.</summary>
        public static float[] DefaultUv(ModelElement e, string direction)
        {
            var f = e.From;
            var t = e.To;
            switch (direction)
            {
                case "down": return new[] { f.X, 16 - t.Z, t.X, 16 - f.Z };
                case "up": return new[] { f.X, f.Z, t.X, t.Z };
                case "north": return new[] { 16 - t.X, 16 - t.Y, 16 - f.X, 16 - f.Y };
                case "south": return new[] { f.X, 16 - t.Y, t.X, 16 - f.Y };
                case "west": return new[] { f.Z, 16 - t.Y, t.Z, 16 - f.Y };
                case "east": return new[] { 16 - t.Z, 16 - t.Y, 16 - f.Z, 16 - f.Y };
                default: return new float[] { 0, 0, 16, 16 };
            }
        }

        /// <summary>Applies the element's rotation (right-handed, about its origin, with Minecraft's rescale).</summary>
        public static Vec3 Rotate(ModelElement e, Vec3 p, bool isDirection)
        {
            if (e.RotationAxis == null || e.RotationAngle == 0)
                return p;
            var o = isDirection ? new Vec3(0, 0, 0) : e.RotationOrigin;
            var v = p - o;
            double a = e.RotationAngle * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            Vec3 r;
            switch (e.RotationAxis)
            {
                case "x": r = new Vec3(v.X, v.Y * c - v.Z * s, v.Y * s + v.Z * c); break;
                case "y": r = new Vec3(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c); break;
                default: r = new Vec3(v.X * c - v.Y * s, v.X * s + v.Y * c, v.Z); break;
            }
            if (e.Rescale && !isDirection)
            {
                float k = 1f / (float)Math.Cos(Math.Abs(a));
                switch (e.RotationAxis)
                {
                    case "x": r = new Vec3(r.X, r.Y * k, r.Z * k); break;
                    case "y": r = new Vec3(r.X * k, r.Y, r.Z * k); break;
                    default: r = new Vec3(r.X * k, r.Y * k, r.Z); break;
                }
            }
            return r + o;
        }
    }
}
