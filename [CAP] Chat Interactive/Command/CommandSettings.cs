// CommandSettings.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.

// A serializable class to hold settings for chat commands
using System;
using System.Collections.Generic;

[Serializable]
public class CommandSettings
{
    public bool Enabled = true;
    public int CooldownSeconds = 0;
    public int Cost = 0;
    public bool SupportsCost = false;

    // Advanced settings that some commands might need
    public int GameDaysCooldown = 0;
    public bool UseGameDaysCooldown = false;
    public bool RequiresConfirmation = false;
    public string CustomPermission = ""; // Now used for command alias (without prefix)
    public int MaxUsesPerStream = 0;
    public bool UseMaxUsesPerStream = false;  // Toggle for the feature

    public bool UseEventCooldown = false;           // Enable per-command event cooldown
    public int MaxUsesPerCooldownPeriod = 0;        // 0 = unlimited, 1+ = specific limit
    public bool RespectsGlobalEventCooldown = true; // Whether to count toward global event limit

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

    // Command-specific data storage
    public string CustomData = "";

    // Constructor to initialize default values
    public CommandSettings()
    {
        // Don't initialize raid-specific lists here - they'll be initialized when needed
        // by the specific commands that use them
    }


}