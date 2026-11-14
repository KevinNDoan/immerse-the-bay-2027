using System;
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

                var asset = Resources.Load<TextAsset>(ResourceFileName);
                if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
                {
                    _baseUrl = asset.text.Trim();
                    return _baseUrl;
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
