using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zap.Constellation;
using Zap.Net;
using Object = UnityEngine.Object;

namespace Zap.World.Tests
{
    public class WorldTravelTests
    {
        [UnityTest]
        public IEnumerator MapThrowPlacesIdeaWithoutRequestingGeneration()
        {
            using var f = new Fixture(false);
            f.Target.Data.suggestions = new[] { "Next idea" };
            f.Target.Body.linearVelocity = Vector3.forward * 3;
            f.Call("HandleOrbThrow", f.Target);
            yield return null;
            Assert.IsFalse(f.Controller.WorldActive);
            Assert.IsFalse(f.Journey.IsActive);
            Assert.AreEqual(Vector3.zero, f.Target.Body.linearVelocity);
            Assert.AreSame(f.Target, f.Controller.Current);
            Assert.AreEqual("Next idea", f.Target.Data.suggestions[0]);
            Assert.AreEqual(0, f.Server.Requests);
            Assert.IsFalse(f.Target.IsTossed);
        }

        [UnityTest]
        public IEnumerator RejectedWorldThrowShattersThenReturnsToSameWorld()
        {
            using var f = new Fixture(true);
            f.Server.Reply = "{\"state\":\"error\",\"notice\":\"Generation disabled for this demo.\"}";
            var pose = f.Camera.transform.position;
            f.Call("HandleOrbThrow", f.Target);
            Assert.IsTrue(f.Journey.IsActive);
            Assert.IsTrue(f.Source.IsVisible, "Do not tear down the current room at throw time.");
            Assert.IsFalse(f.Map.activeSelf, "Do not flash the map during world-to-world travel.");
            yield return f.WaitForPreparation();
            Assert.AreSame(f.Parent, f.Controller.WorldNode);
            Assert.IsTrue(f.Source.IsVisible);
            Assert.IsFalse(f.Map.activeSelf);
            Assert.AreEqual(pose, f.Camera.transform.position);
            StringAssert.Contains("Generation disabled", f.Controller.Status);
            StringAssert.Contains("Parent idea", f.Server.Request);
            Assert.Contains(f.Target, (System.Collections.ICollection)f.Controller.Balls);
            // Include the new free-flight interval before the shatter/fade.
            yield return new WaitForSeconds(3.6f);
            Assert.IsFalse(f.Journey.IsActive);
            Assert.IsTrue(f.Source.IsVisible);
        }

        [UnityTest]
        public IEnumerator InvalidDestinationKeepsDepartureEnvironment()
        {
            using var f = new Fixture(true);
            f.Server.Reply = "{\"state\":\"ready\",\"projection\":\"geometry\",\"environment\":{}}";
            f.Call("HandleOrbThrow", f.Target);
            yield return f.WaitForPreparation();
            Assert.IsTrue(f.Source.IsVisible);
            Assert.AreSame(f.Parent, f.Controller.WorldNode);
            Assert.IsFalse(f.Map.activeSelf);
            StringAssert.Contains("manifest is invalid", f.Controller.Status);
        }

        [UnityTest]
        public IEnumerator SuccessfulWorldThrowLoadsDestinationWithoutReturningHome()
        {
            using var f = new Fixture(true);
            f.Server.Reply = "{\"state\":\"ready\",\"projection\":\"geometry\",\"artifact\":false,\"pane\":{\"title\":\"New world\",\"text\":\"Explore here\"},\"environment\":{\"basePath\":\"/world/assets/" + new string('a',64) + "/\",\"metricScale\":1,\"visual\":[{\"mesh\":\"visual-0.mesh\",\"triangles\":2}],\"collision\":[{\"mesh\":\"collision-0.mesh\"}]}}";
            var pose = f.Camera.transform.position;
            f.Call("HandleOrbThrow", f.Target);
            yield return f.WaitForPreparation();
            Assert.AreSame(f.Target, f.Controller.WorldNode, f.Controller.Status);
            Assert.IsTrue(f.Controller.InWorld);
            Assert.IsFalse(f.Map.activeSelf);
            Assert.AreEqual(pose, f.Camera.transform.position);
            Assert.GreaterOrEqual(f.Server.Requests, 3);
            Assert.IsTrue(f.Parent.gameObject.activeInHierarchy, "The parent remains a linked travel destination.");
            yield return new WaitForSeconds(2.8f);
            Assert.IsFalse(f.Journey.IsActive);
        }

        [UnityTest]
        public IEnumerator CancellingPendingTravelKeepsTheSourceAndIgnoresThePendingJob()
        {
            using var f = new Fixture(true);
            f.Server.Reply = "{\"state\":\"preparing\",\"projection\":\"geometry\",\"cacheKey\":\"" + new string('a',64) + "\",\"notice\":\"Generating\"}";
            f.Call("HandleOrbThrow", f.Target);
            yield return new WaitForSeconds(.2f);
            f.Call("RecoverJourney", "Travel cancelled.");
            yield return new WaitForSeconds(3.2f);
            Assert.AreSame(f.Parent, f.Controller.WorldNode);
            Assert.IsTrue(f.Source.IsVisible);
            Assert.IsFalse(f.Map.activeSelf);
            Assert.IsFalse(f.Controller.Busy);
            Assert.IsFalse(f.Journey.IsActive);
            Assert.AreEqual(1, f.Server.Requests, "A cancelled journey must not keep polling or re-enter on a late result.");
        }

