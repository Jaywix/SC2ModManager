using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    public class ReplayArchiveService
    {
        private const string ReplayRootRelative = "My Games\\SquareEnix\\Supreme Commander 2\\replays";
        private const string ReplayFilePattern = "*.SC2Replay*";

        public void TrackProcessForReplay(Process gameProcess, ReplayCaptureContext context)
        {
            if (gameProcess == null)
                return;

            TryLog($"track-registered pid={gameProcess.Id} debugLog='{context.DebugLogPath ?? ""}' offset={context.DebugLogOffset} sessionStartUtc='{context.SessionStartedAtUtc:O}'");

            _ = Task.Run(async () =>
            {
                try
                {
                    await MonitorAndArchiveDuringSessionAsync(gameProcess, context);
                }
                catch (Exception ex)
                {
                    // best-effort, never break launch flow
                    TryLog($"track-failed: {ex.Message}");
                }
            });
        }

        public void ArchiveLatestReplay(ReplayCaptureContext context)
        {
            string sourceRoot = ResolveReplayRoot(context.ConfiguredReplaysPath);
            TryLog($"start sourceRoot='{sourceRoot}' configured='{context.ConfiguredReplaysPath ?? ""}' debugLog='{context.DebugLogPath ?? ""}' offset={context.DebugLogOffset}");
            if (!Directory.Exists(sourceRoot))
            {
                TryLog("skip: source root does not exist");
                return;
            }

            FileInfo? sourceReplay = null;

            if (TryResolveReplayFromDebugLog(context, out var replayPathFromLog, out var parseReason)
                && !string.IsNullOrWhiteSpace(replayPathFromLog))
            {
                sourceReplay = new FileInfo(replayPathFromLog);
                TryLog($"debug-log-selected replay='{sourceReplay.FullName}'");
            }
            else
            {
                TryLog($"debug-log-fallback reason='{parseReason}'");
                sourceReplay = FindLatestReplayFile(sourceRoot, context.SessionStartedAtUtc);
            }

            if (sourceReplay == null || !sourceReplay.Exists)
            {
                TryLog("skip: latest replay file not found");
                return;
            }

            if (!TryArchiveReplayFile(sourceReplay, context, "exit-fallback"))
            {
                return;
            }
        }

        private async Task MonitorAndArchiveDuringSessionAsync(Process gameProcess, ReplayCaptureContext context)
        {
            long readOffset = context.DebugLogOffset;
            string remainder = "";
            string? pendingReplayPath = null;
            bool archivedDuringSession = false;

            TryLog($"live-monitor-start pid={gameProcess.Id} debugLog='{context.DebugLogPath ?? ""}' offset={readOffset}");

            while (true)
            {
                bool hasExited;
                try
                {
                    hasExited = gameProcess.HasExited;
                }
                catch
                {
                    hasExited = true;
                }

                if (TryReadDebugLogDelta(context.DebugLogPath, ref readOffset, out var chunk, out var readReason))
                {
                    if (!string.IsNullOrEmpty(chunk)
                        && ProcessDebugLogChunk(chunk, ref remainder, ref pendingReplayPath, context, out bool archivedFromChunk)
                        && archivedFromChunk)
                    {
                        archivedDuringSession = true;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(readReason))
                {
                    TryLog($"live-monitor-read-skip reason='{readReason}'");
                }

                if (hasExited)
                    break;

                await Task.Delay(300);
            }

            TryLog($"wait-exit-end pid={gameProcess.Id}");

            // Final read pass after process exit.
            if (TryReadDebugLogDelta(context.DebugLogPath, ref readOffset, out var finalChunk, out _)
                && !string.IsNullOrEmpty(finalChunk)
                && ProcessDebugLogChunk(finalChunk, ref remainder, ref pendingReplayPath, context, out bool archivedFromFinalChunk)
                && archivedFromFinalChunk)
            {
                archivedDuringSession = true;
            }

            if (!archivedDuringSession)
            {
                TryLog("live-monitor-no-archive-detected, running exit fallback");
                ArchiveLatestReplay(context);
            }
            else
            {
                TryLog("live-monitor-finished with archived replay(s)");
            }
        }

        private bool ProcessDebugLogChunk(
            string chunk,
            ref string remainder,
            ref string? pendingReplayPath,
            ReplayCaptureContext context,
            out bool archivedFromChunk)
        {
            archivedFromChunk = false;
            if (string.IsNullOrEmpty(chunk))
                return false;

            string text = remainder + chunk;
            bool endsWithNewLine = text.EndsWith("\n", StringComparison.Ordinal);
            var lines = text.Split('\n');

            if (endsWithNewLine)
            {
                remainder = "";
            }
            else
            {
                remainder = lines.LastOrDefault() ?? "";
                lines = lines.Take(Math.Max(0, lines.Length - 1)).ToArray();
            }

            foreach (var raw in lines)
            {
                string line = raw.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.Contains("[GAME] Saving replay to \"", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(line, "Saving replay to \\\"(?<path>.+?)\\\"", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        pendingReplayPath = match.Groups["path"].Value.Trim();
                        TryLog($"live-marker-save replay='{pendingReplayPath}'");
                    }
                }

                if (line.Contains("[GAME] Stats save block end", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pendingReplayPath))
                {
                    TryLog($"live-marker-stats-end replay='{pendingReplayPath}'");
                    if (TryArchiveReplayPath(pendingReplayPath, context, "live-stats-end"))
                    {
                        archivedFromChunk = true;
                    }

                    pendingReplayPath = null;
                }
            }

            return true;
        }

        private static bool TryReadDebugLogDelta(string? debugLogPath, ref long offset, out string chunk, out string reason)
        {
            chunk = "";
            reason = "";

            if (string.IsNullOrWhiteSpace(debugLogPath))
            {
                reason = "debug.log path is empty";
                return false;
            }

            if (!File.Exists(debugLogPath))
            {
                reason = "debug.log does not exist";
                return false;
            }

            try
            {
                using var fs = new FileStream(
                    debugLogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                if (offset < 0)
                    offset = 0;

                if (offset > fs.Length)
                    offset = 0;

                fs.Seek(offset, SeekOrigin.Begin);
                using var reader = new StreamReader(fs);
                chunk = reader.ReadToEnd();
                offset = fs.Position;
                return true;
            }
            catch (Exception ex)
            {
                reason = $"failed to read debug.log delta: {ex.Message}";
                return false;
            }
        }

        private bool TryArchiveReplayPath(string replayPath, ReplayCaptureContext context, string reasonTag)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(replayPath))
                {
                    TryLog($"skip: empty replay path ({reasonTag})");
                    return false;
                }

                var sourceReplay = new FileInfo(replayPath);
                if (!sourceReplay.Exists)
                {
                    TryLog($"skip: replay path does not exist '{replayPath}' ({reasonTag})");
                    return false;
                }

                return TryArchiveReplayFile(sourceReplay, context, reasonTag);
            }
            catch (Exception ex)
            {
                TryLog($"archive-path-failed reasonTag='{reasonTag}' path='{replayPath}' ex='{ex.Message}'");
                return false;
            }
        }

        private bool TryArchiveReplayFile(FileInfo sourceReplay, ReplayCaptureContext context, string reasonTag)
        {
            if (!CanReadReplayFile(sourceReplay))
            {
                TryLog($"skip: replay file is not readable '{sourceReplay.FullName}' ({reasonTag})");
                return false;
            }

            string archiveRoot = Path.Combine(
                Globals.GetDataPath(),
                "Replays",
                DateTime.Now.ToString("yyyy"),
                DateTime.Now.ToString("yyyy-MM"));
            Directory.CreateDirectory(archiveRoot);

            string ext = sourceReplay.Extension;
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string lobbyPart = string.IsNullOrWhiteSpace(context.LobbyId)
                ? ""
                : $"__lobby-{SanitizeFilePart(context.LobbyId)}";
            string hostPart = string.IsNullOrWhiteSpace(context.LobbyName)
                ? ""
                : $"__name-{SanitizeFilePart(context.LobbyName)}";
            string sourceIdPart = string.IsNullOrWhiteSpace(sourceReplay.Directory?.Name)
                ? ""
                : $"__src-{SanitizeFilePart(sourceReplay.Directory.Name)}";
            string baseName = $"{stamp}{lobbyPart}{hostPart}{sourceIdPart}";

            string replayTarget = NextUniquePath(archiveRoot, baseName, ext);

            File.Copy(sourceReplay.FullName, replayTarget, overwrite: false);

            var meta = new ReplayArchiveMetadata
            {
                CapturedAtLocal = DateTime.Now,
                SourceReplayPath = sourceReplay.FullName,
                ArchivedReplayPath = replayTarget,
                LobbyId = context.LobbyId,
                LobbyName = context.LobbyName,
                SourceReplayFolderId = sourceReplay.Directory?.Name,
                SourceReplayFile = sourceReplay.Name,
                EnabledGenericMods = (context.EnabledGenericMods ?? new List<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                EnabledMaps = (context.EnabledMaps ?? new List<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            string metadataPath = Path.ChangeExtension(replayTarget, ".json");
            File.WriteAllText(metadataPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
            TryLog($"archived replay='{replayTarget}' metadata='{metadataPath}' reasonTag='{reasonTag}'");
            return true;
        }

        private static bool TryResolveReplayFromDebugLog(ReplayCaptureContext context, out string? replayPath, out string reason)
        {
            replayPath = null;
            reason = "unknown";

            if (string.IsNullOrWhiteSpace(context.DebugLogPath))
            {
                reason = "debug.log path is empty";
                return false;
            }

            if (!File.Exists(context.DebugLogPath))
            {
                reason = "debug.log does not exist";
                return false;
            }

            string tail;
            try
            {
                using var fs = new FileStream(
                    context.DebugLogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                long safeOffset = Math.Clamp(context.DebugLogOffset, 0, fs.Length);
                fs.Seek(safeOffset, SeekOrigin.Begin);

                using var reader = new StreamReader(fs);
                tail = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                reason = $"failed to read debug.log tail: {ex.Message}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(tail))
            {
                reason = "debug.log tail is empty";
                return false;
            }

            var lines = tail
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            int saveIndex = -1;
            string? saveLine = null;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].Contains("[GAME] Saving replay to \"", StringComparison.OrdinalIgnoreCase))
                {
                    saveIndex = i;
                    saveLine = lines[i];
                    break;
                }
            }

            if (saveIndex < 0 || string.IsNullOrWhiteSpace(saveLine))
            {
                reason = "no '[GAME] Saving replay to' found in current session tail";
                return false;
            }

            int statsEndIndex = -1;
            for (int i = saveIndex; i < lines.Count; i++)
            {
                if (lines[i].Contains("[GAME] Stats save block end", StringComparison.OrdinalIgnoreCase))
                {
                    statsEndIndex = i;
                    break;
                }
            }

            if (statsEndIndex < 0)
            {
                reason = "save marker found but '[GAME] Stats save block end' is missing";
                return false;
            }

            var match = Regex.Match(saveLine, "Saving replay to \\\"(?<path>.+?)\\\"", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                reason = "failed to parse replay path from save marker";
                return false;
            }

            replayPath = match.Groups["path"].Value.Trim();
            if (string.IsNullOrWhiteSpace(replayPath))
            {
                reason = "parsed replay path is empty";
                return false;
            }

            reason = "ok";
            return true;
        }

        private static void TryLog(string message)
        {
            try
            {
                string logDir = Path.Combine(Globals.GetDataPath(), "Logs");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "replay-archive.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // never throw from logging
            }
        }

        private static string ResolveReplayRoot(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return configuredPath;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ReplayRootRelative);
        }

        private static FileInfo? FindLatestReplayFile(string replayRoot, DateTime? sessionStartedAtUtc)
        {
            var rootDir = new DirectoryInfo(replayRoot);
            if (!rootDir.Exists)
                return null;

            // Search recursively because some installs may write deeper than replays\<ID>.
            // Prefer LastGame.* files first, then newest by write/create time.
            var candidates = new List<FileInfo>();

            try
            {
                candidates = rootDir
                    .GetFiles(ReplayFilePattern, SearchOption.AllDirectories)
                    .Where(IsLikelyReplayFile)
                    .Where(f => !sessionStartedAtUtc.HasValue || f.LastWriteTimeUtc >= sessionStartedAtUtc.Value.AddMinutes(-2))
                    .OrderByDescending(f => IsLastGameReplayName(f.Name))
                    .ThenByDescending(f => f.LastWriteTimeUtc)
                    .ThenByDescending(f => f.CreationTimeUtc)
                    .ThenByDescending(f => f.Length)
                    .ToList();
            }
            catch
            {
                // Fallback below for unusual access issues.
            }

            if (candidates.Count > 0)
                return candidates[0];

            // Fallback for non-standard layout: replay file directly in root.
            try
            {
                return rootDir
                    .GetFiles(ReplayFilePattern, SearchOption.TopDirectoryOnly)
                    .Where(IsLikelyReplayFile)
                    .Where(f => !sessionStartedAtUtc.HasValue || f.LastWriteTimeUtc >= sessionStartedAtUtc.Value.AddMinutes(-2))
                    .OrderByDescending(f => IsLastGameReplayName(f.Name))
                    .ThenByDescending(f => f.LastWriteTimeUtc)
                    .ThenByDescending(f => f.CreationTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsLikelyReplayFile(FileInfo file)
        {
            string name = file.Name;
            return name.EndsWith(".SC2Replay", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".SC2ReplayDLC", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf(".SC2Replay", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLastGameReplayName(string fileName)
            => fileName.StartsWith("LastGame.SC2Replay", StringComparison.OrdinalIgnoreCase);

        private static bool CanReadReplayFile(FileInfo replay)
        {
            try
            {
                replay.Refresh();
                if (!replay.Exists)
                    return false;

                using var stream = new FileStream(
                    replay.FullName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                return stream.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string NextUniquePath(string folder, string baseName, string ext)
        {
            string candidate = Path.Combine(folder, baseName + ext);
            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder, $"{baseName}_{suffix}{ext}");
                suffix++;
            }

            return candidate;
        }

        private static string SanitizeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            string sanitized = Regex.Replace(value.Trim(), "[^a-zA-Z0-9._-]+", "-");
            sanitized = sanitized.Trim('-');

            if (sanitized.Length > 40)
                sanitized = sanitized.Substring(0, 40);

            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }
    }

    public class ReplayCaptureContext
    {
        public string? GamePath { get; set; }
        public string? DebugLogPath { get; set; }
        public long DebugLogOffset { get; set; }
        public DateTime? SessionStartedAtUtc { get; set; }
        public string? ConfiguredReplaysPath { get; set; }
        public string? LobbyId { get; set; }
        public string? LobbyName { get; set; }
        public List<string> EnabledGenericMods { get; set; } = new();
        public List<string> EnabledMaps { get; set; } = new();
    }

    // Named ReplayArchiveMetadata and not ReplayMetadata because Models already has a
    // ReplayMetadata for the parsed replay header info. This one is just the info we
    // save alongside an archived replay.
    public class ReplayArchiveMetadata
    {
        public DateTime CapturedAtLocal { get; set; }
        public string SourceReplayPath { get; set; } = "";
        public string ArchivedReplayPath { get; set; } = "";
        public string? SourceReplayFolderId { get; set; }
        public string? SourceReplayFile { get; set; }
        public string? LobbyId { get; set; }
        public string? LobbyName { get; set; }
        public List<string> EnabledGenericMods { get; set; } = new();
        public List<string> EnabledMaps { get; set; } = new();
    }
}
