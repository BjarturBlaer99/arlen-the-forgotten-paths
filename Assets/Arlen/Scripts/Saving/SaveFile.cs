using System;
using System.IO;
using UnityEngine;

namespace Arlen.Saving
{
    public enum SaveReadResult
    {
        Ok,
        Missing,
        Corrupt,
        TooNew,
    }

    /// <summary>
    /// Reads and writes <see cref="SaveData"/> as JSON. Writes go to a temp file first and are then swapped
    /// in, so a crash mid-write never leaves a half-written save behind. Pure file I/O, unit-tested.
    /// </summary>
    public static class SaveFile
    {
        public static string ToJson(SaveData data) => JsonUtility.ToJson(data, prettyPrint: true);

        public static SaveReadResult FromJson(string json, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json)) return SaveReadResult.Corrupt;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (ArgumentException)
            {
                return SaveReadResult.Corrupt;
            }
            if (data == null) return SaveReadResult.Corrupt;
            if (data.version > SaveData.CurrentVersion)
            {
                data = null;
                return SaveReadResult.TooNew;
            }
            Migrate(data);
            return SaveReadResult.Ok;
        }

        /// <summary>Upgrades older layouts in place. Version 1 is the first format, so nothing to do yet.</summary>
        static void Migrate(SaveData data)
        {
            data.version = SaveData.CurrentVersion;
        }

        public static void Write(string path, SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string temp = path + ".tmp";
            File.WriteAllText(temp, ToJson(data));
            if (File.Exists(path))
            {
                string backup = path + ".bak";
                File.Replace(temp, path, backup);
                File.Delete(backup);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        public static SaveReadResult Read(string path, out SaveData data)
        {
            data = null;
            if (!File.Exists(path)) return SaveReadResult.Missing;
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException)
            {
                return SaveReadResult.Corrupt;
            }
            return FromJson(json, out data);
        }

        public static bool Exists(string path) => File.Exists(path);

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }
}
