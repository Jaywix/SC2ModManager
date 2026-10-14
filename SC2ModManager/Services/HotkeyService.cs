/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 *
 * Created on: April 1, 2026
 * Last updated: October 14, 2026
 * Author: Jacob Wixom
 *
*/

/*
 * ======================== HOW THE HOTKEY STUFF WORKS ========================
 *
 * This is the most fragile part of the mod manager because it's the part that can actually stop the game
 * from launching, so read this before changing anything in here. The normal hotkeys come from Maksing's
 * hotkey mod. The game normally loads its keymap from lua.scd in gamedata. The mod swaps that for luo.scd
 * and puts toc.win.bdf in the game folder (next to gamedata, NOT inside it), which is what tells the game to
 * load luo.scd instead. I don't really know how the toc works inside, someone else figured that out, but it
 * works with any file named luo.scd as long as it's a real luo.scd. The rule that matters: there has to be
 * exactly ONE of lua.scd or luo.scd in gamedata, and our toc should only be there when luo.scd is. Both lua
 * files, neither one, or our toc sitting there with no luo.scd all crash the game. Build mode hotkeys are a
 * totally separate thing. BuildmodeHotkeys.scd just gets dropped in gamedata and doesn't touch lua/luo at all.
 *
 * The editor works on a local copy in Data/HotkeyMods and copies it into the game on save, but gamedata is
 * always the source of truth. When the editor opens it checks the luo.scd in gamedata and if it's different
 * from our copy, we take theirs (and back it up first as luo_BeforeModManagerEdits.scd). "Installed" means the
 * file is actually in gamedata, not just that we have a copy. A lot of modders have their own luo.scd with
 * other stuff in it, so the big rule is we only ever change hotkeys in someone's luo.scd. Normal mode only
 * rewrites defaultKeyMap.lua, Restore Originals only puts defaultKeyMap.lua back (which brings the tooltips
 * back with it), and the tooltips follow the main hotkey they belong to so they don't show the old key.
 * Advanced mode lets people edit the raw lua, but it warns them first that anything they break is on them.
 * Commented out hotkeys never show up in the editor, so if someone comments one out in Advanced mode they
 * have to go back and uncomment it. Every hotkey needs a key, so Escape puts a key back to what it was
 * instead of clearing it. Any key that gets changed has to be in GameKeyNames, which came from the game's own
 * lua/keymap/keyNames.lua, and build mode keys have to be a single letter.
 *
 * Safety stuff, and pretty much all of it is there because something went wrong at some point:
 * Nothing in the game folder gets changed while the game is running (EnsureGameNotRunning), since the game
 * has these files open. Files get copied into .sc2mm-staging files in the game folder first and then renamed
 * into place, so the game never sees a half copied file. Some people have game files marked read-only, which
 * makes deletes fail halfway through a swap, so the read-only flag gets cleared before we delete or replace
 * lua/luo/toc (and on backups, since File.Copy copies the flag too). Every .scd we rewrite gets read back and checked
 * before it replaces the real one. We write the zip ourselves because the game's old zip reader can't handle
 * the way .NET writes zips. Backups never go inside gamedata because the game loads every .scd in there.
 * Our backups live in Data/HotkeyMods/Backups, and anything we back up for the user goes in the game folder
 * as a .bak file (luo.scd.bak, toc.win.bdf.bak). We only ever delete a toc.win.bdf we know is ours, meaning it
 * matches a hash in KnownTocHashes or our local copy. If I ever update toc.win.bdf on GitHub, the new SHA256
 * HAS to get added to KnownTocHashes or every install will reject the download. If someone has their own toc
 * and no luo.scd, installing asks first, then renames theirs to toc.win.bdf.bak, and we never put it back on
 * our own. Downloads go to temp files and get checked (luo.scd has to have the keymap in it, the toc has to
 * match the hash) before they replace anything.
 *
 * Uninstall: if we have a lua.scd backup from this game folder, lua.scd goes back and luo.scd and our toc get
 * deleted. If their luo.scd has changes besides hotkeys, a copy gets saved as luo.scd.bak in the game folder
 * first. If there's no lua.scd backup, they had their own luo.scd before the mod manager, so it stays and only
 * the hotkeys go back. The "Keep hotkey mod game files" setting skips all of that and leaves the game alone.
 * install_record.json remembers which game folder the lua.scd backup came from and a fingerprint of the
 * luo.scd we downloaded (everything except defaultKeyMap.lua), which is how we tell if a luo.scd has other
 * changes in it. People who used older versions won't have that file, so when it's missing we assume the lua
 * backup is from their game folder (same as the old code did) and always back up their luo.scd before deleting
 * it, since we can't tell. Steam's "verify files" puts lua.scd back without removing luo.scd, which crashes the
 * game, so the mod manager checks for bad combos every time it opens and when the Hotkeys page opens
 * (Diagnose) and offers to fix them. ReconcileNormalHotkeyState does the same cleanup without asking and gets
 * called after presets and gamedata restores.
 */
