// Source/RICS/Utilities/ActiveModsExporter.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive aka RICS (Rimworld Interactive Chat System).
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.

using Newtonsoft.Json;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Utilities
{
    /// <summary>
    /// One-time exporter that writes currently active mods to ActiveMods.json
    /// Used only by the external RICS-Pricelist GitHub repo — never read by the mod itself.
    /// </summary>
    public static class ActiveModsExporter
    {
        private class ModExportEntry
        {
            public string author { get; set; }
            public string name { get; set; }
            public string steamId { get; set; }
            public string version { get; set; }
        }

        /// <summary>
        /// Call this once during mod startup (in CAPChatInteractiveMod constructor).
        /// Writes ActiveMods.json to the same folder as RaceSettings.json etc.
        /// </summary>
        public static void ExportActiveMods()
        {
            try
            {
                var activeMods = ModLister.AllInstalledMods
                    .Where(m => m.Active)
                    .Select(m => new ModExportEntry
                    {
                        // Authors is IEnumerable<string>, not string → convert safely
                        author = m.Authors?.Any() == true
                            ? string.Join(", ", m.Authors)
                            : "Unknown",

                        // Name and PackageId are string (can be null)
                        name = !string.IsNullOrEmpty(m.Name)
                            ? m.Name
                            : (!string.IsNullOrEmpty(m.PackageId) ? m.PackageId : "Unnamed Mod"),

                        // === FIXED: Use correct Workshop PublishedFileId (not SteamAppId) ===
                        // SteamAppId only works for official DLCs. Workshop mods use GetPublishedFileId().
                        steamId = (m.OnSteamWorkshop && m.GetPublishedFileId() != PublishedFileId_t.Invalid)
                            ? m.GetPublishedFileId().m_PublishedFileId.ToString()
                            : null,

                        // ModVersion is virtual string (can be null or empty)
                        version = !string.IsNullOrEmpty(m.ModVersion)
                            ? m.ModVersion
                            : null
                    })
                    .OrderBy(m => m.name)
                    .ToList();

                var root = new
                {
                    exportedAt = DateTime.UtcNow.ToString("o"),
                    totalActiveMods = activeMods.Count,
                    mods = activeMods
                };

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include
                };

                string json = JsonConvert.SerializeObject(root, settings);

                bool success = JsonFileManager.SaveFile("ActiveMods.json", json);

                if (success)
                {
                    Logger.Message($"Exported {activeMods.Count} active mods to ActiveMods.json");
                }
                else
                {
                    Logger.Warning("Failed to save ActiveMods.json (disk permission issue?)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export active mods list: {ex.Message}");
                // Graceful degradation — never crash mod load
            }
        }
    }
}
