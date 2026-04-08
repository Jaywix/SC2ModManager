/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: 2024-01-01
 * Last updated: 2024-06-01
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This service handles saving and loading mod presets, which are named collections of gamedata files that can be applied to quickly switch between mod setups.
    /// </summary>
    public class PresetService
    {
        private readonly string presetsPath;
        private readonly string originalFilesListPath;

        public PresetService()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );

            presetsPath = Path.Combine(appData, "Presets");
            originalFilesListPath = Path.Combine(presetsPath, "_original_files.json");

            Directory.CreateDirectory(presetsPath);
        }

        // ================= PRESETS =================

        /// <summary>
        ///     Saves current gamedata folder contents as a named preset.
        /// </summary>
        public void SavePreset(string name, string gameDataPath)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new Exception("Preset name cannot be empty.");

            var files = Directory.GetFiles(gameDataPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(gameDataPath, f))
                .OrderBy(f => f)
                .ToList();

            var preset = new ModPreset
            {
                Name = name,
                CreatedAt = DateTime.Now,
                Files = files
            };

            string safeName = MakeSafeFileName(name);
            string path = Path.Combine(presetsPath, $"{safeName}.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(preset, options));
        }

        /// <summary>
        ///     Overwrites an existing preset file with updated data.
        /// </summary>
        public void ResavePreset(ModPreset preset)
        {
            string safeName = MakeSafeFileName(preset.Name);
            string path = Path.Combine(presetsPath, $"{safeName}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(preset, options));
        }

        /// <summary>
        ///     Returns all saved presets.
        /// </summary>
        public List<ModPreset> LoadAllPresets()
        {
            var result = new List<ModPreset>();

            foreach (var file in Directory.GetFiles(presetsPath, "*.json"))
            {
                // Skip the internal original files list
                if (Path.GetFileName(file).StartsWith("_"))
                    continue;

                try
                {
                    string json = File.ReadAllText(file);
                    var preset = JsonSerializer.Deserialize<ModPreset>(json);
                    if (preset != null)
                        result.Add(preset);
                }
                catch { }
            }

            return result.OrderBy(p => p.CreatedAt).ToList();
        }

        /// <summary>
        ///     Deletes a preset by name.
        /// </summary>
        public void DeletePreset(string name)
        {
            string safeName = MakeSafeFileName(name);
            string path = Path.Combine(presetsPath, $"{safeName}.json");

            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        ///     Applies a preset by copying gamedata to match the preset's file list.
        ///     Files in the preset that exist in gamedata are kept; everything else is removed.
        ///     NOTE: This only manages which files are present — it does not restore file contents.
        ///     Use RestoreOriginalGamedata first if you want a clean slate.
        /// </summary>
        public void ApplyPreset(ModPreset preset, string gameDataPath)
        {
            var presetFiles = preset.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Remove files not in the preset
            foreach (var file in Directory.GetFiles(gameDataPath, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(gameDataPath, file);
                if (!presetFiles.Contains(relative))
                    File.Delete(file);
            }
        }

        // ================= ORIGINAL FILES =================

        /// <summary>
        ///     Saves the list of original gamedata files (call this after RestoreOriginalGamedata).
        /// </summary>
        public void SaveOriginalFilesList(string gameDataPath)
        {
            var files = Directory.GetFiles(gameDataPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(gameDataPath, f))
                .OrderBy(f => f)
                .ToList();

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(originalFilesListPath, JsonSerializer.Serialize(files, options));
        }

        /// <summary>
        ///     Returns the list of original gamedata filenames, or empty if never saved.
        /// </summary>
        public List<string> LoadOriginalFilesList()
        {
            if (!File.Exists(originalFilesListPath))
                return new List<string>();

            try
            {
                string json = File.ReadAllText(originalFilesListPath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        // ================= COMPARE =================

        /// <summary>
        ///     Compares two file lists and returns all unique filenames,
        ///     with a flag indicating whether each differs between the two.
        /// </summary>
        public List<(string FileName, bool IsDifferent)> Compare(
            List<string> leftFiles,
            List<string> rightFiles)
        {
            var all = leftFiles.Union(rightFiles, StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f)
                .ToList();

            var leftSet = leftFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rightSet = rightFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);

            return all.Select(f => (f, leftSet.Contains(f) != rightSet.Contains(f))).ToList();
        }

        // ================= HELPERS =================

        private string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name;
        }
    }
}
