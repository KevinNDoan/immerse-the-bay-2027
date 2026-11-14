using UnityEngine;

namespace Zap.Core
{
    public static class ZapPlatform
    {
        public static bool IsDesktop => !Application.isMobilePlatform && !UnityEngine.XR.XRSettings.isDeviceActive;
        public static bool IsTyping { get; set; }
        public static bool IsMenuOpen { get; set; }
    }
}
