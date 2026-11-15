using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Zap.Constellation;
using Object = UnityEngine.Object;

namespace Zap.World.Tests
{
    public class ConstellationTests
    {
        [Test]
        public void WorldNeighboursKeepParentChildTravelSeparateFromOtherDomains()
        {
            var wall = new NodeData { id="wall", topic="Exterior" };
            var tower = new NodeData { id="tower", topic="Interior", parentIds=new[]{"wall"} };
            var lab = new NodeData { id="lab", topic="Biology" };
            var nodes = new[]{wall,tower,lab};
            CollectionAssert.AreEqual(new[]{tower},NodeContext.Neighbours(wall,nodes));
            CollectionAssert.AreEqual(new[]{wall},NodeContext.Neighbours(tower,nodes));
            Assert.IsEmpty(NodeContext.Neighbours(lab,nodes));
            Assert.IsEmpty(NodeContext.Parents(lab,nodes));
            CollectionAssert.AreEqual(new[]{wall},NodeContext.Parents(tower,nodes));
        }

        [UnityTest]
        public IEnumerator ConnectedWorldOrbsAreReachableBothWaysAndRestoreMapPositions()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var camera=new GameObject("Linked travel camera"); camera.tag="MainCamera";
            camera.AddComponent<Camera>(); camera.transform.position=Vector3.up*1.6f;
            var root=new GameObject("Linked travel controller");
            var controller=root.AddComponent<ConstellationController>(); controller.enabled=false;
            var content=new GameObject("Map nodes").transform; content.SetParent(root.transform);
            var branches=new GameObject("Linked world nodes").transform; branches.SetParent(root.transform);
            var material=new Material(Shader.Find("Zap/Solid"));
            var manager=new GameObject("Linked travel interaction manager"); manager.AddComponent<XRInteractionManager>();
            var hand=new GameObject("Linked travel hand"); hand.transform.position=Vector3.one*100;
            hand.AddComponent<SphereCollider>().isTrigger=true; hand.AddComponent<Rigidbody>().isKinematic=true;
            var interactor=hand.AddComponent<XRDirectInteractor>();
            void Set(string name,object value) => typeof(ConstellationController).GetField(name,flags).SetValue(controller,value);
            void Call(string name) => typeof(ConstellationController).GetMethod(name,flags).Invoke(controller,null);
            TopicBall Target() => (TopicBall)typeof(ConstellationController).GetProperty("LinkedTravelTarget",flags).GetValue(controller);
            TopicBall Ball(NodeData data)
            {
                var obj=new GameObject(data.topic); obj.transform.SetParent(content); obj.transform.position=data.position;
                var ball=obj.AddComponent<TopicBall>(); ball.Initialize(data,material);
                ((System.Collections.Generic.List<TopicBall>)controller.Balls).Add(ball);
                return ball;
            }
            Set("_content",content); Set("_branches",branches);
            Set("_space",root.AddComponent<StudyWorkspace>());
            Set("_environment",root.AddComponent<GeneratedEnvironment>());
            Set("_journey",root.AddComponent<WorldJourney>());
            var parent=Ball(new NodeData {id="parent",topic="Parent idea",position=new Vector3(-1,1.3f,3)});
            var child=Ball(new NodeData {id="child",topic="Child idea",parentIds=new[]{"parent"},position=new Vector3(-1,1.3f,4)});
            var other=Ball(new NodeData {id="other",topic="Independent idea",position=new Vector3(1,1.3f,3)});
            Vector3 parentMap=parent.transform.position,childMap=child.transform.position;
            try
            {
                Set("_worldNode",parent); Set("_worldVisible",true); content.gameObject.SetActive(false); Call("ShowLinkedWorlds");
                yield return null;
                Assert.AreEqual(branches,child.transform.parent); Assert.IsTrue(child.gameObject.activeInHierarchy);
                Assert.IsTrue(child.HasWorldTravelHint); Assert.IsFalse(other.gameObject.activeInHierarchy);
                controller.Select(child);
                Assert.IsNull(Target(),"A remembered hover must not make X unexpectedly enter another world.");
                hand.transform.position=child.transform.position;
                interactor.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)child.Grab);
                yield return null;
                Assert.AreSame(child,Target(),"Holding the linked orb must provide an X travel target.");
                interactor.EndManualInteraction(); hand.transform.position=Vector3.one*100;
                Call("ShowConstellation");
                Assert.AreEqual(content,child.transform.parent); Assert.AreEqual(childMap,child.transform.position);
                Assert.IsFalse(child.HasWorldTravelHint);

                Set("_worldNode",child); Set("_worldVisible",true); content.gameObject.SetActive(false); Call("ShowLinkedWorlds");
                Assert.AreEqual(branches,parent.transform.parent); Assert.IsTrue(parent.gameObject.activeInHierarchy);
                Assert.IsTrue(parent.HasWorldTravelHint); Assert.IsFalse(other.gameObject.activeInHierarchy);
                Call("ShowConstellation");
                Assert.AreEqual(parentMap,parent.transform.position); Assert.IsFalse(parent.HasWorldTravelHint);

