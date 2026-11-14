using System;
using UnityEngine;

namespace Zap.World
{
    public class DomeView : MonoBehaviour
    {
        public enum MappingMode
        {
            Wrap,
            Front
        }

        private const float Radius = 5.0f;
        private const int LonSegments = 64;
        private const int LatSegments = 32;

        public MappingMode Mode { get; private set; } = MappingMode.Wrap;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;
        private Camera _mainCamera;

        private Vector2[] _wrapUVs;
        private Vector2[] _frontUVs;

        private void Awake()
        {
            _meshFilter = gameObject.GetComponent<MeshFilter>();
            if (_meshFilter == null)
            {
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            _meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
            {
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            BuildDomeMesh();
            InitMaterial();

            // Default hidden until a world is entered
            SetVisible(false);
        }

        private void InitMaterial()
        {
            var shader = Shader.Find("Zap/Dome");
            if (shader == null)
            {
                Debug.LogError("[DomeView] Could not find shader 'Zap/Dome'.");
                return;
            }

            _material = new Material(shader)
            {
                name = "ZapDome_RuntimeMaterial"
            };
            _meshRenderer.material = _material;
        }

        private void BuildDomeMesh()
        {
            _mesh = new Mesh
            {
                name = "ZapDome_SphereMesh"
            };

            int numVertices = (LatSegments + 1) * (LonSegments + 1);
            var vertices = new Vector3[numVertices];
            var normals = new Vector3[numVertices];
            _wrapUVs = new Vector2[numVertices];
            _frontUVs = new Vector2[numVertices];

            for (int lat = 0; lat <= LatSegments; lat++)
            {
                float latRatio = (float)lat / LatSegments; // 0 (top, +Y) to 1 (bottom, -Y)
                float theta = latRatio * Mathf.PI; // 0 to PI
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                float y = Radius * cosTheta;
                float rSlice = Radius * sinTheta;

                // Elevation in degrees: +90 at top, 0 at equator, -90 at bottom
                float elevationDeg = (0.5f - latRatio) * 180f;

                for (int lon = 0; lon <= LonSegments; lon++)
                {
                    float lonRatio = (float)lon / LonSegments; // 0 to 1
                    // Azimuth angle: phi in [-PI, +PI], 0 is at +Z (center front)
                    float phi = (lonRatio - 0.5f) * 2f * Mathf.PI;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    float x = rSlice * sinPhi;
                    float z = rSlice * cosPhi;

                    int index = lat * (LonSegments + 1) + lon;
                    var pos = new Vector3(x, y, z);
                    vertices[index] = pos;
                    normals[index] = -pos.normalized; // Inside-out normal pointing to center

                    // Wrap UV:
                    // Full equirectangular panorama.
                    // lonRatio 0.5 corresponds to phi = 0 (+Z), center of frame U = 0.5.
                    _wrapUVs[index] = new Vector2(lonRatio, 1f - latRatio);

                    // Front UV:
                    // Patch 110 degrees wide and 62 degrees tall centered at +Z (phi=0, elevation=0).
                    float azimuthDeg = phi * Mathf.Rad2Deg;
                    float uFront = (azimuthDeg - (-55f)) / 110f;
                    float vFront = (elevationDeg - (-31f)) / 62f;
                    _frontUVs[index] = new Vector2(uFront, vFront);
                }
            }

            int numTriangles = LatSegments * LonSegments * 6;
            var triangles = new int[numTriangles];
            int triIndex = 0;

            for (int lat = 0; lat < LatSegments; lat++)
            {
                for (int lon = 0; lon < LonSegments; lon++)
                {
                    int current = lat * (LonSegments + 1) + lon;
                    int next = current + LonSegments + 1;

                    triangles[triIndex++] = current;
                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next;

                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next + 1;
                    triangles[triIndex++] = next;
                }
            }

            _mesh.vertices = vertices;
            _mesh.normals = normals;
            _mesh.uv = _wrapUVs; // Default is Wrap
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();

            _meshFilter.mesh = _mesh;
        }

        public void SetMappingMode(MappingMode mode)
        {
            Mode = mode;
            if (_mesh != null)
            {
                _mesh.uv = (mode == MappingMode.Wrap) ? _wrapUVs : _frontUVs;
            }
        }

        public void ToggleMappingMode()
        {
            SetMappingMode(Mode == MappingMode.Wrap ? MappingMode.Front : MappingMode.Wrap);
        }

        public void SetVisible(bool visible)
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = visible;
            }
        }

        public void SetTexture(Texture2D texture)
        {
            if (_material != null)
            {
                _material.mainTexture = texture;
            }
        }

        private void LateUpdate()
        {
            // Follow the main camera's position every frame, never its rotation
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_mainCamera != null)
            {
                transform.position = _mainCamera.transform.position;
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }
    }
}
