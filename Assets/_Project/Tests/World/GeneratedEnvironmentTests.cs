using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Zap.Constellation;
using Object = UnityEngine.Object;

namespace Zap.World.Tests
{
    public class GeneratedEnvironmentTests
    {
        [TestCase(1.2f)]
        [TestCase(1.9f)]
        public void SavedWorldCollidersProvideClearEntryAtDifferentViewerHeights(float eyeHeight)
        {
            string cache = Environment.GetEnvironmentVariable("ZAP_FLOOR_CACHE_ROOT");
            if (string.IsNullOrEmpty(cache)) Assert.Ignore("Optional saved-world floor regression matrix.");
            int checkedWorlds = 0;
            foreach (string folder in Directory.GetDirectories(cache))
            {
                string manifestPath = Path.Combine(folder, "splat-manifest.json");
                if (!File.Exists(manifestPath)) continue;
                var owner = new GameObject("Saved world floor test");
                var meshes = new System.Collections.Generic.List<Mesh>();
                try
                {
                    var manifest = JsonUtility.FromJson<EnvironmentManifest>(File.ReadAllText(manifestPath));
                    var environment = owner.AddComponent<GeneratedEnvironment>();
                    var root = new GameObject("Saved collision"); root.transform.SetParent(owner.transform, false);
                    var eye = new Vector3(3, 2 + eyeHeight, -4);
                    root.transform.SetPositionAndRotation(eye, Quaternion.Euler(0, 73, 0));
                    foreach (var asset in manifest.collision)
                    {
                        var surface = new GameObject("Saved surface"); surface.transform.SetParent(root.transform, false);
                        var mesh = GeneratedEnvironment.DecodeMesh(File.ReadAllBytes(Path.Combine(folder, "assets", asset.mesh)));
                        meshes.Add(mesh); surface.AddComponent<MeshCollider>().sharedMesh = mesh;
                    }
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(GeneratedEnvironment).GetField("_root", flags).SetValue(environment, root);
                    bool aligned = (bool)typeof(GeneratedEnvironment).GetMethod("AlignStartingFloor", flags)
                        .Invoke(environment, new object[] { manifest, eye, 2f });
                    Assert.IsTrue(aligned, Path.GetFileName(folder) + " has no entry at height " + eyeHeight);
                    Assert.IsTrue(environment.SurfaceBelow(eye, eyeHeight + .2f, out var ground));
                    Assert.AreEqual(2f, ground.point.y, .01f);
                    Assert.IsEmpty(Physics.OverlapCapsule(new Vector3(eye.x, 2.3f, eye.z),
                        eye - Vector3.up * .15f, .24f), Path.GetFileName(folder) + " clips the viewer");
                    checkedWorlds++;
                }
                finally
                {
                    Object.DestroyImmediate(owner);
                    foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
                    Physics.SyncTransforms();
                }
            }
            Assert.Greater(checkedWorlds, 0);
            Debug.Log($"[Environment tests] Verified {checkedWorlds} saved worlds at viewer height {eyeHeight:0.0}m.");
        }

