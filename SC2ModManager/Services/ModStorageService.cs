/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 23, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This service is responsible for all interactions with the local storage of mods.
    ///     Currently, the file structure is as follows:
    ///     AppData/Roaming/SC2ModManager/Mods/
    ///     Inside Mods exists each mod type folder (e.g. Maps, GenericMods), and inside each of those exists an Enabled and Disabled folder.
    /// </summary>
    public class ModStorageService
    {
        private readonly string mapsEnabledPath;
        private readonly string mapsDisabledPath;
        private readonly string genericModsEnabledPath;
        private readonly string genericModsDisabledPath;

        private readonly string mapsStatePath;
        private readonly string genericModsStatePath;

        private readonly HttpClient httpClient = new HttpClient();

        private static readonly HashSet<string> OriginalGameFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bp.scd", "bp.scd.enc", "effects.scd", "effects.scd.enc", "env.scd", "env.scd.enc", "fonts.scd", "fonts.scd.enc", "loc_cn.scd.enc", "loc_de.scd.enc", "loc_fr.scd.enc", "loc_it.scd.enc", "loc_ja.scd.enc", "loc_jp.scd.enc", "loc_kr.scd.enc", "loc_pl.scd.enc", "loc_ru.scd.enc", "loc_star.scd.enc", "loc_US.scd", "loc_us.scd.enc", "lua.bkup", "lua.scd", "lua.scd.enc", "maps.scd", "maps.scd.enc", "meshes.scd", "meshes.scd.enc", "projectiles.scd", "projectiles.scd.enc", "props.scd", "props.scd.enc", "textures.scd", "textures.scd.enc", "ui.scd", "ui.scd.enc", "uncompiled_lua.scd", "uncompiled_lua.scd.enc", "units.scd", "units.scd.enc", "z_diff1.scd", "z_diff1.scd.enc", "z_dlc1.scd", "z_dlc1.scd.enc", "z_dlc1_map_shared.scd", "z_dlc1_map_shared.scd.enc", "z_lua_dlc1.bkup", "z_lua_dlc1.scd", "z_lua_dlc1.scd.enc", "z_uncompiled_lua_dlc1.scd", "z_uncompiled_lua_dlc1.scd.enc", "loc_cz.scd.enc", "loc_es.scd.enc"
        };

        public static bool IsOriginalGameFile(string fileName) => OriginalGameFiles.Contains(Path.GetFileName(fileName));

        public ModStorageService()
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager/1.0");
            httpClient.Timeout = TimeSpan.FromMinutes(3);

            string appData = Globals.GetDataPath();


            string modsRoot = Path.Combine(appData, "Mods");
            string mapsRoot = Path.Combine(modsRoot, "Maps");
            string genericRoot = Path.Combine(modsRoot, "GenericMods");

            mapsEnabledPath = Path.Combine(mapsRoot, "Enabled");
            mapsDisabledPath = Path.Combine(mapsRoot, "Disabled");
            genericModsEnabledPath = Path.Combine(genericRoot, "Enabled");
            genericModsDisabledPath = Path.Combine(genericRoot, "Disabled");

            mapsStatePath = Path.Combine(mapsRoot, "maps_state.json");
            genericModsStatePath = Path.Combine(genericRoot, "genericmods_state.json");

            Directory.CreateDirectory(mapsEnabledPath);
            Directory.CreateDirectory(mapsDisabledPath);
            Directory.CreateDirectory(genericModsEnabledPath);
            Directory.CreateDirectory(genericModsDisabledPath);
        }

        // ================= HELPERS =================
        /// <summary>
        ///     Pulls every .scd out of a downloaded zip into the folder and returns the paths it made. Won't
        ///     overwrite anything that's already there, and if anything fails partway it removes what it
        ///     already pulled out, so a bad zip never leaves stray files behind.
        /// </summary>
        private async Task<List<string>> ExtractScdFromZipAsync(string zipPath, string destinationFolder)
        {
            return await Task.Run(() =>
            {
                var extracted = new List<string>();
                using ZipArchive archive = ZipFile.OpenRead(zipPath);

                var scdEntries = archive.Entries
                    .Where(e => e.FullName.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Check first so nothing gets half extracted. Overwriting here used to silently replace
                // a different map/mod that happened to have the same file name
                foreach (var entry in scdEntries)
                {
                    string destPath = Path.Combine(destinationFolder, Path.GetFileName(entry.FullName));
                    if (File.Exists(destPath))
                        throw new IOException($"The download contains '{Path.GetFileName(destPath)}', which is already installed, so nothing was extracted.");
                }

                try
                {
                    foreach (var entry in scdEntries)
                    {
                        // Extract to a temp name first, same reason as DownloadToFileAsync
                        string destPath = Path.Combine(destinationFolder, Path.GetFileName(entry.FullName));
                        string tempPath = destPath + ".download";
                        try
                        {
                            entry.ExtractToFile(tempPath, overwrite: true);
                            File.Move(tempPath, destPath);
                            extracted.Add(destPath);
                        }
                        finally
                        {
                            if (File.Exists(tempPath)) File.Delete(tempPath);
                        }
                    }
                }
                catch
                {
                    DeleteFiles(extracted);
                    throw;
                }

                return extracted;
            });
        }

        /// <summary>
        ///     After extracting a zip download, makes sure the file the list says we're getting is actually in
        ///     there. The app only keeps track of that one name, so if the zip had something else in it,
        ///     auto-enable, the duplicate check, and uninstall would all miss it. If it's not there, the
        ///     extracted files get removed and this throws.
        /// </summary>
        private static void EnsureZipHadExpectedFile(List<string> extracted, string folder, string expectedFileName)
        {
            string expected = Path.Combine(folder, expectedFileName);
            if (extracted.Any(p => string.Equals(p, expected, StringComparison.OrdinalIgnoreCase)))
                return;

            string had = extracted.Count == 0 ? "no .scd files" : string.Join(", ", extracted.Select(Path.GetFileName));
            DeleteFiles(extracted);
            throw new InvalidDataException($"The download was supposed to contain '{expectedFileName}' but it had {had}. Nothing was installed.");
        }

        private static void DeleteFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
            }
        }

        /// <summary>
        ///     Downloads to a temp file next to outputPath and only renames it to the real name once the
        ///     whole thing came through. If the internet drops halfway, a half downloaded .scd used to sit
        ///     in the folder looking like an installed mod, and then retrying said "already installed".
        ///     The .download name doesn't end in .scd so nothing ever lists it.
        /// </summary>
        private async Task DownloadToFileAsync(string url, string outputPath)
        {
            string tempPath = outputPath + ".download";

            try
            {
                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

                    await stream.CopyToAsync(fileStream);
                    await fileStream.FlushAsync();
                }

                // The file stream is closed now, so the rename can happen
                File.Move(tempPath, outputPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }


        // When adding new mods, you can probably copy and paste an entire section like the Maps one below.
        // If it is just a .scd file that doesn't overwrite any original game file, then the logic should be very similar to below

        // ================= MAPS: DISK =================

        /// <summary>
        ///     Returns all installed maps (enabled + disabled) with IsEnabled set correctly.
        /// </summary>
        public List<Map> GetInstalledMaps()
        {
            var result = new List<Map>();

            foreach (var file in Directory.GetFiles(mapsEnabledPath, "*.scd"))
                result.Add(new Map(Path.GetFileName(file)) { IsEnabled = true, IsDownloaded = true });

            foreach (var file in Directory.GetFiles(mapsDisabledPath, "*.scd"))
                result.Add(new Map(Path.GetFileName(file)) { IsEnabled = false, IsDownloaded = true });

            return result;
        }

        /// <summary>
        ///     Moves a map file to the Enabled folder.
        /// </summary>
        public void MoveMapToEnabled(Map map)
        {
            string src = Path.Combine(mapsDisabledPath, map.FileName);
            string dest = Path.Combine(mapsEnabledPath, map.FileName);

            if (File.Exists(src))
                File.Move(src, dest, true);
        }

        /// <summary>
        ///     Moves a map file to the Disabled folder.
        /// </summary>
        public void MoveMapToDisabled(Map map)
        {
            string src = Path.Combine(mapsEnabledPath, map.FileName);
            string dest = Path.Combine(mapsDisabledPath, map.FileName);

            if (File.Exists(src))
                File.Move(src, dest, true);
        }

        /// <summary>
        ///     Deletes a map from both folders.
        /// </summary>
        public void DeleteMap(Map map)
        {
            string enabledPath = Path.Combine(mapsEnabledPath, map.FileName);
            string disabledPath = Path.Combine(mapsDisabledPath, map.FileName);

            if (File.Exists(enabledPath)) File.Delete(enabledPath);
            if (File.Exists(disabledPath)) File.Delete(disabledPath);
        }

        /// <summary>
        ///     Downloads a map from GitHub into the Disabled folder.
        /// </summary>
        public async Task DownloadMapAsync(Map map)
        {
            if (string.IsNullOrEmpty(map.DownloadURL))
                throw new Exception("Map has no download URL.");

            string url = map.DownloadURL;

            if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                string tempZip = Path.Combine(Path.GetTempPath(), $"{map.ID}_download.zip");
                try
                {
                    await DownloadToFileAsync(url, tempZip);
                    var extracted = await ExtractScdFromZipAsync(tempZip, mapsDisabledPath);
                    EnsureZipHadExpectedFile(extracted, mapsDisabledPath, map.FileName);
                }
                finally
                {
                    // Clean up the zip even when the download or extract failed
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                }
            }
            else if (url.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
            {
                string path = Path.Combine(mapsDisabledPath, map.FileName);
                await DownloadToFileAsync(url, path);
            }
            else
            {
                throw new Exception($"Unsupported download format for: {url}");
            }
        }

        /// <summary>
        ///     Copies a .scd file from an external path into the Disabled folder.
        /// </summary>
        public async Task ImportMapAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception("Map file not found.");

            string fileName = Path.GetFileName(filePath);
            string dest = Path.Combine(mapsDisabledPath, fileName);

            await Task.Run(() => File.Copy(filePath, dest, true));
        }

        /// <summary>
        ///     Copies a .scd file from an external path into Maps/Enabled (for scan imports).
        /// </summary>
        public async Task ImportMapAsEnabledAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception("Map file not found.");

            string fileName = Path.GetFileName(filePath);
            string dest = Path.Combine(mapsEnabledPath, fileName);

            await Task.Run(() => File.Copy(filePath, dest, true));
        }

        /// <summary>
        ///     Extracts .scd files from a zip into the Disabled folder.
        /// </summary>
        public async Task ExtractAndImportMapsAsync(string zipPath)
        {
            if (!File.Exists(zipPath))
                throw new Exception("Zip file not found.");

            string tempDir = Path.Combine(Path.GetTempPath(), "SC2_map_extract");

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            Directory.CreateDirectory(tempDir);

            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir));

            foreach (var file in Directory.GetFiles(tempDir, "*.scd", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(mapsDisabledPath, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            Directory.Delete(tempDir, true);
        }

        // ================= MAPS: STATE JSON =================

        /// <summary>
        ///     Saves the state of all maps in the enabled/disabled view from a json file
        /// </summary>
        public void SaveMapsState(IEnumerable<Map> allMaps)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(allMaps, options);
            File.WriteAllText(mapsStatePath, json);
        }

        /// <summary>
        ///     Loads the state of all the maps to put into the enabled/disabled view
        /// </summary>
        public List<Map> LoadMapsState()
        {
            if (!File.Exists(mapsStatePath))
                return new List<Map>();

            try
            {
                string json = File.ReadAllText(mapsStatePath);
                return JsonSerializer.Deserialize<List<Map>>(json) ?? new List<Map>();
            }
            catch
            {
                return new List<Map>();
            }
        }

        /// <summary>
        ///     Rewrites the maps state to match what's on disk while keeping the saved metadata
        ///     (name, hash, image, etc.). Only the enabled/disabled flag comes from disk. Use this
        ///     instead of SaveMapsState(GetInstalledMaps()) — GetInstalledMaps only knows filenames,
        ///     so saving it directly wipes all the metadata.
        /// </summary>
        public void SyncMapsStateWithDisk()
        {
            var saved = LoadMapsState().ToDictionary(m => m.FileName, m => m, StringComparer.OrdinalIgnoreCase);
            var merged = new List<Map>();

            foreach (var onDisk in GetInstalledMaps())
            {
                if (saved.TryGetValue(onDisk.FileName, out var s))
                {
                    s.IsEnabled = onDisk.IsEnabled;
                    s.IsDownloaded = true;
                    merged.Add(s);
                }
                else
                {
                    merged.Add(onDisk);
                }
            }

            SaveMapsState(merged);
        }

        // ================= MAPS: GITHUB =================

        /// <summary>
        ///     Gets the json file from github and deserializes it into a list of maps that are available for download
        /// </summary>
        public async Task<List<Map>> GetDownloadableMapsAsync()
        {
            string json = await httpClient.GetStringAsync(Globals.MapsListUrl);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Map>>(json, options) ?? new List<Map>();
        }








        // ================= GENERIC MODS: DISK =================

        /// <summary>
        ///     Returns all installed generic mods (enabled + disabled) with IsEnabled set correctly.
        /// </summary>
        /// <param name="includeAllFileTypes">
        ///     False (the default) only returns .scd files. True returns every file, for the installed mods page
        ///     where the "Show only .scd files" checkbox decides what's shown. Manual Import can add other file
        ///     types, and before this they got copied in but never showed up anywhere.
        /// </param>
        public List<GenericGamedataMod> GetInstalledGenericMods(bool includeAllFileTypes = false)
        {
            var result = new List<GenericGamedataMod>();
            string pattern = includeAllFileTypes ? "*" : "*.scd";

            foreach (var file in Directory.GetFiles(genericModsEnabledPath, pattern).Where(IsModFile))
                result.Add(new GenericGamedataMod(Path.GetFileName(file)) { IsEnabled = true, IsDownloaded = true });

            foreach (var file in Directory.GetFiles(genericModsDisabledPath, pattern).Where(IsModFile))
                result.Add(new GenericGamedataMod(Path.GetFileName(file)) { IsEnabled = false, IsDownloaded = true });

            return result;
        }

        // A download that's still going (or got interrupted) sits there as name.download until it's done. Not a mod
        private static bool IsModFile(string path) => !path.EndsWith(".download", StringComparison.OrdinalIgnoreCase);

        public bool HasEnabledGenericMods()
        {
            return Directory.GetFiles(genericModsEnabledPath, "*.scd").Length > 0;
        }

        /// <summary>
        ///     Moves a generic mod file to the Enabled folder.
        /// </summary>
        public void MoveGenericModToEnabled(GenericGamedataMod mod)
        {
            string src = Path.Combine(genericModsDisabledPath, mod.FileName);
            string dest = Path.Combine(genericModsEnabledPath, mod.FileName);

            if (File.Exists(src))
                File.Move(src, dest, true);
        }

        /// <summary>
        ///     Moves a generic mod file to the Disabled folder.
        /// </summary>
        public void MoveGenericModToDisabled(GenericGamedataMod mod)
        {
            string src = Path.Combine(genericModsEnabledPath, mod.FileName);
            string dest = Path.Combine(genericModsDisabledPath, mod.FileName);

            if (File.Exists(src))
                File.Move(src, dest, true);
        }

        /// <summary>
        ///     Deletes a generic mod from both folders.
        /// </summary>
        public void DeleteGenericMod(GenericGamedataMod mod)
        {
            string enabledPath = Path.Combine(genericModsEnabledPath, mod.FileName);
            string disabledPath = Path.Combine(genericModsDisabledPath, mod.FileName);

            if (File.Exists(enabledPath)) File.Delete(enabledPath);
            if (File.Exists(disabledPath)) File.Delete(disabledPath);
        }

        /// <summary>
        ///     Downloads a generic mod from GitHub into the Disabled folder.
        /// </summary>
        public async Task DownloadGenericModAsync(GenericGamedataMod mod)
        {
            if (string.IsNullOrEmpty(mod.DownloadURL))
                throw new Exception("Mod has no download URL.");

            string url = mod.DownloadURL;

            if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                string tempZip = Path.Combine(Path.GetTempPath(), $"{mod.ID}_download.zip");
                try
                {
                    await DownloadToFileAsync(url, tempZip);
                    var extracted = await ExtractScdFromZipAsync(tempZip, genericModsDisabledPath);
                    EnsureZipHadExpectedFile(extracted, genericModsDisabledPath, mod.FileName);
                }
                finally
                {
                    // Clean up the zip even when the download or extract failed
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                }
            }
            else if (url.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
            {
                string path = Path.Combine(genericModsDisabledPath, mod.FileName);
                await DownloadToFileAsync(url, path);
            }
            else
            {
                throw new Exception($"Unsupported download format for: {url}");
            }
        }

        /// <summary>
        ///     Copies any .scd file from an external path into GenericMods/Disabled (manual import).
        /// </summary>
        public async Task ImportGenericModAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception("File not found.");

            string fileName = Path.GetFileName(filePath);
            string dest = Path.Combine(genericModsDisabledPath, fileName);

            await Task.Run(() => File.Copy(filePath, dest, true));
        }

        /// <summary>
        ///     Copies a .scd file from an external path into GenericMods/Enabled (for scan imports).
        /// </summary>
        public async Task ImportGenericModAsEnabledAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception("File not found.");

            string fileName = Path.GetFileName(filePath);
            string dest = Path.Combine(genericModsEnabledPath, fileName);

            await Task.Run(() => File.Copy(filePath, dest, true));
        }

        // ================= GENERIC MODS: STATE JSON =================

        /// <summary>
        ///     Saves the state of all generic mods in the enabled/disabled view to a json file
        /// </summary>
        public void SaveGenericModsState(IEnumerable<GenericGamedataMod> allMods)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(allMods, options);
            File.WriteAllText(genericModsStatePath, json);
        }

        /// <summary>
        ///     Gets the state of all generic mods in the enabled/disabled view from a json file
        /// </summary>
        public List<GenericGamedataMod> LoadGenericModsState()
        {
            if (!File.Exists(genericModsStatePath))
                return new List<GenericGamedataMod>();

            try
            {
                string json = File.ReadAllText(genericModsStatePath);
                return JsonSerializer.Deserialize<List<GenericGamedataMod>>(json) ?? new List<GenericGamedataMod>();
            }
            catch
            {
                return new List<GenericGamedataMod>();
            }
        }

        /// <summary>
        ///     Same as SyncMapsStateWithDisk but for generic mods — keeps the saved metadata and only
        ///     takes the enabled/disabled flag from disk. Use this instead of
        ///     SaveGenericModsState(GetInstalledGenericMods()), which wipes the metadata.
        /// </summary>
        public void SyncGenericModsStateWithDisk()
        {
            var saved = LoadGenericModsState().ToDictionary(m => m.FileName, m => m, StringComparer.OrdinalIgnoreCase);
            var merged = new List<GenericGamedataMod>();

            foreach (var onDisk in GetInstalledGenericMods())
            {
                if (saved.TryGetValue(onDisk.FileName, out var s))
                {
                    s.IsEnabled = onDisk.IsEnabled;
                    s.IsDownloaded = true;
                    merged.Add(s);
                }
                else
                {
                    merged.Add(onDisk);
                }
            }

            SaveGenericModsState(merged);
        }

        // ================= GENERIC MODS: GITHUB =================

        /// <summary>
        ///     Returns a list of all the generic mods available for download from GitHub by getting the json file and deserializing it into a list of GenericGamedataMod objects
        /// </summary>
        public async Task<List<GenericGamedataMod>> GetDownloadableGenericModsAsync()
        {
            string json = await httpClient.GetStringAsync(Globals.GenericModsListUrl);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<GenericGamedataMod>>(json, options) ?? new List<GenericGamedataMod>();
        }





        // ================= SCANNING METHODS =================

        /// <summary>
        ///     I don't know if this is the best way, but this will get all of the maps that are inside the enabled and disabled folders
        /// </summary>
        public HashSet<string> GetAllKnownModFileNames()
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Maps
            foreach(var file in Directory.GetFiles(mapsEnabledPath, "*.scd"))
                known.Add(Path.GetFileName(file));

            foreach(var file in Directory.GetFiles(mapsDisabledPath, "*.scd"))
                known.Add(Path.GetFileName(file));

            // Generic mods. Every file type, not just .scd, otherwise a non-.scd file imported from a scan would
            // show up as unknown again on the next scan
            foreach(var file in Directory.GetFiles(genericModsEnabledPath, "*").Where(IsModFile))
                known.Add(Path.GetFileName(file));

            foreach(var file in Directory.GetFiles(genericModsDisabledPath, "*").Where(IsModFile))
                known.Add(Path.GetFileName(file));




            return known;
        }
    }
}