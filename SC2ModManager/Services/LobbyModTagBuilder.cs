using SC2ModManager.Models;
using System.Collections.Generic;
using System.Linq;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     Builds lobby tags for enabled mods (host session).
    /// </summary>
    public static class LobbyModTagBuilder
    {
        public const string LauncherIdKey = "LauncherId";
        public const string LauncherIdValue = "SC2ModManager";
        public const string HasModsKey = "mod_has_mods";

        public static List<TagEntry> BuildHostTags(IEnumerable<GenericGamedataMod> genericMods)
        {
            var enabledGenericMods = genericMods.Where(m => m.IsEnabled).ToList();

            var tags = new List<TagEntry>
            {
                new() { key = LauncherIdKey, value = LauncherIdValue },
                new() { key = HasModsKey, value = enabledGenericMods.Count > 0 ? "1" : "0" }
            };

            foreach (var mod in enabledGenericMods)
            {
                var hash = mod.ComputedHash;
                if (!string.IsNullOrEmpty(hash))
                    tags.Add(new TagEntry { key = $"mod_{hash}", value = "1" });
            }

            return tags;
        }
    }
}
