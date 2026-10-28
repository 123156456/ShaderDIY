namespace ShaderDIY
{
    /// <summary>
    /// 第 1 区由框架固定提供，不写进每个 Host Shader。
    /// </summary>
    public static class ShaderDIYRenderState
    {
        public const string KeyCull = "Cull";
        public const string KeyZWrite = "ZWrite";
        public const string KeyQueue = "Queue";
        public const string KeyReceiveShadows = "ReceiveShadows";
        public const string KeyLightingMode = "LightingMode";
        public const string KeyEngine = "EngineCompat";

        public const string DefaultCull = "Back";
        public const string DefaultZWrite = "On";
        public const string DefaultQueue = "Geometry";
        public const string DefaultReceiveShadows = "On";
        public const string DefaultLighting = "Multi";
        public const string DefaultEngine = "Unity2021";

        public static readonly string[] CategoryLabels = { "无光照", "PBR", "卡渲", "自定义", "大气" };
        public static readonly string[] CullLabels = { "正面", "背面", "双面" };
        public static readonly string[] CullValues = { "Back", "Front", "Off" };
        public static readonly string[] QueueLabels = { "不透明", "镂空", "透明" };
        public static readonly string[] QueueValues = { "Geometry", "AlphaTest", "Transparent" };
        public static readonly string[] LightLabels = { "单光源", "多光源" };
        public static readonly string[] LightValues = { "Single", "Multi" };
        public static readonly string[] EngineLabels = { "Unity 2021", "Unity 2022", "Unity 6" };
        public static readonly string[] EngineValues = { "Unity2021", "Unity2022", "Unity6" };

        public static string NormalizeEngine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return DefaultEngine;
            if (value.IndexOf("2022", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("URP14", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Unity2022";
            if (value.IndexOf("Unity6", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value == "6" ||
                value.IndexOf("URP17", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Unity6";
            return "Unity2021";
        }

        public static string ToUrpDefine(string value)
        {
            switch (NormalizeEngine(value))
            {
                case "Unity2022": return "URP14";
                case "Unity6": return "URP17";
                default: return "URP12";
            }
        }

        public static int IndexOf(string[] values, string current, int fallback)
        {
            if (values == null || string.IsNullOrEmpty(current))
                return fallback;
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], current, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return fallback;
        }
    }
}
