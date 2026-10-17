/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using SC2ModManager.Views;
using SC2ModManager.ViewModels;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This service does everything that has to do with the gamedata folder.
    ///     For now, each type of mod is going to need its own enable/disable methods.
    ///     Each mod is split up into labled sections, so whenever adding a mod type, copy one 
    ///     of the existing ones and modify it to fit the new mod type. The restore method is just for the original gamedata backup
    /// </summary>
    public class GamedataService
    {
        // ================= MAPS =================

        /// <summary>
        ///     Copies the map from Mods/Maps/Enabled into the game's gamedata folder.
        /// </summary>
        public void EnableMap(Map map, string mapsEnabledPath, string gameDataPath)
        {
            string source = Path.Combine(mapsEnabledPath, map.FileName);
            string destination = Path.Combine(gameDataPath, map.FileName);

            if (!File.Exists(source))
                throw new Exception($"Map not found in Enabled folder: {map.FileName}");

            File.Copy(source, destination, true);
        }

        /// <summary>
        ///     Removes the map from the game's gamedata folder.
        /// </summary>
        public void DisableMap(Map map, string gameDataPath)
        {
            string path = Path.Combine(gameDataPath, map.FileName);

            if (File.Exists(path))
                File.Delete(path);
        }

        // ================= GENERIC MODS =================

        /// <summary>
        ///     Copies the mod from Mods/GenericMods/Enabled into the game's gamedata folder.
        /// </summary>
        public void EnableGenericMod(GenericGamedataMod mod, string genericModsEnabledPath, string gameDataPath)
        {
            string source = Path.Combine(genericModsEnabledPath, mod.FileName);
            string destination = Path.Combine(gameDataPath, mod.FileName);

            if (!File.Exists(source))
                throw new Exception($"Mod not found in Enabled folder: {mod.FileName}");

            // Older versions let lua.scd/luo.scd get imported as generic mods. Copying one of those into
            // gamedata can crash the game, so they only ever get changed from the Hotkeys tab
            if (Globals.IsHotkeyManagedFile(mod.FileName))
                throw new Exception($"'{mod.FileName}' is one of the game's hotkey files and can't be enabled as a mod.");

            File.Copy(source, destination, true);
        }

        /// <summary>
        ///     Removes the mod from the game's gamedata folder.
        /// </summary>
        public void DisableGenericMod(GenericGamedataMod mod, string gameDataPath)
        {
            // Same as above, never delete a hotkey file from here. Deleting lua.scd/luo.scd can leave the game with no keymap
            if (Globals.IsHotkeyManagedFile(mod.FileName))
                return;

            string path = Path.Combine(gameDataPath, mod.FileName);

            if (File.Exists(path))
                File.Delete(path);
        }

        // ================= FULL GAMEDATA BACKUP =================
        // One-slot backup of the exact gamedata state (plus whether toc.win.bdf was in the game
        // root), taken right before Restore Original Game Files so the user can undo it. This is a
        // dumb file copy on purpose — it does not know or care about the hotkey mod, presets, or
        // anything else. Restoring it puts back exactly what was there, byte for byte.

        public class FullBackupInfo
        {
            public DateTime CreatedAt { get; set; }
            public bool TocWasPresent { get; set; }
        }

        // ================= PROGRESS WINDOW =================
        // The big gamedata jobs lock the main window while they run (on purpose, nobody should be enabling
        // maps or applying presets while gamedata is getting replaced) but the progress bar keeps moving and
        // has a Cancel button. The window used to be a loose popup you could ignore or close with the job
        // still going. Cancel works right up until the final folder swap, see RESTORE HELPERS

        private static ProgressWindow OpenProgressWindow(ProgressViewModel progressVm)
        {
            var window = new ProgressWindow { DataContext = progressVm };

            var owner = System.Windows.Application.Current?.MainWindow;
            if (owner != null && owner.IsVisible && !ReferenceEquals(owner, window))
            {
                window.Owner = owner;      // stays on top of the mod manager and centers on it
                owner.IsEnabled = false;   // nothing else can be clicked until the job is done
            }

            window.Show();
            return window;
        }

        private static void CloseProgressWindow(ProgressWindow window)
        {
            var owner = window.Owner;
            if (owner != null)
                owner.IsEnabled = true;

            window.CloseWhenDone();
            owner?.Activate();
        }

        private static string GetFullBackupDir() => Path.Combine(Globals.GetDataPath(), "FullGamedataBackup");
        private static string GetFullBackupGamedataDir() => Path.Combine(GetFullBackupDir(), "gamedata");
        private static string GetFullBackupInfoPath() => Path.Combine(GetFullBackupDir(), "backup_info.json");
        private static string GetFullBackupTocPath() => Path.Combine(GetFullBackupDir(), Globals.TocWinBdfName);

        /// <summary>
        ///     True if a complete full backup exists. The info file is written last during creation,
        ///     so if it's there the backup finished.
        /// </summary>
        public bool HasFullBackup() => File.Exists(GetFullBackupInfoPath());

        public FullBackupInfo? GetFullBackupInfo()
        {
            if (!HasFullBackup()) return null;

            try
            {
                return JsonSerializer.Deserialize<FullBackupInfo>(File.ReadAllText(GetFullBackupInfoPath()));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Copies the entire gamedata folder (and toc.win.bdf from the game root if present) into
        ///     the backup slot. Replaces any previous full backup — there is only ever one.
        /// </summary>
        public async Task CreateFullBackupAsync(string gameDataPath)
        {
            var progressVm = new ProgressViewModel { Status = "Backing up gamedata..." };
            var progressWindow = OpenProgressWindow(progressVm);
            var cancel = progressVm.CancellationToken;

            try
            {
                await Task.Run(() =>
                {
                    string backupDir = GetFullBackupDir();

                    // Build the new backup in its own folder first. The old backup used to get deleted right
                    // at the start, so if the new one failed partway (disk full, locked file) you ended up
                    // with no backup at all. Now the old one only goes away once the new one is complete
                    string buildingDir = backupDir + ".building";
                    string oldDir = backupDir + ".old";

                    // Leftovers from a crash. Force delete since backed up game files can be read-only, and
                    // copying keeps that flag, which makes a normal Directory.Delete throw
                    DeleteDirectoryForce(buildingDir);
                    DeleteDirectoryForce(oldDir);

                    try
                    {
                        string buildingGamedataDir = Path.Combine(buildingDir, "gamedata");
                        Directory.CreateDirectory(buildingGamedataDir);

                        var files = Directory.GetFiles(gameDataPath, "*", SearchOption.AllDirectories);
                        for (int i = 0; i < files.Length; i++)
                        {
                            // Cancelling a backup is always safe, the half built copy gets thrown away below
                            // and the old backup (if there is one) is still there
                            cancel.ThrowIfCancellationRequested();

                            string relative = Path.GetRelativePath(gameDataPath, files[i]);
                            string dest = Path.Combine(buildingGamedataDir, relative);

                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            File.Copy(files[i], dest);

                            progressVm.Progress = (int)((i + 1) * 100L / files.Length);
                            progressVm.Status = $"Backing up gamedata... {progressVm.Progress}%";
                        }

                        // Remember the toc.win.bdf state too, it lives in the game root next to gamedata
                        string gameRoot = Path.GetDirectoryName(gameDataPath)!;
                        string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);
                        bool tocPresent = File.Exists(tocPath);

                        if (tocPresent)
                            File.Copy(tocPath, Path.Combine(buildingDir, Globals.TocWinBdfName));

                        // Written last, this file existing is what marks the backup as complete
                        var info = new FullBackupInfo { CreatedAt = DateTime.Now, TocWasPresent = tocPresent };
                        File.WriteAllText(Path.Combine(buildingDir, Path.GetFileName(GetFullBackupInfoPath())),
                            JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch
                    {
                        // The new one didn't finish, throw it away and leave the old backup alone
                        try { DeleteDirectoryForce(buildingDir); } catch { }
                        throw;
                    }

                    // Last chance to cancel. The swap below is just two quick renames
                    if (!progressVm.TryLockCancel("Finishing up the backup..."))
                    {
                        try { DeleteDirectoryForce(buildingDir); } catch { }
                        throw new OperationCanceledException();
                    }

                    // New backup is complete, swap it in. The old one gets renamed out of the way first so
                    // there's never a moment with no complete backup on disk
                    if (Directory.Exists(backupDir))
                        Directory.Move(backupDir, oldDir);

                    Directory.Move(buildingDir, backupDir);

                    try { DeleteDirectoryForce(oldDir); } catch { }
                });
            }
            finally
            {
                CloseProgressWindow(progressWindow);
            }
        }

        /// <summary>
        ///     Puts gamedata (and the toc.win.bdf state) back exactly how it was when the full backup
        ///     was taken. The backup is kept afterwards so this can be done again if needed.
        /// </summary>
        /// <returns>
        ///     Where their own toc.win.bdf got copied to if the restore was about to replace or delete it,
        ///     or null if that didn't happen.
        /// </returns>
        public async Task<string?> RestoreFullBackupAsync(string gameDataPath)
        {
            string? savedToc = null;

            var info = GetFullBackupInfo()
                ?? throw new InvalidOperationException("No full gamedata backup exists.");

            var progressVm = new ProgressViewModel { Status = "Restoring backup..." };
            var progressWindow = OpenProgressWindow(progressVm);
            var cancel = progressVm.CancellationToken;
            string stagingDir = StagingGamedataPath(gameDataPath);

            try
            {
                await Task.Run(() =>
                {
                    string backupGamedataDir = GetFullBackupGamedataDir();
                    var files = Directory.GetFiles(backupGamedataDir, "*", SearchOption.AllDirectories);
                    long totalBytes = files.Sum(f => new FileInfo(f).Length);

                    RecoverInterruptedSwap(gameDataPath);
                    EnsureFreeSpace(gameDataPath, totalBytes);

                    // 1. Copy the backup into a new folder next to gamedata. The real gamedata isn't touched yet,
                    //    so cancelling or failing here just throws the new folder away and nothing changed
                    try
                    {
                        Directory.CreateDirectory(stagingDir);

                        for (int i = 0; i < files.Length; i++)
                        {
                            cancel.ThrowIfCancellationRequested();

                            string relative = Path.GetRelativePath(backupGamedataDir, files[i]);
                            string dest = Path.Combine(stagingDir, relative);

                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            File.Copy(files[i], dest, overwrite: true);

                            progressVm.Progress = (int)((i + 1) * 100L / files.Length);
                            progressVm.Status = $"Restoring backup... {progressVm.Progress}%";
                        }

                        // 2. Make sure the copy came out complete before it replaces anything
                        VerifyStagedCopy(stagingDir, files.Length, totalBytes);
                    }
                    catch
                    {
                        DeleteDirectoryForce(stagingDir);
                        throw;
                    }

                    // 3. Swap it in. Two quick renames, so cancel turns off here
                    if (!progressVm.TryLockCancel("Finishing up..."))
                    {
                        DeleteDirectoryForce(stagingDir);
                        throw new OperationCanceledException();
                    }

                    progressVm.Status = "Swapping in the restored files...";
                    SwapInStagedGamedata(gameDataPath, stagingDir);

                    // Put toc.win.bdf back in the exact state it was in — present or not
                    string gameRoot = Path.GetDirectoryName(gameDataPath)!;
                    string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);

                    // If the toc in the game right now is going to get replaced or deleted, and it isn't the
                    // hotkey mod's, save a copy as toc.win.bdf.bak first so we never lose someone's own toc
                    bool restoringSameToc = info.TocWasPresent && File.Exists(tocPath) && File.Exists(GetFullBackupTocPath()) &&
                        File.ReadAllBytes(tocPath).AsSpan().SequenceEqual(File.ReadAllBytes(GetFullBackupTocPath()));
                    if (!restoringSameToc)
                        savedToc = HotkeyService.BackUpTocIfNotOurs(gameRoot);

                    if (info.TocWasPresent && File.Exists(GetFullBackupTocPath()))
                        File.Copy(GetFullBackupTocPath(), tocPath, overwrite: true);
                    else if (!info.TocWasPresent && File.Exists(tocPath))
                        File.Delete(tocPath);

                    // 4. The restored copy is in place and checked, so now the old gamedata can go
                    progressVm.Status = "Cleaning up...";
                    DeleteOldGamedata(gameDataPath);
                });
            }
            finally
            {
                CloseProgressWindow(progressWindow);
            }

            return savedToc;
        }

        // ================= RESTORE HELPERS =================
        // Both restores build the new gamedata in a folder next to the real one, check it, then swap the folders
        // with two renames and delete the old one. The old way wiped gamedata first and then filled it back in,
        // so cancelling or any failure partway left the game half wiped. This needs room for one extra copy of
        // gamedata while it runs (about 4 GB), which EnsureFreeSpace checks before starting

        private static string StagingGamedataPath(string gameDataPath) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDataPath)) + ".sc2mm-new";

        private static string OldGamedataPath(string gameDataPath) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDataPath)) + ".sc2mm-old";

        /// <summary>
        ///     Cleans up after a restore that got interrupted (crash, power loss, killed). Safe to call any time.
        /// </summary>
        public static void RecoverInterruptedSwap(string gameDataPath)
        {
            if (string.IsNullOrEmpty(gameDataPath))
                return;

            string staging = StagingGamedataPath(gameDataPath);
            string old = OldGamedataPath(gameDataPath);

            // It died between the two renames, so gamedata is missing but the old one is right there. Put it back
            if (!Directory.Exists(gameDataPath) && Directory.Exists(old))
                Directory.Move(old, gameDataPath);

            // A new copy that never got swapped in
            if (Directory.Exists(staging))
                DeleteDirectoryForce(staging);

            // The swap finished but deleting the old one didn't (locked file or whatever), finish that now
            if (Directory.Exists(old) && Directory.Exists(gameDataPath))
                DeleteDirectoryForce(old);
        }

        private static void EnsureFreeSpace(string gameDataPath, long bytesNeeded)
        {
            string root = Path.GetPathRoot(Path.GetFullPath(gameDataPath))!;
            long free = new DriveInfo(root).AvailableFreeSpace;
            long margin = 500L * 1024 * 1024; // a little extra so we don't fill the drive right to the top

            if (free < bytesNeeded + margin)
                throw new IOException(
                    $"There isn't enough free space on {root} to restore safely. It needs about {(bytesNeeded + margin) / (1024.0 * 1024 * 1024):0.0} GB free " +
                    $"and there's {free / (1024.0 * 1024 * 1024):0.0} GB. Nothing was changed.");
        }

        /// <summary>
        ///     Makes sure the new copy has every file and the right total size before it replaces gamedata.
        /// </summary>
        private static void VerifyStagedCopy(string stagingDir, int expectedFiles, long expectedBytes)
        {
            var files = Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories);
            long bytes = files.Sum(f => new FileInfo(f).Length);

            if (files.Length != expectedFiles || bytes != expectedBytes)
                throw new IOException(
                    $"The restored copy didn't come out complete ({files.Length} of {expectedFiles} files), so nothing was changed.");
        }

        /// <summary>
        ///     Moves gamedata out of the way and the new copy into its place. If the second step fails, the
        ///     first one gets undone so gamedata is never left missing. If the game has files open, the first
        ///     rename fails before anything changed.
        /// </summary>
        private static void SwapInStagedGamedata(string gameDataPath, string stagingDir)
        {
            string old = OldGamedataPath(gameDataPath);
            if (Directory.Exists(old))
                DeleteDirectoryForce(old);

            bool hadGamedata = Directory.Exists(gameDataPath);
            if (hadGamedata)
            {
                try
                {
                    Directory.Move(gameDataPath, old);
                }
                catch (Exception ex)
                {
                    DeleteDirectoryForce(stagingDir);
                    throw new IOException(
                        $"Couldn't move the current gamedata out of the way ({ex.Message}). Make sure the game is closed. Nothing was changed.", ex);
                }
            }

            try
            {
                Directory.Move(stagingDir, gameDataPath);
            }
            catch
            {
                if (hadGamedata)
                    Directory.Move(old, gameDataPath);
                DeleteDirectoryForce(stagingDir);
                throw;
            }
        }

        /// <summary>
        ///     Deletes the old gamedata after a successful swap. If something in it is locked it just stays, and
        ///     RecoverInterruptedSwap finishes deleting it next time. The restore itself still worked either way.
        /// </summary>
        private static void DeleteOldGamedata(string gameDataPath)
        {
            try { DeleteDirectoryForce(OldGamedataPath(gameDataPath)); } catch { }
        }

        private static void DeleteDirectoryForce(string dir)
        {
            if (!Directory.Exists(dir))
                return;

            // Read-only files make Directory.Delete throw
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }

            Directory.Delete(dir, recursive: true);
        }

        // ================= RESTORE =================

        /// <summary>
        ///     Restores the original gamedata from GitHub. Nothing in the existing gamedata
        ///     folder is deleted until BOTH backup archives have been fully downloaded and
        ///     validated — a failed or interrupted download leaves the current gamedata intact.
        /// </summary>
        public async Task RestoreOriginalGamedataAsync(string gameDataPath)
        {
            var progressVm = new ProgressViewModel { Status = "Restoring original gamedata..." };
            var progressWindow = OpenProgressWindow(progressVm);
            var cancel = progressVm.CancellationToken;

            string tempDir = Path.Combine(Path.GetTempPath(), "SC2ModManager_GameData");
            string zip1Path = Path.Combine(tempDir, "gamedata1.zip");
            string zip2Path = Path.Combine(tempDir, "gamedata2.zip");

            try
            {
                Directory.CreateDirectory(tempDir);

                // 1. Download both archives completely BEFORE touching gamedata. Cancelling here is safe,
                //    nothing in the game has changed yet
                var progress = new Progress<int>(value =>
                {
                    progressVm.Progress = value;
                    progressVm.Status = $"Downloading... {value}%";
                });

                using (HttpClient client = new HttpClient())
                {
                    await Task.WhenAll(
                        DownloadToFileAsync(client, Globals.GameDataBackupPt1GithubReleaseUrl, zip1Path, progress, cancel),
                        DownloadToFileAsync(client, Globals.GameDataBackupPt2GithubReleaseUrl, zip2Path, progress, cancel)
                    );
                }

                // 2. Verify both archives are readable zips before deleting anything
                progressVm.Status = "Validating downloads...";
                ValidateZipArchive(zip1Path);
                ValidateZipArchive(zip2Path);

                // Runs in the background so the progress bar keeps moving. The extract used to run on the main
                // thread, which froze the window ("Not responding") the whole time
                string stagingDir = StagingGamedataPath(gameDataPath);
                await Task.Run(() =>
                {
                    // What the restored gamedata should end up with. If both zips have the same file, the second
                    // one wins, same as extracting them on top of each other
                    var expected = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    int totalEntries = AddZipFiles(zip1Path, expected) + AddZipFiles(zip2Path, expected);
                    long expectedBytes = expected.Values.Sum();

                    RecoverInterruptedSwap(gameDataPath);
                    EnsureFreeSpace(gameDataPath, expectedBytes);

                    // 3. Extract into a new folder next to gamedata. The real gamedata isn't touched yet, so
                    //    cancelling or failing here just throws the new folder away and nothing changed
                    try
                    {
                        Directory.CreateDirectory(stagingDir);

                        progressVm.Progress = 0;
                        int done = 0;

                        void ReportExtract()
                        {
                            cancel.ThrowIfCancellationRequested();

                            done++;
                            int percent = totalEntries == 0 ? 100 : (int)(done * 100L / totalEntries);
                            if (percent != progressVm.Progress)
                            {
                                progressVm.Progress = percent;
                                progressVm.Status = $"Extracting files... {percent}%";
                            }
                        }

                        ExtractZipToDirectory(zip1Path, stagingDir, ReportExtract);
                        ExtractZipToDirectory(zip2Path, stagingDir, ReportExtract);

                        // Make sure everything came out before it replaces anything
                        VerifyStagedCopy(stagingDir, expected.Count, expectedBytes);
                    }
                    catch
                    {
                        DeleteDirectoryForce(stagingDir);
                        throw;
                    }

                    // 4. Swap it in. Two quick renames, so cancel turns off here
                    if (!progressVm.TryLockCancel("Finishing up..."))
                    {
                        DeleteDirectoryForce(stagingDir);
                        throw new OperationCanceledException();
                    }

                    progressVm.Status = "Swapping in the original files...";
                    SwapInStagedGamedata(gameDataPath, stagingDir);

                    // 5. The restore brings back the original lua.scd, so the hotkey mod isn't active
                    //    anymore. Clean up the lua/luo/toc files, mainly to remove a leftover
                    //    toc.win.bdf from the game root which would break the game.
                    new HotkeyService().ReconcileNormalHotkeyState(gameDataPath);

                    // 6. The restored copy is in place and checked, so now the old gamedata can go
                    progressVm.Status = "Cleaning up...";
                    DeleteOldGamedata(gameDataPath);
                });

                await Task.Delay(500);
            }
            finally
            {
                try
                {
                    if (File.Exists(zip1Path)) File.Delete(zip1Path);
                    if (File.Exists(zip2Path)) File.Delete(zip2Path);
                }
                catch { }

                // Always close the progress window, even when the restore fails or gets cancelled
                CloseProgressWindow(progressWindow);
            }
        }

        /// <summary>
        ///     Adds each file in the zip (name and size) to the expected list and returns how many entries the zip
        ///     has in total, folders included, for the progress bar.
        /// </summary>
        private static int AddZipFiles(string zipPath, Dictionary<string, long> expected)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                    expected[entry.FullName.Replace('\\', '/')] = entry.Length;
            }
            return archive.Entries.Count;
        }

        /// <summary>
        ///     Throws if the file isn't a readable zip with stuff in it (like when a failed download
        ///     saves an html error page instead of the actual zip).
        /// </summary>
        private static void ValidateZipArchive(string zipPath)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count == 0)
                throw new InvalidDataException($"Downloaded archive is empty: {Path.GetFileName(zipPath)}");
        }

        private async Task DownloadToFileAsync(HttpClient client, string url, string filePath, IProgress<int> progress,
            System.Threading.CancellationToken cancel = default)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(cancel);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalRead = 0L;
            var buffer = new byte[81920];
            int read;

            // Passing the cancel token here is what makes the Cancel button stop the download right away
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancel)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancel);
                totalRead += read;

                if (totalBytes > 0)
                    progress?.Report((int)(totalRead * 100 / totalBytes));
            }
        }

        private void ExtractZipToDirectory(string zipPath, string destination, Action? onEntryDone = null)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                string fullPath = Path.Combine(destination, entry.FullName);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(fullPath);
                    onEntryDone?.Invoke();
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                entry.ExtractToFile(fullPath, true);
                onEntryDone?.Invoke();
            }
        }
    }
}