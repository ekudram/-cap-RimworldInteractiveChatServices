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
        // Initialize raid types with all options enabled by default
        if (AllowedRaidTypes.Count == 0)
        {
            AllowedRaidTypes = new List<string> {
                "standard", "drop", "dropcenter", "dropedge", "dropchaos",
                "dropgroups", "mech", "mechcluster", "manhunter", "infestation",
                "water", "wateredge"
            };
        }

        // Initialize raid strategies with all options enabled by default
        if (AllowedRaidStrategies.Count == 0)
        {
            AllowedRaidStrategies = new List<string> {
                "default", "immediate", "smart", "sappers", "breach",
                "breachsmart", "stage", "siege"
            };
        }
    }
}