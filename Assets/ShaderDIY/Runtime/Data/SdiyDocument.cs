using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShaderDIY
{
    [Serializable]
    public class SdiyProperty
    {
        public PanelRegion region = PanelRegion.Auto;
        public string name;
        public string displayName;
        public string tooltip;
        public string type;
        public string defaultValue;
        public TexOps texOps = TexOps.None;
        public string showIfProperty;
        public string showIfValue;
        public bool invertOp;
        public string tabName;
        public List<string> extraAttributes = new List<string>();
        public bool isFeature;
        public string sourceSlot;

        public bool IsTexture
        {
            get
            {
                if (string.IsNullOrEmpty(type))
                    return false;
                var t = type.Trim();
                return t == "2D" || t == "Cube" || t == "3D" || t.StartsWith("2D", StringComparison.Ordinal);
            }
        }

        public bool IsRange
        {
            get { return !string.IsNullOrEmpty(type) && type.StartsWith("Range", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsColor
        {
            get { return string.Equals(type, "Color", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsVector
        {
            get { return string.Equals(type, "Vector", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsToggle
        {
            get
            {
                return string.Equals(type, "Toggle", StringComparison.OrdinalIgnoreCase) ||
                       extraAttributes.Exists(a => a.IndexOf("Toggle", StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        public bool IsEnum
        {
            get { return !string.IsNullOrEmpty(type) && type.StartsWith("Enum", StringComparison.OrdinalIgnoreCase); }
        }
    }

    [Serializable]
    public class SdiySlot
    {
        public string name;
        public string defaultModule;
        public string signature;
        public string category;
        public string kind;

        public bool IsAddon
        {
            get
            {
                if (!string.IsNullOrEmpty(kind) &&
                    kind.Equals("Addon", StringComparison.OrdinalIgnoreCase))
                    return true;
                return !string.IsNullOrEmpty(name) && name.StartsWith("Layer", StringComparison.Ordinal);
            }
        }
    }

    [Serializable]
    public class SdiyPass
    {
        public string name;
        public string tags;
        public string hlsl;
        public string states;
    }

    [Serializable]
    public class SdiyCost
    {
        public int alu;
        public int texture;
        public int interpolator;
    }

    [Serializable]
    public class SdiyDocument
    {
        public SdiyAssetKind kind;
        public string name;
        public string slot;
        public string category = "Custom";
        public string pipeline = "URP";
        public string description;
        public List<string> companionScripts = new List<string>();
        public List<SdiyProperty> features = new List<SdiyProperty>();
        public List<SdiyProperty> properties = new List<SdiyProperty>();
        public List<SdiySlot> slots = new List<SdiySlot>();
        public List<SdiyPass> passes = new List<SdiyPass>();
        public List<string> keywords = new List<string>();
        public SdiyCost cost = new SdiyCost();
        public string formula;
        public string hlslInclude;
        public string parseError;
        public string sourcePath;

        public ShaderDIYCategory CategoryEnum
        {
            get
            {
                if (string.Equals(category, "PBR", StringComparison.OrdinalIgnoreCase))
                    return ShaderDIYCategory.PBR;
                if (string.Equals(category, "Toon", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(category, "卡渲", StringComparison.OrdinalIgnoreCase))
                    return ShaderDIYCategory.Toon;
                if (string.Equals(category, "Unlit", StringComparison.OrdinalIgnoreCase))
                    return ShaderDIYCategory.Unlit;
                if (string.Equals(category, "Atmosphere", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(category, "大气", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(category, "大气散射", StringComparison.OrdinalIgnoreCase))
                    return ShaderDIYCategory.Atmosphere;
                if (string.Equals(category, "FX", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(category, "特效", StringComparison.OrdinalIgnoreCase))
                    return ShaderDIYCategory.FX;
                return ShaderDIYCategory.Custom;
            }
        }
    }

    [Serializable]
    public class SlotChoice
    {
        public string slotName;
        public string moduleName;
    }

    [Serializable]
    public class FeatureValue
    {
        public string name;
        public string value;
    }

    [Serializable]
    public class AnimParam
    {
        public string name;
        public int kind;
        public Vector4 value;
    }

    [Serializable]
    public class AnimKey
    {
        public float time;
        public List<AnimParam> values = new List<AnimParam>();
    }
}
