using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    public class LobbySyncResult
    {
        public List<string> ModsToInstall { get; set; } = new();
        public List<string> ModsToEnable { get; set; } = new();
        public List<string> ModsToRemove { get; set; } = new();
        public List<string> UnknownHashes { get; set; } = new();
        public List<GenericGamedataMod> ModsToDownload { get; set; } = new();
        public List<Map> MapsToDownload { get; set; } = new();

        public bool HasUnknownMods => UnknownHashes.Any();
        public bool NeedsChanges =>
            ModsToInstall.Any() || ModsToEnable.Any() || ModsToRemove.Any() ||
            ModsToDownload.Any() || MapsToDownload.Any();
    }

    public class LobbySyncService
    {
        private readonly ModStorageService _storage;

        public LobbySyncService(ModStorageService storage)
        {
            _storage = storage;
        }

        public Task<LobbySyncResult> CompareAsync(
            LobbyInfo lobby,
            List<GenericGamedataMod> downloadableMods,
            List<Map> downloadableMaps)
        {
            var result = new LobbySyncResult();

            var requiredHashes = lobby.Mods
                .Where(m => m.IsEnabled)
                .Select(m => m.Hash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var localMods = _storage.GetInstalledGenericMods();
            var localMaps = _storage.GetInstalledMaps();

            var localByHash = new Dictionary<string, (string fileName, bool isEnabled)>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in localMods)
            {
                var hash = m.ComputedHash;
                if (!string.IsNullOrEmpty(hash))
                    localByHash[hash] = (m.FileName, m.IsEnabled);
            }
            foreach (var m in localMaps)
            {
                var hash = m.ComputedHash;
                if (!string.IsNullOrEmpty(hash))
                    localByHash[hash] = (m.FileName, m.IsEnabled);
            }

            var dlModsByHash = downloadableMods
                .Where(m => !string.IsNullOrEmpty(m.ModHash))
                .ToDictionary(m => m.ModHash!, m => m, StringComparer.OrdinalIgnoreCase);

            var dlMapsByHash = downloadableMaps
                .Where(m => !string.IsNullOrEmpty(m.ModHash))
                .ToDictionary(m => m.ModHash!, m => m, StringComparer.OrdinalIgnoreCase);

            foreach (var hash in requiredHashes)
            {
                if (localByHash.TryGetValue(hash, out var local))
                {
                    if (!local.isEnabled)
                    {
                        result.ModsToEnable.Add(local.fileName);
                    }
                    continue;
                }

                if (dlModsByHash.TryGetValue(hash, out var dlMod))
                {
                    result.ModsToInstall.Add(dlMod.FileName);
                    result.ModsToDownload.Add(dlMod);
                }
                else if (dlMapsByHash.TryGetValue(hash, out var dlMap))
                {
                    result.ModsToInstall.Add(dlMap.FileName);
                    result.MapsToDownload.Add(dlMap);
                }
                else
                {
                    result.UnknownHashes.Add(hash);
                }
            }

            foreach (var kv in localByHash)
            {
                if (!kv.Value.isEnabled)
                    continue;
                if (!requiredHashes.Contains(kv.Key))
                    result.ModsToRemove.Add(kv.Value.fileName);
            }

            return Task.FromResult(result);
        }

        public async Task ApplySyncAsync(LobbySyncResult sync)
        {
            foreach (var mod in sync.ModsToDownload)
                await _storage.DownloadGenericModAsync(mod);

            foreach (var map in sync.MapsToDownload)
                await _storage.DownloadMapAsync(map);

            var allMods = _storage.GetInstalledGenericMods();
            var allMaps = _storage.GetInstalledMaps();

            var toEnable = sync.ModsToInstall.Concat(sync.ModsToEnable).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in allMods.Where(m => toEnable.Contains(m.FileName) && !m.IsEnabled))
                _storage.MoveGenericModToEnabled(mod);

            foreach (var map in allMaps.Where(m => toEnable.Contains(m.FileName) && !m.IsEnabled))
                _storage.MoveMapToEnabled(map);

            foreach (var mod in allMods.Where(m => sync.ModsToRemove.Contains(m.FileName) && m.IsEnabled))
                _storage.MoveGenericModToDisabled(mod);

            foreach (var map in allMaps.Where(m => sync.ModsToRemove.Contains(m.FileName) && m.IsEnabled))
                _storage.MoveMapToDisabled(map);

            _storage.SaveGenericModsState(_storage.GetInstalledGenericMods());
            _storage.SaveMapsState(_storage.GetInstalledMaps());
        }
    }
}
