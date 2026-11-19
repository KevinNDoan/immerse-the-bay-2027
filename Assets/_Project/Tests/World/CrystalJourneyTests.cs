using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Zap.Constellation;
using Object = UnityEngine.Object;

namespace Zap.World.Tests
{
    public class CrystalJourneyTests
    {
        [Test]
        public void HyperspaceKeepsItsInitialSpeedThroughoutLongWaits()
        {
            var root = new GameObject("Journey speed test");
            try
            {
                var journey = root.AddComponent<WorldJourney>();
                journey.Begin(Vector3.zero, Vector3.forward);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var render = typeof(WorldJourney).GetMethod("RenderAt", flags);
                var material = (Material)typeof(WorldJourney).GetField("_tunnelMaterial", flags).GetValue(journey);
                foreach (float age in new[] { 2f, 4.5f, 5f, 6f, 30f, 300f })
                {
                    render.Invoke(journey, new object[] { age, 0f });
                    float start = material.GetFloat("_Travel");
                    render.Invoke(journey, new object[] { age + 1, 0f });
                    Assert.AreEqual(1f, material.GetFloat("_Travel") - start, .0001f,
                        "Travel speed changed during the provider wait at " + age);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TravelGestureRejectsPlacementAndThrowsTowardTheFace()
        {
            Assert.IsFalse(TopicBall.IsTravelThrow(Vector3.forward*.4f,Vector3.forward*.5f,Vector3.forward,.5f));
            Assert.IsFalse(TopicBall.IsTravelThrow(Vector3.back*3,Vector3.forward*.5f,Vector3.forward,.5f));
            Assert.IsFalse(TopicBall.IsTravelThrow(Vector3.forward*3,Vector3.forward*.1f,Vector3.forward,.5f));
            Assert.IsFalse(TopicBall.IsTravelThrow(Vector3.forward*3,Vector3.forward*.5f,Vector3.forward,.05f));
            Assert.IsTrue(TopicBall.IsTravelThrow(new Vector3(.3f,.2f,2),Vector3.forward*.5f,Vector3.forward,.5f));
        }

        [UnityTest]
        public IEnumerator PhysicalThrowRequestsOneJourney()
        {
            using var fixture = new GrabFixture();
            int requests = 0; fixture.ball.EnterRequested += _ => requests++;
            yield return null;
            fixture.Begin();
            yield return fixture.MoveHand(2.6f);
            fixture.hand.EndManualInteraction();
            yield return new WaitForFixedUpdate(); yield return null; yield return null;
            Assert.AreEqual(1,requests,"XRI detach velocity should trigger exactly one journey.");
            Assert.Greater(fixture.ball.LastThrowVelocity.magnitude,1.35f);
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(1,requests);
        }

        [UnityTest]
        public IEnumerator WorldThrowSurvivesVelocityLostAfterDetach()
        {
            using var fixture = new GrabFixture();
            fixture.ball.SetWorldTravelHint(true);
            int requests = 0;
            fixture.ball.EnterRequested += _ => requests++;
            // Simulate a room collision immediately after XRI detaches the orb.
            ((TravelGrabInteractable)fixture.ball.Grab).Detached += _ => fixture.ball.Body.linearVelocity = Vector3.zero;
            yield return null;
            fixture.Begin();
            yield return fixture.MoveHand(2.6f);
            fixture.hand.EndManualInteraction();
            yield return new WaitForFixedUpdate(); yield return null; yield return null;
            Assert.AreEqual(1, requests, "A room collision must not swallow a valid throw.");
            Assert.Greater(fixture.ball.LastThrowVelocity.magnitude, 1.35f);
            Assert.IsFalse(fixture.ball.IsTossed, "Travel must never discard a node.");
        }

        [UnityTest]
        public IEnumerator GentleReleaseAndCanceledSelectionDoNotTravel()
        {
            using var fixture = new GrabFixture();
            int requests = 0; fixture.ball.EnterRequested += _ => requests++;
            yield return null;
            fixture.Begin(); yield return fixture.MoveHand(.2f);
            fixture.hand.EndManualInteraction();
            yield return new WaitForFixedUpdate(); yield return null; yield return null;
            Assert.AreEqual(0,requests);
            fixture.Begin(); yield return fixture.MoveHand(2.6f);
            fixture.ball.gameObject.SetActive(false);
            yield return new WaitForFixedUpdate(); yield return null; yield return null;
            Assert.AreEqual(0,requests,"A disabled interactable emits a canceled release, never a travel gesture.");
        }

        [UnityTest]
        public IEnumerator JourneyWaitsForReadinessAndDoesNotMoveTheCamera()
        {
            var cameraObject = new GameObject("Journey test camera"); cameraObject.tag="MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.transform.position=new Vector3(1,1.6f,2);
            var root = new GameObject("Journey lifecycle test"); var journey=root.AddComponent<WorldJourney>();
            try
            {
                var position=camera.transform.position; var rotation=camera.transform.rotation;
                journey.Begin(position+Vector3.forward*.5f,Vector3.forward);
                yield return new WaitForSeconds(2.1f);
                Assert.IsTrue(journey.IsActive,"Elapsed time alone must not reveal an unready destination.");
                Assert.AreEqual(position,camera.transform.position); Assert.AreEqual(rotation,camera.transform.rotation);
                journey.MarkReady(); yield return new WaitForSeconds(.95f);
                Assert.IsFalse(journey.IsActive);
                journey.Begin(position,Vector3.forward); journey.Cancel();
                yield return null;
                Assert.IsFalse(journey.IsActive);
                Assert.AreEqual(position,camera.transform.position);
            }
            finally { Object.Destroy(root); Object.Destroy(cameraObject); }
        }

        [UnityTest]
        public IEnumerator PreviewCanBeReplacedAndCleared()
        {
            var obj=new GameObject("Preview lifecycle test"); var visual=obj.AddComponent<CrystalBallVisual>();
            var image=new Texture2D(4,2,TextureFormat.RGB24,false);
            try
            {
                image.SetPixels(new[] {Color.red,Color.green,Color.blue,Color.white,Color.red,Color.green,Color.blue,Color.white}); image.Apply();
                string data=Convert.ToBase64String(image.EncodeToPNG());
                Assert.IsTrue(visual.SetPreview(data)); Assert.IsNotNull(visual.PreviewTexture);
                Assert.IsFalse(visual.SetPreview("invalid base64")); Assert.IsFalse(visual.PreviewReady);
                Assert.IsTrue(visual.SetPreview(data)); Assert.IsFalse(visual.SetPreview(""));
                Assert.IsNull(visual.PreviewTexture);
                yield return null;
            }
            finally { Object.Destroy(obj); Object.Destroy(image); }
        }

        [UnityTest]
        public IEnumerator RenderCrystalAndHyperspacePreviews()
        {
            if (SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) Assert.Ignore("Requires GPU rendering.");
            string folder=Environment.GetEnvironmentVariable("ZAP_CRYSTAL_PREVIEW_DIR");
            string source=Environment.GetEnvironmentVariable("ZAP_CRYSTAL_PREVIEW_IMAGE");
            if (string.IsNullOrEmpty(folder)||string.IsNullOrEmpty(source)) Assert.Ignore("Optional local visual review.");
            Directory.CreateDirectory(folder);
            var cameraObject=new GameObject("Crystal preview camera"); cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>(); camera.transform.position=Vector3.up*1.6f;
            camera.fieldOfView=62; camera.nearClipPlane=.03f; camera.farClipPlane=150;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.014f,.017f,.035f);
            var orbObject=new GameObject("Preview crystal"); orbObject.transform.position=new Vector3(-.06f,1.53f,.42f);
            orbObject.transform.localScale=Vector3.one*.24f;
            var crystal=orbObject.AddComponent<CrystalBallVisual>();
            var root=new GameObject("Preview journey"); var journey=root.AddComponent<WorldJourney>();
            var target=new RenderTexture(1280,800,24); var read=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                Assert.IsTrue(crystal.SetPreview(Convert.ToBase64String(File.ReadAllBytes(source))));
                target.Create(); yield return null;
                Capture(camera,target,read,Path.Combine(folder,"crystal.png"));
                journey.Begin(orbObject.transform.position,Vector3.forward,crystal.PreviewTexture);
                orbObject.SetActive(false);
                yield return new WaitForSeconds(.4f);
                Capture(camera,target,read,Path.Combine(folder,"fracture.png"));
                yield return new WaitForSeconds(.8f);
                Capture(camera,target,read,Path.Combine(folder,"hyperspace.png"));
                var pixels=read.GetPixels32(); int lit=0;
                foreach (var pixel in pixels) if(pixel.b>50) lit++;
                Assert.Greater(lit,pixels.Length/4,"Hyperspace must visibly cover the view without bloom.");
            }
            finally
            {
                Object.Destroy(root); Object.Destroy(orbObject); Object.Destroy(cameraObject);
                target.Release(); Object.Destroy(target); Object.Destroy(read);
            }
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D read, string path)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            var previous=RenderTexture.active;
            try { RenderTexture.active=target; read.ReadPixels(new Rect(0,0,target.width,target.height),0,0); read.Apply(); File.WriteAllBytes(path,read.EncodeToPNG()); }
            finally { RenderTexture.active=previous; }
        }

