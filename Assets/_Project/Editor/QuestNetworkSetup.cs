using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Zap.EditorTools
{
    public sealed class QuestNetworkSetup : IPostGenerateGradleAndroidProject, IPreprocessBuildWithReport
    {
        private static bool _development;
        public int callbackOrder => 100;
        public void OnPreprocessBuild(BuildReport report) { _development = (report.summary.options & BuildOptions.Development) != 0; }
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // This development prototype talks to a laptop on the same LAN.
            // Apply permissions to generated build output, not a hand-edited Unity asset.
            var file = Path.Combine(path, "src/main/AndroidManifest.xml");
            var xml = new XmlDocument(); xml.Load(file);
            const string android = "http://schemas.android.com/apk/res/android";
            var manifest = xml.DocumentElement;
            foreach (var permission in new[] { "android.permission.INTERNET", "android.permission.RECORD_AUDIO" })
            {
                bool exists = false;
                foreach (XmlElement element in manifest.GetElementsByTagName("uses-permission"))
                    if (element.GetAttribute("name", android) == permission) exists = true;
                if (!exists)
                {
                    var element = xml.CreateElement("uses-permission"); element.SetAttribute("name", android, permission);
                    manifest.AppendChild(element);
                }
            }
            if (_development)
                ((XmlElement)manifest.GetElementsByTagName("application")[0]).SetAttribute("usesCleartextTraffic", android, "true");
            xml.Save(file);
        }

        public static void ConfigureRelayAddress()
        {
            string url = Environment.GetEnvironmentVariable("ZAP_RELAY_URL");
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException("ZAP_RELAY_URL must be an HTTP(S) URL.");
            const string folder = "Assets/_Project/Scripts/Net/Resources";
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/zap_relay_url.txt", url.Trim().TrimEnd('/'));
            AssetDatabase.Refresh();
        }
    }
}