using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     Thrown when something tries to change the hotkey files while the game is open. The game
    ///     has them open while it runs, so swapping them out from under it can break it.
    /// </summary>
    public class GameRunningException : InvalidOperationException
    {
        public GameRunningException()
            : base("Supreme Commander 2 is running. Close the game first, then try again.") { }
    }

    /// <summary>
    ///     Combinations of lua.scd / luo.scd / toc.win.bdf that stop the game from launching.
    /// </summary>
    public enum HotkeyGamedataProblem
    {
        None,
        BothLuaAndLuo,    // usually Steam's "verify files" or a game update putting lua.scd back
        LuoWithoutToc,    // luo.scd can't load without the toc
        LeftoverToc,      // our toc is still there but luo.scd is gone
        NoKeymapFile      // neither lua.scd nor luo.scd
    }

    /// <summary>
    ///     What happened during an uninstall, so the UI can tell the user about it.
    /// </summary>
    public class HotkeyUninstallResult
    {
        /// <summary>Where a copy of their luo.scd got saved, if it had changes besides hotkeys.</summary>
        public string? LuoBackupPath { get; set; }

        /// <summary>True when they had their own luo.scd before the mod manager, so it stayed in the game.</summary>
        public bool KeptUserLuo { get; set; }
    }

    public class HotkeyService
    {
        // ── Internal lua paths inside each archive ──────────────────────────────

        public const string DefaultKeyMapLuaPath = "lua/keymap/defaultKeyMap.lua";
        public const string KeyActionsLuaPath = "lua/keymap/keyactions.lua";
        public const string KeyDescriptionsLuaPath = "lua/keymap/keydescriptions.lua";

        // The only file the normal editor changes. Everything else in luo.scd counts as "not hotkeys", which
        // is what we compare to figure out if someone's luo.scd has other mod changes in it. keyactions.lua
        // and keydescriptions.lua count as other changes on purpose, keyactions can have people's own lua
        // code in it (custom actions), so anyone who changed those in Advanced mode gets a luo.scd.bak on uninstall
        private static readonly string[] HotkeyOnlyLuaPaths =
        [
            DefaultKeyMapLuaPath
        ];

        public const string BuildModeLuaPath = "mods/DLC1/shadow/lua/ui/game/buildmodedata.lua";

        // Temp copies get this suffix and live in the game folder, NOT gamedata. The game loads
        // whatever is in gamedata so nothing half finished should ever sit in there
        private const string StagingSuffix = ".sc2mm-staging";

        // SHA256 of the toc.win.bdf we host on GitHub. If that file ever gets updated, add the new
        // hash here or downloads will get rejected
        private static readonly HashSet<string> KnownTocHashes = new(StringComparer.OrdinalIgnoreCase)
        {
            "061D3FA02630ED84BFBE260EF9CD0061C0CA25B59EE5C54520907A8608F47C39"
        };

        // Every key name the game understands. Pulled from lua/keymap/keyNames.lua inside luo.scd.
        // Left out the mouse buttons, the IME keys and the bare modifier keys since you can't bind those
        public static readonly HashSet<string> GameKeyNames = new(StringComparer.Ordinal)
        {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "Num0", "Num1", "Num2", "Num3", "Num4", "Num5", "Num6", "Num7", "Num8", "Num9",
            "NumStar", "NumPlus", "NumMinus", "NumPeriod", "NumSlash", "Separator",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21", "F22", "F23", "F24",
            "Backspace", "Tab", "Clear", "Enter", "Pause", "CapsLock", "Esc", "Space",
            "PageUp", "PageDown", "End", "Home", "LeftArrow", "UpArrow", "RightArrow", "DownArrow",
            "Select", "Print", "Execute", "PrintScreen", "Insert", "Delete", "Help",
            "LeftWindows", "RightWindows", "Applications", "Sleep", "NumLock", "ScrollLock",
            "Semicolon", "Equals", "Comma", "Minus", "Period", "Slash", "Tilde",
            "LeftBracket", "Backslash", "RightBracket", "Quote", "MWheelUp", "MWheelDown"
        };

        private static readonly string[] ModifierNames = { "Ctrl", "Shift", "Alt" };

        private static readonly Regex KeyMapEntryRegex = new(@"\['([^']+)'\]\s*=\s*'([^']+)'", RegexOptions.Compiled);
        private static readonly Regex KeyMapKeySideRegex = new(@"\['[^']+'\]", RegexOptions.Compiled);
        private static readonly Regex KeyMapValueSideRegex = new(@"(\]\s*=\s*)'[^']+'", RegexOptions.Compiled);

        // ── Game running guard ───────────────────────────────────────────────────

        /// <summary>
        ///     Throws if the game is open. Every method that changes files in the game folder calls this
        ///     right before it starts, since the game could have been launched from Steam at any point.
        /// </summary>
        public static void EnsureGameNotRunning()
        {
            if (DllInjectionService.IsGameRunning())
                throw new GameRunningException();
        }

        // ── Key validation ───────────────────────────────────────────────────────

        /// <summary>
        ///     True if the game would understand this key, like "R", "Ctrl-1" or "Shift-Alt-F5".
        /// </summary>
        public static bool IsValidGameKey(string combo)
        {
            if (string.IsNullOrWhiteSpace(combo) || combo != combo.Trim())
                return false;

            string[] parts = combo.Split('-');
            if (!GameKeyNames.Contains(parts[^1]))
                return false;

            string[] mods = parts[..^1];
            return mods.All(m => ModifierNames.Contains(m)) && mods.Distinct().Count() == mods.Length;
        }

        /// <summary>
        ///     Build mode only uses single letters, same as every key in the original file.
        /// </summary>
        public static bool IsValidBuildModeKey(string key) =>
            key is { Length: 1 } && key[0] >= 'A' && key[0] <= 'Z';

        // ── Installation detection ───────────────────────────────────────────────

        /// <summary>
        ///     Returns true if the local .scd file exists for the given mod type
        /// </summary>
        public bool IsModInstalled(HotkeyModType modType)
        {
            string path = GetLocalScdPath(modType);
            return File.Exists(path);
        }

        /// <summary>
        ///     Returns true if the mod's .scd is actually in the game's gamedata folder. This is what
        ///     counts as installed, our local copy existing doesn't mean the game is using it.
        /// </summary>
        public bool IsInstalledInGame(HotkeyModType modType, string? gamedataPath)
        {
            if (string.IsNullOrEmpty(gamedataPath))
                return false;

            return File.Exists(Path.Combine(gamedataPath, GetScdName(modType)));
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
        ///     Copies the .scd that is actually installed in gamedata over our local copy if they are
        ///     different. The hotkey editor edits the local copy and ApplyToGamedata copies it back over
        ///     gamedata, so if someone put their own luo.scd in gamedata, saving hotkeys would wipe out
        ///     their version with our old copy. Syncing gamedata to local first means we always edit
        ///     whatever is actually installed. Call this before loading the editor. Does nothing if the
        ///     mod isn't installed.
        ///     Returns where the backup of their luo.scd went if we just picked up a version we hadn't
        ///     seen before, so the editor can tell them. Null otherwise.
        /// </summary>
        public string? SyncLocalFromGamedata(HotkeyModType modType, string gamedataPath)
        {
            string? beforeEditBackup = null;

            if (string.IsNullOrEmpty(gamedataPath) || !Directory.Exists(gamedataPath))
                return null;

            string installedPath = Path.Combine(gamedataPath, GetScdName(modType));
            if (!File.Exists(installedPath))
                return null;

            string localPath = GetLocalScdPath(modType);

            if (File.Exists(localPath))
            {
                var installed = new FileInfo(installedPath);
                var local = new FileInfo(localPath);

                // File.Copy keeps the size and write time the same, so if these don't match
                // then the file in gamedata was changed outside the mod manager
                if (installed.Length == local.Length &&
                    installed.LastWriteTimeUtc == local.LastWriteTimeUtc)
                    return null;

                // Back up our current copy first so Restore Originals still goes back to the downloaded version
                CreateBackupsIfAbsent(modType);
            }
            else
            {
                Directory.CreateDirectory(Globals.GetHotkeyModsPath());
            }

            // This is a version of their luo.scd we haven't seen before, so keep a copy of it exactly
            // how it is before we edit anything. If a save ever goes wrong, this is how they get it back
            if (modType == HotkeyModType.NormalHotkey)
            {
                Directory.CreateDirectory(Globals.GetHotkeyModsBackupPath());
                ClearReadOnly(GetBeforeEditBackupPath());
                File.Copy(installedPath, GetBeforeEditBackupPath(), overwrite: true);
                beforeEditBackup = GetBeforeEditBackupPath();
            }

            ClearReadOnly(localPath);
            File.Copy(installedPath, localPath, overwrite: true);
            ClearReadOnly(localPath);

            // If there was never a backup (the file never came through the manager), the file we just
            // took becomes its own original. Does nothing if a backup already exists.
            CreateBackupsIfAbsent(modType);

            return beforeEditBackup;
        }

        /// <summary>
        ///     True if the .scd in gamedata isn't the same file as our local copy anymore, meaning someone
        ///     changed or replaced it after the editor loaded it. Saving now would put our older copy over
        ///     theirs, so the editor checks this right before every save. Missing in gamedata counts as
        ///     changed too.
        /// </summary>
        public bool GamedataChangedSinceLoad(HotkeyModType modType, string gamedataPath)
        {
            string installedPath = Path.Combine(gamedataPath, GetScdName(modType));
            string localPath = GetLocalScdPath(modType);

            if (!File.Exists(installedPath) || !File.Exists(localPath))
                return true;

            // Same check as SyncLocalFromGamedata, File.Copy keeps the size and write time
            var installed = new FileInfo(installedPath);
            var local = new FileInfo(localPath);
            return installed.Length != local.Length || installed.LastWriteTimeUtc != local.LastWriteTimeUtc;
        }

        /// <summary>
        ///     Applies the local mod file to the game.
        ///     Normal mod: backs up and removes lua.scd, then places luo.scd (and toc.win.bdf if it's missing).
        ///     Build mode mod: places BuildmodeHotkeys.scd in gamedata.
        ///     Everything gets copied into staging files in the game folder first and then renamed into
        ///     place, so a failed copy never leaves the game with a half written or missing file.
        /// </summary>
        public void ApplyToGamedata(HotkeyModType modType, string gamedataPath)
        {
            EnsureGameNotRunning();

            string src = GetLocalScdPath(modType);
            if (!File.Exists(src))
                throw new FileNotFoundException($"Local mod file not found: {src}");

            // Don't go creating a gamedata folder somewhere that isn't actually the game
            if (!Directory.Exists(gamedataPath))
                throw new DirectoryNotFoundException($"Couldn't find the game's gamedata folder: {gamedataPath}");

            string gameRoot = Path.GetDirectoryName(gamedataPath)!;

            if (modType == HotkeyModType.BuildModeHotkey)
            {
                ReplaceFileStaged(src, Path.Combine(gamedataPath, Globals.BuildModeScdName), gameRoot);
                return;
            }

            string luaPath = Path.Combine(gamedataPath, Globals.LuaScdName);
            string luoPath = Path.Combine(gamedataPath, Globals.NormalHotkeyScdName);
            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);
            string localBdf = Globals.GetLocalTocBdfPath();

            // luo.scd without a toc crashes the game, so if there's no toc and we can't put one there,
            // stop before anything changes. If they already have a toc (ours or their own) we leave it alone
            bool needToc = !File.Exists(tocPath);
            if (needToc && !File.Exists(localBdf))
                throw new FileNotFoundException(
                    "toc.win.bdf is missing from the game folder and the mod manager doesn't have a copy to put there. " +
                    "luo.scd can't load without it, so nothing was changed. Open the Hotkeys page to download it again.");

            string luoStage = Path.Combine(gameRoot, Globals.NormalHotkeyScdName + StagingSuffix);
            string tocStage = Path.Combine(gameRoot, Globals.TocWinBdfName + StagingSuffix);

            try
            {
                // 1. Copy everything into staging first. If one of these fails nothing the game uses has changed yet
                ClearReadOnly(luoStage);
                ClearReadOnly(tocStage);
                File.Copy(src, luoStage, overwrite: true);
                if (needToc)
                    File.Copy(localBdf, tocStage, overwrite: true);

                // 2. Back up lua.scd before it goes anywhere, and clear read-only on it now. If lua.scd
                //    can't be deleted we want to find out before luo.scd goes in, not after
                if (File.Exists(luaPath))
                {
                    BackUpLuaScd(luaPath, gamedataPath);
                    ClearReadOnly(luaPath);
                }

                // 3. Swap everything in. These are renames on the same drive so each one is basically
                //    instant, which keeps the time the game folder is in a weird state tiny. If something
                //    still dies in the middle, the startup check fixes it next time the mod manager opens
                if (needToc)
                    MoveIntoPlace(tocStage, tocPath);
                MoveIntoPlace(luoStage, luoPath);
                DeleteGameFile(luaPath);
            }
            finally
            {
                DeleteIfExists(luoStage);
                DeleteIfExists(tocStage);
            }
        }

        /// <summary>
        ///     Puts toc.win.bdf in the game folder from our local copy. For when luo.scd is there but the toc isn't.
        /// </summary>
        public void PlaceToc(string gamedataPath)
        {
            EnsureGameNotRunning();

            string localBdf = Globals.GetLocalTocBdfPath();
            if (!File.Exists(localBdf))
                throw new FileNotFoundException("The mod manager doesn't have a copy of toc.win.bdf to put in the game folder.");

            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            ReplaceFileStaged(localBdf, Path.Combine(gameRoot, Globals.TocWinBdfName), gameRoot);
        }

        /// <summary>
        ///     Moves a toc.win.bdf that isn't ours out of the way (renamed to toc.win.bdf.bak in the game
        ///     folder) so the hotkey mod's toc can go in. Returns where it went. We never put it back on
        ///     our own, the user knows they had it and can swap it back if they want.
        /// </summary>
        public string? MoveExistingTocAside(string gamedataPath)
        {
            EnsureGameNotRunning();

            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);
            if (!File.Exists(tocPath))
                return null;

            return BackupIntoGameFolder(tocPath, gameRoot, Globals.TocWinBdfName, move: true);
        }

        /// <summary>
        ///     Copies a toc.win.bdf that isn't the hotkey mod's to toc.win.bdf.bak in the game folder, for
        ///     right before something else replaces or deletes it (like restoring a full gamedata backup).
        ///     Returns where the copy went, or null if there was nothing of theirs to save.
        /// </summary>
        public static string? BackUpTocIfNotOurs(string gameRoot)
        {
            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);
            if (!File.Exists(tocPath) || IsOurToc(tocPath))
                return null;

            return BackupIntoGameFolder(tocPath, gameRoot, Globals.TocWinBdfName, move: false);
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
        ///     Only used for build mode now, that file is ours and only has the build mode keys in it.
        /// </summary>
        public void RestoreFromBackups(HotkeyModType modType)
        {
            string backupScd = GetBackupScdPath(modType);
            if (!File.Exists(backupScd)) return;

            string localScd = GetLocalScdPath(modType);
            ClearReadOnly(localScd);
            File.Copy(backupScd, localScd, overwrite: true);
        }

        /// <summary>
        ///     Puts defaultKeyMap.lua in the local luo.scd back to the one from the original backup.
        ///     Nothing else in luo.scd changes, so a modder's other changes in there stay put.
        /// </summary>
        public void RestoreDefaultKeyMapFromBackup()
        {
            string backup = GetBackupScdPath(HotkeyModType.NormalHotkey);
            if (!File.Exists(backup))
                throw new FileNotFoundException("No original backup of luo.scd was found.");

            string originalKeyMap = ReadZipEntry(backup, DefaultKeyMapLuaPath);
            WriteZipEntries(GetLocalScdPath(HotkeyModType.NormalHotkey),
                new Dictionary<string, string> { [DefaultKeyMapLuaPath] = originalKeyMap });
        }

        /// <summary>
        ///     True if we have a backup of lua.scd that came from this game folder.
        /// </summary>
        public bool HasUsableLuaBackup(string gamedataPath) =>
            File.Exists(GetLuaScdBackupPath()) && LuaBackupBelongsTo(gamedataPath);

        // ====================== Install record ======================
        // A small json file of what the mod manager actually did, so uninstall doesn't have to guess.
        // Anyone who used an older version won't have it, so everything that reads it has a safe
        // fallback for when it's missing (see LuaBackupBelongsTo and IsPlainDownloadedLuo).

        private class HotkeyInstallRecord
        {
            /// <summary>The gamedata folder our lua.scd backup came from.</summary>
            public string? GamedataPath { get; set; }

            /// <summary>Fingerprint of everything except defaultKeyMap.lua in the luo.scd we downloaded.</summary>
            public string? DownloadedLuoFingerprint { get; set; }
        }

        private static string RecordPath => Path.Combine(Globals.GetHotkeyModsPath(), "install_record.json");

        private static HotkeyInstallRecord LoadRecord()
        {
            try
            {
                if (!File.Exists(RecordPath))
                    return new HotkeyInstallRecord();

                return JsonSerializer.Deserialize<HotkeyInstallRecord>(File.ReadAllText(RecordPath)) ?? new HotkeyInstallRecord();
            }
            catch
            {
                return new HotkeyInstallRecord();
            }
        }

        private static void SaveRecord(HotkeyInstallRecord record)
        {
            Directory.CreateDirectory(Globals.GetHotkeyModsPath());
            File.WriteAllText(RecordPath, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>
        ///     Saves the fingerprint of a luo.scd we just downloaded, so later we can tell if the one in
        ///     the game has had anything besides hotkeys changed.
        /// </summary>
        public void RecordDownloadedLuo(string luoPath)
        {
            var record = LoadRecord();
            record.DownloadedLuoFingerprint = ComputeNonKeymapFingerprint(luoPath);
            SaveRecord(record);
        }

        private static void RecordLuaBackupSource(string gamedataPath)
        {
            var record = LoadRecord();
            record.GamedataPath = NormalizeDir(gamedataPath);
            SaveRecord(record);
        }

        private static void ClearLuaBackupRecord()
        {
            var record = LoadRecord();
            record.GamedataPath = null;
            SaveRecord(record);
        }

        private static bool LuaBackupBelongsTo(string gamedataPath)
        {
            var record = LoadRecord();

            // Older versions didn't save this. Back then the backup was always made from whatever game
            // folder was set, so assume it's this one. Same thing the old uninstall assumed
            if (string.IsNullOrEmpty(record.GamedataPath))
                return true;

            return string.Equals(record.GamedataPath, NormalizeDir(gamedataPath), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     True only if this luo.scd matches the one we downloaded in everything except defaultKeyMap.lua,
        ///     meaning nobody has put other mod changes in it. If we don't know (older versions
        ///     never saved the fingerprint) this says false, so the caller backs it up to be safe.
        /// </summary>
        public bool IsPlainDownloadedLuo(string luoPath)
        {
            string? fingerprint = LoadRecord().DownloadedLuoFingerprint;
            if (string.IsNullOrEmpty(fingerprint) || !File.Exists(luoPath))
                return false;

            try
            {
                return string.Equals(ComputeNonKeymapFingerprint(luoPath), fingerprint, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     If the luo.scd in gamedata has changes besides hotkeys (or we can't tell), copy it into the
        ///     game folder as luo.scd.bak. Used before anything deletes or rewrites it. Returns the backup
        ///     path, or null if no backup was needed.
        /// </summary>
        public string? BackUpLuoIfModded(string gamedataPath)
        {
            string luoPath = Path.Combine(gamedataPath, Globals.NormalHotkeyScdName);
            if (!File.Exists(luoPath) || IsPlainDownloadedLuo(luoPath))
                return null;

            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            return BackupIntoGameFolder(luoPath, gameRoot, Globals.NormalHotkeyScdName, move: false);
        }

        // ====================== Uninstall ======================

        /// <summary>
        ///     Uninstalls the mod.
        ///     Normal mod, if we have a lua.scd backup from this game folder: they had lua.scd before we
        ///     installed the mod, so lua.scd goes back, luo.scd gets deleted, and our toc gets deleted. If
        ///     their luo.scd had changes besides hotkeys, a copy is saved as luo.scd.bak in the game folder first.
        ///     Normal mod, with NO lua.scd backup: they already had luo.scd before the mod manager (modders do
        ///     this). Deleting it would leave the game with no keymap, so it stays along with the toc. We
        ///     just put its hotkeys back to the original backup's.
        ///     Build mode mod: deletes BuildmodeHotkeys.scd from gamedata.
        ///     Both: deletes the local copy and backups.
        ///     keepGameFiles (advanced setting for modders): leaves luo.scd and toc.win.bdf in the game
        ///     untouched and only deletes the local copy and backups. lua.scd is NOT restored in that case,
        ///     both lua files in gamedata at once breaks the game. The lua.scd backup is kept so it can
        ///     still be put back later if they ever take luo.scd out.
        /// </summary>
        public HotkeyUninstallResult UninstallMod(HotkeyModType modType, string gamedataPath, bool keepGameFiles = false)
        {
            EnsureGameNotRunning();

            var result = new HotkeyUninstallResult();

            if (modType == HotkeyModType.NormalHotkey)
            {
                if (!keepGameFiles && Directory.Exists(gamedataPath))
                    UninstallNormalFromGame(gamedataPath, result);
            }
            else
            {
                // Build mode mod: just delete BuildmodeHotkeys.scd from gamedata
                DeleteGameFile(Path.Combine(gamedataPath, Globals.BuildModeScdName));
            }

            // Delete local .scd copy and its backups
            DeleteIfExists(GetLocalScdPath(modType));
            DeleteIfExists(GetBackupScdPath(modType));
            if (modType == HotkeyModType.NormalHotkey)
                DeleteIfExists(GetBeforeEditBackupPath());

            return result;
        }

        private void UninstallNormalFromGame(string gamedataPath, HotkeyUninstallResult result)
        {
            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            string luaPath = Path.Combine(gamedataPath, Globals.LuaScdName);
            string luoPath = Path.Combine(gamedataPath, Globals.NormalHotkeyScdName);
            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);
            string luaBackup = GetLuaScdBackupPath();

            if (HasUsableLuaBackup(gamedataPath))
            {
                // They had lua.scd before the mod: put lua back, take luo and our toc out
                result.LuoBackupPath = BackUpLuoIfModded(gamedataPath);

                string luaStage = Path.Combine(gameRoot, Globals.LuaScdName + StagingSuffix);
                try
                {
                    // Copy lua into staging first, that's the slow part. Then the swap is just a delete and a rename
                    ClearReadOnly(luaStage);
                    if (!File.Exists(luaPath))
                        File.Copy(luaBackup, luaStage, overwrite: true);

                    DeleteGameFile(luoPath);
                    if (File.Exists(luaStage)) MoveIntoPlace(luaStage, luaPath);

                    // Only take out a toc we know is the hotkey mod's. Some other toc isn't ours to delete
                    if (IsOurToc(tocPath)) DeleteGameFile(tocPath);
                }
                finally
                {
                    DeleteIfExists(luaStage);
                }

                DeleteGameFile(luaBackup);
                ClearLuaBackupRecord();
            }
            else if (File.Exists(luoPath))
            {
                // They had luo.scd before the mod manager. It stays (and so does the toc), we only put
                // the hotkeys back to how they were in the original backup
                result.KeptUserLuo = true;

                string original = GetBackupScdPath(HotkeyModType.NormalHotkey);
                if (!File.Exists(original))
                    return;

                string originalKeyMap = ReadZipEntry(original, DefaultKeyMapLuaPath);
                if (ReadZipEntry(luoPath, DefaultKeyMapLuaPath) == originalKeyMap)
                    return; // hotkeys are already the originals, nothing to do

                result.LuoBackupPath = BackUpLuoIfModded(gamedataPath);

                string stage = Path.Combine(gameRoot, Globals.NormalHotkeyScdName + StagingSuffix);
                try
                {
                    ClearReadOnly(stage);
                    File.Copy(luoPath, stage, overwrite: true);
                    ClearReadOnly(stage); // the copy keeps luo.scd's read-only flag if it had one
                    WriteZipEntries(stage, new Dictionary<string, string> { [DefaultKeyMapLuaPath] = originalKeyMap });
                    MoveIntoPlace(stage, luoPath);
                }
                finally
                {
                    DeleteIfExists(stage);
                }
            }
            // No lua backup and no luo: nothing of ours is in the game, so there's nothing to undo
        }

        // ======================  State Reconciliation ======================

        /// <summary>
        ///     Makes sure the hotkey files are in a combination the game can actually run with.
        ///     There should only ever be ONE of lua.scd/luo.scd in gamedata, and toc.win.bdf (which
        ///     goes in the game root next to the gamedata folder, not inside it) should only exist
        ///     when luo.scd does. The game breaks if both lua files are there, if neither is there,
        ///     or if the toc file gets left behind without luo.scd.
        ///     Call this after anything that moves gamedata files around (like presets) and it will
        ///     clean up whatever it finds. Safe to call whenever. Does nothing while the game is running,
        ///     the startup check catches anything left over next time.
        /// </summary>
        public void ReconcileNormalHotkeyState(string gamedataPath)
        {
            if (string.IsNullOrEmpty(gamedataPath) || !Directory.Exists(gamedataPath))
                return;

            if (DllInjectionService.IsGameRunning())
                return;

            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            string luaPath = Path.Combine(gamedataPath, Globals.LuaScdName);
            string luoPath = Path.Combine(gamedataPath, Globals.NormalHotkeyScdName);
            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);

            bool luoPresent = File.Exists(luoPath);
            bool luaPresent = File.Exists(luaPath);

            if (luoPresent)
            {
                // Hotkey mod is active. lua.scd can't be in gamedata at the same time as luo.scd,
                // so back it up (don't overwrite an existing backup) and delete it.
                if (luaPresent)
                {
                    BackUpLuaScd(luaPath, gamedataPath);
                    DeleteGameFile(luaPath);
                }

                // luo.scd needs toc.win.bdf in the game root to load
                if (!File.Exists(tocPath))
                {
                    string localBdf = Globals.GetLocalTocBdfPath();
                    if (File.Exists(localBdf))
                        ReplaceFileStaged(localBdf, tocPath, gameRoot);
                }
            }
            else
            {
                // Hotkey mod is not active. Don't leave our toc file behind without luo.scd, it points
                // the game at a file that isn't there anymore and that breaks it. A toc that isn't ours
                // might belong to some other mod, so that one gets left alone
                if (IsOurToc(tocPath))
                    DeleteGameFile(tocPath);

                // The game still needs its keymap, so restore the original lua.scd if it's gone.
                if (!luaPresent && HasUsableLuaBackup(gamedataPath))
                    ReplaceFileStaged(GetLuaScdBackupPath(), luaPath, gameRoot);
            }
        }

        // ======================  Startup check + fixes ======================

        /// <summary>
        ///     Looks for a lua/luo/toc combination that would stop the game from launching. A toc that
        ///     isn't ours sitting next to lua.scd counts as fine, that's somebody else's setup.
        /// </summary>
        public HotkeyGamedataProblem Diagnose(string gamedataPath)
        {
            if (string.IsNullOrEmpty(gamedataPath) || !Directory.Exists(gamedataPath))
                return HotkeyGamedataProblem.None;

            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            bool lua = File.Exists(Path.Combine(gamedataPath, Globals.LuaScdName));
            bool luo = File.Exists(Path.Combine(gamedataPath, Globals.NormalHotkeyScdName));
            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);

            if (luo && lua) return HotkeyGamedataProblem.BothLuaAndLuo;
            if (luo && !File.Exists(tocPath)) return HotkeyGamedataProblem.LuoWithoutToc;
            if (!luo && !lua) return HotkeyGamedataProblem.NoKeymapFile;
            if (!luo && IsOurToc(tocPath)) return HotkeyGamedataProblem.LeftoverToc;

            return HotkeyGamedataProblem.None;
        }

        /// <summary>
        ///     Fix for both lua.scd and luo.scd being in gamedata: keep the hotkey mod. lua.scd gets backed
        ///     up (if we don't have a backup yet) and removed.
        /// </summary>
        public void KeepLuoRemoveLua(string gamedataPath)
        {
            EnsureGameNotRunning();

            string luaPath = Path.Combine(gamedataPath, Globals.LuaScdName);
            if (!File.Exists(luaPath))
                return;

            BackUpLuaScd(luaPath, gamedataPath);
            DeleteGameFile(luaPath);
        }

        /// <summary>
        ///     Fix for both lua.scd and luo.scd being in gamedata: go back to the original keymap. luo.scd
        ///     is removed (with a luo.scd.bak copy in the game folder if it had other mod changes) and our
        ///     toc is removed. Returns the luo backup path if one was made.
        /// </summary>
        public string? RemoveLuoKeepLua(string gamedataPath)
        {
            EnsureGameNotRunning();

            string luaPath = Path.Combine(gamedataPath, Globals.LuaScdName);
            if (!File.Exists(luaPath))
                throw new FileNotFoundException("lua.scd isn't in gamedata, so luo.scd can't be removed without leaving the game with no keymap.");

            string? backup = BackUpLuoIfModded(gamedataPath);

            DeleteGameFile(Path.Combine(gamedataPath, Globals.NormalHotkeyScdName));

            string tocPath = Path.Combine(Path.GetDirectoryName(gamedataPath)!, Globals.TocWinBdfName);
            if (IsOurToc(tocPath)) DeleteGameFile(tocPath);

            return backup;
        }

        /// <summary>
        ///     Fix for our toc being left behind without luo.scd.
        /// </summary>
        public void RemoveLeftoverToc(string gamedataPath)
        {
            EnsureGameNotRunning();

            string tocPath = Path.Combine(Path.GetDirectoryName(gamedataPath)!, Globals.TocWinBdfName);
            if (IsOurToc(tocPath))
                DeleteGameFile(tocPath);
        }

        /// <summary>
        ///     Fix for gamedata having no keymap file at all: put lua.scd back from our backup.
        /// </summary>
        public void RestoreLuaFromBackup(string gamedataPath)
        {
            EnsureGameNotRunning();

            if (!HasUsableLuaBackup(gamedataPath))
                throw new FileNotFoundException("The mod manager doesn't have a backup of lua.scd for this game folder.");

            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            ReplaceFileStaged(GetLuaScdBackupPath(), Path.Combine(gamedataPath, Globals.LuaScdName), gameRoot);

            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);
            if (IsOurToc(tocPath)) DeleteGameFile(tocPath);
        }

        // ======================  Download checks ======================

        /// <summary>
        ///     Makes sure a downloaded luo.scd is really a zip with the keymap in it. A failed download
        ///     can come back as an error page, and installing that would break the game.
        /// </summary>
        public static void VerifyDownloadedLuo(string path)
        {
            try
            {
                using var archive = ZipFile.OpenRead(path);
                if (FindEntry(archive, DefaultKeyMapLuaPath) == null)
                    throw new InvalidDataException("no keymap inside it");
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"The downloaded luo.scd doesn't look right ({ex.Message}), so nothing was installed.", ex);
            }
        }

        /// <summary>
        ///     Makes sure a downloaded toc.win.bdf is exactly the file we host.
        /// </summary>
        public static void VerifyDownloadedToc(string path)
        {
            if (!File.Exists(path) || !KnownTocHashes.Contains(HashFile(path)))
                throw new InvalidDataException("The downloaded toc.win.bdf doesn't match the expected file, so nothing was installed.");
        }

        /// <summary>
        ///     Makes sure a downloaded BuildmodeHotkeys.scd is really a zip with the build mode data in it.
        /// </summary>
        public static void VerifyDownloadedBuildMode(string path)
        {
            try
            {
                using var archive = ZipFile.OpenRead(path);
                if (FindEntry(archive, BuildModeLuaPath) == null)
                    throw new InvalidDataException("no build mode data inside it");
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"The downloaded BuildmodeHotkeys.scd doesn't look right ({ex.Message}), so nothing was installed.", ex);
            }
        }

        /// <summary>
        ///     True if this toc.win.bdf is the hotkey mod's, either the one we host or the same bytes as our local copy.
        /// </summary>
        public static bool IsOurToc(string tocPath)
        {
            if (!File.Exists(tocPath))
                return false;

            try
            {
                string hash = HashFile(tocPath);
                if (KnownTocHashes.Contains(hash))
                    return true;

                string localBdf = Globals.GetLocalTocBdfPath();
                return File.Exists(localBdf) && string.Equals(HashFile(localBdf), hash, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     True if we have a local toc.win.bdf that can be put in the game folder.
        /// </summary>
        public static bool HasLocalToc() => File.Exists(Globals.GetLocalTocBdfPath());

        // ======================  Normal Hotkey Parsing ======================

        /// <summary>
        ///     Parses defaultKeyMap.lua from luo.scd and returns all hotkey entries across all three
        ///     sections (Main, Tooltip, Debug).
        /// </summary>
        public List<HotkeyEntry> ReadDefaultKeyMap(HotkeyModType modType = HotkeyModType.NormalHotkey)
        {
            string scdPath = GetLocalScdPath(modType);
            string lua = ReadZipEntry(scdPath, DefaultKeyMapLuaPath);
            var descriptions = ReadKeyDescriptions(modType);

            return ParseDefaultKeyMap(lua, descriptions);
        }

        /// <summary>
        ///     Writes the provided hotkey entries back into defaultKeyMap.lua inside luo.scd.
        ///     Preserves all other content in the file (non-entry lines are kept verbatim).
        /// </summary>
        public void WriteDefaultKeyMap(List<HotkeyEntry> entries, HotkeyModType modType = HotkeyModType.NormalHotkey)
        {
            // Last line of defense. The editor already checks all this before saving, but if a blank or broken
            // key ever gets here somehow (cut/paste, some new UI path, whatever), a blank key gets written as
            // [''] and that hotkey disappears from the editor for good. So refuse to write anything at all
            foreach (var e in entries)
                EnsureKeyIsSafeToWrite(e.KeyCombo, e.OriginalKeyCombo, e.Command, IsValidGameKey);

            string scdPath = GetLocalScdPath(modType);
            string original = ReadZipEntry(scdPath, DefaultKeyMapLuaPath);
            string updated = RebuildDefaultKeyMap(original, entries);

            WriteZipEntries(scdPath, new Dictionary<string, string> { [DefaultKeyMapLuaPath] = updated });
        }

        /// <summary>
        ///     Reads keydescriptions.lua from luo.scd and returns a dictionary of command → description.
        /// </summary>
        public Dictionary<string, string> ReadKeyDescriptions(HotkeyModType modType = HotkeyModType.NormalHotkey)
        {
            string scdPath = GetLocalScdPath(modType);
            string lua = ReadZipEntry(scdPath, KeyDescriptionsLuaPath);

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
        ///     Writes raw text back to lua files inside the archive, all in one rewrite so they either
        ///     all get saved or none of them do.
        /// </summary>
        public void WriteRawLuaFiles(HotkeyModType modType, Dictionary<string, string> filesByInternalPath)
        {
            WriteZipEntries(GetLocalScdPath(modType), filesByInternalPath);
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
            // Same last line of defense as the normal hotkeys
            foreach (var e in entries)
                EnsureKeyIsSafeToWrite(e.Key, e.OriginalKey, string.IsNullOrEmpty(e.Comment) ? e.UnitId : e.Comment, IsValidBuildModeKey);

            string scdPath = GetLocalScdPath(HotkeyModType.BuildModeHotkey);
            string original = ReadZipEntry(scdPath, BuildModeLuaPath);
            string updated = RebuildBuildModeData(original, entries);

            WriteZipEntries(scdPath, new Dictionary<string, string> { [BuildModeLuaPath] = updated });
        }

        /// <summary>
        ///     Throws if a key can't be written to the file. Blank keys are never allowed, a key that got
        ///     changed has to pass the validity check, and nothing that would break the lua (quotes,
        ///     backslashes, line breaks) can get through even if it came from the file.
        /// </summary>
        private static void EnsureKeyIsSafeToWrite(string? key, string originalKey, string name, Func<string, bool> isValidKey)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidDataException($"'{name}' has no key, and every hotkey needs one. Nothing was saved.");

            if (key.IndexOfAny(new[] { '\'', '\\', '\r', '\n' }) >= 0)
                throw new InvalidDataException($"'{name}' has a key with characters that would break the hotkey file. Nothing was saved.");

            if (key != originalKey && !isValidKey(key))
                throw new InvalidDataException($"'{name}' has a key the game doesn't recognize (\"{key}\"). Nothing was saved.");
        }

        // ======================  Lua Parsing Helpers ======================

        private static List<HotkeyEntry> ParseDefaultKeyMap(string lua, Dictionary<string, string> descriptions)
        {
            var entries = new List<HotkeyEntry>();

            // Matches lines like:  ['R'] = 'repair',   or  ['Ctrl-1'] = 'set_group1',
            // AI helped generate this Regex and it seems to work. Haven't touched this stuff since my Models of Computation class
            HotkeySection currentSection = HotkeySection.Main;

            foreach (string rawLine in lua.Split('\n'))
            {
                string line = rawLine.TrimEnd();

                // Skip comment lines completely. If we don't, a comment that mentions "defaultKeyMap"
                // can flip the section back to Main, and commented out bindings like
                // --['Tab'] = 'next_cam_position' get picked up as real hotkeys. The mod has a bunch
                // of disabled bindings like that and they were showing up in the editor.
                bool isComment = line.TrimStart().StartsWith("--");
                if (isComment) continue;

                currentSection = DetectSection(line, currentSection);

                var m = KeyMapEntryRegex.Match(line);
                if (!m.Success) continue;

                // The tooltip table is written the other way around from the others:
                // ['overcharge'] = 'O' instead of ['O'] = 'overcharge'. The key is on the right there
                bool isTooltip = currentSection == HotkeySection.Tooltip;
                string keyCombo = isTooltip ? m.Groups[2].Value : m.Groups[1].Value;
                string command = isTooltip ? m.Groups[1].Value : m.Groups[2].Value;
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

        /// <summary>
        ///     Makes each tooltip follow the main hotkey it's for, so rebinding Overcharge also changes the
        ///     key shown on its tooltip. Tooltips are named after the main command (overcharge = overcharge),
        ///     or the main command with RULEUCC_ in front for unit orders (RULEUCC_Move = move).
        ///     They only get linked if they had the same key when the file was loaded. If a modder made a
        ///     tooltip show something different on purpose, we leave it alone.
        /// </summary>
        public static void LinkTooltipsToMainHotkeys(IEnumerable<HotkeyEntry> mainEntries, IEnumerable<HotkeyEntry> tooltipEntries)
        {
            var tooltips = tooltipEntries.ToList();

            foreach (var main in mainEntries)
            {
                var linked = tooltips
                    .Where(t => TooltipIsForCommand(t.Command, main.Command) && t.OriginalKeyCombo == main.OriginalKeyCombo)
                    .ToList();

                if (linked.Count == 0)
                    continue;

                main.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName != nameof(HotkeyEntry.KeyCombo))
                        return;

                    foreach (var tooltip in linked)
                        tooltip.KeyCombo = main.KeyCombo;
                };
            }
        }

        private static bool TooltipIsForCommand(string tooltipName, string mainCommand)
        {
            const string orderPrefix = "RULEUCC_";
            string name = tooltipName.StartsWith(orderPrefix, StringComparison.OrdinalIgnoreCase)
                ? tooltipName[orderPrefix.Length..]
                : tooltipName;

            return string.Equals(name, mainCommand, StringComparison.OrdinalIgnoreCase);
        }

        private static HotkeySection DetectSection(string line, HotkeySection current)
        {
            if (line.Contains("keymapTooltipHotkeys")) return HotkeySection.Tooltip;
            if (line.Contains("debugKeyMap")) return HotkeySection.Debug;
            if (line.Contains("defaultKeyMap")) return HotkeySection.Main;
            return current;
        }

        private static string RebuildDefaultKeyMap(string originalLua, List<HotkeyEntry> entries)
        {
            // Lookup is (originalKey, command, section) -> new keycombo. We key by the original key
            // and not just the command because the same command can have multiple bindings in the
            // same section (like ['B'] = 'build' and ['Shift-B'] = 'build'). If we only keyed by
            // command then the last one would win and the first binding would get messed up on save.
            var lookup = new Dictionary<(string origKey, string command, HotkeySection section), string>();
            foreach (var e in entries)
                lookup[(e.OriginalKeyCombo, e.Command, e.Section)] = e.KeyCombo;

            HotkeySection currentSection = HotkeySection.Main;

            // Lines get edited in place and joined back with '\n', so the original line endings stay
            // exactly how they were and the file doesn't grow an extra blank line every save
            string[] lines = originalLua.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Leave comment lines alone, don't update section state or rewrite keys on them
                if (line.TrimStart().StartsWith("--"))
                    continue;

                currentSection = DetectSection(line, currentSection);

                var m = KeyMapEntryRegex.Match(line);
                if (!m.Success)
                    continue;

                bool isTooltip = currentSection == HotkeySection.Tooltip;
                string currentKey = isTooltip ? m.Groups[2].Value : m.Groups[1].Value;
                string command = isTooltip ? m.Groups[1].Value : m.Groups[2].Value;

                if (!lookup.TryGetValue((currentKey, command, currentSection), out string? newKey) || newKey == currentKey)
                    continue;

                lines[i] = isTooltip
                    // Tooltip: swap the value on the right, ['overcharge'] = 'O' -> ['overcharge'] = 'X'
                    ? KeyMapValueSideRegex.Replace(line, mm => mm.Groups[1].Value + "'" + newKey + "'", 1)
                    // Everything else: swap the key on the left, ['OldKey'] -> ['NewKey']
                    : KeyMapKeySideRegex.Replace(line, _ => "['" + newKey + "']", 1);
            }

            return string.Join("\n", lines);
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
                        OriginalKey = m.Groups[1].Value,
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
                RegexOptions.Compiled);

            var entryRegex = new Regex(
                @"(\['(?:[A-Za-z0-9\-]+)'\])(\s*=\s*')([^']+)(')",
                RegexOptions.Compiled);

            BuildModeFaction currentFaction = BuildModeFaction.UEF;
            string currentCategory = string.Empty;

            // Same as the normal keymap, edit lines in place so line endings stay the same and
            // there's no extra blank line added at the end each save
            string[] lines = originalLua.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

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
                if (!em.Success)
                    continue;

                string unitId = em.Groups[3].Value;
                if (lookup.TryGetValue((currentFaction, currentCategory, unitId), out string? newKey))
                {
                    // Only the first entry on the line, the rest of the line stays exactly how it was
                    lines[i] = entryRegex.Replace(line,
                        mm => $"['{newKey}']{mm.Groups[2].Value}{mm.Groups[3].Value}{mm.Groups[4].Value}", 1);
                }
            }

            return string.Join("\n", lines);
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

        private static void WriteZipEntries(string scdPath, Dictionary<string, string> replacements)
        {
            // .NET's ZipArchiveMode.Create writes "data descriptor" records (bit 3 set,
            // sizes = 0 in local file headers) because it streams forward without seeking back.
            // The SC2 SCD loader is a 2010-era ZIP reader that does NOT support data descriptors —
            // it sees size = 0 and treats the entry as empty, causing the game to silently fail.
            //
            // Fix: build the ZIP manually so every local file header contains the correct
            // CRC and sizes up front (bit 3 clear), exactly as WinRAR / 7-Zip produce, which work
            // when editing the files manually

            var newContent = replacements.ToDictionary(
                kv => kv.Key.Replace('\\', '/'),
                kv => new UTF8Encoding(false).GetBytes(kv.Value), // no BOM
                StringComparer.OrdinalIgnoreCase);

            // Read all entries from the original archive, preserving exact entry names
            var names = new List<string>();
            var datas = new List<byte[]>();
            var times = new List<DateTimeOffset>();
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var arc = ZipFile.OpenRead(scdPath))
            {
                foreach (var e in arc.Entries)
                {
                    string normalised = e.FullName.Replace('\\', '/');

                    names.Add(e.FullName);        // preserve the exact original name
                    times.Add(e.LastWriteTime);

                    if (newContent.TryGetValue(normalised, out byte[]? replacement))
                    {
                        found.Add(normalised);
                        datas.Add(replacement);   // replace with new content
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

            var missing = newContent.Keys.Where(k => !found.Contains(k)).ToList();
            if (missing.Count > 0)
                throw new FileNotFoundException($"Entry '{missing[0]}' not found in '{scdPath}'.");

            string tmp = scdPath + ".tmp";
            try
            {
                WriteRawZip(tmp, names, datas, times);

                // Read the new file back and make sure every entry matches before it replaces anything.
                // If it doesn't, we bail and the original file never got touched
                VerifyWrittenZip(tmp, names, datas);

                MoveIntoPlace(tmp, scdPath);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        private static void VerifyWrittenZip(string path, List<string> names, List<byte[]> datas)
        {
            using var arc = ZipFile.OpenRead(path);

            if (arc.Entries.Count != names.Count)
                throw new InvalidDataException("The rewritten .scd has a different number of files than the original, so it wasn't saved.");

            for (int i = 0; i < names.Count; i++)
            {
                var entry = arc.Entries[i];
                if (entry.FullName != names[i])
                    throw new InvalidDataException($"The rewritten .scd has the wrong name for '{names[i]}', so it wasn't saved.");

                using var ms = new MemoryStream();
                using (var es = entry.Open())
                    es.CopyTo(ms);

                if (!ms.ToArray().AsSpan().SequenceEqual(datas[i]))
                    throw new InvalidDataException($"'{names[i]}' didn't come out the same in the rewritten .scd, so it wasn't saved.");
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

        /// <summary>
        ///     Hash of every file in the .scd except defaultKeyMap.lua. Two luo.scd files with the
        ///     same fingerprint only differ in their hotkeys.
        /// </summary>
        private static string ComputeNonKeymapFingerprint(string scdPath)
        {
            using var archive = ZipFile.OpenRead(scdPath);
            var sb = new StringBuilder();

            var entries = archive.Entries
                .Select(e => (Name: e.FullName.Replace('\\', '/').ToLowerInvariant(), Entry: e))
                .Where(x => !HotkeyOnlyLuaPaths.Any(k => string.Equals(k, x.Name, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.Name, StringComparer.Ordinal);

            foreach (var (name, entry) in entries)
            {
                using var s = entry.Open();
                sb.Append(name).Append(':').Append(Convert.ToHexString(SHA256.HashData(s))).Append('\n');
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        // ======================  File helpers ======================

        /// <summary>
        ///     Copies src into a staging file in the game folder, then renames it over dest. The game
        ///     either sees the old file or the new one, never a half copied one.
        /// </summary>
        private static void ReplaceFileStaged(string src, string dest, string gameRoot)
        {
            string stage = Path.Combine(gameRoot, Path.GetFileName(dest) + StagingSuffix);
            try
            {
                ClearReadOnly(stage);
                File.Copy(src, stage, overwrite: true);
                MoveIntoPlace(stage, dest);
            }
            finally
            {
                DeleteIfExists(stage);
            }
        }

        /// <summary>
        ///     Backs up lua.scd before it gets removed. Keeps an existing backup from this game folder,
        ///     but if the backup we have came from a different game folder, this one replaces it so the
        ///     lua.scd we're removing right now can always be put back.
        /// </summary>
        private static void BackUpLuaScd(string luaPath, string gamedataPath)
        {
            string luaBackup = GetLuaScdBackupPath();
            if (File.Exists(luaBackup) && LuaBackupBelongsTo(gamedataPath))
                return;

            Directory.CreateDirectory(Globals.GetHotkeyModsBackupPath());
            ClearReadOnly(luaBackup);
            File.Copy(luaPath, luaBackup, overwrite: true);
            ClearReadOnly(luaBackup); // keeps lua.scd's read-only flag otherwise, and then uninstall can't delete it
            RecordLuaBackupSource(gamedataPath);
        }

        /// <summary>
        ///     Copies (or moves) a file into the game folder as name.bak, or name.bak2, name.bak3 if
        ///     those are taken. Always the game folder and never gamedata, since the game loads every
        ///     .scd in gamedata.
        /// </summary>
        private static string BackupIntoGameFolder(string sourcePath, string gameRoot, string fileName, bool move)
        {
            string target = Path.Combine(gameRoot, fileName + ".bak");
            int n = 2;
            while (File.Exists(target))
                target = Path.Combine(gameRoot, $"{fileName}.bak{n++}");

            if (move)
                File.Move(sourcePath, target);
            else
                File.Copy(sourcePath, target);

            return target;
        }

        private static string HashFile(string path)
        {
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(fs));
        }

        // Some game files end up marked read-only (backup tools, copying off a disc, antivirus). Deleting or
        // replacing one of those throws, and if that happens halfway through a swap the game can end up with
        // both lua.scd and luo.scd. So the flag gets cleared first everywhere we touch lua/luo/toc. File.Copy
        // copies the flag too, which is why backups and staging files get the same treatment
        private static void ClearReadOnly(string path)
        {
            if (!File.Exists(path))
                return;

            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }

        private static void DeleteGameFile(string path)
        {
            if (!File.Exists(path))
                return;

            ClearReadOnly(path);
            File.Delete(path);
        }

        private static void MoveIntoPlace(string source, string dest)
        {
            ClearReadOnly(source);
            ClearReadOnly(dest);
            File.Move(source, dest, overwrite: true);
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    ClearReadOnly(path);
                    File.Delete(path);
                }
            }
            catch
            {
                // leftover temp files are harmless, they never live in gamedata
            }
        }

        private static string NormalizeDir(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // ======================  Path helpers ======================

        private static string GetScdName(HotkeyModType modType) =>
            modType == HotkeyModType.NormalHotkey ? Globals.NormalHotkeyScdName : Globals.BuildModeScdName;

        public static string GetLocalScdPath(HotkeyModType modType)
        {
            return Path.Combine(Globals.GetHotkeyModsPath(), GetScdName(modType));
        }

        private static string GetBackupScdPath(HotkeyModType modType)
        {
            string baseName = Path.GetFileNameWithoutExtension(GetScdName(modType));

            return Path.Combine(Globals.GetHotkeyModsBackupPath(), $"{baseName}{Globals.HotkeyModBackupSuffix}.scd");
        }

        private static string GetLuaScdBackupPath()
        {
            string baseName = Path.GetFileNameWithoutExtension(Globals.LuaScdName);

            return Path.Combine(Globals.GetHotkeyModsBackupPath(), $"{baseName}{Globals.HotkeyModBackupSuffix}.scd");
        }

        // Their luo.scd exactly how it was the last time we picked up a new version of it from gamedata
        private static string GetBeforeEditBackupPath() =>
            Path.Combine(Globals.GetHotkeyModsBackupPath(), "luo_BeforeModManagerEdits.scd");
    }
}
