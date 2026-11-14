using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
using Zap.Net;
#endif

namespace Zap.World
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public class WorldDebugKeys : MonoBehaviour
    {
        private WorldSession _session;
        private DomeView _domeView;

        private void Awake()
        {
            _session = GetComponent<WorldSession>();
            _domeView = GetComponent<DomeView>();
        }

        private void OnEnable()
        {
            if (_session == null)
            {
                _session = GetComponent<WorldSession>();
            }

            if (_session != null)
            {
                _session.OnStatus += HandleStatus;
                _session.OnPrompt += HandlePrompt;
            }
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.OnStatus -= HandleStatus;
                _session.OnPrompt -= HandlePrompt;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_session == null)
            {
                _session = GetComponent<WorldSession>();
            }
            if (_domeView == null)
            {
                _domeView = GetComponent<DomeView>();
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                Debug.Log("[WorldDebugKeys] 'E' pressed: entering test topic 'volcanoes'");
                _session?.Enter("test-volcanoes", "volcanoes");
            }
            else if (keyboard.qKey.wasPressedThisFrame)
            {
                Debug.Log("[WorldDebugKeys] 'Q' pressed: exiting world");
                _session?.Exit();
            }
            else if (keyboard.mKey.wasPressedThisFrame)
            {
                if (_domeView != null)
                {
                    _domeView.ToggleMappingMode();
                    Debug.Log($"[WorldDebugKeys] 'M' pressed: switched mapping mode to {_domeView.Mode}");
                }
            }

            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                Debug.Log("[WorldDebugKeys] Up arrow pressed: steer 'move forward'");
                _session?.Steer("move", "move forward");
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                Debug.Log("[WorldDebugKeys] Down arrow pressed: steer 'move back'");
                _session?.Steer("move", "move back");
            }
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
            {
                Debug.Log("[WorldDebugKeys] Left arrow pressed: steer 'move left'");
                _session?.Steer("move", "move left");
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
            {
                Debug.Log("[WorldDebugKeys] Right arrow pressed: steer 'move right'");
                _session?.Steer("move", "move right");
            }
        }

        private void HandleStatus(StatusMessage status)
        {
            Debug.Log($"[WorldDebugKeys] Status: state='{status.state}', detail='{status.detail}'");
        }

        private void HandlePrompt(string text, uint appliesAtSeq)
        {
            Debug.Log($"[WorldDebugKeys] Prompt: text='{text}', appliesAtSeq={appliesAtSeq}");
        }
    }
#else
    public class WorldDebugKeys : MonoBehaviour
    {
    }
#endif
}