        private sealed class GrabFixture : IDisposable
        {
            private readonly GameObject _camera, _manager, _handObject;
            public readonly XRDirectInteractor hand;
            public readonly TopicBall ball;
            public GrabFixture()
            {
                _camera=new GameObject("Throw camera"); _camera.tag="MainCamera";
                _camera.AddComponent<Camera>(); _camera.transform.position=Vector3.up*1.5f;
                _manager=new GameObject("Throw manager"); _manager.AddComponent<XRInteractionManager>();
                _handObject=new GameObject("Throw hand"); _handObject.transform.position=new Vector3(0,1.5f,.5f);
                var collider=_handObject.AddComponent<SphereCollider>(); collider.isTrigger=true; collider.radius=.1f;
                var body=_handObject.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
                hand=_handObject.AddComponent<XRDirectInteractor>();
                var obj=new GameObject("Throw crystal"); obj.transform.position=_handObject.transform.position;
                ball=obj.AddComponent<TopicBall>(); ball.Initialize(new NodeData {topic="A test idea"},null); ball.Grab.attachEaseInTime=0;
            }
            public void Begin() => hand.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)ball.Grab);
            public IEnumerator MoveHand(float speed)
            {
                float start=Time.unscaledTime;
                while(Time.unscaledTime-start<.4f) { _handObject.transform.position+=Vector3.forward*(speed*Time.unscaledDeltaTime); yield return null; }
            }
            public void Dispose() { Object.Destroy(ball.gameObject); Object.Destroy(_handObject); Object.Destroy(_manager); Object.Destroy(_camera); }
        }
    }
}
