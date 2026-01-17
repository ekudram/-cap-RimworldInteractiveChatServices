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

    // fields for raid command
    public List<string> AllowedRaidTypes = new List<string>();
    public List<string> AllowedRaidStrategies = new List<string>();
    public int DefaultRaidWager = 5000;
    public int MinRaidWager = 1000;
    public int MaxRaidWager = 20000;

    // fields for militaryaid command
    public int DefaultMilitaryAidWager = 1500;
    public int MinMilitaryAidWager = 1000;
    public int MaxMilitaryAidWager = 10000;

    // Lootbox settings
    public int DefaultLootBoxSize = 1;
    public int MinLootBoxSize = 1;
    public int MaxLootBoxSize = 10;

    // Command-specific data storage
    public string CustomData = "";

    // Constructor to initialize default values
    public CommandSettings()
    {
        // Don't initialize raid-specific lists here - they'll be initialized when needed
        // by the specific commands that use them
    }
}