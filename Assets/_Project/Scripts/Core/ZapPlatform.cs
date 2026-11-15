using UnityEngine;

namespace Zap.Core
{
    public static class ZapPlatform
    {
        // False in the Editor: Play mode there runs headset mode with the XR Interaction Simulator, which reports no active XR device.
        public static bool IsDesktop => !Application.isEditor && !Application.isMobilePlatform && !UnityEngine.XR.XRSettings.isDeviceActive;
        public static bool IsTyping { get; set; }
        public static bool IsMenuOpen { get; set; }
    }
}
