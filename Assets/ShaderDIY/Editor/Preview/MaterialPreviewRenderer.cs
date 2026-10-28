using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShaderDIY.Editor
{
    public enum PreviewMeshKind
    {
        Sphere = 0,
        Cube = 1,
        Capsule = 2,
        Cylinder = 3,
        Quad = 4,
        Plane = 5,
        Custom = 6
    }

    public sealed class MaterialPreviewRenderer : IDisposable
    {
        static readonly string[] BuiltinNames =
        {
            "New-Sphere.fbx",
            "Cube.fbx",
            "Capsule.fbx",
            "Cylinder.fbx",
            "Quad.fbx",
            "Plane.fbx"
        };

        PreviewRenderUtility _util;
        Mesh _mesh;
        Mesh _custom;
        PreviewMeshKind _kind = PreviewMeshKind.Sphere;
        Vector2 _orbit;
        float _zoom = 1f;
        const float MinZoom = 0.4f;
        const float MaxZoom = 2.5f;
        bool _inited;

        public PreviewMeshKind Kind
        {
            get { return _kind; }
        }

        public Mesh CustomMesh
        {
            get { return _custom; }
        }

        public void Dispose()
        {
            if (_util != null)
            {
                _util.Cleanup();
                _util = null;
            }
        }

        public void SetMesh(PreviewMeshKind kind, Mesh custom)
        {
            _kind = kind;
            _custom = custom;
            _mesh = ResolveMesh();
        }

        public void Draw(Rect rect, Material material)
        {
            if (rect.width < 8 || rect.height < 8)
                return;

            Ensure();
            if (_mesh == null)
                _mesh = ResolveMesh();

            var e = Event.current;
            bool inside = rect.Contains(e.mousePosition);
            if (e.type == EventType.MouseDrag && inside && e.button == 0)
            {
                _orbit.x -= e.delta.x;
                _orbit.y = Mathf.Clamp(_orbit.y - e.delta.y, -80f, 80f);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && inside)
            {
                _zoom = Mathf.Clamp(_zoom * Mathf.Pow(1.12f, -e.delta.y * 0.08f), MinZoom, MaxZoom);
                e.Use();
            }

            if (e.type != EventType.Repaint)
                return;

            _util.BeginPreview(rect, GUIStyle.none);
            var cam = _util.camera;
            cam.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.cameraType = CameraType.Preview;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            cam.fieldOfView = 30f;
            cam.transform.position = new Vector3(0, 0, -4.2f);
            cam.transform.rotation = Quaternion.identity;

            if (_util.lights != null && _util.lights.Length > 0)
            {
                _util.lights[0].intensity = 1.35f;
                _util.lights[0].transform.rotation = Quaternion.Euler(50f, 40f, 0f);
                if (_util.lights.Length > 1)
                {
                    _util.lights[1].intensity = 0.45f;
                    _util.lights[1].transform.rotation = Quaternion.Euler(340f, 220f, 0f);
                }
            }
            _util.ambientColor = new Color(0.18f, 0.18f, 0.2f, 1f);

            if (material != null && _mesh != null)
            {
                var rot = Quaternion.Euler(_orbit.y, 0f, 0f) * Quaternion.Euler(0f, _orbit.x, 0f);
                float scale = FitScale(_mesh) * _zoom;
                var mtx = Matrix4x4.TRS(rot * (-_mesh.bounds.center * scale), rot, Vector3.one * scale);
                _util.DrawMesh(_mesh, mtx, material, 0);
            }

            cam.Render();
            var tex = _util.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        Mesh ResolveMesh()
        {
            if (_kind == PreviewMeshKind.Custom && _custom != null)
                return _custom;
            int idx = (int)_kind;
            if (idx < 0 || idx >= BuiltinNames.Length)
                idx = 0;
            var mesh = Resources.GetBuiltinResource<Mesh>(BuiltinNames[idx]);
            if (mesh == null && idx == 0)
                mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            return mesh;
        }

        static float FitScale(Mesh mesh)
        {
            var e = mesh.bounds.extents;
            float mag = Mathf.Max(0.001f, e.magnitude);
            return 1.55f / mag;
        }

        void Ensure()
        {
            if (_inited && _util != null)
                return;
            _util = new PreviewRenderUtility();
            _util.cameraFieldOfView = 30f;
            if (_util.camera != null && _util.camera.GetComponent<UniversalAdditionalCameraData>() == null)
                _util.camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            _mesh = ResolveMesh();
            _inited = true;
        }
    }
}
