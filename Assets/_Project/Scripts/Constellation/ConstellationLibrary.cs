using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Zap.World.Tests")]

namespace Zap.Constellation
{
    public static class ConstellationLibrary
    {
        public sealed class Entry
        {
            public string id;
            public string name;
            public string path;
            public DateTime updatedUtc;
            public int topicCount;
        }

        internal static string FolderOverride;

        public static string Folder => string.IsNullOrEmpty(FolderOverride)
            ? Path.Combine(Application.persistentDataPath, "constellations")
            : FolderOverride;

        public static string LastOpenedId
        {
            get => PlayerPrefs.GetString("zap.lastConstellation", "");
            set
            {
                if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey("zap.lastConstellation");
                else PlayerPrefs.SetString("zap.lastConstellation", value);
                PlayerPrefs.Save();
            }
        }

        public static List<Entry> List()
        {
            Directory.CreateDirectory(Folder);
            var entries = new List<Entry>();
            foreach (var path in Directory.GetFiles(Folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (!NodeStore.TryLoad(path, out var data)) continue;
                DateTime updatedUtc;
                if (string.IsNullOrEmpty(data.updated)
                    || !DateTime.TryParse(data.updated, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out updatedUtc))
                    updatedUtc = File.GetLastWriteTimeUtc(path);
                else
                    updatedUtc = updatedUtc.ToUniversalTime();

                entries.Add(new Entry
                {
                    id = Path.GetFileNameWithoutExtension(path),
                    name = string.IsNullOrWhiteSpace(data.name) ? "Untitled" : data.name,
                    path = Path.GetFullPath(path),
                    updatedUtc = updatedUtc,
                    topicCount = data.nodes.Count(node => node != null)
                });
            }
            entries.Sort((left, right) => right.updatedUtc.CompareTo(left.updatedUtc));
            return entries;
        }

        public static Entry Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return List().FirstOrDefault(entry => string.Equals(entry.id, id, StringComparison.Ordinal));
        }

        public static Entry Create(string name)
        {
            Directory.CreateDirectory(Folder);
            string id = Guid.NewGuid().ToString("N");
            string path = Path.Combine(Folder, id + ".json");
            var data = new ConstellationData
            {
                name = NormalizeName(name),
                updated = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            NodeStore.Save(path, data);
            return ToEntry(id, path, data);
        }

        public static Entry EnsureDefault()
        {
            var last = Find(LastOpenedId);
            if (last != null) return last;

            var entries = List();
            if (entries.Count > 0) return entries[0];

            string oldPath = string.IsNullOrEmpty(FolderOverride)
                ? Path.Combine(Application.persistentDataPath, "zap-constellation.json")
                : Path.Combine(Path.GetDirectoryName(Folder), "zap-constellation.json");
            if (File.Exists(oldPath) && NodeStore.TryLoad(oldPath, out var oldData))
            {
                Directory.CreateDirectory(Folder);
                string id = Guid.NewGuid().ToString("N");
                string path = Path.Combine(Folder, id + ".json");
                oldData.name = "My first constellation";
                oldData.updated = File.GetLastWriteTimeUtc(oldPath).ToString("O", CultureInfo.InvariantCulture);
                NodeStore.Save(path, oldData);
                return ToEntry(id, path, oldData);
            }

            return Create("My first constellation");
        }

        private static Entry ToEntry(string id, string path, ConstellationData data)
        {
            DateTime updatedUtc;
            if (!DateTime.TryParse(data.updated, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out updatedUtc))
                updatedUtc = File.GetLastWriteTimeUtc(path);
            return new Entry
            {
                id = id,
                name = string.IsNullOrWhiteSpace(data.name) ? "Untitled" : data.name,
                path = Path.GetFullPath(path),
                updatedUtc = updatedUtc.ToUniversalTime(),
                topicCount = data.nodes.Count(node => node != null)
            };
        }

        private static string NormalizeName(string name)
        {
            name = name?.Trim();
            return string.IsNullOrEmpty(name) ? "Untitled" : name;
        }
    }
}
