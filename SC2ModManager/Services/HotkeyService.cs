/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: May 12, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SC2ModManager.Services
{
    public class HotkeyService
    {
        // ── Internal lua paths inside each archive ──────────────────────────────

        private static readonly string[] NormalLuaPaths =
        [
            "lua/keymap/defaultKeyMap.lua",
            "lua/keymap/keyactions.lua",
            "lua/keymap/keydescriptions.lua"
        ];

        private const string BuildModeLuaPath = "mods/DLC1/shadow/lua/ui/game/buildmodedata.lua";

        // ── Installation detection ───────────────────────────────────────────────

        /// <summary>
        ///     Returns true if the local .scd file exists for the given mod type
        /// </summary>
        public bool IsModInstalled(HotkeyModType modType)
        {
            string path = GetLocalScdPath(modType);
            return File.Exists(path);
        }

        // ── File management ──────────────────────────────────────────────────────

        /// <summary>
        ///     Copies a user-provided .scd file into {DataPath}/HotkeyMods/ and creates a backup
        /// </summary>
        public void ImportModFile(string sourceScdPath, HotkeyModType modType)
        {
            string destDir = Globals.GetHotkeyModsPath();
            Directory.CreateDirectory(destDir);

            string dest = GetLocalScdPath(modType);
            File.Copy(sourceScdPath, dest, overwrite: true);

            CreateBackupsIfAbsent(modType);
        }

        /// <summary>
        ///     Adopts the .scd currently installed in gamedata as the local working copy when
        ///     it differs from ours. The hotkey editor edits the LOCAL copy and ApplyToGamedata
        ///     copies it over gamedata — so if the user replaced the installed file with their
        ///     own version (e.g. a custom luo.scd), editing/saving hotkeys would silently
        ///     overwrite their file with our stale copy. Syncing gamedata → local first means
        ///     the editor always operates on whatever is actually installed.
        ///     Call this before loading the editor. Safe no-op when nothing is installed.
        /// </summary>
        public void SyncLocalFromGamedata(HotkeyModType modType, string gamedataPath)
        {
            if (string.IsNullOrEmpty(gamedataPath) || !Directory.Exists(gamedataPath))
                return;

            string scdName = modType == HotkeyModType.NormalHotkey
                ? Globals.NormalHotkeyScdName
                : Globals.BuildModeScdName;

            string installedPath = Path.Combine(gamedataPath, scdName);
            if (!File.Exists(installedPath))
                return;

            string localPath = GetLocalScdPath(modType);

            if (File.Exists(localPath))
            {
                var installed = new FileInfo(installedPath);
                var local = new FileInfo(localPath);

                // ApplyToGamedata uses File.Copy, which preserves size and write time — so a
                // mismatch here means the installed file was changed outside the manager.
                if (installed.Length == local.Length &&
                    installed.LastWriteTimeUtc == local.LastWriteTimeUtc)
                    return;

                // Preserve the pristine downloaded copy as the restore point before adopting
                CreateBackupsIfAbsent(modType);
            }
            else
            {
                Directory.CreateDirectory(Globals.GetHotkeyModsPath());
            }

            File.Copy(installedPath, localPath, overwrite: true);

            // If no backup existed at all (file was never imported through the manager),
            // the adopted file becomes its own "original". No-op when a backup exists.
            CreateBackupsIfAbsent(modType);
        }

        /// <summary>
        ///     Applies the local mod file to the game.
        ///     Normal mod: backs up and deletes lua.scd, then places luo.scd and toc.win.bdf.
        ///     Build mode mod: places BuildmodeHotkeys.scd in gamedata.
        /// </summary>
        public void ApplyToGamedata(HotkeyModType modType, string gamedataPath)
        {
            string src = GetLocalScdPath(modType);
            if (!File.Exists(src))
                throw new FileNotFoundException($"Local mod file not found: {src}");

            string scdName = modType == HotkeyModType.NormalHotkey
                ? Globals.NormalHotkeyScdName
                : Globals.BuildModeScdName;

            Directory.CreateDirectory(gamedataPath);

            if (modType == HotkeyModType.NormalHotkey)
            {
                // Back up lua.scd then delete it — the mod loads luo.scd via toc.win.bdf instead
                string luaScdPath = Path.Combine(gamedataPath, Globals.LuaScdName);
                if (File.Exists(luaScdPath))
                {
                    string backupDir = Globals.GetHotkeyModsBackupPath();
                    Directory.CreateDirectory(backupDir);

                    string luaBackup = GetLuaScdBackupPath();
                    if (!File.Exists(luaBackup))
                        File.Copy(luaScdPath, luaBackup);

                    File.Delete(luaScdPath);
                }

                // Copy toc.win.bdf to game root
                string localBdf = Globals.GetLocalTocBdfPath();
                if (File.Exists(localBdf))
                {
                    string gameRoot = Path.GetDirectoryName(gamedataPath)!;
                    File.Copy(localBdf, Path.Combine(gameRoot, Globals.TocWinBdfName), overwrite: true);
                }
            }

            File.Copy(src, Path.Combine(gamedataPath, scdName), overwrite: true);
        }

        //  ======================  Backup System ====================== 

        /// <summary>
        ///     Copies the local .scd to the backups folder as {name}_Original_MadeBy_HotkeyMod.scd.
        ///     Never overwrites an existing backup.
        /// </summary>
        public void CreateBackupsIfAbsent(HotkeyModType modType)
        {
            string localScd = GetLocalScdPath(modType);
            if (!File.Exists(localScd)) return;

            string backupDir = Globals.GetHotkeyModsBackupPath();
            Directory.CreateDirectory(backupDir);

            string backupPath = GetBackupScdPath(modType);
            if (File.Exists(backupPath)) return; // never overwrite

            File.Copy(localScd, backupPath);
        }

        /// <summary>
        ///     Returns true if a backup .scd exists for the given mod type.
        /// </summary>
        public bool HasBackups(HotkeyModType modType) => File.Exists(GetBackupScdPath(modType));

        /// <summary>
        ///     Copies the backup .scd over the local .scd, restoring it to the original downloaded state.
        /// </summary>
        public void RestoreFromBackups(HotkeyModType modType)
        {
            string backupScd = GetBackupScdPath(modType);
            if (!File.Exists(backupScd)) return;

            string localScd = GetLocalScdPath(modType);
            File.Copy(backupScd, localScd, overwrite: true);
        }

        // ====================== Uninstall ====================== 

        /// <summary>
        ///     Uninstalls the mod:
        ///     Normal mod: deletes luo.scd from gamedata, restores lua.scd, deletes toc.win.bdf from game root.
        ///     Build mode mod: deletes BuildmodeHotkeys.scd from gamedata.
        ///     Both: deletes local copy and backup.
        /// </summary>
        public void UninstallMod(HotkeyModType modType, string gamedataPath)
        {
            if (modType == HotkeyModType.NormalHotkey)
            {
                // 1. Delete luo.scd from gamedata
                string luoPath = Path.Combine(gamedataPath, Globals.NormalHotkeyScdName);
                if (File.Exists(luoPath)) File.Delete(luoPath);

                // 2. Restore original lua.scd from backup
                string luaBackup = GetLuaScdBackupPath();
                if (File.Exists(luaBackup))
                {
                    Directory.CreateDirectory(gamedataPath);
                    File.Copy(luaBackup, Path.Combine(gamedataPath, Globals.LuaScdName), overwrite: true);
                    File.Delete(luaBackup);
                }

                // 3. Delete toc.win.bdf from game root
                string gameRoot = Path.GetDirectoryName(gamedataPath)!;
                string bdfFile = Path.Combine(gameRoot, Globals.TocWinBdfName);
                if (File.Exists(bdfFile)) File.Delete(bdfFile);
            }
            else
            {
                // Build mode mod: just delete BuildmodeHotkeys.scd from gamedata
                string bmPath = Path.Combine(gamedataPath, Globals.BuildModeScdName);
                if (File.Exists(bmPath)) File.Delete(bmPath);
            }

            // Delete local .scd copy and its backup
            string localScd = GetLocalScdPath(modType);
            if (File.Exists(localScd)) File.Delete(localScd);

            string backupScd = GetBackupScdPath(modType);
            if (File.Exists(backupScd)) File.Delete(backupScd);
        }

        // ======================  State Reconciliation ======================

        /// <summary>
        ///     Forces the normal-hotkey gamedata files into a valid combination so the game
        ///     never sees a broken state. The game requires EXACTLY ONE of {lua.scd, luo.scd}
        ///     in gamedata, and toc.win.bdf — which lives in the game ROOT, next to the
        ///     gamedata folder — must be present if and only if luo.scd is active:
        ///
        ///       • luo.scd present, lua.scd absent  → hotkey mod active   → toc.win.bdf present
        ///       • luo.scd absent                   → hotkey mod inactive → toc.win.bdf removed,
        ///                                            and lua.scd restored from backup if missing
        ///       • both present                     → invalid → keep luo.scd, back up & delete lua.scd
        ///
        ///     This is a self-healing safety net (e.g. after a preset swaps gamedata files
        ///     around) that guarantees smooth transitions. It is safe to call at any time.
        /// </summary>
        public void ReconcileNormalHotkeyState(string gamedataPath)
        {
            if (string.IsNullOrEmpty(gamedataPath) || !Directory.Exists(gamedataPath))
                return;

            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            string luaPath = Path.Combine(gamedataPath, Globals.LuaScdName);
            string luoPath = Path.Combine(gamedataPath, Globals.NormalHotkeyScdName);
            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);

            bool luoPresent = File.Exists(luoPath);
            bool luaPresent = File.Exists(luaPath);

            if (luoPresent)
            {
                // Hotkey mod is active. lua.scd must NOT coexist with luo.scd — back it up
                // (without clobbering an existing backup) and remove it.
                if (luaPresent)
                {
                    string backupDir = Globals.GetHotkeyModsBackupPath();
                    Directory.CreateDirectory(backupDir);

                    string luaBackup = GetLuaScdBackupPath();
                    if (!File.Exists(luaBackup))
                        File.Copy(luaPath, luaBackup);

                    File.Delete(luaPath);
                }

                // toc.win.bdf must be present in the game root for luo.scd to load.
                if (!File.Exists(tocPath))
                {
                    string localBdf = Globals.GetLocalTocBdfPath();
                    if (File.Exists(localBdf))
                        File.Copy(localBdf, tocPath, overwrite: true);
                }
            }
            else
            {
                // Hotkey mod is inactive. Never leave the toc behind without luo.scd — it
                // would point the game at a file that no longer exists.
                if (File.Exists(tocPath))
                    File.Delete(tocPath);

                // The game still needs its keymap, so restore the original lua.scd if it's gone.
                if (!luaPresent)
                {
                    string luaBackup = GetLuaScdBackupPath();
                    if (File.Exists(luaBackup))
                        File.Copy(luaBackup, luaPath, overwrite: true);
                }
            }
        }

        // ======================  Normal Hotkey Parsing ======================

        /// <summary>
        ///     Parses defaultKeyMap.lua from luo.scd and returns all hotkey entries across all three
        ///     sections (Main, Tooltip, Debug).
        /// </summary>
        public List<HotkeyEntry> ReadDefaultKeyMap(HotkeyModType modType = HotkeyModType.NormalHotkey)
        {
            string scdPath = GetLocalScdPath(modType);
            string lua = ReadZipEntry(scdPath, "lua/keymap/defaultKeyMap.lua");
            var descriptions = ReadKeyDescriptions(modType);

            return ParseDefaultKeyMap(lua, descriptions);
        }

        /// <summary>
        ///     Writes the provided hotkey entries back into defaultKeyMap.lua inside luo.scd.
        ///     Preserves all other content in the file (non-entry lines are kept verbatim).
        /// </summary>
        public void WriteDefaultKeyMap(List<HotkeyEntry> entries, HotkeyModType modType = HotkeyModType.NormalHotkey)
        {
            string scdPath = GetLocalScdPath(modType);
            string original = ReadZipEntry(scdPath, "lua/keymap/defaultKeyMap.lua");
            string updated = RebuildDefaultKeyMap(original, entries);

            WriteZipEntry(scdPath, "lua/keymap/defaultKeyMap.lua", updated);
        }

        /// <summary>
        ///     Reads keydescriptions.lua from luo.scd and returns a dictionary of command → description.
        /// </summary>
        public Dictionary<string, string> ReadKeyDescriptions(HotkeyModType modType = HotkeyModType.NormalHotkey)
        {
            string scdPath = GetLocalScdPath(modType);
            string lua = ReadZipEntry(scdPath, "lua/keymap/keydescriptions.lua");

            return ParseDescriptions(lua);
        }

        /// <summary>
        ///     Returns the raw text of any lua file inside the archive
        /// </summary>
        public string ReadRawLuaFile(HotkeyModType modType, string internalPath)
        {
            return ReadZipEntry(GetLocalScdPath(modType), internalPath);
        }

        /// <summary>
        ///     Writes raw text back to a lua file inside the archive
        /// </summary>
        public void WriteRawLuaFile(HotkeyModType modType, string internalPath, string content)
        {
            WriteZipEntry(GetLocalScdPath(modType), internalPath, content);
        }

        // ======================  Build Mode Parsing ====================== 

        /// <summary>
        ///     Parses buildmodedata.lua from BuildmodeHotkeys.scd and returns all build mode entries
        ///     with Faction and Category populated per entry.
        /// </summary>
        public List<BuildModeEntry> ReadBuildModeData()
        {
            string scdPath = GetLocalScdPath(HotkeyModType.BuildModeHotkey);
            string lua = ReadZipEntry(scdPath, BuildModeLuaPath);

            return ParseBuildModeData(lua);
        }

        /// <summary>
        ///     Writes build mode entries back into buildmodedata.lua inside BuildmodeHotkeys.scd.
        /// </summary>
        public void WriteBuildModeData(List<BuildModeEntry> entries)
        {
            string scdPath = GetLocalScdPath(HotkeyModType.BuildModeHotkey);
            string original = ReadZipEntry(scdPath, BuildModeLuaPath);
            string updated = RebuildBuildModeData(original, entries);

            WriteZipEntry(scdPath, BuildModeLuaPath, updated);
        }

        // ======================  Lua Parsing Helpers ======================

        private static List<HotkeyEntry> ParseDefaultKeyMap(string lua, Dictionary<string, string> descriptions)
        {
            var entries = new List<HotkeyEntry>();

            // Matches lines like:  ['R'] = 'repair',   or  ['Ctrl-1'] = 'set_group1',
            // AI helped generate this Regex and it seems to work. Haven't touched this stuff since my Models of Computation class
            var entryRegex = new Regex(
                @"\['([^']+)'\]\s*=\s*'([^']+)'",
                RegexOptions.Compiled);

            HotkeySection currentSection = HotkeySection.Main;

            foreach (string rawLine in lua.Split('\n'))
            {
                string line = rawLine.TrimEnd();

                // Comment lines must be skipped entirely. They must not drive section
                // transitions — a comment containing "defaultKeyMap" would otherwise flip
                // the current section back to Main mid-Tooltip/Debug — and, just as
                // importantly, a commented-out binding such as  --['Tab'] = 'next_cam_position'
                // must NOT be parsed as a real entry. The mod ships several disabled
                // bindings this way; parsing them surfaces phantom hotkeys for commands
                // that aren't actually bound.
                bool isComment = line.TrimStart().StartsWith("--");
                if (isComment) continue;

                // Detect section transitions
                if (line.Contains("keymapTooltipHotkeys"))
                    currentSection = HotkeySection.Tooltip;
                else if (line.Contains("debugKeyMap"))
                    currentSection = HotkeySection.Debug;
                else if (line.Contains("defaultKeyMap"))
                    currentSection = HotkeySection.Main;

                var m = entryRegex.Match(line);
                if (!m.Success) continue;

                string keyCombo = m.Groups[1].Value;
                string command = m.Groups[2].Value;
                descriptions.TryGetValue(command, out string? desc);

                entries.Add(new HotkeyEntry
                {
                    KeyCombo = keyCombo,
                    OriginalKeyCombo = keyCombo,
                    Command = command,
                    Description = desc ?? string.Empty,
                    Section = currentSection
                });
            }

            return entries;
        }

        private static string RebuildDefaultKeyMap(string originalLua, List<HotkeyEntry> entries)
        {
            // Build lookup: (originalKey, command, section) → new keycombo.
            // Keying by the original key (not just command) lets us correctly handle
            // files where the same command has multiple bindings in the same section
            // (e.g. ['B'] = 'build' and ['Shift-B'] = 'build').  A command-only key
            // would use last-write-wins and silently corrupt the first binding on save.
            var lookup = new Dictionary<(string origKey, string command, HotkeySection section), string>();
            foreach (var e in entries)
                lookup[(e.OriginalKeyCombo, e.Command, e.Section)] = e.KeyCombo;

            var entryRegex = new Regex(
                @"(\['[^']+'\](\s*)=\s*)'([^']+)'",
                RegexOptions.Compiled);

            HotkeySection currentSection = HotkeySection.Main;
            var sb = new StringBuilder();

            foreach (string rawLine in originalLua.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');

                // Leave comment lines untouched — don't update section state or rewrite keys
                if (!line.TrimStart().StartsWith("--"))
                {
                    if (line.Contains("keymapTooltipHotkeys"))
                        currentSection = HotkeySection.Tooltip;
                    else if (line.Contains("debugKeyMap"))
                        currentSection = HotkeySection.Debug;
                    else if (line.Contains("defaultKeyMap"))
                        currentSection = HotkeySection.Main;

                    // Try to match: ['OldKey'] = 'command'
                    // We need to swap the key, not the command
                    // The format is ['key'] = 'command' — key is group 1 of the original parse
                    // In rebuild: we look for a line whose command value (right side) matches an entry
                    var cmdMatch = new Regex(@"\['([^']+)'\]\s*=\s*'([^']+)'").Match(line);
                    if (cmdMatch.Success)
                    {
                        string currentKey = cmdMatch.Groups[1].Value;
                        string command    = cmdMatch.Groups[2].Value;
                        if (lookup.TryGetValue((currentKey, command, currentSection), out string? newKey))
                        {
                            // Replace the key on the left side: ['OldKey'] -> ['NewKey']
                            line = new Regex(@"\['[^']+'\]").Replace(line, $"['{newKey}']", 1);
                        }
                    }
                }

                sb.Append(line).Append('\n');
            }

            return sb.ToString();
        }

        private static Dictionary<string, string> ParseDescriptions(string lua)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            var regex = new Regex(@"\['([^']+)'\]\s*=\s*'([^']*)'", RegexOptions.Compiled);

            foreach (Match m in regex.Matches(lua))
                dict[m.Groups[1].Value] = m.Groups[2].Value;

            return dict;
        }

        private static List<BuildModeEntry> ParseBuildModeData(string lua)
        {
            var entries = new List<BuildModeEntry>();

            // Maps lua local variable name prefix to faction
            // UEF locals start with "U", Cybran with "C", Illuminate with "I"
            // e.g. local UBasicEngineering = {
            var tableHeaderRegex = new Regex(
                @"^\s*local\s+([UCI])(\w+)\s*=\s*\{",
                RegexOptions.Compiled | RegexOptions.Multiline);

            var entryRegex = new Regex(
                @"\['([A-Za-z0-9\-]+)'\]\s*=\s*'([^']+)'(?:\s*,?\s*--\s*(.*))?",
                RegexOptions.Compiled);

            // Split into table blocks by finding each header
            var headers = tableHeaderRegex.Matches(lua).Cast<Match>().ToList();

            for (int i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                int blockStart = header.Index + header.Length;
                int blockEnd = i + 1 < headers.Count ? headers[i + 1].Index : lua.Length;
                string block = lua[blockStart..blockEnd];

                char factionChar = header.Groups[1].Value[0];
                string category = header.Groups[2].Value;

                BuildModeFaction faction = factionChar switch
                {
                    'U' => BuildModeFaction.UEF,
                    'C' => BuildModeFaction.Cybran,
                    _ => BuildModeFaction.Illuminate
                };

                foreach (Match m in entryRegex.Matches(block))
                {
                    entries.Add(new BuildModeEntry
                    {
                        Faction = faction,
                        Category = category,
                        Key = m.Groups[1].Value,
                        UnitId = m.Groups[2].Value,
                        Comment = m.Groups[3].Success ? m.Groups[3].Value.Trim() : string.Empty
                    });
                }
            }

            return entries;
        }

        private static string RebuildBuildModeData(string originalLua, List<BuildModeEntry> entries)
        {
            // Build lookup: (faction, category, unitId) -> new key
            var lookup = new Dictionary<(BuildModeFaction faction, string category, string unitId), string>();
            foreach (var e in entries)
                lookup[(e.Faction, e.Category, e.UnitId)] = e.Key;

            var tableHeaderRegex = new Regex(
                @"^\s*local\s+([UCI])(\w+)\s*=\s*\{",
                RegexOptions.Compiled | RegexOptions.Multiline);

            var entryRegex = new Regex(
                @"(\['(?:[A-Za-z0-9\-]+)'\])(\s*=\s*')([^']+)(')",
                RegexOptions.Compiled);

            BuildModeFaction currentFaction = BuildModeFaction.UEF;
            string currentCategory = string.Empty;
            var sb = new StringBuilder();

            foreach (string rawLine in originalLua.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');

                var headerMatch = tableHeaderRegex.Match(line);
                if (headerMatch.Success)
                {
                    char fc = headerMatch.Groups[1].Value[0];
                    currentFaction = fc switch
                    {
                        'U' => BuildModeFaction.UEF,
                        'C' => BuildModeFaction.Cybran,
                        _ => BuildModeFaction.Illuminate
                    };
                    currentCategory = headerMatch.Groups[2].Value;
                }

                // Replace key for lines like  ['D'] = 'uub0001', -- Land Factory
                var em = entryRegex.Match(line);
                if (em.Success)
                {
                    string unitId = em.Groups[3].Value;
                    if (lookup.TryGetValue((currentFaction, currentCategory, unitId), out string? newKey))
                    {
                        line = entryRegex.Replace(line,
                            $"['{newKey}']{em.Groups[2].Value}{unitId}{em.Groups[4].Value}");
                    }
                }

                sb.Append(line).Append('\n');
            }

            return sb.ToString();
        }

        // ======================  ZIP helpers ====================== 

        private static string ReadZipEntry(string scdPath, string internalPath)
        {
            using var archive = ZipFile.OpenRead(scdPath);
            var entry = FindEntry(archive, internalPath)
                ?? throw new FileNotFoundException($"Entry '{internalPath}' not found in '{scdPath}'.");

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            return reader.ReadToEnd();
        }

        private static void WriteZipEntry(string scdPath, string internalPath, string content)
        {
            // .NET's ZipArchiveMode.Create writes "data descriptor" records (bit 3 set,
            // sizes = 0 in local file headers) because it streams forward without seeking back.
            // The SC2 SCD loader is a 2010-era ZIP reader that does NOT support data descriptors —
            // it sees size = 0 and treats the entry as empty, causing the game to silently fail.
            //
            // Fix: build the ZIP manually so every local file header contains the correct
            // CRC and sizes up front (bit 3 clear), exactly as WinRAR / 7-Zip produce.

            string normalised = internalPath.Replace('\\', '/');
            byte[] newBytes = new UTF8Encoding(false).GetBytes(content); // no BOM

            // Read all entries from the original archive, preserving exact entry names
            var names = new List<string>();
            var datas = new List<byte[]>();
            var times = new List<DateTimeOffset>();
            bool found = false;

            using (var arc = ZipFile.OpenRead(scdPath))
            {
                foreach (var e in arc.Entries)
                {
                    bool isTarget = string.Equals(
                        e.FullName.Replace('\\', '/'), normalised,
                        StringComparison.OrdinalIgnoreCase);

                    names.Add(e.FullName);        // preserve the exact original name
                    times.Add(e.LastWriteTime);

                    if (isTarget)
                    {
                        found = true;
                        datas.Add(newBytes);      // replace with new content
                    }
                    else
                    {
                        using var ms = new MemoryStream();
                        using var es = e.Open();
                        es.CopyTo(ms);
                        datas.Add(ms.ToArray());  // decompressed bytes of unchanged entry
                    }
                }
            }

            if (!found)
                throw new FileNotFoundException($"Entry '{internalPath}' not found in '{scdPath}'.");

            string tmp = scdPath + ".tmp";
            try
            {
                WriteRawZip(tmp, names, datas, times);
                File.Copy(tmp, scdPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        /// <summary>
        ///     Writes a minimal, standards-compliant ZIP file where every local file header
        ///     has the correct CRC and sizes written up front — no data descriptor records,
        ///     no extra fields, no ZIP64. Compatible with old game ZIP readers.
        /// </summary>
        private static void WriteRawZip(
            string destPath,
            List<string> names,
            List<byte[]> datas,
            List<DateTimeOffset> times)
        {
            int n = names.Count;
            var offsets    = new long[n];
            var compressed = new byte[n][];
            var methods    = new ushort[n];
            var crcs       = new uint[n];

            // Pre-compress every entry and compute CRC32 of the uncompressed data
            for (int i = 0; i < n; i++)
            {
                crcs[i] = ComputeCrc32(datas[i]);
                using var ms = new MemoryStream();
                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    ds.Write(datas[i], 0, datas[i].Length);
                byte[] deflated = ms.ToArray();

                if (deflated.Length < datas[i].Length)
                {
                    compressed[i] = deflated;
                    methods[i]    = 8;  // DEFLATE
                }
                else
                {
                    compressed[i] = datas[i];
                    methods[i]    = 0;  // STORE
                }
            }

            using var file = new FileStream(destPath, FileMode.Create, FileAccess.Write);

            // Local file headers + compressed data
            for (int i = 0; i < n; i++)
            {
                offsets[i] = file.Position;
                byte[] nb = Encoding.UTF8.GetBytes(names[i]);
                GetDosDateTime(times[i], out ushort dosTime, out ushort dosDate);

                var lh = new byte[30 + nb.Length];
                PutU32(lh,  0, 0x04034b50u);                   // PK local file header
                PutU16(lh,  4, 20);                             // version needed: 2.0
                PutU16(lh,  6, 0);                              // bit flag: 0 — NO data descriptor
                PutU16(lh,  8, methods[i]);
                PutU16(lh, 10, dosTime);
                PutU16(lh, 12, dosDate);
                PutU32(lh, 14, crcs[i]);
                PutU32(lh, 18, (uint)compressed[i].Length);    // compressed size
                PutU32(lh, 22, (uint)datas[i].Length);         // uncompressed size
                PutU16(lh, 26, (ushort)nb.Length);
                PutU16(lh, 28, 0);                              // extra field length: 0
                Buffer.BlockCopy(nb, 0, lh, 30, nb.Length);
                file.Write(lh,            0, lh.Length);
                file.Write(compressed[i], 0, compressed[i].Length);
            }

            // Central directory
            long cdStart = file.Position;
            for (int i = 0; i < n; i++)
            {
                byte[] nb = Encoding.UTF8.GetBytes(names[i]);
                GetDosDateTime(times[i], out ushort dosTime, out ushort dosDate);

                var cd = new byte[46 + nb.Length];
                PutU32(cd,  0, 0x02014b50u);                   // PK central dir
                PutU16(cd,  4, 20);                             // version made by
                PutU16(cd,  6, 20);                             // version needed
                PutU16(cd,  8, 0);                              // bit flag
                PutU16(cd, 10, methods[i]);
                PutU16(cd, 12, dosTime);
                PutU16(cd, 14, dosDate);
                PutU32(cd, 16, crcs[i]);
                PutU32(cd, 20, (uint)compressed[i].Length);
                PutU32(cd, 24, (uint)datas[i].Length);
                PutU16(cd, 28, (ushort)nb.Length);
                PutU16(cd, 30, 0);                              // extra field length
                PutU16(cd, 32, 0);                              // file comment length
                PutU16(cd, 34, 0);                              // disk number start
                PutU16(cd, 36, 0);                              // internal file attr
                PutU32(cd, 38, 0u);                             // external file attr
                PutU32(cd, 42, (uint)offsets[i]);               // offset of local header
                Buffer.BlockCopy(nb, 0, cd, 46, nb.Length);
                file.Write(cd, 0, cd.Length);
            }
            long cdSize = file.Position - cdStart;

            // End of central directory record
            var eocd = new byte[22];
            PutU32(eocd,  0, 0x06054b50u);
            PutU16(eocd,  4, 0);
            PutU16(eocd,  6, 0);
            PutU16(eocd,  8, (ushort)n);
            PutU16(eocd, 10, (ushort)n);
            PutU32(eocd, 12, (uint)cdSize);
            PutU32(eocd, 16, (uint)cdStart);
            PutU16(eocd, 20, 0);
            file.Write(eocd, 0, eocd.Length);
        }

        private static void GetDosDateTime(DateTimeOffset dto, out ushort dosTime, out ushort dosDate)
        {
            var d = dto.LocalDateTime;
            if (d.Year < 1980) d = new DateTime(1980, 1, 1, 0, 0, 0);
            dosTime = (ushort)(((d.Hour   & 0x1F) << 11) | ((d.Minute & 0x3F) << 5) | ((d.Second / 2) & 0x1F));
            dosDate = (ushort)((((d.Year - 1980) & 0x7F) << 9) | ((d.Month & 0x0F) << 5) | (d.Day & 0x1F));
        }

        private static void PutU16(byte[] b, int o, ushort v)
        {
            b[o]     = (byte)(v & 0xFF);
            b[o + 1] = (byte)(v >> 8);
        }

        private static void PutU32(byte[] b, int o, uint v)
        {
            b[o]     = (byte)(v         & 0xFF);
            b[o + 1] = (byte)((v >>  8) & 0xFF);
            b[o + 2] = (byte)((v >> 16) & 0xFF);
            b[o + 3] = (byte)((v >> 24) & 0xFF);
        }

        private static uint ComputeCrc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1u) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }

        private static ZipArchiveEntry? FindEntry(ZipArchive archive, string internalPath)
        {
            string normalised = internalPath.Replace('\\', '/');
            return archive.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), normalised, StringComparison.OrdinalIgnoreCase));
        }

        // ======================  Path helpers ====================== 

        public static string GetLocalScdPath(HotkeyModType modType)
        {
            string scdName = modType == HotkeyModType.NormalHotkey
                ? Globals.NormalHotkeyScdName
                : Globals.BuildModeScdName;
                
            return Path.Combine(Globals.GetHotkeyModsPath(), scdName);
        }

        private static string GetBackupScdPath(HotkeyModType modType)
        {
            string baseName = modType == HotkeyModType.NormalHotkey
                ? Path.GetFileNameWithoutExtension(Globals.NormalHotkeyScdName)
                : Path.GetFileNameWithoutExtension(Globals.BuildModeScdName);

            return Path.Combine(Globals.GetHotkeyModsBackupPath(), $"{baseName}{Globals.HotkeyModBackupSuffix}.scd");
        }

        private static string GetLuaScdBackupPath()
        {
            string baseName = Path.GetFileNameWithoutExtension(Globals.LuaScdName);

            return Path.Combine(Globals.GetHotkeyModsBackupPath(), $"{baseName}{Globals.HotkeyModBackupSuffix}.scd");
        }
    }
}
