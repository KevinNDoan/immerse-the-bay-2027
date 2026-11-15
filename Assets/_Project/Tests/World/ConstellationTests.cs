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
using Zap.Core;
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

        [Test]
        public void ActiveGeometryHeartbeatKeepsLongJourneyAliveButCannotReviveCancelledOrStalledTravel()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var root=new GameObject("Long generation heartbeat test");
            var controller=root.AddComponent<ConstellationController>(); controller.enabled=false;
            var journey=root.AddComponent<WorldJourney>();
            void Set(string name,object value) => typeof(ConstellationController).GetField(name,flags).SetValue(controller,value);
            bool Expired() => (bool)typeof(ConstellationController).GetProperty("LoadingTimedOut",flags).GetValue(controller);
            bool Heartbeat(PreparedWorld value,int version=7) => (bool)typeof(ConstellationController)
                .GetMethod("AcceptPreparationHeartbeat",flags).Invoke(controller,new object[]{value,version});
            var pending=new PreparedWorld {projection="geometry",state="preparing",cacheKey=new string('a',64)};
            try
            {
                Set("_journey",journey); Set("_worldRequest",7); Set("_preparingWorld",true);
                journey.Begin(Vector3.zero,Vector3.forward);
                Set("_loadingStarted",Time.unscaledTime-2400);
                Assert.IsTrue(Expired(),"A silent forty-minute request should time out.");
                Assert.IsTrue(Heartbeat(pending));
                Assert.IsFalse(Expired(),"An active export must keep hyperspace alive beyond four minutes.");
                Assert.IsTrue(journey.IsActive,"A preparation heartbeat must not reveal the unfinished world.");

                Set("_loadingStarted",Time.unscaledTime-241);
                Assert.IsTrue(Expired(),"If heartbeats stop, the original timeout must still apply.");
                foreach (var invalid in new[] {null,
                    new PreparedWorld {projection="geometry",state="error",cacheKey=pending.cacheKey},
                    new PreparedWorld {projection="geometry",state="ready",cacheKey=pending.cacheKey},
                    new PreparedWorld {projection="panorama",state="preparing",cacheKey=pending.cacheKey},
                    new PreparedWorld {projection="geometry",state="preparing",cacheKey="invalid"}})
                {
                    Assert.IsFalse(Heartbeat(invalid));
                    Assert.IsTrue(Expired(),"Invalid or completed jobs cannot extend a pending journey.");
                }
                Assert.IsFalse(Heartbeat(pending,6)); Assert.IsTrue(Expired(),"A previous request cannot extend the current one.");
                Set("_preparingWorld",false);
                Assert.IsFalse(Heartbeat(pending)); Assert.IsTrue(Expired(),"A stalled live stream must retain its timeout.");
                journey.Cancel();
                Assert.IsFalse(Heartbeat(pending)); Assert.IsFalse(Expired());
                Assert.IsFalse(journey.IsActive,"A late response must not restart a cancelled journey.");
            }
            finally { Object.Destroy(root); }
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
        public void HomeFrameAlignPutsHeadOverOriginFacingForward()
        {
            var rig = new GameObject("Test rig");
            rig.transform.position = new Vector3(1f, 0f, 2f);
            rig.transform.eulerAngles = new Vector3(0f, 30f, 0f);
            var head = new GameObject("Test head");
            head.transform.SetParent(rig.transform, false);
            head.transform.localPosition = new Vector3(.3f, 1.6f, -.2f);
            head.transform.localEulerAngles = new Vector3(10f, 70f, 5f);
            try
            {
                HomeFrame.Align(rig.transform, head.transform);

                Assert.That(head.transform.position.x, Is.EqualTo(0f).Within(1e-3f));
                Assert.That(head.transform.position.z, Is.EqualTo(0f).Within(1e-3f));
                Assert.That(head.transform.position.y, Is.EqualTo(1.6f).Within(1e-3f));
                var forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
                Assert.That(Vector3.Distance(forward, Vector3.forward), Is.LessThan(1e-3f));
            }
            finally
            {
                Object.DestroyImmediate(head);
                Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void LineCutterDetectsSweepAcrossLine()
        {
            var origin = Vector3.zero;
            var prevDir = new Vector3(-0.2f, 0f, 1f);
            var currDir = new Vector3(0.2f, 0f, 1f);
            float range = 4f;
            var lineStart = new Vector3(0f, -0.5f, 1f);
            var lineEnd = new Vector3(0f, 0.5f, 1f);

            Assert.IsTrue(LineCutter.SweepCrosses(origin, prevDir, origin, currDir, range, lineStart, lineEnd, out float distance));
            Assert.That(distance, Is.EqualTo(1f).Within(0.01f));

            Assert.IsFalse(LineCutter.SweepCrosses(origin, prevDir, origin, currDir, range, new Vector3(0f, -0.5f, 10f), new Vector3(0f, 0.5f, 10f), out distance));
            Assert.AreEqual(0f, distance);

            Assert.IsFalse(LineCutter.SweepCrosses(origin, prevDir, origin, currDir, range, new Vector3(2f, -0.5f, 1f), new Vector3(2f, 0.5f, 1f), out distance));
            Assert.AreEqual(0f, distance);

            Assert.IsFalse(LineCutter.SweepCrosses(origin, prevDir, origin, currDir, range, new Vector3(0f, 0.2f, 1f), new Vector3(0f, 0.5f, 1f), out distance));
            Assert.AreEqual(0f, distance);

            var movePrev = new Vector3(-0.2f, 0f, 0f);
            var moveCurr = new Vector3(0.2f, 0f, 0f);
            var moveDir = new Vector3(0f, 0f, 1f);
            Assert.IsTrue(LineCutter.SweepCrosses(movePrev, moveDir, moveCurr, moveDir, range, lineStart, lineEnd, out distance));
            Assert.That(distance, Is.EqualTo(1.02f).Within(0.01f));

            Assert.IsFalse(LineCutter.SweepCrosses(origin, prevDir, origin, prevDir, range, lineStart, lineEnd, out distance));
            Assert.AreEqual(0f, distance);

            Assert.IsFalse(LineCutter.SweepCrosses(origin, prevDir, origin, currDir, 0f, lineStart, lineEnd, out distance));
            Assert.AreEqual(0f, distance);
        }

        [Test]
        public void SuggestionPickerFiltersAndRemovesTopics()
        {
            var node = new NodeData
            {
                topic = "Astronomy",
                suggestions = new[] { "  Nebulae  ", "astronomy", "NEBULAE", " Moon ", "Exoplanets", "Galaxies", "Black holes" }
            };
            var existing = new[] { node, new NodeData { topic = "moon" }, null };

            CollectionAssert.AreEqual(new[] { "Nebulae", "Exoplanets", "Galaxies" }, SuggestionPicker.Pick(node, existing));
            CollectionAssert.IsEmpty(SuggestionPicker.Pick(null, existing));
            CollectionAssert.IsEmpty(SuggestionPicker.Pick(new NodeData { suggestions = null }, existing));
            CollectionAssert.AreEqual(new[] { "Venus" }, SuggestionPicker.Without(new[] { "Mars", "Venus", " MARS " }, "mars"));
            CollectionAssert.IsEmpty(SuggestionPicker.Without(null, "Mars"));
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
        public void ConstellationLibraryCreatesListsAndUsesFallbackName()
        {
            string root = Path.Combine(Application.temporaryCachePath, "zap-constellation-library-" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "constellations");
            ConstellationLibrary.FolderOverride = folder;
            try
            {
                var first = ConstellationLibrary.Create("First constellation");
                var entries = ConstellationLibrary.List();
                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual("First constellation", entries[0].name);
                Assert.AreEqual(0, entries[0].topicCount);

                var firstData = NodeStore.Load(first.path);
                firstData.name = "";
                firstData.updated = "2000-01-01T00:00:00.0000000Z";
                NodeStore.Save(first.path, firstData);
                var second = ConstellationLibrary.Create("Second constellation");

                entries = ConstellationLibrary.List();
                Assert.AreEqual(2, entries.Count);
                Assert.AreEqual(second.id, entries[0].id);
                Assert.AreEqual(first.id, entries[1].id);
                Assert.AreEqual("Untitled", entries[1].name);
            }
            finally
            {
                ConstellationLibrary.FolderOverride = null;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
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

        [Test]
        public void OnboardingRulesAdvanceOnTheRightActions()
        {
            var starts = new[]
            {
                new TutorialFacts(),
                new TutorialFacts(),
                new TutorialFacts(),
                new TutorialFacts()
            };

            for (int step = 0; step < Onboarding.StepCount; step++)
                Assert.IsFalse(Onboarding.Done(step, starts[step], starts[step], false), "Step " + step + " advanced without its action.");

            Assert.IsTrue(Onboarding.Done(0, starts[0], new TutorialFacts { Created = 1 }, false));

            var holding = new TutorialFacts { Holding = true };
            Assert.IsTrue(Onboarding.Middle(1, starts[1], holding));
            Assert.IsFalse(Onboarding.Done(1, starts[1], holding, true));
            Assert.IsFalse(Onboarding.Done(1, starts[1], starts[1], false));
            Assert.IsTrue(Onboarding.Done(1, starts[1], starts[1], true));

            Assert.IsTrue(Onboarding.Done(2, starts[2], new TutorialFacts { Created = 1 }, false));
            Assert.IsTrue(Onboarding.Done(2, starts[2], new TutorialFacts { LinkChanges = 1 }, false), "Linking two topics should also finish step 3.");
            // The bug this guards: a topic thrown away after the step began lowers the count, and one new topic must still finish the step.
            var afterThrow = new TutorialFacts { Topics = 3, Created = 5 };
            Assert.IsTrue(Onboarding.Done(2, afterThrow, new TutorialFacts { Topics = 3, Created = 6 }, false));
            Assert.IsFalse(Onboarding.Done(2, afterThrow, new TutorialFacts { Topics = 2, Created = 5 }, false));
            Assert.IsTrue(Onboarding.Done(3, starts[3], new TutorialFacts { InWorld = true }, false));
        }

        [Test]
        public void TopicBallTossThreshold()
        {
            Assert.IsFalse(TopicBall.IsToss(new Vector3(0f, 0f, TopicBall.TossSpeed - .1f)));
            Assert.IsTrue(TopicBall.IsToss(new Vector3(0f, 0f, TopicBall.TossSpeed)));
        }

        [Test]
        public void LinkAimPicksTheNearestWithinTheCone()
        {
            var positions = new[]
            {
                Vector3.forward,
                Quaternion.AngleAxis(5f, Vector3.up) * Vector3.forward * 2f,
                Quaternion.AngleAxis(30f, Vector3.up) * Vector3.forward
            };

            Assert.AreEqual(0, LinkAim.Nearest(positions, Vector3.zero, Vector3.forward, 14f, 6f, -1));
            Assert.AreEqual(1, LinkAim.Nearest(positions, Vector3.zero, Vector3.forward, 14f, 6f, 0));
            Assert.AreEqual(-1, LinkAim.Nearest(positions, Vector3.zero, Vector3.forward, 2f, 6f, 0));
            Assert.AreEqual(-1, LinkAim.Nearest(new[] { Vector3.forward * 7f }, Vector3.zero, Vector3.forward, 14f, 6f, -1));
        }

        [UnityTest]
        public IEnumerator ControlsCardBuildsAndSwitchesLayout()
        {
            var parent = new GameObject("Test controls card parent");
            try
            {
                var card = ControlsCard.Create(parent.transform);
                yield return null;

                card.Show("Volcanoes", "Hold A and speak.", "Listening…", "Click the left stick to skip", true, .5f, false);
                Assert.IsFalse(card.IsCompact);

                card.Show("Volcanoes", null, "Opening…", "Look at a controller", false, 0f, true);
                Assert.IsTrue(card.IsCompact);

                card.Visible = false;
                Assert.IsFalse(card.gameObject.activeSelf);
                card.Visible = true;
                Assert.IsTrue(card.gameObject.activeSelf);
            }
            finally
            {
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator ReleasedTopicStaysPutAndTossedTopicIsRemoved()
        {
            var placedObject = new GameObject("Placed topic");
            placedObject.transform.position = Vector3.up * 1.5f;
            var placed = placedObject.AddComponent<TopicBall>();
            var tossedObject = new GameObject("Tossed topic");
            tossedObject.transform.position = Vector3.up * 1.5f;
            var tossed = tossedObject.AddComponent<TopicBall>();
            var material = new Material(Shader.Find("Zap/Solid"));
            try
            {
                placed.Initialize(new NodeData { topic = "Volcanoes" }, material);
                placed.Release(new Vector3(0f, 0f, .5f));
                Assert.IsFalse(placed.IsTossed);
                Assert.AreEqual(RigidbodyConstraints.FreezeAll, placed.Body.constraints);
                Assert.AreEqual(Vector3.zero, placed.Body.linearVelocity);
                Vector3 released = placed.transform.position;
                yield return new WaitForSeconds(.3f);
                Assert.That(Vector3.Distance(placed.transform.position, released), Is.LessThan(1e-4f));

                bool tossRaised = false;
                tossed.Initialize(new NodeData { topic = "Comets" }, material);
                tossed.Tossed += _ => tossRaised = true;
                tossed.Release(new Vector3(0f, 0f, 4f));
                Assert.IsTrue(tossRaised, "The Tossed event should fire before Release returns.");
                Assert.IsTrue(tossed.IsTossed);
                float deadline = Time.realtimeSinceStartup + 1.5f;
                while (tossed != null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(tossed == null, "The tossed topic should destroy itself after shrinking.");
            }
            finally
            {
                if (placedObject != null) Object.Destroy(placedObject);
                if (tossedObject != null) Object.Destroy(tossedObject);
                if (material != null) Object.Destroy(material);
            }
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
            bool discarded = false; ball.Tossed += _ => discarded = true;
            try
            {
                yield return null;
                interactor.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)ball.Grab);
                Assert.IsTrue(ball.Grab.isSelected);
                float start = Time.time;
                while (Time.time - start < .4f)
                {
                    hand.transform.position += Vector3.forward * (2.8f * Time.deltaTime);
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
                Assert.IsTrue(ball != null, "A physical travel throw must keep its idea node.");
                Assert.IsFalse(discarded, "The physical release must never invoke explicit discard.");
                Assert.IsFalse(ball.IsTossed);
                Assert.Greater(ball.transform.position.z, released.z + .1f);
                Assert.Less(ball.Body.linearVelocity.magnitude, speed);
                Assert.That(ball.transform.position.y, Is.EqualTo(released.y).Within(.05f), "The thrown ball must not fall.");
            }
            finally { Object.Destroy(obj); Object.Destroy(hand); Object.Destroy(managerObject); Object.Destroy(material); }
        }

        [UnityTest]
        public IEnumerator ConstellationControllerStartsWithoutInitializerErrors()
        {
            string root = Path.Combine(Application.temporaryCachePath, "zap-controller-start-" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "constellations");
            string previousLastOpenedId = ConstellationLibrary.LastOpenedId;
            ConstellationLibrary.FolderOverride = folder;
            var obj = new GameObject("Test constellation controller");
            try
            {
                obj.AddComponent<ConstellationController>();
                yield return null;
                yield return null;
                Object.Destroy(obj);
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (obj != null) Object.Destroy(obj);
                ConstellationLibrary.FolderOverride = null;
                ConstellationLibrary.LastOpenedId = previousLastOpenedId;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
