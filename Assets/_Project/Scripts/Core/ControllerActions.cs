using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace Zap.Core
{
    // Shared by the constellation (Zap.Constellation) and in-world steering (Zap.World).
    // XR device features work with the OpenXR controller profiles already in the project.
    public sealed class ControllerActions
    {
        public bool CreateHeld, EditHeld, EnterDown, ExitDown, EventDown;
        public bool LeftGrip, RightGrip, RightTrigger;
        public Vector2 Move, Look;
        private bool _enter, _exit, _event;
        public void Poll()
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            CreateHeld = Read(right, CommonUsages.primaryButton);
            EditHeld = Read(right, CommonUsages.secondaryButton);
            LeftGrip = Read(left, CommonUsages.gripButton);
            RightGrip = Read(right, CommonUsages.gripButton);
            RightTrigger = Read(right, CommonUsages.triggerButton);
            left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Move);
            right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Look);
            bool enter = Read(left, CommonUsages.primaryButton);
            bool exit = Read(left, CommonUsages.secondaryButton);
            bool worldEvent = RightTrigger;
            var keyboard = Keyboard.current;
            if (keyboard != null && (Application.isEditor || Debug.isDebugBuild))
            {
                CreateHeld |= keyboard.vKey.isPressed;
                EditHeld |= keyboard.bKey.isPressed;
                enter |= keyboard.enterKey.isPressed;
                exit |= keyboard.escapeKey.isPressed;
                worldEvent |= keyboard.spaceKey.isPressed;
                if (keyboard.upArrowKey.isPressed) Move.y = 1;
                if (keyboard.downArrowKey.isPressed) Move.y = -1;
                if (keyboard.leftArrowKey.isPressed) Look.x = -1;
                if (keyboard.rightArrowKey.isPressed) Look.x = 1;
            }
            EnterDown = enter && !_enter; ExitDown = exit && !_exit; EventDown = worldEvent && !_event;
            _enter = enter; _exit = exit; _event = worldEvent;
        }
        private static bool Read(UnityEngine.XR.InputDevice device, InputFeatureUsage<bool> usage)
            => device.TryGetFeatureValue(usage, out bool value) && value;
    }
}
