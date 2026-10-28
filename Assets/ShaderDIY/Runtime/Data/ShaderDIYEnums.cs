using System;

namespace ShaderDIY
{
    public enum PanelRegion
    {
        Auto = 0,
        Features = 1,
        Textures = 2,
        Parameters = 3,
        Performance = 4
    }

    public enum ShaderDIYCategory
    {
        Unlit = 0,
        PBR = 1,
        Toon = 2,
        Custom = 3,
        Atmosphere = 4,
        FX = 5
    }

    public enum LightingMode
    {
        Single = 0,
        Multi = 1
    }

    public enum EngineCompat
    {
        Unity2021 = 0,
        Unity2022 = 1,
        Unity6 = 2
    }

    public enum CullModeOption
    {
        Back = 0,
        Front = 1,
        Off = 2
    }

    public enum QueueOption
    {
        Geometry = 2000,
        AlphaTest = 2450,
        Transparent = 3000
    }

    public enum TexChannel
    {
        RGBA = 0,
        R = 1,
        G = 2,
        B = 3,
        A = 4
    }

    [Flags]
    public enum TexOps
    {
        None = 0,
        TilingOffset = 1 << 0,
        Channel = 1 << 1,
        Invert = 1 << 2,
        MipBias = 1 << 3,
        All = TilingOffset | Channel | Invert | MipBias
    }

    public enum SdiyAssetKind
    {
        Host = 0,
        Module = 1
    }
}
