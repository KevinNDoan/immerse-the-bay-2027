using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using Zap.Constellation;
using Zap.Core;

namespace Zap.Desktop
{
    public sealed class DesktopMode : MonoBehaviour
    {
        private const float DefaultDistance = 1.6f;
        private const float DoubleClickSeconds = .35f;
        private const float DragPixels = 4f;
        private const float RaycastRange = 20f;

        private ConstellationController _controller;
        private SuggestedStars _suggestedStars;
        private Camera _camera;
        private Mouse _mouse;
        private Keyboard _keyboard;
        private Material _linkMaterial;
        private LineRenderer _linkLine;

        private Vector3 _pivot;
        private float _distance = DefaultDistance;
        private float _yaw;
        private float _pitch = 10f;
        private bool _setup;
        private bool _wasInWorld;
        private bool _rightDragAllowed;

        private TopicBall _pressBall;
        private TopicBall _lastClickBall;
        private Vector2 _pressPosition;
        private Vector3 _dragStartPosition;
        private Plane _dragPlane;
        private float _lastClickTime = float.NegativeInfinity;
        private bool _draggingBall;
        private bool _linking;
        private bool _cutting;
        private bool _cutComplete;
        private Vector3 _previousCutDirection;

        private Vector3 _worldCameraPosition;
        private Quaternion _worldCameraRotation;
        private float _worldFieldOfView;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != "Void" || !ZapPlatform.IsDesktop
                || FindAnyObjectByType<DesktopMode>() != null)
                return;

            // on a Mac the app logs one error when it fails to start VR, and the development console must not pop up over the desktop panel.
            Debug.developerConsoleEnabled = false;
            Debug.developerConsoleVisible = false; // The VR error is logged before this runs, so also close a console that is already open.

