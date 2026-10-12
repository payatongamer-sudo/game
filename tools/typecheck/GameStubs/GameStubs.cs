// Signature stubs for the Schedule I (0.4.7 IL2CPP, Il2CppInterop names) types Brewing Point uses.
// Shapes come from S1API 3.2.1 and PocketPlug sources that compile against the real game. Compile check only.
#pragma warning disable CS1591
using System;
using UnityEngine;

namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public List() : base(IntPtr.Zero) { }
        public int Count => 0;
        public T this[int index] { get => default!; set { } }
        public void Add(T item) { }
    }
}

namespace Il2CppScheduleOne.ItemFramework
{
    public class ItemDefinition : UnityEngine.Object { public string ID => ""; public string Name => ""; }
    public class ItemInstance : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public ItemInstance() : base(IntPtr.Zero) { }
        public string ID => "";
        public ItemDefinition Definition => null!;
    }
    public class ItemSlot : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public ItemSlot() : base(IntPtr.Zero) { }
        public ItemInstance ItemInstance => null!;
        public int Quantity => 0;
    }
}

namespace Il2CppScheduleOne.Effects
{
    public class Effect : UnityEngine.Object { public string ID => ""; }
    public static class EffectMixCalculator
    {
        public static Il2CppSystem.Collections.Generic.List<Effect> MixProperties(
            Il2CppSystem.Collections.Generic.List<Effect> existingProperties, Effect newProperty, Product.EDrugType drugType) => null!;
    }
}

namespace Il2CppScheduleOne.Product
{
    public enum EDrugType { Marijuana, Methamphetamine, Cocaine, Shrooms }
    public class PropertyItemDefinition : ItemFramework.ItemDefinition { public Il2CppSystem.Collections.Generic.List<Effects.Effect> Properties => null!; }
    public class ProductDefinition : PropertyItemDefinition { public EDrugType DrugType => default; }
}

namespace Il2CppScheduleOne
{
    public static class Registry { public static ItemFramework.ItemDefinition GetItem(string id) => null!; }
}

namespace Il2CppScheduleOne.Management
{
    public class NumberField : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public NumberField() : base(IntPtr.Zero) { }
        public float Value => 0;
        public void Configure(float min, float max, bool wholeNumbers) { }
        public void SetValue(float value, bool network) { }
    }
    public class MixingStationConfiguration : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public MixingStationConfiguration() : base(IntPtr.Zero) { }
        public NumberField StartThrehold => null!;
    }
}

namespace Il2CppScheduleOne.ObjectScripts
{
    public class MixOperation : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public MixOperation() : base(IntPtr.Zero) { }
        public int Quantity => 0;
    }
    public class MixingStation : MonoBehaviour
    {
        public int MaxMixQuantity { get; set; }
        public int MixTimePerItem { get; set; }
        public Management.MixingStationConfiguration stationConfiguration => null!;
        public MonoBehaviour Clock => null!;
        public ItemFramework.ItemInstance ItemInstance => null!;
        public MixOperation CurrentMixOperation => null!;
        public bool IsMixingDone => false;
        public ItemFramework.ItemSlot ProductSlot => null!;
        public ItemFramework.ItemSlot MixerSlot => null!;
        public ItemFramework.ItemSlot OutputSlot => null!;
    }
}

namespace Il2CppScheduleOne.UI.Stations
{
    public class MixingStationInterface : MonoBehaviour
    {
        public ObjectScripts.MixingStation Station => null!;
        public Il2CppTMPro.TextMeshProUGUI InstructionLabel => null!;
        public UnityEngine.UI.Button BeginButton => null!;
        public void UpdateInstruction() { }
        public void UpdateBeginButton() { }
        public void BeginButtonPressed() { }
    }
}
