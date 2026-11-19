using System;
using System.Collections;
using System.IO;
using System.Reflection;
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
        [Test]
        public void WorldLoadingDoesNotConsumeReturnAsVoiceCancellation()
        {
            var obj=new GameObject("Loading cancellation"); var controller=obj.AddComponent<ConstellationController>(); controller.enabled=false;
            try
            {
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(ConstellationController).GetField("_busy",flags).SetValue(controller,true);
                typeof(ConstellationController).GetField("_preparingWorld",flags).SetValue(controller,true);
                Assert.IsFalse(controller.CanCancelVoice);
                Assert.IsFalse(controller.CancelPendingVoice(),"Y must reach LeaveWorld during environment loading.");
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [UnityTest]
        public IEnumerator WorldWithoutArtifactStillHasMovablePaneAndCanAnswerOrAddObjects()
        {
            var camera=new GameObject("Guide camera"); camera.tag="MainCamera"; camera.AddComponent<Camera>(); camera.transform.position=Vector3.up*1.6f;
            var obj=new GameObject("Guide workspace"); var workspace=obj.AddComponent<StudyWorkspace>();
            string id="guide-test-"+Guid.NewGuid().ToString("N");
            try
            {
                var response=JsonUtility.FromJson<PreparedWorld>("{\"plan\":null,\"pane\":{\"title\":\"Explore the architecture\",\"text\":\"Look at the arches and the surrounding landscape.\"}}");
                Assert.IsFalse(response.HasArtifact);
                workspace.Open(id,response.HasArtifact ? response.plan : null,response.pane);
                yield return null;
                var guide=workspace.Current;
                Assert.IsFalse(guide.HasModel); Assert.IsNull(guide.Grab);
                Assert.IsNotNull(guide.Pane.Grab);
                StringAssert.Contains("arches",guide.Pane.BodyText);
                Assert.AreEqual(0,guide.PartCount,"A world without an artifact must not get dummy geometry.");
                Assert.IsTrue(workspace.ApplyReply(new StudyReply {action="answer",answer="The arch carries load around its opening."},guide));
                guide.Pane.Place(new Vector3(.2f,1.5f,1.2f),Quaternion.Euler(0,10,0));
                workspace.Save(); workspace.Close(); yield return null;
                workspace.Open(id,null); yield return null;
                guide=workspace.Current;
                Assert.IsFalse(guide.HasModel); Assert.IsTrue(guide.Pane.IsPlaced);
                Assert.AreEqual("The arch carries load around its opening.",guide.Pane.BodyText);
                for(int i=0;i<6;i++) Assert.IsTrue(workspace.ApplyReply(new StudyReply {action="add",answer="An object to inspect.",model=Model()},guide));
                Assert.AreEqual(7,workspace.Objects.Count); Assert.IsTrue(workspace.Full);
                Assert.IsFalse(workspace.ApplyReply(new StudyReply {action="add",answer="Too many.",model=Model()},guide));
                Assert.IsFalse(guide.HasModel,"Adding an object must preserve the world guide.");
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
                first.Pane.Place(new Vector3(.4f,1.7f,1.1f),Quaternion.Euler(5,20,0));
                first.Explain("A saved answer remains with this object.");
                workspace.Add(Model()); Assert.AreEqual(2,workspace.Objects.Count); Assert.AreSame(first,workspace.Objects[0]);
                workspace.Save(); workspace.Close(); yield return null;
                workspace.Open(id,null); yield return null;
                Assert.AreEqual(2,workspace.Objects.Count); Assert.AreEqual(0,workspace.Objects[0].StepIndex);
                Assert.IsTrue(workspace.Objects[0].Pane.IsPlaced);
                Assert.Less(Vector3.Distance(new Vector3(.4f,1.7f,1.1f),workspace.Objects[0].Pane.transform.position),.001f);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(5,20,0),workspace.Objects[0].Pane.transform.rotation),.1f);
                Assert.AreEqual("A saved answer remains with this object.",workspace.Objects[0].Answer);
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
        public IEnumerator AnswersStayOnTheRequestedObjectAndHoverDoesNotHideThem()
        {
            var camera=new GameObject("Answer camera"); camera.tag="MainCamera"; camera.AddComponent<Camera>();
            var obj=new GameObject("Answer workspace"); var workspace=obj.AddComponent<StudyWorkspace>();
            try
            {
                var first=workspace.Add(Model()); var second=workspace.Add(Model());
                workspace.SetCurrent(first);
                first.RequestFeedback("LISTENING", "Ask your question."); yield return null;
                Assert.AreEqual("LISTENING",first.Pane.StatusText);
                first.RequestFeedback("TRANSCRIBING", "Turning voice into text."); yield return null;
                Assert.AreEqual("TRANSCRIBING",first.Pane.StatusText);
                workspace.SetCurrent(second,false); // Pointer moved while the request was pending.
                Assert.IsTrue(workspace.ApplyReply(new StudyReply {action="answer",answer="This answer belongs to the first object."},first));
                yield return null;
                workspace.SetCurrent(second,false); yield return null;
                Assert.IsTrue(first.Active); Assert.IsFalse(second.Active);
                Assert.AreEqual("This answer belongs to the first object.",first.Pane.BodyText);
                first.RequestFeedback("REQUEST NOT COMPLETED", "No speech heard. Hold left trigger to try again.",false,true);
                yield return null;
                StringAssert.Contains("No speech heard",first.Pane.BodyText);
                first.ClearRequest(); yield return null;
                Assert.AreEqual(first.Answer,first.Pane.BodyText,"Cancelling must restore the previous answer.");
                first.Pane.Place(new Vector3(.5f,1,1.3f),Quaternion.Euler(0,15,0));
                Assert.IsTrue(workspace.ApplyReply(new StudyReply {action="revise",answer="Revised explanation.",model=Model()},first));
                yield return null;
                Assert.IsTrue(first.Pane.IsPlaced);
                Assert.Less(Vector3.Distance(new Vector3(.5f,1,1.3f),first.Pane.transform.position),.001f);
                Assert.AreEqual("Revised explanation.",first.Pane.BodyText);
                string longAnswer="";
                while(longAnswer.Length<800) longAnswer+="The model helps you explore how its parts fit together. ";
                first.Explain(longAnswer.Substring(0,800)); yield return null; yield return null;
                Assert.IsFalse(first.Pane.IsBodyTruncated,"A full-size AI answer was silently truncated by the reading pane.");
                workspace.SetCurrent(second); yield return null;
                Assert.IsFalse(first.Active); Assert.IsTrue(second.Active);
            }
            finally { Object.Destroy(obj); Object.Destroy(camera); }
        }

        [UnityTest]
        public IEnumerator PaneCanBeGrabbedAndPlacedIndependentlyOfItsObject()
        {
            var camera=new GameObject("Pane camera"); camera.tag="MainCamera"; camera.AddComponent<Camera>(); camera.transform.position=Vector3.up*1.6f;
            var manager=new GameObject("Pane interaction manager"); manager.AddComponent<XRInteractionManager>();
            var obj=new GameObject("Pane test object"); var exhibit=obj.AddComponent<StudyExhibit>();
            var hand=new GameObject("Pane hand"); hand.AddComponent<SphereCollider>().isTrigger=true;
            hand.AddComponent<Rigidbody>().isKinematic=true; var interactor=hand.AddComponent<XRDirectInteractor>();
            try
            {
                exhibit.Build(Model()); yield return null;
                var pane=exhibit.Pane; pane.Grab.attachEaseInTime=0;
                var original=pane.transform.position; var modelPosition=exhibit.Grab.transform.position;
                hand.transform.position=original;
                interactor.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)pane.Grab);
                yield return null;
                Assert.IsTrue(pane.IsHeld);
                for(int i=0;i<12;i++) { hand.transform.position+=Vector3.left*.015f; hand.transform.Rotate(0,1,0); yield return null; }
                interactor.EndManualInteraction(); yield return null;
                var placed=pane.transform.position; var rotation=pane.transform.rotation;
                Assert.Greater(Vector3.Distance(original,placed),.12f); Assert.IsTrue(pane.IsPlaced);
                Assert.Less(Vector3.Distance(modelPosition,exhibit.Grab.transform.position),.001f);
                camera.transform.Rotate(0,30,0); exhibit.Grab.transform.position+=Vector3.right*.25f;
                yield return new WaitForSeconds(.2f);
                Assert.Less(Vector3.Distance(placed,pane.transform.position),.001f);
                Assert.Less(Quaternion.Angle(rotation,pane.transform.rotation),.1f);
            }
            finally { Object.Destroy(obj); Object.Destroy(hand); Object.Destroy(manager); Object.Destroy(camera); }
        }

        [UnityTest]
        public IEnumerator CancelledIdeaIgnoresLateResponseAndDoesNotInterruptNextRequest()
        {
            var camera=new GameObject("Cancellation camera"); camera.tag="MainCamera"; camera.AddComponent<Camera>();
            var obj=new GameObject("Cancellation controller"); var controller=obj.AddComponent<ConstellationController>(); controller.enabled=false;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            try
            {
                controller.BeginDraft();
                var apply=typeof(ConstellationController).GetMethod("ApplyText",flags);
                var old=(IEnumerator)apply.Invoke(controller,new object[]{"an idea","create",null,-1,null,null,null});
                Assert.IsTrue(old.MoveNext());
                // Stop at the transport boundary. Deliver a completed response after cancellation
                // without contacting any provider or relying on a particular network delay.
                var pending=(IEnumerator)old.Current;
                var deliver=(Action<string>)pending.GetType().GetField("success",flags).GetValue(pending);
                Assert.IsTrue(controller.Busy); Assert.IsTrue(controller.CancelPendingVoice());
                Assert.IsFalse(controller.Busy); Assert.AreEqual(0,controller.Balls.Count);
                var nextDraft=controller.BeginDraft();
                var next=(IEnumerator)apply.Invoke(controller,new object[]{"a different idea","create",null,-1,null,null,null});
                Assert.IsTrue(next.MoveNext()); Assert.IsTrue(controller.Busy);
                deliver("{\"topic\":\"Late result\",\"summary\":\"Must be ignored\",\"worldPrompt\":\"No world\",\"suggestions\":[]}");
                Assert.IsFalse(old.MoveNext());
                Assert.AreEqual(0,controller.Balls.Count,"A cancelled response created a saved node.");
                Assert.IsTrue(controller.Busy,"The cancelled request reset a newer operation.");
                Assert.IsTrue(nextDraft.gameObject.activeSelf);
                controller.CancelPendingVoice(); yield return null;
                Assert.IsTrue(nextDraft==null); Assert.AreEqual(0,controller.Balls.Count);
                (next as IDisposable)?.Dispose(); (pending as IDisposable)?.Dispose();
            }
            finally { Object.Destroy(obj); Object.Destroy(camera); }
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
            camera.fieldOfView=80; camera.nearClipPlane=.03f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.035f,.06f);
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
                exhibit.Clear(); yield return null;
                var draftObject=new GameObject("Recording orb preview");
                var controller=draftObject.AddComponent<ConstellationController>(); controller.enabled=false;
                controller.BeginDraft(); yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                var oldTarget=RenderTexture.active; RenderTexture.active=target;
                read.ReadPixels(new Rect(0,0,1400,1200),0,0); read.Apply(); RenderTexture.active=oldTarget;
                File.WriteAllBytes(Path.Combine(folder,"recording-orb.png"),read.EncodeToPNG());
                controller.CancelDraft(); Object.Destroy(draftObject);
            }
            finally { Object.Destroy(obj); Object.Destroy(cameraObject); Object.Destroy(read); target.Release(); Object.Destroy(target); }
        }
    }
}
