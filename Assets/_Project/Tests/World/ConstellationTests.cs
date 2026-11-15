using System;
using System.Collections;
using System.IO;
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

        [UnityTest]
        public IEnumerator ControlsCardBuildsAndSwitchesLayout()
        {
            var parent = new GameObject("Test controls card parent");
            try
            {
                var card = ControlsCard.Create(parent.transform);
                yield return null;

                card.Show("Volcanoes", "Listening…", false, true, .5f, false);
                Assert.IsFalse(card.IsCompact);

                card.Show("Volcanoes", "Opening…", true, false, 0f, true);
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
