using System.Collections.Generic;
using UnityEngine;

namespace ShaderDIY
{
    [CreateAssetMenu(menuName = "ShaderDIY/材质图 (Graph)", fileName = "NewShaderDIYGraph")]
    public class ShaderDIYGraph : ScriptableObject
    {
        public string hostGuid;
        public string hostPath;
        public string hostName;
        public ShaderDIYCategory category = ShaderDIYCategory.PBR;
        public List<SlotChoice> slotChoices = new List<SlotChoice>();
        public List<FeatureValue> featureValues = new List<FeatureValue>();
        public Material material;
        public Shader generatedShader;
        public string lastCompositionHash;
        public string lastCompileError;
        public int lastAluCost;
        public int lastTexCost;
        public int lastInterpolatorCost;
        public int lastVariantEstimate;
        public double lastCompileMs;
        public List<AnimKey> animKeys = new List<AnimKey>();
        public float animLength = 4f;
        public float animTime;
        public bool animLoop = true;

        public string GetSlotModule(string slotName)
        {
            if (slotChoices == null)
                return null;
            for (int i = 0; i < slotChoices.Count; i++)
            {
                if (slotChoices[i] != null && slotChoices[i].slotName == slotName)
                    return slotChoices[i].moduleName;
            }
            return null;
        }

        public void SetSlotModule(string slotName, string moduleName)
        {
            if (slotChoices == null)
                slotChoices = new List<SlotChoice>();
            for (int i = 0; i < slotChoices.Count; i++)
            {
                if (slotChoices[i] != null && slotChoices[i].slotName == slotName)
                {
                    slotChoices[i].moduleName = moduleName;
                    return;
                }
            }
            slotChoices.Add(new SlotChoice { slotName = slotName, moduleName = moduleName });
        }

        public string GetFeature(string name, string fallback)
        {
            if (featureValues == null)
                return fallback;
            for (int i = 0; i < featureValues.Count; i++)
            {
                if (featureValues[i] != null && featureValues[i].name == name)
                    return featureValues[i].value;
            }
            return fallback;
        }

        public void SetFeature(string name, string value)
        {
            if (featureValues == null)
                featureValues = new List<FeatureValue>();
            for (int i = 0; i < featureValues.Count; i++)
            {
                if (featureValues[i] != null && featureValues[i].name == name)
                {
                    featureValues[i].value = value;
                    return;
                }
            }
            featureValues.Add(new FeatureValue { name = name, value = value });
        }
    }
}