                Set("_worldNode",other); Set("_worldVisible",true); content.gameObject.SetActive(false); Call("ShowLinkedWorlds");
                Assert.AreEqual(0,branches.childCount,"Independent roots must not gain unrelated travel orbs.");
                Call("ShowConstellation");
            }
            finally { Object.Destroy(root); Object.Destroy(hand); Object.Destroy(manager); Object.Destroy(camera); Object.Destroy(material); }
        }

        [Test]
        public void ParentContextIncludesMultipleBranchesAndSurvivesSave()
        {
            var ocean = new NodeData { topic = "Ocean ecosystems", summary = "Life in the deep ocean" };
            var volcano = new NodeData { topic = "Underwater volcanoes", summary = "Seafloor volcanism", parentIds = new[] { ocean.id } };
            var chemistry = new NodeData { topic = "Chemistry", summary = "Chemical energy supports life" };
            var child = new NodeData { topic = "Eruptions", parentIds = new[] { volcano.id, chemistry.id } };
            var nodes = new[] { ocean, volcano, chemistry, child };
            var context = NodeContext.WorldPrompt(child, nodes);
            StringAssert.Contains("Ocean ecosystems", context);
            StringAssert.Contains("Underwater volcanoes", context);
            StringAssert.Contains("Chemistry", context);
            StringAssert.Contains("Eruptions", context);
            Assert.IsTrue(NodeContext.WouldCycle(child, ocean, nodes));
            Assert.IsFalse(NodeContext.WouldCycle(chemistry, volcano, nodes));
            var path = Path.Combine(Application.temporaryCachePath, Guid.NewGuid() + ".json");
            try
            {
                NodeStore.Save(path, new ConstellationData { nodes = nodes });
                var loaded = NodeStore.Load(path);
                Assert.AreEqual(context, NodeContext.WorldPrompt(loaded.nodes[3], loaded.nodes));
                loaded.nodes[1].summary = "New parent explanation";
                StringAssert.Contains("New parent explanation", NodeContext.WorldPrompt(loaded.nodes[3], loaded.nodes));
                NodeStore.Save(path, loaded);
                File.WriteAllText(path, "invalid");
                Assert.AreEqual(4, NodeStore.Load(path).nodes.Length, "Recover the previous complete save.");
            }
            finally { foreach (var suffix in new[] { "", ".tmp", ".bak" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
        }

        [Test]
        public void VoiceWavIsMonoPcmAndDownmixesChannels()
        {
            var bytes = VoiceRecorder.EncodeWav(new[] { .5f, -.5f, 1f, 1f }, 16000, 2);
            Assert.AreEqual(48, bytes.Length);
            Assert.AreEqual(1, BitConverter.ToInt16(bytes, 22));
            Assert.AreEqual(16000, BitConverter.ToInt32(bytes, 24));
            Assert.AreEqual(0, BitConverter.ToInt16(bytes, 44));
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 46));
        }

        [UnityTest]
        public IEnumerator GrabReleaseTransfersThrowVelocityAndBallFloats()
        {
            var managerObject = new GameObject("Test interaction manager");
            managerObject.AddComponent<XRInteractionManager>();
            var hand = new GameObject("Test hand");
            hand.transform.position = Vector3.up * 1.5f;
            var collider = hand.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = .1f;
            var body = hand.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var interactor = hand.AddComponent<XRDirectInteractor>();
            var obj = new GameObject("Test topic"); obj.transform.position = hand.transform.position;
            var ball = obj.AddComponent<TopicBall>();
            var material = new Material(Shader.Find("Zap/Solid"));
            ball.Initialize(new NodeData { topic = "Volcanoes" }, material);
            ball.Grab.attachEaseInTime = 0;
            try
            {
                yield return null;
                interactor.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)ball.Grab);
                Assert.IsTrue(ball.Grab.isSelected);
                float start = Time.time;
                while (Time.time - start < .4f)
                {
                    hand.transform.position += Vector3.forward * (1.6f * Time.deltaTime);
                    yield return null;
                }
                Assert.IsTrue(ball.Grab.isSelected, "Selection must persist throughout the throw gesture.");
                interactor.EndManualInteraction();
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.IsFalse(ball.Grab.isSelected);
                Assert.IsFalse(ball.Body.isKinematic);
                Assert.IsFalse(ball.Body.useGravity);
                Assert.Greater(ball.Body.linearVelocity.z, .2f, "Release should carry controller motion into the throw.");
                float speed = ball.Body.linearVelocity.magnitude;
                Vector3 released = ball.transform.position;
                yield return new WaitForSeconds(1.5f);
                Assert.Greater(ball.transform.position.z, released.z + .1f);
                Assert.Less(ball.Body.linearVelocity.magnitude, speed);
                Assert.That(ball.transform.position.y, Is.EqualTo(released.y).Within(.05f), "The thrown ball must not fall.");
            }
            finally { Object.Destroy(obj); Object.Destroy(hand); Object.Destroy(managerObject); Object.Destroy(material); }
        }
    }
}
