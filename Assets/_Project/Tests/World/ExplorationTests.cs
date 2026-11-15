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
    public class ExplorationTests
    {
        private static StudyModelData Model() => new StudyModelData {
            title="A connected structure", explanation="Inspect a continuous curve and its repeated connections.",
            parts=new[] {
                new StudyPartData {name="First curve",detail="A continuous path",shape="helix",color="#27C6B7",scale=Vector3.one*.04f,curve=new StudyCurveData(),motion="still"},
                new StudyPartData {name="Second curve",detail="A matching path",shape="helix",color="#E8AB55",scale=Vector3.one*.04f,curve=new StudyCurveData {phase=180},motion="still"},
                new StudyPartData {name="Connections",detail="Each line meets both paths",shape="links",color="#EEEEDD",scale=Vector3.one*.025f,
                    links=new StudyLinksData {from=0,to=1,count=20},motion="still"} },
            steps=new[] {new StudyStepData {title="See inside",explanation="The outer paths move apart to reveal the space they enclose.",focus=new[]{0,1},
                changes=new[] {new StudyChangeData {part=0,offset=Vector3.left*.25f}, new StudyChangeData {part=1,offset=Vector3.right*.25f},
                    new StudyChangeData {part=2,visible=false}}}},
            experiment=new StudyExperimentData {label="Separation",lowLabel="Together",highLabel="Apart",lowExplanation="Paths are connected.",highExplanation="The paths are separated for inspection.",
                changes=new[] {new StudyChangeData {part=0,offset=Vector3.left*.3f}}} };

        [UnityTest]
        public IEnumerator FloatingModelsHaveObservableExplorationAndZoomWhileHeld()
        {
            var cameraObject=new GameObject("Exploration camera"); cameraObject.tag="MainCamera";
            cameraObject.AddComponent<Camera>(); cameraObject.transform.position=Vector3.up*1.6f;
            var manager=new GameObject("Exploration manager"); manager.AddComponent<XRInteractionManager>();
            var obj=new GameObject("Exploration model"); var exhibit=obj.AddComponent<StudyExhibit>();
            var hand=new GameObject("Exploration hand"); hand.AddComponent<SphereCollider>().isTrigger=true;
            hand.AddComponent<Rigidbody>().isKinematic=true; var interactor=hand.AddComponent<XRDirectInteractor>();
            try
            {
                exhibit.Build(Model()); yield return null;
                Assert.AreEqual(3,exhibit.PartCount);
                var body=exhibit.Grab.GetComponent<Rigidbody>();
                Vector3 initial=body.position;
                yield return new WaitForSeconds(.25f);
                Assert.Less(Vector3.Distance(initial,body.position),.001f); Assert.IsFalse(body.useGravity);
                Transform first=exhibit.Grab.transform.Find("Generated geometry").GetChild(0);
                Vector3 before=first.localPosition;
                exhibit.NextStep(); yield return new WaitForSeconds(.5f);
                Assert.Greater(Vector3.Distance(before,first.localPosition),.2f);
                StringAssert.Contains("move apart",exhibit.LearningText);
                exhibit.ResetExploration(); yield return new WaitForSeconds(.5f);
                Assert.Less(Vector3.Distance(before,first.localPosition),.02f);
                hand.transform.position=exhibit.Grab.transform.position;
                interactor.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)exhibit.Grab);
                yield return null;
                for(int i=0;i<30;i++) { exhibit.Zoom(1); yield return null; }
                Assert.Greater(exhibit.Grab.transform.localScale.x,1.05f,"Joystick zoom was overwritten by the grab transformer.");
                interactor.EndManualInteraction();
                float scale=exhibit.Grab.transform.localScale.x;
                yield return null;
                Assert.AreEqual(scale,exhibit.Grab.transform.localScale.x,.03f);
            }
            finally { Object.Destroy(obj); Object.Destroy(hand); Object.Destroy(manager); Object.Destroy(cameraObject); }
        }

        [UnityTest]
        public IEnumerator ObjectCollectionKeepsEarlierObjectsAndRestoresExploration()
        {
            var camera=new GameObject("Workspace camera"); camera.tag="MainCamera"; camera.AddComponent<Camera>(); camera.transform.position=Vector3.up*1.6f;
            var obj=new GameObject("Workspace"); var workspace=obj.AddComponent<StudyWorkspace>();
            string id="test-"+Guid.NewGuid().ToString("N");
            try
            {
                workspace.Open(id,Model()); var first=workspace.Current; first.NextStep();
                workspace.Add(Model()); Assert.AreEqual(2,workspace.Objects.Count); Assert.AreSame(first,workspace.Objects[0]);
                workspace.Save(); workspace.Close(); yield return null;
                workspace.Open(id,null); yield return null;
                Assert.AreEqual(2,workspace.Objects.Count); Assert.AreEqual(0,workspace.Objects[0].StepIndex);
            }
            finally
            {
                workspace.Close();
                using(var hash=System.Security.Cryptography.SHA256.Create())
                {
                    string key=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id))).Replace("-", "");
                    string path=Path.Combine(Application.persistentDataPath,"exploration",key+".json");
                    foreach(string suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix);
                }
                Object.Destroy(obj); Object.Destroy(camera);
            }
        }

        [UnityTest]
        public IEnumerator RecordingOrbExistsAndGlowsBeforeAnySpeechResponse()
        {
            var camera=new GameObject("Draft camera"); camera.tag="MainCamera"; camera.AddComponent<Camera>();
            var obj=new GameObject("Draft controller"); var controller=obj.AddComponent<ConstellationController>(); controller.enabled=false;
            try
            {
                var orb=controller.BeginDraft();
                Assert.IsTrue(orb.gameObject.activeSelf); Assert.IsFalse(orb.Grab.enabled);
                Assert.AreEqual(0,controller.Balls.Count,"An unfinished recording must not become a saved idea.");
                var renderer=orb.Crystal.transform.Find("Glass world lens").GetComponent<Renderer>();
                Assert.AreEqual(1,renderer.sharedMaterial.GetFloat("_Recording"));
                Assert.IsTrue(orb.Crystal.transform.Find("Recording aura").gameObject.activeSelf);
                controller.CancelDraft(); yield return null;
                Assert.IsTrue(orb==null);
            }
            finally { Object.Destroy(obj); Object.Destroy(camera); }
        }

        [UnityTest]
        public IEnumerator WallContactSlidesInsteadOfStoppingAllMovement()
        {
            var rig=new GameObject("XR Origin (XR Rig)"); var camera=new GameObject("Movement camera");
            camera.tag="MainCamera"; camera.AddComponent<Camera>(); camera.transform.SetParent(rig.transform); camera.transform.localPosition=Vector3.up*1.6f;
            var room=new GameObject("Test environment"); var environment=room.AddComponent<GeneratedEnvironment>();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(room.transform);
            wall.transform.position=new Vector3(.6f,1,0); wall.transform.localScale=new Vector3(.1f,2,5);
            try
            {
                yield return new WaitForFixedUpdate(); Physics.SyncTransforms();
                var navigation=new WorldNavigation(); navigation.Enter(environment);
                var movement=navigation.Slide(Vector3.zero,camera.transform.position,new Vector3(.8f,0,.8f));
                Assert.Less(movement.x,.4f,"Movement crossed the wall."); Assert.Greater(movement.z,.65f,"Diagonal movement should continue along the wall.");
                navigation.Exit();
            }
            finally { Object.Destroy(room); Object.Destroy(rig); }
        }

        [UnityTest]
        public IEnumerator RenderExplorationModels()
        {
            string folder=Environment.GetEnvironmentVariable("ZAP_EXPLORATION_DIR");
            if(string.IsNullOrEmpty(folder)) Assert.Ignore("Optional generated exploration preview.");
            var cameraObject=new GameObject("Exploration render camera"); cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>(); camera.transform.position=Vector3.up*1.6f;
            camera.fieldOfView=65; camera.nearClipPlane=.03f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.035f,.06f);
            var obj=new GameObject("Rendered exploration"); var exhibit=obj.AddComponent<StudyExhibit>();
            var target=new RenderTexture(1400,1200,24); var read=new Texture2D(1400,1200,TextureFormat.RGB24,false);
            try
            {
                target.Create();
                foreach(string name in new[]{"coil","dna"})
                {
                    exhibit.Build(JsonUtility.FromJson<StudyModelData>(File.ReadAllText(Path.Combine(folder,name+".json"))));
                    for(int view=0;view<4;view++)
                    {
                        if(view>0) exhibit.NextStep();
                        yield return new WaitForSeconds(.5f);
                        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                        var previous=RenderTexture.active; RenderTexture.active=target;
                        read.ReadPixels(new Rect(0,0,1400,1200),0,0); read.Apply(); RenderTexture.active=previous;
                        File.WriteAllBytes(Path.Combine(folder,name+"-"+view+".png"),read.EncodeToPNG());
                    }
                }
            }
            finally { Object.Destroy(obj); Object.Destroy(cameraObject); Object.Destroy(read); target.Release(); Object.Destroy(target); }
        }
    }
}
