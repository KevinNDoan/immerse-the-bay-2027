using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmerseTheBay.Editor
{
    /// <summary>
    /// Records clips of the app from inside the Editor's Play mode, for demo and submission videos.
    /// A second camera renders the scene to an image file per frame at a fixed frame rate, so the clip is smooth
    /// however slowly the Editor runs, and nothing from the desktop or the Editor's own overlays is captured.
    /// Turn the frames into a video with ffmpeg: ffmpeg -framerate 30 -i f_%05d.jpg -pix_fmt yuv420p clip.mp4
    /// Call it from Play mode: queue actions with At, optionally set a camera path, then Begin.
    /// </summary>
    public static class ClipRecorder
    {
        static readonly List<KeyValuePair<float, Action>> Actions = new List<KeyValuePair<float, Action>>();

        static Camera _camera;
        static RenderTexture _target;
        static Texture2D _frameImage;
        static Func<float, Pose> _path;
        static string _folder;
        static int _fps;
        static int _frame;
        static int _frames;
        static int _saved;
        static bool _hooked;

        public static bool Recording { get; private set; }
        public static string State => Recording ? $"recording {_saved}/{_frames} frames to {_folder}" : $"idle; last clip {_saved} frames in {_folder}";

        /// <summary>Runs an action once the clip reaches this many seconds. Queue before Begin.</summary>
        public static void At(float seconds, Action action)
        {
            if (action != null) Actions.Add(new KeyValuePair<float, Action>(seconds, action));
        }

        /// <summary>Where the recording camera is at each moment of the clip. Without one it rides the headset view.</summary>
        public static void CameraPath(Func<float, Pose> path) { _path = path; }

        public static void Begin(string folder, float seconds, int fps = 30, int width = 1920, int height = 1080, float fieldOfView = 75f)
        {
            if (!Application.isPlaying) { Debug.LogWarning("ClipRecorder: enter Play mode first."); return; }
            if (Recording) Stop();
            var main = Camera.main;
            if (main == null) { Debug.LogWarning("ClipRecorder: no main camera."); return; }

            _folder = folder;
            Directory.CreateDirectory(_folder);
            _fps = Mathf.Max(1, fps);
            _frames = Mathf.Max(1, Mathf.RoundToInt(seconds * _fps));
            _frame = 0;
            _saved = 0;

            _target = new RenderTexture(width, height, 24) { name = "Clip recorder target", antiAliasing = 4 };
            _target.Create();
            _frameImage = new Texture2D(width, height, TextureFormat.RGB24, false);
            var holder = new GameObject("[Clip recorder camera]");
            _camera = holder.AddComponent<Camera>();
            _camera.CopyFrom(main);
            _camera.stereoTargetEye = StereoTargetEyeMask.None;
            _camera.fieldOfView = fieldOfView;
            _camera.targetTexture = _target;
            _camera.tag = "Untagged";

            Time.captureFramerate = _fps;
            if (!_hooked) { RenderPipelineManager.beginContextRendering += OnBeginFrame; _hooked = true; }
            Recording = true;
        }

        public static void Stop()
        {
            if (_hooked) { RenderPipelineManager.beginContextRendering -= OnBeginFrame; _hooked = false; }
            Time.captureFramerate = 0;
            Recording = false;
            Actions.Clear();
            _path = null;
            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            if (_target != null) { _target.Release(); UnityEngine.Object.Destroy(_target); }
            if (_frameImage != null) UnityEngine.Object.Destroy(_frameImage);
            _camera = null;
            _target = null;
            _frameImage = null;
        }

        // Once per rendered frame: save the picture the camera drew last frame, then set up this frame.
        static void OnBeginFrame(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!Recording || _camera == null) { Stop(); return; }
            if (_frame > 0) SaveLastFrame();
            if (_saved >= _frames) { Debug.Log($"ClipRecorder: saved {_saved} frames to {_folder}"); Stop(); return; }

            float seconds = _frame / (float)_fps;
            for (int i = Actions.Count - 1; i >= 0; i--)
            {
                if (Actions[i].Key > seconds) continue;
                var action = Actions[i].Value;
                Actions.RemoveAt(i);
                try { action(); }
                catch (Exception exception) { Debug.LogWarning("ClipRecorder action failed: " + exception); }
            }

            var main = Camera.main;
            Pose pose = _path != null ? _path(seconds)
                : main != null ? new Pose(main.transform.position, main.transform.rotation) : Pose.identity;
            _camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _frame++;
        }

        static void SaveLastFrame()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            _frameImage.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            RenderTexture.active = previous;
            _saved++;
            File.WriteAllBytes(Path.Combine(_folder, $"f_{_saved:D5}.jpg"), _frameImage.EncodeToJPG(95));
        }
    }
}