        private sealed class Fixture : IDisposable
        {
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
            public readonly GameObject Root, Camera, Map;
            public readonly ConstellationController Controller;
            public readonly TopicBall Parent, Target;
            public readonly GeneratedEnvironment Source;
            public readonly WorldJourney Journey;
            public readonly Server Server = new Server();
            readonly string oldUrl = RelayConfig.BaseUrl;
            readonly string saveId = Guid.NewGuid().ToString("N");
            public Fixture(bool inWorld)
            {
                RelayConfig.BaseUrl = Server.Url;
                Camera = new GameObject("Travel test head"); Camera.tag = "MainCamera";
                Camera.AddComponent<Camera>(); Camera.transform.position = Vector3.up * 1.6f;
                Root = new GameObject("Travel controller test");
                Controller = Root.AddComponent<ConstellationController>(); Controller.enabled = false;
                Map = new GameObject("Map"); Map.transform.SetParent(Root.transform);
                var branches = new GameObject("Branches"); branches.transform.SetParent(Root.transform);
                Set("_content", Map.transform); Set("_branches", branches.transform);
                Set("_voice", Root.AddComponent<VoiceRecorder>());
                Set("_world", Root.AddComponent<WorldSession>());
                Set("_space", Root.AddComponent<StudyWorkspace>());
                Source = Root.AddComponent<GeneratedEnvironment>(); Set("_environment", Source);
                Journey = Root.AddComponent<WorldJourney>(); Set("_journey", Journey);
                Parent = Ball("parent", "Parent idea", Array.Empty<string>());
                Target = Ball(saveId, "Child idea", new[] { "parent" });
                if (inWorld)
                {
                    var room = new GameObject("Existing room"); room.transform.SetParent(Root.transform);
                    typeof(GeneratedEnvironment).GetField("_root", Flags).SetValue(Source, room);
                    Set("_worldNode", Parent); Set("_worldVisible", true); Set("_worldContext", "Source context");
                    var positions = (Dictionary<TopicBall, Vector3>)typeof(ConstellationController).GetField("_branchPositions", Flags).GetValue(Controller);
                    positions[Target] = Target.Data.position;
                    Target.transform.SetParent(branches.transform, true);
                    Map.SetActive(false);
                }
            }
            TopicBall Ball(string id, string topic, string[] parents)
            {
                var obj = new GameObject(topic); obj.transform.SetParent(Map.transform);
                obj.transform.position = Camera.transform.position + Vector3.forward;
                var ball = obj.AddComponent<TopicBall>();
                ball.Initialize(new NodeData { id=id, topic=topic, parentIds=parents, position=obj.transform.position }, null);
                ((List<TopicBall>)Controller.Balls).Add(ball); return ball;
            }
            void Set(string name, object value) => typeof(ConstellationController).GetField(name, Flags).SetValue(Controller,value);
            public void Call(string name, params object[] args) => typeof(ConstellationController).GetMethod(name,Flags).Invoke(Controller,args);
            public IEnumerator WaitForPreparation()
            {
                float until = Time.realtimeSinceStartup + 10;
                while (Controller.Busy && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsFalse(Controller.Busy, "Preparation failed to settle within ten seconds.");
            }
            public void Dispose()
            {
                Controller.StopAllCoroutines();
                Object.DestroyImmediate(Root); Object.DestroyImmediate(Camera);
                RelayConfig.BaseUrl = oldUrl; Server.Dispose();
                using var hash = System.Security.Cryptography.SHA256.Create();
                string key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(saveId))).Replace("-", "");
                foreach (var suffix in new[]{".json", ".json.bak"})
                {
                    string path = Path.Combine(Application.persistentDataPath,"exploration",key+suffix);
                    if (File.Exists(path)) File.Delete(path);
                }
            }
        }

        private sealed class Server : IDisposable
        {
            readonly HttpListener listener = new HttpListener();
            public string Url, Reply = "{}", Request;
            public int Requests;
            public Server()
            {
                var port = new TcpListener(IPAddress.Loopback,0); port.Start();
                int number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
                Url = "http://127.0.0.1:" + number;
                listener.Prefixes.Add(Url+"/"); listener.Start(); _ = Run();
            }
            async Task Run()
            {
                try
                {
                    while (listener.IsListening)
                    {
                        var context = await listener.GetContextAsync(); Requests++;
                        byte[] bytes;
                        if (context.Request.Url.AbsolutePath == "/world/prepare")
                        { Request = await new StreamReader(context.Request.InputStream).ReadToEndAsync(); bytes = Encoding.UTF8.GetBytes(Reply); }
                        else bytes = Floor();
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes,0,bytes.Length); context.Response.Close();
                    }
                }
                catch (Exception) when (!listener.IsListening) { }
            }
            static byte[] Floor()
            {
                using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
                writer.Write(0x31534D5A); writer.Write(4); writer.Write(6);
                foreach (float value in new float[]{-10,-1.6f,-10, -10,-1.6f,10, 10,-1.6f,10, 10,-1.6f,-10}) writer.Write(value);
                for(int i=0;i<8;i++) writer.Write(0f);
                for(int i=0;i<4;i++) writer.Write(uint.MaxValue);
                foreach(int index in new[]{0,1,2,0,2,3}) writer.Write(index);
                return memory.ToArray();
            }
            public void Dispose() { listener.Close(); }
        }
    }
}
