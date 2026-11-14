using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Zap.Net;
using Zap.World;

namespace Zap.World.Tests
{
    public class WorldStreamPlayModeTest
    {
        [UnityTest]
        public IEnumerator WorldStream_EndToEnd_StreamingAndSteering()
        {
            string testUrl = Environment.GetEnvironmentVariable("ZAP_TEST_RELAY_URL");
            if (!string.IsNullOrEmpty(testUrl)) RelayConfig.BaseUrl = testUrl;
            // 1. Health check with 2s timeout
            string healthUrl = RelayConfig.BaseUrl.TrimEnd('/') + "/health";
            using var req = UnityWebRequest.Get(healthUrl);
            req.timeout = 2;
            var op = req.SendWebRequest();

            float startTime = Time.realtimeSinceStartup;
            while (!op.isDone)
            {
                if (Time.realtimeSinceStartup - startTime > 2f)
                {
                    req.Abort();
                    Assert.Ignore("Relay is not running (health check timed out after 2 seconds).");
                    yield break;
                }
                yield return null;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Assert.Ignore($"Relay is not running at {healthUrl}: {req.error}");
                yield break;
            }

            var health = JsonUtility.FromJson<HealthResponse>(req.downloadHandler.text);
            bool live = health.mode == "live";
            // 2. Set up test GameObject and components
            var testObj = new GameObject("[Test ZAP World]");
            var relayClient = testObj.AddComponent<RelayClient>();
            var domeView = testObj.AddComponent<DomeView>();
            var stream = testObj.AddComponent<WorldStream>();
            var session = testObj.AddComponent<WorldSession>();

            try
            {
                // Enter world
                session.Enter("test-playmode-node", "volcanoes");

                // Wait up to 10 seconds for at least 5 frames and streaming state
                float waitStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - waitStart < (live ? 120f : 10f))
                {
                    if (stream.FrameCount >= 5 && session.State == "streaming")
                    {
                        break;
                    }
                    yield return null;
                }

                Assert.GreaterOrEqual(stream.FrameCount, 5, $"Expected at least 5 video frames, got {stream.FrameCount}");
                Assert.AreEqual("streaming", session.State, $"Expected session state 'streaming', got '{session.State}'");
                Assert.IsNotNull(stream.Texture, "Stream texture should not be null");
                if (live)
                {
                    Assert.Greater(stream.Texture.width, 300);
                    Assert.LessOrEqual(stream.Texture.width, 1920);
                }
                else
                {
                    Assert.AreEqual(832, stream.Texture.width);
                    Assert.AreEqual(480, stream.Texture.height);
                }

                // 3. Send steer and wait up to 5 seconds for prompt message
                bool promptArrived = false;
                string receivedPrompt = null;
                uint receivedSeq = 0;

                void OnPrompt(string text, uint seq)
                {
                    promptArrived = true;
                    receivedPrompt = text;
                    receivedSeq = seq;
                }

                session.OnPrompt += OnPrompt;
                session.Steer("move", "move forward");

                float steerStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - steerStart < 5f)
                {
                    if (promptArrived)
                    {
                        break;
                    }
                    yield return null;
                }

                session.OnPrompt -= OnPrompt;
                Assert.IsTrue(promptArrived, "Expected prompt message to arrive within 5 seconds after steer.");
                Assert.IsFalse(string.IsNullOrEmpty(receivedPrompt), "Prompt text should not be empty.");
                float changedAt = Time.realtimeSinceStartup;
                while (stream.LatestSeq < receivedSeq && Time.realtimeSinceStartup - changedAt < 15f) yield return null;
                Assert.GreaterOrEqual(stream.LatestSeq, receivedSeq, "Video must continue through the expected steering boundary.");

                // 4. Exit and assert state returns to idle
                session.Exit();

                float exitStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - exitStart < 5f)
                {
                    if (session.State == "idle")
                    {
                        break;
                    }
                    yield return null;
                }

                Assert.AreEqual("idle", session.State, $"Expected session state 'idle' after exit, got '{session.State}'");
                using var kill = new UnityWebRequest(RelayConfig.BaseUrl.TrimEnd('/') + "/world/kill", "POST");
                kill.downloadHandler = new DownloadHandlerBuffer();
                yield return kill.SendWebRequest();
                Assert.AreEqual(UnityWebRequest.Result.Success, kill.result);
            }
            finally
            {
                if (testObj != null)
                {
                    UnityEngine.Object.Destroy(testObj);
                }
            }
        }
    }
}
