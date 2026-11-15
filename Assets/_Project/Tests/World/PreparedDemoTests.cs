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
    public class PreparedDemoTests
    {
        [UnityTest]
        public IEnumerator RenderPreparedWorldWithOptionalArtifactAndVerifyWalking()
        {
            string folder = Environment.GetEnvironmentVariable("ZAP_DEMO_PREVIEW_DIR");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("Requires an explicitly prepared demo export.");
            var prepared = JsonUtility.FromJson<PreparedWorld>(File.ReadAllText(Path.Combine(folder,"prepared.json")));
            var rig = new GameObject("XR Origin (XR Rig)");
            var cameraObject = new GameObject("Prepared demo camera"); cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(rig.transform); cameraObject.transform.localPosition = Vector3.up*1.6f;
            var camera = cameraObject.AddComponent<Camera>(); camera.fieldOfView = 80; camera.nearClipPlane = .035f; camera.farClipPlane = 250;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var environmentObject = new GameObject("Prepared demo environment"); var environment = environmentObject.AddComponent<GeneratedEnvironment>();
            var guideObject = new GameObject("Prepared demo guide"); var guide = guideObject.AddComponent<StudyExhibit>();
            var target = new RenderTexture(1600,1000,24); var image = new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try
            {
                string failure = null;
                yield return environment.Load(prepared.environment,new Uri(folder.TrimEnd('/')+"/").AbsoluteUri,_=>{},error=>failure=error,prepared.HasArtifact);
                Assert.IsNull(failure); Assert.IsTrue(environment.IsVisible); Assert.Greater(environment.TriangleCount,1000);
                Assert.IsTrue(environment.SurfaceBelow(camera.transform.position,3,out var ground));
                Assert.Less(Mathf.Abs(ground.point.y-rig.transform.position.y),.08f,"The learner did not start on the terrace floor.");
                guide.Build(prepared.HasArtifact ? prepared.plan : new StudyModelData {title=prepared.pane.title,explanation=prepared.pane.text,parts=Array.Empty<StudyPartData>()});
                yield return null;
                Assert.IsNotNull(guide.Pane);
                if (!prepared.HasArtifact) { Assert.IsFalse(guide.HasModel); Assert.AreEqual(0,guide.PartCount); }
                target.Create();
                for(int view=0;view<6;view++)
                {
                    camera.transform.rotation=Quaternion.Euler(0,view<4 ? view*90 : 0,0);
                    if(view==4) camera.transform.position+=Vector3.right*.5f;
                    if(view==5) camera.transform.position+=Vector3.right;
                    yield return null;
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                    var previous=RenderTexture.active; RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); RenderTexture.active=previous;
                    File.WriteAllBytes(Path.Combine(folder,"view-"+view+".png"),image.EncodeToPNG());
                }
                camera.transform.SetLocalPositionAndRotation(Vector3.up*1.6f,Quaternion.identity);
                var navigation=new WorldNavigation(); navigation.Enter(environment);
                Vector3 start=rig.transform.position; float distance=0;
                foreach(var direction in new[]{Vector2.up,Vector2.down,Vector2.right,Vector2.left})
                {
                    rig.transform.position=start;
                    for(int frame=0;frame<35;frame++)
                    {
                        yield return new WaitForFixedUpdate();
                        navigation.Tick(direction,Vector2.zero,true,(_,__)=>Assert.Fail("Walking in a mesh world must not call a generation model."));
                    }
                    distance=Mathf.Max(distance,Vector3.Distance(start,rig.transform.position));
                    if(distance>.1f) break;
                }
                Assert.Greater(distance,.1f,"No direction allowed walking from the starting terrace.");
                navigation.Exit();
                File.WriteAllText(Path.Combine(folder,"verification.txt"),"Triangles: "+environment.TriangleCount+"\nWalked: "+distance+"m\nArtifact: "+guide.HasModel);
            }
            finally
            {
                Object.Destroy(guideObject); Object.Destroy(environmentObject); Object.Destroy(rig);
                Object.Destroy(image); target.Release(); Object.Destroy(target);
            }
        }
    }
}
