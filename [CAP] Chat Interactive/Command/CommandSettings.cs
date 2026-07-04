// CommandSettings.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
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

// A serializable class to hold settings for chat commands
using CAP_ChatInteractive;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Verse;

[Serializable]
public class CommandSettings
{
    public bool Enabled = true;
    public int CooldownSeconds = 0;
    public int Cost = 0;
    public bool SupportsCost = false;

    

    public string PermissionLevel = "everyone"; // New field for permission level

    // Advanced settings that some commands might need
    public bool RequiresConfirmation = false;
    public string CommandAlias = ""; // Now used for command alias (without prefix)

    public bool useCommandCooldown = false;           // Enable per-command event cooldown
    public int MaxUsesPerCooldownPeriod = 0;        // 0 = unlimited, 1+ = specific limit

    // Raid command specific fields (lists)
    // Note: Wager values have been moved to per-command CustomData via <CustomData> in Commands.xml
    // to avoid polluting settings JSON for commands that don't use them.
    public List<string> AllowedRaidTypes = new List<string>();
    public List<string> AllowedRaidStrategies = new List<string>();

    // Lootbox settings
    public int DefaultLootBoxSize = 1;
    public int MinLootBoxSize = 1;
    public int MaxLootBoxSize = 10;

    // Command-specific data storage
    // Use this for per-command extra settings declared via <CustomData> ... </CustomData> in the ChatCommandDef XML.
    // The definition lists UI elements in order (Label, CheckBox, LabelTextBox, NumericTextBox).
    // Values are serialized as JSON object in this string (e.g. {"defaultRaidWager":"500", "useAdvanced":"true"} ).
    // Access via GetCustom<T>/SetCustom helpers.
    //
    // Example in Commands.xml:
    // <CustomData>
    //   <li><type>Label</type><label>Raid Advanceded Settings</label></li>
    //   <li><type>NumericTextBox</type><name>defaultRaidWager</name><label>Default Raid Wager</label><defaultValue>500</defaultValue></li>
    //   ...
    // </CustomData>
    public string CustomData = "";

    // --- Custom data helpers (for XML-declared per-command settings) ---

    private Dictionary<string, string> _customDictCache;

    private Dictionary<string, string> GetCustomDict()
    {
        if (_customDictCache != null) return _customDictCache;
        if (string.IsNullOrEmpty(CustomData))
        {
            _customDictCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return _customDictCache;
        }
        try
        {
            _customDictCache = JsonConvert.DeserializeObject<Dictionary<string, string>>(CustomData)
                               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _customDictCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        return _customDictCache;
    }

    private void SaveCustomDict(Dictionary<string, string> dict)
    {
        _customDictCache = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
        CustomData = JsonConvert.SerializeObject(_customDictCache, Formatting.None);
    }

    /// <summary>Get a custom value (typed). Falls back to schema default or provided defaultVal.</summary>
    public T GetCustom<T>(string key, T defaultVal = default)
    {
        var dict = GetCustomDict();
        if (!dict.TryGetValue(key, out var strVal) || string.IsNullOrEmpty(strVal))
            return defaultVal;

        try
        {
            if (typeof(T) == typeof(bool)) return (T)(object)bool.Parse(strVal);
            if (typeof(T) == typeof(int)) return (T)(object)int.Parse(strVal);
            if (typeof(T) == typeof(float)) return (T)(object)float.Parse(strVal, System.Globalization.CultureInfo.InvariantCulture);
            if (typeof(T) == typeof(string)) return (T)(object)strVal;
            return (T)Convert.ChangeType(strVal, typeof(T));
        }
        catch { return defaultVal; }
    }

    /// <summary>Set a custom value (will be stored in CustomData JSON).</summary>
    public void SetCustom<T>(string key, T value)
    {
        var dict = GetCustomDict();
        string str = value?.ToString() ?? "";
        if (typeof(T) == typeof(float))
            str = ((float)(object)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        dict[key] = str;
        SaveCustomDict(dict);
    }

    /// <summary>
    /// Ensure this settings object contains defaults for the given schema (from ChatCommandDef.customSettings).
    /// Called on load for commands that declare customs. Does not overwrite existing values.
    /// </summary>
    public void EnsureCustomDefaults(IEnumerable<CommandCustomSetting> schema)
    {
        if (schema == null) return;
        var dict = GetCustomDict();
        bool changed = false;
        foreach (var s in schema)
        {
            if (!dict.ContainsKey(s.name))
            {
                dict[s.name] = s.defaultValue ?? "";
                changed = true;
            }
        }
        if (changed) SaveCustomDict(dict);
    }

    // Constructor to initialize default values
    public CommandSettings()
    {
        // Don't initialize raid-specific lists here - they'll be initialized when needed
        // by the specific commands that use them
    }
}