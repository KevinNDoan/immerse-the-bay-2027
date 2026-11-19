using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zap.Constellation;
using Object = UnityEngine.Object;

namespace Zap.World.Tests
{
    public class ObjectActionTests
    {
        [UnityTest]
        public IEnumerator CompoundRequestReplacesNamedObjectAtCapacity()
        {
            using var f = new Fixture();
            var dna = f.Space.Add(Model("DNA"));
            var others = Enumerable.Range(0, 5).Select(i => f.Space.Add(Model("Other " + i))).ToArray();
            var poses = others.Select(item => item.Pose.position).ToArray();
            Assert.IsTrue(f.Space.Full);
            Assert.IsTrue(f.Space.ApplyReply(Reply(
                new StudyObjectAction { action = "add", model = Model("RNA") },
                new StudyObjectAction { action = "remove", targetId = dna.ObjectId }), others[0]));
            yield return null;
            Assert.AreEqual(6, f.Space.Objects.Count);
            Assert.IsFalse(f.Space.Objects.Any(item => item.Data.title == "DNA"));
            Assert.AreEqual("RNA", f.Space.Current.Data.title);
            for (int i = 0; i < others.Length; i++)
            {
                Assert.IsTrue(f.Space.Objects.Contains(others[i]));
                Assert.AreEqual(poses[i], others[i].Pose.position, "Unrelated objects must stay where the learner placed them.");
            }
        }

        [UnityTest]
        public IEnumerator RevisionTargetsNamedObjectAndPreservesItsPlacement()
        {
            using var f = new Fixture();
            var target = f.Space.Add(Model("Target"));
            var selected = f.Space.Add(Model("Selected other object"));
            target.Pose.SetPositionAndRotation(new Vector3(-2, 1, 3), Quaternion.Euler(10, 20, 30));
            target.Pose.localScale = Vector3.one * 1.7f;
            target.Pane.Place(new Vector3(-1, 1.3f, 2), Quaternion.Euler(0, 45, 0));
            string identity = target.ObjectId;
            Assert.IsTrue(f.Space.ApplyReply(Reply(new StudyObjectAction {
                action = "revise", targetId = identity, model = Model("Revised target") }), selected));
            yield return null;
            var revised = f.Space.Current;
            Assert.AreEqual(identity, revised.ObjectId);
            Assert.AreEqual(new Vector3(-2, 1, 3), revised.Pose.position);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(10, 20, 30), revised.Pose.rotation), .01f);
            Assert.AreEqual(Vector3.one * 1.7f, revised.Pose.localScale);
            Assert.IsTrue(revised.Pane.IsPlaced);
            Assert.AreEqual(new Vector3(-1, 1.3f, 2), revised.Pane.transform.position);
            Assert.AreEqual("Selected other object", selected.Data.title);
        }

        [UnityTest]
        public IEnumerator FailedReplacementKeepsOriginalAndDoesNotPartiallyAdd()
        {
            using var f = new Fixture();
            var original = f.Space.Add(Model("Keep me"));
            var invalid = new StudyModelData { title = "Cannot build", parts = new StudyPartData[] { null } };
            Assert.IsFalse(f.Space.ApplyReply(Reply(
                new StudyObjectAction { action = "remove", targetId = original.ObjectId },
                new StudyObjectAction { action = "add", model = Model("Valid staged addition") },
                new StudyObjectAction { action = "add", model = invalid }), original));
            yield return null;
            Assert.AreEqual(1, f.Space.Objects.Count);
            Assert.AreSame(original, f.Space.Current);
            Assert.IsTrue(original.HasModel);
            Assert.IsTrue(original.gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator MissingRepeatedAndOverCapacityActionsMakeNoChanges()
        {
            using var f = new Fixture();
            var original = f.Space.Add(Model("Keep me"));
            Assert.IsFalse(f.Space.ApplyReply(Reply(
                new StudyObjectAction { action = "remove", targetId = "missing" }), original));
            Assert.IsFalse(f.Space.ApplyReply(Reply(
                new StudyObjectAction { action = "remove", targetId = original.ObjectId },
                new StudyObjectAction { action = "revise", targetId = original.ObjectId, model = Model("Conflict") }), original));
            for (int i = 0; i < 5; i++) f.Space.Add(Model("Other " + i));
            Assert.IsFalse(f.Space.ApplyReply(Reply(new StudyObjectAction { action = "add", model = Model("Overflow") }), original));
            yield return null;
            Assert.AreEqual(6, f.Space.Objects.Count);
            Assert.IsTrue(f.Space.Objects.Contains(original));
        }

        [UnityTest]
        public IEnumerator RemovingLastObjectKeepsPaneAndPersistsDeletionOnRevisit()
        {
            using var f = new Fixture();
            f.Space.Open(f.SaveId, Model("Original"));
            var original = f.Space.Current;
            string identity = original.ObjectId;
            f.Space.Save(); f.Space.Close(); yield return null;
            f.Space.Open(f.SaveId, Model("Original"));
            Assert.AreEqual(identity, f.Space.Current.ObjectId, "Object identity must survive a revisit.");
            var reply = JsonUtility.FromJson<StudyReply>("{\"action\":\"apply\",\"answer\":\"Object removed.\",\"actions\":[{\"action\":\"remove\",\"targetId\":\"" + identity + "\",\"model\":null}]}");
            Assert.IsTrue(f.Space.ApplyReply(reply, f.Space.Current));
            f.Space.Save(); f.Space.Close(); yield return null;
            f.Space.Open(f.SaveId, Model("Original"));
            yield return null;
            Assert.AreEqual(1, f.Space.Objects.Count);
            Assert.IsFalse(f.Space.Current.HasModel, "Revisiting must not restore the deleted initial object.");
            Assert.AreEqual("Object removed.", f.Space.Current.Answer);
            Assert.IsTrue(f.Space.ApplyReply(Reply(new StudyObjectAction { action = "add", model = Model("New object") }), f.Space.Current));
            Assert.IsTrue(f.Space.Current.HasModel);
        }

        private static StudyReply Reply(params StudyObjectAction[] actions) =>
            new StudyReply { action = "apply", answer = "Requested changes complete.", actions = actions };

        private static StudyModelData Model(string title) => new StudyModelData { title = title,
            explanation = "An object to inspect", parts = new[] { new StudyPartData { name = "Body", detail = "Body",
                shape = "sphere", color = "#44BBEE", scale = Vector3.one, motion = "still" } } };

        private sealed class Fixture : IDisposable
        {
            public readonly string SaveId = "object-actions-" + Guid.NewGuid().ToString("N");
            public readonly StudyWorkspace Space;
            readonly GameObject root, camera;
            public Fixture()
            {
                camera = new GameObject("Object action camera"); camera.tag = "MainCamera";
                camera.AddComponent<Camera>(); camera.transform.position = Vector3.up * 1.6f;
                root = new GameObject("Object action workspace"); Space = root.AddComponent<StudyWorkspace>();
            }
            public void Dispose()
            {
                Space.Close(); Object.DestroyImmediate(root); Object.DestroyImmediate(camera);
                using var hash = System.Security.Cryptography.SHA256.Create();
                string key = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(SaveId))).Replace("-", "");
                string path = Path.Combine(Application.persistentDataPath, "exploration", key + ".json");
                foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }
    }
}
