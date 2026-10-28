using System.Collections.Generic;
using ShaderDIY;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public sealed class ShaderDIYPerfHistory
    {
        public const int Capacity = 40;

        public struct Sample
        {
            public float alu;
            public float tex;
            public float variants;
            public float interpolators;
            public float load;
        }

        readonly Sample[] _buf = new Sample[Capacity];
        int _count;
        int _start;
        string _lastKey;

        public int Count { get { return _count; } }

        public Sample Get(int index)
        {
            return _buf[(_start + index) % Capacity];
        }

        public Sample Latest
        {
            get { return _count == 0 ? default(Sample) : Get(_count - 1); }
        }

        public void Push(ShaderDIYGraph graph)
        {
            if (graph == null)
                return;
            var s = FromGraph(graph);
            var key = graph.lastAluCost + "|" + graph.lastTexCost + "|" + graph.lastVariantEstimate + "|" + graph.lastInterpolatorCost;
            if (key == _lastKey && _count > 0)
                return;
            _lastKey = key;
            if (_count < Capacity)
            {
                _buf[_count] = s;
                _count++;
                return;
            }
            _buf[_start] = s;
            _start = (_start + 1) % Capacity;
        }

        public static Sample FromGraph(ShaderDIYGraph graph)
        {
            var s = new Sample
            {
                alu = graph.lastAluCost,
                tex = graph.lastTexCost,
                variants = graph.lastVariantEstimate,
                interpolators = graph.lastInterpolatorCost
            };
            s.load = Mathf.Clamp01(s.alu / 80f) * 0.42f +
                     Mathf.Clamp01(s.tex / 8f) * 0.28f +
                     Mathf.Clamp01(s.variants / 16f) * 0.18f +
                     Mathf.Clamp01(s.interpolators / 12f) * 0.12f;
            return s;
        }

        static readonly Dictionary<int, ShaderDIYPerfHistory> Map = new Dictionary<int, ShaderDIYPerfHistory>();

        public static ShaderDIYPerfHistory For(ShaderDIYGraph graph)
        {
            if (graph == null)
                return null;
            int id = graph.GetInstanceID();
            ShaderDIYPerfHistory h;
            if (!Map.TryGetValue(id, out h) || h == null)
            {
                h = new ShaderDIYPerfHistory();
                Map[id] = h;
            }
            return h;
        }
    }
}
