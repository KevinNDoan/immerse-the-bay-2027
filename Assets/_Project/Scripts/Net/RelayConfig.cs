using System;
using System.IO;
using UnityEngine;

namespace Zap.Net
{
    public static class RelayConfig
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:8787";
        private const string ResourceFileName = "zap_relay_url";

        private static string _baseUrl;

        public static string BaseUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(_baseUrl))
                {
                    return _baseUrl;
                }

                // Let a development headset use USB forwarding or a new LAN address
                // without rebuilding the APK. Release builds use the bundled address.
                if (Debug.isDebugBuild || Application.isEditor)
                {
                    try
                    {
                        string path = Path.Combine(Application.persistentDataPath, "zap-relay-url.txt");
                        if (File.Exists(path))
                        {
                            string candidate = File.ReadAllText(path).Trim();
                            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                                (uri.Scheme == "http" || uri.Scheme == "https") &&
                                !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo) &&
                                uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) &&
                                string.IsNullOrEmpty(uri.Fragment))
                            {
                                _baseUrl = candidate.TrimEnd('/');
                                Debug.Log($"[RelayConfig] Development relay override: {_baseUrl}");
                                return _baseUrl;
                            }
                            Debug.LogWarning("[RelayConfig] Ignoring invalid development relay URL.");
                        }
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        Debug.LogWarning("[RelayConfig] Could not read development relay override; using bundled address.");
                    }
                }

                // The bundled address is baked in for a headset build and points at the build machine's network address. In the Editor the relay runs on this machine, so skip it and use the default unless the development override file above says otherwise.
                if (!Application.isEditor)
                {
                    var asset = Resources.Load<TextAsset>(ResourceFileName);
                    if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
                    {
                        _baseUrl = asset.text.Trim();
                        return _baseUrl;
                    }
                }

                _baseUrl = DefaultBaseUrl;
                return _baseUrl;
            }
            set => _baseUrl = value;
        }

        public static string WsBaseUrl
        {
            get
            {
                string httpUrl = BaseUrl.TrimEnd('/');
                if (httpUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return "wss://" + httpUrl.Substring("https://".Length);
                }
                if (httpUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    return "ws://" + httpUrl.Substring("http://".Length);
                }
                return "ws://" + httpUrl;
            }
        }

        public static string WorldWsUrl => $"{WsBaseUrl}/ws/world";

        public static void Reset()
        {
            _baseUrl = null;
        }
    }
}