        [TestCase(.56f, 1.6f)]
        [TestCase(-.8f, 1.6f)]
        [TestCase(.56f, 1.9f)]
        public void IncorrectGroundEstimateUsesRealFloorWithoutEnlargingRoom(float floorError, float eyeHeight)
        {
            var owner = new GameObject("Inaccurate ground estimate test");
            try
            {
                var environment = owner.AddComponent<GeneratedEnvironment>();
                var root = new GameObject("Generated room"); root.transform.SetParent(owner.transform, false);
                root.transform.rotation = Quaternion.Euler(0, 37, 0);
                foreach (float y in new[] { floorError - 1.65f, floorError + 1.45f })
                {
                    var slab = new GameObject("Room surface"); slab.transform.SetParent(root.transform, false);
                    slab.transform.localPosition = new Vector3(0, y, 0);
                    slab.AddComponent<BoxCollider>().size = new Vector3(6, .1f, 6);
                }
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(GeneratedEnvironment).GetField("_root", flags).SetValue(environment, root);
                var align = typeof(GeneratedEnvironment).GetMethod("AlignStartingFloor", flags);
                var eye = Vector3.up * eyeHeight;
                Assert.IsTrue((bool)align.Invoke(environment, new object[] {
                    new EnvironmentManifest { metricScale = 1, groundOffset = 1.6f }, eye, 0f }));
                Assert.AreEqual(Vector3.one, root.transform.localScale,
                    "Incorrect floor metadata must not make a normal room larger.");
                Assert.IsTrue(environment.SurfaceBelow(eye, 2, out var ground));
                Assert.AreEqual(0f, ground.point.y, .001f);
                Assert.IsEmpty(Physics.OverlapCapsule(Vector3.up * .3f,
                    Vector3.up * (eyeHeight - .15f), .24f));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase(1f, true)]
        [TestCase(3f, false)]
        public void SmallWorldsExpandOnlyWhenStandingClearanceRequiresIt(float roomHeight, bool shouldExpand)
        {
            var owner = new GameObject("Standing clearance test");
            try
            {
                var environment = owner.AddComponent<GeneratedEnvironment>();
                var root = new GameObject("Generated room"); root.transform.SetParent(owner.transform, false);
                foreach (float y in new[] { -.15f, roomHeight - .05f })
                {
                    var slab = new GameObject("Room surface"); slab.transform.SetParent(root.transform, false);
                    slab.transform.localPosition = new Vector3(0, y, 0);
                    slab.AddComponent<BoxCollider>().size = new Vector3(4, .1f, 4);
                }
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(GeneratedEnvironment).GetField("_root", flags).SetValue(environment, root);
                var align = typeof(GeneratedEnvironment).GetMethod("AlignStartingFloor", flags);
                var eye = Vector3.up * 1.6f;
                Assert.IsTrue((bool)align.Invoke(environment, new object[] {
                    new EnvironmentManifest { metricScale = 1, groundOffset = .1f }, eye, 0f }));
                Assert.AreEqual(shouldExpand, root.transform.localScale.x > 1);
                Assert.IsTrue(environment.SurfaceBelow(eye, 2, out var ground));
                Assert.AreEqual(0f, ground.point.y, .001f);
                Assert.IsEmpty(Physics.OverlapCapsule(Vector3.up * .3f, Vector3.up * 1.45f, .24f),
                    "Standing position must have real body clearance, not bypass collision.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void MeshReaderRejectsTruncatedBuffersAndEscapingPaths()
        {
            Assert.Throws<InvalidDataException>(() => GeneratedEnvironment.DecodeMesh(new byte[4]));
            Assert.IsFalse(GeneratedEnvironment.ValidBasePath("/world/assets/../../"));
            Assert.IsTrue(GeneratedEnvironment.ValidBasePath("/world/assets/" + new string('a',64) + "/"));
        }

        [UnityTest]
        public IEnumerator RenderImportedWorldAndVerifySurfacePlacement()
        {
            string folder = Environment.GetEnvironmentVariable("ZAP_ENV_PREVIEW_DIR");
            string url = Environment.GetEnvironmentVariable("ZAP_ENV_PREVIEW_URL");
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(url)) Assert.Ignore("Optional real mesh verification.");
            var rigObject = new GameObject("XR Origin (XR Rig)");
            var cameraObject = new GameObject("Generated scene camera"); cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(rigObject.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = Vector3.up * 1.6f;
            camera.fieldOfView = 75; camera.nearClipPlane = .04f; camera.farClipPlane = 150;
            // Match VoidEnvironment's headset camera. A skybox renders after opaque
            // geometry and would overwrite the depthless background panorama.
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            var environmentObject = new GameObject("Generated scene test");
            var environment = environmentObject.AddComponent<GeneratedEnvironment>();
            var studyObject = new GameObject("Scene learning object"); var study = studyObject.AddComponent<StudyExhibit>();
            var target = new RenderTexture(1200,900,24); var read = new Texture2D(1200,900,TextureFormat.RGB24,false);
            try
            {
                var manifest = JsonUtility.FromJson<EnvironmentManifest>(File.ReadAllText(Path.Combine(folder,"manifest.json")));
                string error = null;
                yield return environment.Load(manifest, url, _ => {}, failure => error = failure);
                Assert.IsNull(error); Assert.IsTrue(environment.IsVisible);
                if (manifest.splats != null) Assert.Greater(environment.GetComponentInChildren<GaussianEnvironment>().Count,1000);
                else Assert.Greater(environment.TriangleCount,1000);
                var backdrop = environment.GetComponentInChildren<DomeView>();
                Assert.IsNotNull(backdrop,"The environment panorama did not load.");
                Assert.IsTrue(backdrop.GetComponent<MeshRenderer>().enabled,"The panorama is hidden after initialization.");
                Assert.Greater(backdrop.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.width,32,
                    "The panorama texture was discarded during initialization.");
                Assert.IsTrue(environment.SurfaceBelow(camera.transform.position,3,out var ground),"No floor beneath the initial viewpoint.");
                Assert.Less(ground.point.y,camera.transform.position.y-.3f);
                Assert.Less(Mathf.Abs(ground.point.y-rigObject.transform.position.y),.08f,
                    "The starting point is on furniture instead of the room floor.");
                study.SetEnvironment(environment);
                study.Build(JsonUtility.FromJson<StudyModelData>(File.ReadAllText(Path.Combine(folder,"plan.json"))));
                yield return null;
                Assert.IsTrue(environment.FindStudySurface(out var surface),"No surface available for the learning object.");
                Assert.IsTrue(study.Grab.GetComponent<Rigidbody>().isKinematic);
                Assert.IsFalse(study.Grab.GetComponent<Rigidbody>().useGravity);
                var body = study.Grab.GetComponent<Rigidbody>();
                body.position += Vector3.up * .15f;
                Vector3 floatingPosition = body.position;
                for (int step=0; step<70; step++) yield return new WaitForFixedUpdate();
                Assert.Less(Vector3.Distance(body.position, floatingPosition), .01f,
                    "Released objects must remain floating where the learner leaves them.");
                Assert.Less(body.linearVelocity.magnitude,.12f,"The learning object has not come to rest.");
                File.WriteAllText(Path.Combine(folder,"placement.txt"),
                    "Eye: " + camera.transform.position + "\nSupport: " + surface.point +
                    "\nModel: " + body.position + "\nEnvironment: " + environment.transform.GetChild(0).position +
                    "\nEnvironment yaw: " + environment.transform.GetChild(0).eulerAngles.y);
                target.Create();
                for (int i=0;i<8;i++)
                {
                    camera.transform.rotation=Quaternion.Euler(i==0?15:0,(i%4)*90,0);
                    if (i==4) camera.transform.position += Vector3.right * .55f;
                    if (i==5)
                    {
                        camera.transform.position = Vector3.up * 1.6f;
                        camera.transform.LookAt(study.Grab.transform.position);
                    }
                    if (i>=6)
                    {
                        camera.transform.position = Vector3.up * 1.6f;
                        camera.transform.rotation = Quaternion.Euler(15,0,0);
                        studyObject.SetActive(false);
                    }
                    if (i==7)
                        foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>())
                            if (renderer.GetComponent<DomeView>() == null) renderer.enabled = false;
                    yield return null;
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                    var previous=RenderTexture.active; RenderTexture.active=target;
                    read.ReadPixels(new Rect(0,0,1200,900),0,0); read.Apply(); RenderTexture.active=previous;
                    if (i==7)
                        foreach (var sample in new[] { new Vector2Int(300,300), new Vector2Int(600,450), new Vector2Int(900,600) })
                        {
                            Color pixel = read.GetPixel(sample.x,sample.y);
                            Assert.Greater(Vector3.Distance(new Vector3(pixel.r,pixel.g,pixel.b),Vector3.right+Vector3.forward),.2f,
                                "The panorama did not cover the camera's clear color.");
                        }
                    File.WriteAllBytes(Path.Combine(folder,"spatial-"+i+".png"),read.EncodeToPNG());
                }
                studyObject.SetActive(true);
                foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = true;
                camera.transform.SetPositionAndRotation(Vector3.up*1.6f,Quaternion.identity);
                var navigation = new WorldNavigation(); navigation.Enter(environment);
                Vector3 original = rigObject.transform.position;
                // Generated rooms need not have a clear aisle in one predetermined direction.
                float walked = 0;
                foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                {
                    rigObject.transform.position = original;
                    for(int i=0;i<40;i++)
                    {
                        yield return new WaitForFixedUpdate();
                        navigation.Tick(direction,Vector2.zero,true,(_,__) => Assert.Fail("Geometry walking must not prompt a video model."));
                    }
                    walked = Mathf.Max(walked, Vector3.Distance(original,rigObject.transform.position));
                    if (walked > .08f) break;
                }
                Assert.Greater(walked,.08f,"Local walking did not move through the clear floor area.");
                navigation.Tick(Vector2.zero,Vector2.right,true,(_,__) => Assert.Fail());
                Assert.Greater(Quaternion.Angle(rigObject.transform.rotation,Quaternion.identity),20);
                navigation.Exit();
                Assert.Less(Vector3.Distance(original,rigObject.transform.position),.001f);
                Assert.Less(Quaternion.Angle(rigObject.transform.rotation,Quaternion.identity),.001f);
                environment.Clear(); Assert.IsFalse(environment.IsVisible);
            }
            finally
            {
                Object.Destroy(studyObject); Object.Destroy(environmentObject); Object.Destroy(cameraObject);
                Object.Destroy(rigObject);
                Object.Destroy(read); target.Release(); Object.Destroy(target);
            }
        }
    }
}