            var root = new GameObject("[ZAP Desktop]");
            root.AddComponent<DesktopMode>();
            root.AddComponent<DesktopHud>();
            root.AddComponent<DesktopSettings>();
        }

        private void Update()
        {
            // The failed VR start logs an error before this object exists, which opens the development console. Keep it closed.
            if (Debug.developerConsoleVisible) Debug.developerConsoleVisible = false;

            if (!_setup)
            {
                TrySetup();
                return;
            }

            if (_mouse == null) _mouse = Mouse.current;
            if (_keyboard == null) _keyboard = Keyboard.current;

            bool inWorld = _controller.WorldActive;
            if (inWorld != _wasInWorld)
            {
                if (inWorld) EnterWorldView();
                else LeaveWorldView();
                _wasInWorld = inWorld;
            }

            if (inWorld || _mouse == null) return;

            if (ZapPlatform.IsMenuOpen)
                ClearPressState();
            else
            {
                UpdateOrbitInput();
                UpdateLeftMouse();
            }
            if (_controller.WorldActive)
            {
                EnterWorldView();
                _wasInWorld = true;
                return;
            }
            EaseCamera();
        }

        private void TrySetup()
        {
            _controller = FindAnyObjectByType<ConstellationController>();
            _camera = Camera.main;
            if (_controller == null || _camera == null) return;

            var poseDrivers = _camera.GetComponents<TrackedPoseDriver>();
            for (int i = 0; i < poseDrivers.Length; i++) poseDrivers[i].enabled = false;

            SetControllerActive("XR Origin (XR Rig)/Camera Offset/Left Controller", false);
            SetControllerActive("XR Origin (XR Rig)/Camera Offset/Right Controller", false);

            _controller.ShowControlsCard = false;
            _suggestedStars = _controller.GetComponent<SuggestedStars>();
            _mouse = Mouse.current;
            _keyboard = Keyboard.current;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            FocusAllBalls();
            CreateLinkLine();
            _wasInWorld = _controller.WorldActive;
            if (_wasInWorld) EnterWorldView();
            _setup = true;
        }

        private static void SetControllerActive(string path, bool active)
        {
            var controllerObject = GameObject.Find(path);
            if (controllerObject != null) controllerObject.SetActive(active);
        }

        private void CreateLinkLine()
        {
            var lineObject = new GameObject("Desktop link preview");
            lineObject.transform.SetParent(transform, false);
            _linkLine = lineObject.AddComponent<LineRenderer>();
            _linkLine.positionCount = 2;
            _linkLine.useWorldSpace = true;
            _linkLine.enabled = false;

            var shader = Shader.Find("Zap/Solid");
            if (shader != null)
            {
                _linkMaterial = new Material(shader);
                _linkLine.sharedMaterial = _linkMaterial;
            }
        }

        private void UpdateOrbitInput()
        {
            if (_mouse.rightButton.wasPressedThisFrame)
                _rightDragAllowed = !PointerOverPanel();

            if (_mouse.rightButton.isPressed && _rightDragAllowed)
            {
                Vector2 delta = _mouse.delta.ReadValue();
                bool pan = !ZapPlatform.IsTyping && _keyboard != null
                    && (_keyboard.leftShiftKey.isPressed || _keyboard.rightShiftKey.isPressed);
                if (pan)
                {
                    float scale = Mathf.Max(.0002f, _distance * .0012f);
                    _pivot -= (_camera.transform.right * delta.x + _camera.transform.up * delta.y) * scale;
                }
                else
                {
                    _yaw += delta.x * .18f;
                    _pitch = Mathf.Clamp(_pitch - delta.y * .18f, -80f, 80f);
                }
            }
            if (_mouse.rightButton.wasReleasedThisFrame) _rightDragAllowed = false;

            if (!PointerOverPanel())
            {
                float scroll = _mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .001f)
                    _distance = Mathf.Clamp(_distance * Mathf.Exp(-scroll * .0015f), .3f, 8f);
            }

            if (!ZapPlatform.IsTyping && _keyboard != null && _keyboard.fKey.wasPressedThisFrame)
                FocusAllBalls();
        }

        private void UpdateLeftMouse()
        {
            Vector2 pointer = _mouse.position.ReadValue();
            if (_mouse.leftButton.wasPressedThisFrame) BeginLeftPress(pointer);

            if (_mouse.leftButton.isPressed)
            {
                if (_cutting) UpdateCut(pointer);
                else if (_linking) UpdateLinkPreview(pointer);
                else if (_pressBall != null) UpdateBallDrag(pointer);
            }

            if (_mouse.leftButton.wasReleasedThisFrame) EndLeftPress(pointer);
        }

        private void BeginLeftPress(Vector2 pointer)
        {
            ClearPressState();
            if (PointerOverPanel()) return;

            bool keysAllowed = !ZapPlatform.IsTyping && _keyboard != null;
            bool shift = keysAllowed && (_keyboard.leftShiftKey.isPressed || _keyboard.rightShiftKey.isPressed);
            bool alt = keysAllowed && (_keyboard.leftAltKey.isPressed || _keyboard.rightAltKey.isPressed);
            TopicBall ball = Pick(pointer, out Collider collider, out Ray ray);

            if (ball != null)
            {
                if (ball == _lastClickBall && Time.unscaledTime - _lastClickTime <= DoubleClickSeconds)
                {
                    _lastClickBall = null;
                    _lastClickTime = float.NegativeInfinity;
                    _controller.EnterWorld(ball);
                    return;
                }

                _lastClickBall = ball;
                _lastClickTime = Time.unscaledTime;
                _controller.Select(ball);
                _pressBall = ball;
                _pressPosition = pointer;
                _dragStartPosition = ball.transform.position;
                _dragPlane = new Plane(_camera.transform.forward, _dragStartPosition);
                if (shift)
                {
                    _linking = true;
                    ShowLinkPreview(ray);
                }
                return;
            }

            if (collider != null && _suggestedStars != null && _suggestedStars.TryKeep(collider)) return;

            _lastClickBall = null;
            if (alt)
            {
                _cutting = true;
                _cutComplete = false;
                _previousCutDirection = ray.direction;
            }
            else if (!shift)
            {
                _controller.Select(null);
            }
        }

        private void UpdateBallDrag(Vector2 pointer)
        {
            if (!_draggingBall && (pointer - _pressPosition).sqrMagnitude > DragPixels * DragPixels)
                _draggingBall = true;
            if (!_draggingBall) return;

            Ray ray = _camera.ScreenPointToRay(pointer);
            if (_dragPlane.Raycast(ray, out float enter))
                _pressBall.transform.position = ray.GetPoint(enter);
            _pressBall.Body.linearVelocity = Vector3.zero;
        }

        private void UpdateLinkPreview(Vector2 pointer)
        {
            if (_pressBall == null) return;
            Ray ray = _camera.ScreenPointToRay(pointer);
            ShowLinkPreview(ray);
        }

        private void ShowLinkPreview(Ray ray)
        {
            if (_linkLine == null || _pressBall == null) return;
            var theme = ZapTheme.Current;
            if (_linkMaterial != null) _linkMaterial.SetColor("_BaseColor", theme.lineColor);
            _linkLine.startWidth = theme.lineWidth;
            _linkLine.endWidth = theme.lineWidth;
            _linkLine.SetPosition(0, _pressBall.transform.position);
            _linkLine.SetPosition(1, _dragPlane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : ray.GetPoint(RaycastRange));
            _linkLine.enabled = true;
        }

        private void UpdateCut(Vector2 pointer)
        {
            Ray ray = _camera.ScreenPointToRay(pointer);
            if (!_cutComplete)
                _cutComplete = _controller.CutAcross(_camera.transform.position, _previousCutDirection,
                    _camera.transform.position, ray.direction);
            _previousCutDirection = ray.direction;
        }

        private void EndLeftPress(Vector2 pointer)
        {
            if (_linking && _pressBall != null)
            {
                TopicBall end = Pick(pointer, out _, out _);
                if (end != null && end != _pressBall) _controller.ToggleLink(_pressBall, end);
            }
            ClearPressState();
        }

        private void ClearPressState()
        {
            _pressBall = null;
            _draggingBall = false;
            _linking = false;
            _cutting = false;
            _cutComplete = false;
            if (_linkLine != null) _linkLine.enabled = false;
        }

        private TopicBall Pick(Vector2 pointer, out Collider collider, out Ray ray)
        {
            ray = _camera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out RaycastHit hit, RaycastRange))
            {
                collider = hit.collider;
                return collider.GetComponentInParent<TopicBall>();
            }
            collider = null;
            return null;
        }

        private void FocusAllBalls()
        {
            var balls = _controller.Balls;
            if (balls.Count == 0)
            {
                _pivot = new Vector3(0f, 1.3f, .6f);
                return;
            }

            Vector3 total = Vector3.zero;
            for (int i = 0; i < balls.Count; i++) total += balls[i].transform.position;
            _pivot = total / balls.Count;
        }

        private void EaseCamera()
        {
            Quaternion targetRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 targetPosition = _pivot - targetRotation * Vector3.forward * _distance;
            float blend = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, targetPosition, blend);
            _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, targetRotation, blend);
        }

        private void EnterWorldView()
        {
            ClearPressState();
            _worldCameraPosition = _camera.transform.position;
            _worldCameraRotation = _camera.transform.rotation;
            _worldFieldOfView = _camera.fieldOfView;
            _camera.transform.rotation = Quaternion.identity;
            _camera.fieldOfView = 62f;
        }

        private void LeaveWorldView()
        {
            _camera.transform.position = _worldCameraPosition;
            _camera.transform.rotation = _worldCameraRotation;
            _camera.fieldOfView = _worldFieldOfView;
        }

        private static bool PointerOverPanel()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void OnDestroy()
        {
            ZapPlatform.IsTyping = false;
            if (_linkMaterial != null) Destroy(_linkMaterial);
        }
    }
}
