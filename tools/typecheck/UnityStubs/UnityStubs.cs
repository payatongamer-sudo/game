// Signature stubs for the Unity API surface Brewing Point uses, shaped like Il2CppInterop's generated
// UnityEngine assemblies (objects derive from Il2CppObjectBase, arrays are Il2CppStructArray/ReferenceArray).
// Compile check only; never shipped or run.
#pragma warning disable CS1591, CS0067
using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    public enum HideFlags { None = 0, DontUnloadUnusedAsset = 32 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum TextureFormat { RGBA32 = 4 }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }

    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => default; }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 one => new Vector3(1, 1, 1); }
    public struct Quaternion { public float x, y, z, w; }
    public struct Rect { public Rect(float x, float y, float w, float h) { } }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; } public static Color white => new Color(1, 1, 1); }
    public struct Bounds { public Vector3 center => default; public Vector3 min => default; public void Encapsulate(Bounds b) { } }

    public static class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Abs(float v) => Math.Abs(v);
        public static int Clamp(int v, int min, int max) => Math.Clamp(v, min, max);
    }
    public static class Time { public static float unscaledTime => 0f; }
    public static class Random { public static int Range(int min, int max) => min; }

    public class Object : Il2CppObjectBase
    {
        public Object(IntPtr p) : base(p) { }
        public Object() : base(IntPtr.Zero) { }
        public string name { get; set; } = "";
        public HideFlags hideFlags { get; set; }
    }
    public class Component : Object
    {
        public Transform transform => null!;
        public GameObject gameObject => null!;
        public T GetComponent<T>() => default!;
        public T GetComponentInChildren<T>(bool includeInactive) => default!;
        public Il2CppArrayBase<T> GetComponentsInChildren<T>(bool includeInactive) => null!;
    }
    public class Behaviour : Component { public bool enabled { get; set; } }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 lossyScale => default;
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public bool IsChildOf(Transform parent) => false;
    }
    public sealed class GameObject : Object
    {
        public GameObject(string name) { }
        public Transform transform => null!;
        public bool activeInHierarchy => true;
        public void SetActive(bool value) { }
        public T AddComponent<T>() => default!;
        public T GetComponent<T>() => default!;
        public T GetComponentInChildren<T>(bool includeInactive) => default!;
    }
    public class Shader : Object { public static Shader Find(string name) => null!; }
    public class Texture : Object
    {
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public int width => 16;
        public int height => 16;
    }
    public sealed class Texture2D : Texture { public Texture2D(int w, int h, TextureFormat f, bool mip) { } }
    public sealed class Sprite : Object { public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu) => null!; }
    public static class ImageConversion { public static bool LoadImage(Texture2D tex, Il2CppStructArray<byte> data, bool markNonReadable) => true; }
    public class Material : Object
    {
        public Material(Material source) { }
        public Material(Shader shader) { }
        public Texture mainTexture { get; set; } = null!;
        public int renderQueue { get; set; }
        public bool HasProperty(string name) => false;
        public void SetTexture(string name, Texture? value) { }
        public void SetColor(string name, Color value) { }
        public void SetFloat(string name, float value) { }
        public void EnableKeyword(string kw) { }
        public void DisableKeyword(string kw) { }
    }
    public sealed class Mesh : Object
    {
        public Il2CppStructArray<Vector3> vertices { get; set; } = null!;
        public Il2CppStructArray<Vector3> normals { get; set; } = null!;
        public Il2CppStructArray<Vector2> uv { get; set; } = null!;
        public int subMeshCount { get; set; }
        public void SetTriangles(Il2CppStructArray<int> triangles, int submesh) { }
        public void RecalculateBounds() { }
    }
    public sealed class MeshFilter : Component { public Mesh sharedMesh { get; set; } = null!; }
    public class Renderer : Component
    {
        public bool enabled { get; set; }
        public Bounds bounds => default;
        public Material sharedMaterial { get; set; } = null!;
        public Il2CppReferenceArray<Material> sharedMaterials { get; set; } = null!;
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
    }
    public sealed class MeshRenderer : Renderer { }
    public sealed class SkinnedMeshRenderer : Renderer { }
    public sealed class AudioClip : Object
    {
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => null!;
        public bool SetData(Il2CppStructArray<float> data, int offsetSamples) => true;
    }
    public sealed class AudioSource : Behaviour
    {
        public bool playOnAwake { get; set; }
        public float spatialBlend { get; set; }
        public AudioRolloffMode rolloffMode { get; set; }
        public float minDistance { get; set; }
        public float maxDistance { get; set; }
        public float volume { get; set; }
        public Audio.AudioMixerGroup outputAudioMixerGroup { get; set; } = null!;
        public void PlayOneShot(AudioClip clip) { }
    }
}

namespace UnityEngine.Audio { public class AudioMixerGroup : UnityEngine.Object { } }
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off, On } }
namespace UnityEngine.UI { public class Button : UnityEngine.Behaviour { public bool interactable { get; set; } } }
namespace Il2CppTMPro
{
    public class TMP_Text : UnityEngine.Behaviour { public string text { get; set; } = ""; }
    public class TextMeshProUGUI : TMP_Text { }
}
