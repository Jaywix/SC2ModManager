using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    public class ModRepositoryService
    {
        private readonly HttpClient httpClient = new HttpClient();

        // Update these URLs to your actual raw GitHub JSON files
        private readonly string mapsUrl = Globals.MapsListUrl;
        //private readonly string hotkeysUrl = "https://raw.githubusercontent.com/YOUR_REPO/hotkeys.json";
        private readonly string genericModsUrl = Globals.GenericModsListUrl;


        public ModRepositoryService()
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager/1.0");
        }

        public async Task<List<Map>> GetAvailableMapsAsync()
        {
            try
            {
                var json = await httpClient.GetStringAsync(mapsUrl);
                return JsonSerializer.Deserialize<List<Map>>(json) ?? new List<Map>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch maps from repository: {ex.Message}");
            }
        }

        //public async Task<List<HotkeyMod>> GetAvailableHotkeyModsAsync()
        //{
        //    try
        //    {
        //        var json = await httpClient.GetStringAsync(hotkeysUrl);
        //        return JsonSerializer.Deserialize<List<HotkeyMod>>(json) ?? new List<HotkeyMod>();
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception($"Failed to fetch hotkey mods from repository: {ex.Message}");
        //    }
        //}

        public async Task<List<GenericGamedataMod>> GetAvailableGenericModsAsync()
        {
            try
            {
                var json = await httpClient.GetStringAsync(genericModsUrl);
                return JsonSerializer.Deserialize<List<GenericGamedataMod>>(json) ?? new List<GenericGamedataMod>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch generic mods from repository: {ex.Message}");
            }
        }

  
    }
}