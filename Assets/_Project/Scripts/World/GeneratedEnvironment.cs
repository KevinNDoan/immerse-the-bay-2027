using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace Zap.World
{
    [Serializable] public class EnvironmentAsset { public string mesh, texture; public int vertices, triangles; }
    [Serializable] public class EnvironmentManifest
    {
        public string basePath, panorama;
        public EnvironmentAsset[] visual, collision;
        public Color ambient = Color.gray;
        public float metricScale = 1;
        public float groundOffset;
    }

    // Imported visual and collision surfaces share the same world transform.
    // These are real meshes, so parallax, stereo and occlusion use the headset pose.
    public sealed class GeneratedEnvironment : MonoBehaviour
    {
        public bool IsVisible => _root != null && _root.activeSelf;
        public Color Ambient { get; private set; } = Color.gray;
        public int TriangleCount { get; private set; }
        private GameObject _root;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private UnityWebRequest _request;
        private int _generation;
        private readonly RaycastHit[] _hits = new RaycastHit[48];
        private readonly Collider[] _overlaps = new Collider[48];

        public IEnumerator Load(EnvironmentManifest manifest, string relayUrl, Action<string> status, Action<string> done)
        {
            Clear(); int generation = _generation;
            if (manifest?.visual == null || manifest.visual.Length == 0 || manifest.visual.Length > 64 ||
                manifest.collision == null || manifest.collision.Length == 0 || manifest.collision.Length > 64 ||
                !ValidBasePath(manifest.basePath) || !Finite(manifest.metricScale) || manifest.metricScale < .05f || manifest.metricScale > 20 ||
                !Finite(manifest.groundOffset) || Mathf.Abs(manifest.groundOffset) > 20)
            { done("The generated environment manifest is invalid."); yield break; }
            _root = new GameObject("Generated 3D environment"); _root.transform.SetParent(transform, false);
            _root.SetActive(false); Ambient = manifest.ambient;
            DomeView distantBackground = null; Texture2D panoramaTexture = null;
            string prefix = relayUrl.TrimEnd('/') + manifest.basePath;
            for (int kind = 0; kind < 2; kind++)
            {
                var assets = kind == 0 ? manifest.visual : manifest.collision;
                for (int i = 0; i < assets.Length; i++)
                {
                    status(kind == 0 ? "Loading the 3D surroundings…" : "Preparing walkable surfaces…");
                    var asset = assets[i]; byte[] bytes = null; string failure = null;
                    yield return Download(prefix, asset.mesh, data => bytes = data, error => failure = error);
                    if (generation != _generation) yield break;
                    if (failure != null) { Clear(); done(failure); yield break; }
                    Mesh mesh = null;
                    try { mesh = DecodeMesh(bytes); } catch (Exception e) when (e is IOException || e is ArgumentException || e is OverflowException)
                    { failure = "The environment mesh could not be read."; }
                    if (mesh == null) { Clear(); done(failure); yield break; }
                    _meshes.Add(mesh);
                    var obj = new GameObject(kind == 0 ? "Environment surface" : "Environment collision");
                    obj.transform.SetParent(_root.transform, false);
                    if (kind == 1)
                    {
                        obj.AddComponent<MeshCollider>().sharedMesh = mesh;
                    }
                    else
                    {
                        TriangleCount += asset.triangles;
                        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var material = new Material(Shader.Find("Zap/Environment")); _materials.Add(material);
                        if (!string.IsNullOrEmpty(asset.texture))
                        {
                            bytes = null;
                            yield return Download(prefix, asset.texture, data => bytes = data, error => failure = error);
                            if (generation != _generation) yield break;
                            if (failure != null) { Clear(); done(failure); yield break; }
                            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true); _textures.Add(texture);
                            if (!ImageConversion.LoadImage(texture, bytes, true)) { Clear(); done("The environment texture is invalid."); yield break; }
                            texture.wrapMode = TextureWrapMode.Repeat; texture.anisoLevel = 4;
                            material.SetTexture("_MainTex", texture);
                        }
                        obj.AddComponent<MeshRenderer>().sharedMaterial = material;
                        mesh.UploadMeshData(true);
                    }
                    yield return null;
                }
            }
            if (!string.IsNullOrEmpty(manifest.panorama))
            {
                byte[] bytes = null; string failure = null;
                yield return Download(prefix, manifest.panorama, data => bytes = data, error => failure = error);
                if (generation != _generation) yield break;
                if (failure == null)
                {
                    var texture = new Texture2D(2,2,TextureFormat.RGB24,false); _textures.Add(texture);
                    if (ImageConversion.LoadImage(texture, bytes, true))
                    {
                        var background = new GameObject("Environment distant background");
                        background.transform.SetParent(_root.transform, false);
                        distantBackground = background.AddComponent<DomeView>();
                        panoramaTexture = texture;
                    }
                }
            }
            if (Camera.main != null)
                _root.transform.SetPositionAndRotation(Camera.main.transform.position,
                    Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0));
            _root.transform.localScale = Vector3.one * manifest.metricScale;
            foreach (var background in _root.GetComponentsInChildren<DomeView>(true))
                background.transform.localScale = Vector3.one / manifest.metricScale;
            _root.SetActive(true); Physics.SyncTransforms();
            // DomeView.Awake initializes its material and starts hidden. Configure only
            // after activation, otherwise that initialization discards the texture/visibility.
            if (distantBackground != null)
            {
                distantBackground.SetTexture(panoramaTexture);
                distantBackground.SetProjection("panorama", 0);
                distantBackground.SetVisible(true);
            }
            if (Camera.main != null)
            {
                var rig = GameObject.Find("XR Origin (XR Rig)")?.transform;
                float floor = rig != null ? rig.position.y : Camera.main.transform.position.y - 1.6f;
                var position = _root.transform.position;
                if (Mathf.Abs(manifest.groundOffset) > .001f) position.y = floor + manifest.groundOffset;
                else if (SurfaceBelow(Camera.main.transform.position, 4, out var ground)) position.y += floor - ground.point.y;
                _root.transform.position = position; Physics.SyncTransforms();
                if (!FindStandingPoint(Camera.main.transform.position, floor, out var standing))
                { Clear(); done("No clear starting floor was found in this saved world."); yield break; }
                // Keep the user's tracked rig in place during entry; align the room's
                // nearest clear floor with it instead of spawning inside generated furniture.
                _root.transform.position += new Vector3(Camera.main.transform.position.x - standing.x,
                    floor - standing.y, Camera.main.transform.position.z - standing.z);
                Physics.SyncTransforms();
                // Reveal a useful supporting surface in front of the viewer when one
                // exists nearby, without rotating or moving the tracked camera.
                if (FindStudySurface(out var support) && support.point.y > floor + .4f)
                {
                    var toward = Vector3.ProjectOnPlane(support.point - Camera.main.transform.position, Vector3.up);
                    var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
                    _root.transform.RotateAround(Camera.main.transform.position, Vector3.up,
                        Vector3.SignedAngle(toward, forward, Vector3.up));
                    Physics.SyncTransforms();
                }
            }
            done(null);
        }

        public static bool ValidBasePath(string path) => path != null &&
            System.Text.RegularExpressions.Regex.IsMatch(path, @"^/world/assets/[a-f0-9]{64}/$");

        private IEnumerator Download(string prefix, string file, Action<byte[]> done, Action<string> error)
        {
            if (string.IsNullOrEmpty(file) || !System.Text.RegularExpressions.Regex.IsMatch(file, @"^((visual|collision)-[0-9]+\.(mesh|jpg)|panorama\.jpg)$"))
            { error("Invalid environment asset path."); yield break; }
            using var request = UnityWebRequest.Get(prefix + file); _request = request; request.timeout = 120;
            yield return request.SendWebRequest();
            if (_request == request) _request = null;
            if (request.result != UnityWebRequest.Result.Success) error("Could not load the environment. Check the relay connection.");
            else done(request.downloadHandler.data);
        }

        public static Mesh DecodeMesh(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12 || bytes.Length > 90_000_000) throw new InvalidDataException();
            using var reader = new BinaryReader(new MemoryStream(bytes));
            if (reader.ReadUInt32() != 0x31534D5A) throw new InvalidDataException();
            int count = reader.ReadInt32(), indexCount = reader.ReadInt32();
            if (count < 3 || count > 2_000_000 || indexCount < 3 || indexCount > 4_500_000 || indexCount % 3 != 0 ||
                12L + count * 24L + indexCount * 4L != bytes.Length) throw new InvalidDataException();
            var vertices = new Vector3[count]; var uv = new Vector2[count]; var colors = new Color32[count];
            var values = new float[count * 3]; Buffer.BlockCopy(bytes, 12, values, 0, count * 12);
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(values[i*3], values[i*3+1], values[i*3+2]);
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) throw new InvalidDataException();
                vertices[i] = p;
            }
            Buffer.BlockCopy(bytes, 12 + count * 12, values, 0, count * 8);
            for (int i = 0; i < count; i++)
            {
                if (!Finite(values[i*2]) || !Finite(values[i*2+1])) throw new InvalidDataException();
                uv[i] = new Vector2(values[i*2], values[i*2+1]);
            }
            int colorOffset = 12 + count * 20;
            for (int i = 0; i < count; i++) colors[i] = new Color32(bytes[colorOffset+i*4], bytes[colorOffset+i*4+1], bytes[colorOffset+i*4+2], bytes[colorOffset+i*4+3]);
            var indices = new int[indexCount]; Buffer.BlockCopy(bytes, 12 + count * 24, indices, 0, indexCount * 4);
            foreach (int index in indices) if (index < 0 || index >= count) throw new InvalidDataException();
            var mesh = new Mesh { name = "Generated environment mesh", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors32 = colors; mesh.triangles = indices;
            mesh.RecalculateBounds(); return mesh;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) < 10001;

        public bool SurfaceBelow(Vector3 point, float distance, out RaycastHit surface)
        {
            surface = default; float nearest = distance;
            int count = Physics.RaycastNonAlloc(point, Vector3.down, _hits, distance, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
                if (_hits[i].collider.GetComponentInParent<GeneratedEnvironment>() == this &&
                    _hits[i].normal.y > .6f && _hits[i].distance < nearest)
                { surface = _hits[i]; nearest = surface.distance; found = true; }
            return found;
        }

        public bool FindStudySurface(out RaycastHit surface)
        {
            surface = default;
            if (Camera.main == null || !IsVisible) return false;
            var camera = Camera.main.transform;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            float floor = GameObject.Find("XR Origin (XR Rig)")?.transform.position.y ?? camera.position.y - 1.6f;
            bool found = false; float best = float.NegativeInfinity;
            foreach (float distance in new[] { .85f, 1.2f, 1.6f, 2f, 2.4f })
                foreach (float angle in new[] { 0f, -45, 45, -90, 90, -135, 135, 180 })
                {
                    var direction = Quaternion.Euler(0, angle, 0) * forward;
                    if (!SurfaceBelow(camera.position + direction * distance - Vector3.up * .3f, 2.6f, out var candidate)) continue;
                    bool clear = true;
                    foreach (var offset in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                        if (!SurfaceBelow(candidate.point + offset * .3f + Vector3.up * .18f, .36f, out var edge) ||
                            Mathf.Abs(edge.point.y - candidate.point.y) > .15f) { clear = false; break; }
                    if (!clear) continue;
                    var sight = candidate.point + Vector3.up * .2f - camera.position;
                    int count = Physics.RaycastNonAlloc(camera.position, sight.normalized, _hits,
                        Mathf.Max(0, sight.magnitude - .08f), ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                        if (_hits[i].collider.GetComponentInParent<GeneratedEnvironment>() == this) { clear = false; break; }
                    if (!clear) continue;
                    bool raised = candidate.point.y > floor + .4f;
                    float score = (raised ? 100 : 0) - distance * 3 - Mathf.Abs(angle) * .015f;
                    if (score <= best) continue;
                    surface = candidate; best = score; found = true;
                }
            return found;
        }

        private bool FindStandingPoint(Vector3 eye, float floor, out Vector3 standing)
        {
            standing = default;
            float height = Mathf.Clamp(eye.y - floor, 1, 2);
            for (int ring = 0; ring <= 8; ring++)
            {
                int samples = ring == 0 ? 1 : 16;
                for (int i = 0; i < samples; i++)
                {
                    float angle = (i * 360f / samples + _root.transform.eulerAngles.y) * Mathf.Deg2Rad;
                    var point = new Vector3(eye.x + Mathf.Sin(angle) * ring * .4f, floor,
                        eye.z + Mathf.Cos(angle) * ring * .4f);
                    if (!SurfaceBelow(point + Vector3.up * .4f, .75f, out var ground) ||
                        Mathf.Abs(ground.point.y - floor) > .35f) continue;
                    point.y = ground.point.y;
                    bool clear = true;
                    foreach (var offset in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                        if (!SurfaceBelow(point + offset * .28f + Vector3.up * .25f, .5f, out var edge) ||
                            Mathf.Abs(edge.point.y - point.y) > .15f) { clear = false; break; }
                    if (!clear) continue;
                    int count = Physics.OverlapCapsuleNonAlloc(point + Vector3.up * .3f,
                        point + Vector3.up * Mathf.Max(.3f, height - .15f), .24f,
                        _overlaps, ~0, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < count; j++)
                        if (_overlaps[j].GetComponentInParent<GeneratedEnvironment>() == this) { clear = false; break; }
                    if (!clear) continue;
                    standing = point; return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            _generation++; _request?.Abort(); _request = null;
            if (_root != null) { _root.SetActive(false); Destroy(_root); _root = null; }
            foreach (var mesh in _meshes) Destroy(mesh);
            foreach (var material in _materials) Destroy(material);
            foreach (var texture in _textures) Destroy(texture);
            _meshes.Clear(); _materials.Clear(); _textures.Clear(); TriangleCount = 0;
        }
        private void OnDestroy() => Clear();
    }
}
