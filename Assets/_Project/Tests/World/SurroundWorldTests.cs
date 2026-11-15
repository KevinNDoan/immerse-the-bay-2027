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
    public class SurroundWorldTests
    {
        [UnityTest]
        public IEnumerator GeneratedModelCanBeGrabbedSpreadAndReplaced()
        {
            var cameraObject = new GameObject("Study test camera"); cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>(); cameraObject.transform.position = Vector3.up * 1.6f;
            var managerObject = new GameObject("Study test manager"); managerObject.AddComponent<XRInteractionManager>();
            var exhibitObject = new GameObject("Study test");
            var exhibit = exhibitObject.AddComponent<StudyExhibit>();
            var hand = new GameObject("Study test hand");
            var handCollider = hand.AddComponent<SphereCollider>(); handCollider.isTrigger = true;
            var handBody = hand.AddComponent<Rigidbody>(); handBody.isKinematic = true;
            var interactor = hand.AddComponent<XRDirectInteractor>();
            try
            {
                var data = new StudyModelData { title = "Flow model", explanation = "A schematic relationship", eventPrompt = "Change the flow",
                    parts = new[] {
                        Part("Source", "sphere", new Vector3(-.25f,0,0)),
                        Part("Destination", "box", new Vector3(.25f,0,0)),
                        new StudyPartData {name="Connection",detail="The relationship between them",shape="tube",color="#EEAA33",motion="still",
                            scale=new Vector3(.025f,1,1),points=new[] {new Vector3(-.25f,0,0),new Vector3(.25f,0,0)}} } };
                exhibit.Build(data); yield return null;
                Assert.AreEqual(3,exhibit.PartCount);
                hand.transform.position = exhibit.Grab.transform.position;
                interactor.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)exhibit.Grab);
                yield return null;
                Assert.IsTrue(exhibit.IsHeld);
                interactor.EndManualInteraction(); yield return null;
                Assert.IsFalse(exhibit.IsHeld);
                Assert.IsTrue(exhibit.Grab.GetComponent<Rigidbody>().isKinematic);
                exhibit.ToggleExploded(); yield return new WaitForSeconds(.6f);
                Assert.IsTrue(exhibit.Exploded);
                data.parts = new[] { Part("Revised", "ring", Vector3.zero) };
                exhibit.Build(data); yield return null;
                Assert.AreEqual(1,exhibit.PartCount);
                Assert.IsFalse(exhibit.Exploded);
                exhibit.Clear(); yield return null;
                Assert.AreEqual(0,exhibit.PartCount);
            }
            finally { Object.Destroy(exhibitObject); Object.Destroy(hand); Object.Destroy(managerObject); Object.Destroy(cameraObject); }
        }

        private static StudyPartData Part(string name,string shape,Vector3 position) => new StudyPartData
        { name=name, detail="A generated schematic part",shape=shape,position=position,scale=Vector3.one*.2f,color="#44BBEE",motion="still",points=Array.Empty<Vector3>() };

        [UnityTest]
        public IEnumerator RenderGeneratedWorldPreview()
        {
            string folder=Environment.GetEnvironmentVariable("ZAP_PREVIEW_DIR");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("Optional live artifact preview.");
            var cameraObject=new GameObject("Preview camera"); cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>(); camera.transform.position=Vector3.up*1.6f;
            camera.fieldOfView=75; camera.nearClipPlane=.05f; camera.farClipPlane=150;
            var domeObject=new GameObject("Preview surroundings"); var dome=domeObject.AddComponent<DomeView>();
            var studyObject=new GameObject("Preview model"); var study=studyObject.AddComponent<StudyExhibit>();
            var seed=new Texture2D(2,2); var atlas=new Texture2D(2,2);
            var target=new RenderTexture(1200,900,24); var read=new Texture2D(1200,900,TextureFormat.RGB24,false);
            try
            {
                ImageConversion.LoadImage(seed,File.ReadAllBytes(Path.Combine(folder,"seed.jpg")));
                ImageConversion.LoadImage(atlas,File.ReadAllBytes(Path.Combine(folder,"atlas.jpg")));
                int mask=int.Parse(File.ReadAllText(Path.Combine(folder,"mask.txt")));
                dome.SetSeed(seed); dome.SetTexture(atlas); dome.SetProjection("multiview",mask); dome.SetVisible(true);
                study.Build(JsonUtility.FromJson<StudyModelData>(File.ReadAllText(Path.Combine(folder,"plan.json"))));
                target.Create(); yield return null;
                for(int i=0;i<4;i++)
                {
                    camera.transform.rotation=Quaternion.Euler(0,i*90,0);
                    yield return null;
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                    var previous=RenderTexture.active; RenderTexture.active=target;
                    read.ReadPixels(new Rect(0,0,1200,900),0,0); read.Apply(); RenderTexture.active=previous;
                    File.WriteAllBytes(Path.Combine(folder,"view-"+i+".png"),read.EncodeToPNG());
                }
                File.WriteAllBytes(Path.Combine(folder,"surround.jpg"),Convert.FromBase64String(dome.CapturePanorama()));
                Assert.Greater(study.PartCount,0);
            }
            finally
            {
                Object.Destroy(studyObject); Object.Destroy(domeObject); Object.Destroy(cameraObject);
                Object.Destroy(seed); Object.Destroy(atlas); Object.Destroy(read); target.Release(); Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator SurroundRendersFourDirectionsAndCapturesAnEnvironmentMap()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            var cameraObject = new GameObject("Surround test camera"); cameraObject.tag="MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.fieldOfView=65; camera.nearClipPlane=.05f; camera.farClipPlane=150;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            var domeObject=new GameObject("Surround test dome"); var dome=domeObject.AddComponent<DomeView>();
            var atlas=new Texture2D(64,64,TextureFormat.RGB24,false);
            var seed=new Texture2D(4,2,TextureFormat.RGB24,false);
            var target=new RenderTexture(128,128,24);
            var read=new Texture2D(128,128,TextureFormat.RGB24,false);
            Color[] colors={Color.red,Color.green,Color.blue,Color.yellow};
            try
            {
                for (int y=0;y<64;y++) for (int x=0;x<64;x++) atlas.SetPixel(x,y,colors[(y>=32?0:2)+(x>=32?1:0)]);
                atlas.Apply(); for(int y=0;y<2;y++) for(int x=0;x<4;x++) seed.SetPixel(x,y,Color.gray); seed.Apply();
                dome.SetSeed(seed); dome.SetTexture(atlas); dome.SetProjection("multiview",15); dome.SetVisible(true);
                target.Create(); yield return null;
                for (int direction=0;direction<4;direction++)
                {
                    camera.transform.rotation=Quaternion.Euler(0,direction*90,0);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                    var previous=RenderTexture.active; RenderTexture.active=target;
                    read.ReadPixels(new Rect(0,0,128,128),0,0); read.Apply(); RenderTexture.active=previous;
                    Color actual=read.GetPixel(64,64);
                    Assert.Less(Vector3.Distance(new Vector3(actual.r,actual.g,actual.b),new Vector3(colors[direction].r,colors[direction].g,colors[direction].b)),.18f,"Wrong view at yaw "+direction*90);
                }
                var captured=dome.CapturePanorama();
                var panorama=new Texture2D(2,2);
                try
                {
                    Assert.IsTrue(ImageConversion.LoadImage(panorama,Convert.FromBase64String(captured)));
                    Assert.AreEqual(1024,panorama.width); Assert.AreEqual(512,panorama.height);
                    var forward=panorama.GetPixel(512,256);
                    Assert.Greater(forward.r,.8f); Assert.Less(forward.g,.15f);
                }
                finally { Object.Destroy(panorama); }
            }
            finally { Object.Destroy(domeObject); Object.Destroy(cameraObject); Object.Destroy(atlas); Object.Destroy(seed); Object.Destroy(read); target.Release(); Object.Destroy(target); }
        }
    }
}
