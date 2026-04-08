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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This service is responsible for fetching the list of available mods from the repository. 
    ///     It has methods for each mod type, and each method returns a list of the corresponding mod model. 
    ///     The service uses HttpClient to make requests to the repository's API endpoints and deserializes the JSON responses into C# objects. 
    ///     If there is an error during the fetch process, it throws an exception with a descriptive message.
    /// </summary>
    public class ModRepositoryService
    {
        private readonly HttpClient httpClient = new HttpClient();

        // Probably should not use these and just use the globals directly
        private readonly string mapsUrl = Globals.MapsListUrl;
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

        // For future use
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