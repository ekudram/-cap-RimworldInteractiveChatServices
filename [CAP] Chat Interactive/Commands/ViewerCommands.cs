using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    public class CheckBalance : ChatCommand
    {
        public override string Name => "bal";
        public override string Description => "Check your coin & Karma balance";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The CheckBalance command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            var viewer = Viewers.GetViewer(user.Username);
            if (viewer != null)
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                // Use the shared karma emoji method
                string karmaEmoji = GetKarmaEmoji(viewer.Karma);

                return $"You have {viewer.Coins}{currencySymbol} and {viewer.Karma} karma! {karmaEmoji}";
            }
            return "Could not find your viewer data.";
        }
    }

    public class WhatIsKarma : ChatCommand
    {
        public override string Name => "whatiskarma";
        public override string Description => "Explain what karma is";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The WhatIsKarma command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            return "Karma affects your coin rewards! Higher karma = more coins per message. Be active and positive to increase your karma!";
        }
    }

    public class Pawn : ChatCommand
    {
        public override string Name => "pawn";
        public override string Description => "Purchase a pawn to join the colony";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (!IsEnabled())
            {
                return "The Pawn command is currently disabled.";
            }

            Logger.Debug($"Pawn command executed by {user.Username} with args: [{string.Join(", ", args)}]");

            // Handle different argument patterns
            if (args.Length == 0)
            {
                return ShowPawnHelp();
            }

            string firstArg = args[0].ToLower();

            // Handle list commands
            if (firstArg == "list" || firstArg == "races" || firstArg == "xenotypes")
            {
                return HandleListCommand(user, args);
            }

            // Handle purchase
            return HandlePawnPurchase(user, args);
        }

        private string ShowPawnHelp()
        {
            return "Usage: !pawn <race> [xenotype] [gender] [age] OR !pawn list <races|xenotypes> OR !pawn mypawn\n" +
                   "Examples:\n" +
                   "!pawn human\n" +
                   "!pawn human hussar\n" +
                   "!pawn human baseliner female\n" +
                   "!pawn human genie male 25\n" +
                   "!pawn list races\n" +
                   "!pawn mypawn";
        }

        private string HandleListCommand(ChatMessageWrapper user, string[] args)
        {
            if (args.Length > 1)
            {
                string listType = args[1].ToLower();
                switch (listType)
                {
                    case "races":
                        return ListAvailableRaces();
                    case "xenotypes":
                        return ListAvailableXenotypes();
                    default:
                        return $"Unknown list type: {listType}. Use: races, xenotypes";
                }
            }

            return "List types: races, xenotypes. Example: !pawn list races";
        }

        private string ListAvailableRaces()
        {
            var availableRaces = GetEnabledRaces();
            if (availableRaces.Count == 0)
            {
                return "No races available for purchase.";
            }

            var raceList = availableRaces.Select(r => r.LabelCap.RawText);
            return $"Available races: {string.Join(", ", raceList)}";
        }

        private string ListAvailableXenotypes()
        {
            if (!ModsConfig.BiotechActive)
            {
                return "Biotech DLC not active - only baseliners available.";
            }

            var xenotypes = DefDatabase<XenotypeDef>.AllDefs.Where(x => x != XenotypeDefOf.Baseliner);
            var xenotypeList = xenotypes.Select(x => x.defName);
            return $"Available xenotypes: {string.Join(", ", xenotypeList)}";
        }

        private string HandlePawnPurchase(ChatMessageWrapper user, string[] args)
        {
            // Handle "mypawn" subcommand
            if (args.Length > 0 && args[0].ToLower() == "mypawn")
            {
                return HandleMyPawnCommand(user);
            }

            // Parse arguments with better logic
            string raceName = "";
            string xenotypeName = "Baseliner";
            string genderName = "Random";
            string ageString = "Random";

            if (args.Length > 0)
            {
                raceName = args[0];

                // Process remaining arguments
                for (int i = 1; i < args.Length; i++)
                {
                    string arg = args[i].ToLower();

                    // Check if argument is a gender
                    if (arg == "male" || arg == "female")
                    {
                        genderName = arg;
                    }
                    // Check if argument is an age (numeric)
                    else if (int.TryParse(arg, out int age))
                    {
                        ageString = age.ToString();
                    }
                    // Check if argument is "random" for age
                    else if (arg == "random")
                    {
                        ageString = "Random";
                    }
                    // Otherwise, assume it's a xenotype
                    else
                    {
                        xenotypeName = args[i]; // Keep original casing for xenotype lookup
                    }
                }
            }
            else
            {
                return ShowPawnHelp();
            }

            Logger.Debug($"Parsed - Race: {raceName}, Xenotype: {xenotypeName}, Gender: {genderName}, Age: {ageString}");

            // Validate that we have at least a race name
            if (string.IsNullOrEmpty(raceName))
            {
                return "You must specify a race. Usage: !pawn <race> [xenotype] [gender] [age]";
            }

            // Call the command handler
            return BuyPawnCommandHandler.HandleBuyPawnCommand(user, raceName, xenotypeName, genderName, ageString);
        }

        private string HandleMyPawnCommand(ChatMessageWrapper user)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var pawn = assignmentManager.GetAssignedPawn(user.Username);

            if (pawn != null && !pawn.Dead)
            {
                string status = pawn.Spawned ? "alive and in colony" : "alive but not in colony";
                string health = pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent();

                return $"Your pawn {pawn.Name} is {status}. Health: {health}, Age: {pawn.ageTracker.AgeBiologicalYears}";
            }
            else
            {
                // Clear the assignment if pawn is dead
                assignmentManager.UnassignPawn(user.Username);
                return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
            }
        }

        private List<ThingDef> GetEnabledRaces()
        {
            // Get races from your race settings that are enabled
            var pawnSettings = Find.WindowStack.WindowOfType<Dialog_PawnSettings>();
            if (pawnSettings != null)
            {
                // Access the race settings from your dialog
                // This will need to be implemented based on your data structure
            }

            // Fallback: all humanlike races
            return DefDatabase<ThingDef>.AllDefs
                .Where(d => d.race?.Humanlike ?? false)
                .ToList();
        }
    }

    public class Buy : ChatCommand
    {
        public override string Name => "buy";
        public override string Description => "Purchase an item from the store or pawn";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (!IsEnabled())
            {
                return "The Buy command is currently disabled.";
            }

            if (args.Length == 0)
            {
                return "Usage: !buy <item> OR !buy pawn <race> <xenotype> <gender> <age>";
            }

            // Check if this is a pawn purchase
            if (args[0].ToLower() == "pawn")
            {
                // Redirect to pawn command with remaining arguments
                var pawnArgs = args.Skip(1).ToArray();
                var pawnCommand = new Pawn();
                return pawnCommand.Execute(user, pawnArgs);
            }

            // TODO: Implement regular store purchasing logic
            return $"Store purchasing for '{args[0]}' coming soon!";
        }
    }

    public class Use : ChatCommand
    {
        public override string Name => "use";
        public override string Description => "Use an item immediately";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The Use command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();
            // TODO: Implement item usage logic
            return "Item usage functionality coming soon!";
        }
    }

    public class Equip : ChatCommand
    {
        public override string Name => "equip";
        public override string Description => "Equip an item to your pawn";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The equip command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();
            // TODO: Implement equipment logic
            return "Equipment functionality coming soon!";
        }
    }

    public class Wear : ChatCommand
    {
        public override string Name => "wear";
        public override string Description => "Wear apparel on your pawn";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The Wear command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();
            // TODO: Implement apparel logic
            return "Apparel functionality coming soon!";
        }
    }

    public class Backpack : ChatCommand
    {
        public override string Name => "backpack";
        public override string Description => "Add item to your pawn's inventory";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The Backpack command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();
            // TODO: Implement inventory logic
            return "Inventory functionality coming soon!";
        }
    }

    // Event command
    public class Event : ChatCommand
    {
        public override string Name => "event";
        public override string Description => "Trigger a game event (weather, raid, etc.)";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The Event command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            if (args.Length == 0)
            {
                return "Usage: !event <type> [options]. Types: weather, raid, animal, trader. Example: !event weather rain";
            }

            string eventType = args[0].ToLower();

            switch (eventType)
            {
                case "weather":
                    return HandleWeatherEvent(user, args.Skip(1).ToArray());
                //case "raid":
                //    return HandleRaidEvent(user, args.Skip(1).ToArray());
                case "animal":
                    return HandleAnimalEvent(user, args.Skip(1).ToArray());
                case "trader":
                    return HandleTraderEvent(user, args.Skip(1).ToArray());
                default:
                    return $"Unknown event type: {eventType}. Available: weather, raid, animal, trader";
            }
        }

        private string HandleWeatherEvent(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !event weather <type>. Types: rain, snow, fog, thunderstorm, clear, list, etc.";
            }

            string weatherType = args[0].ToLower();

            // Handle list for event command
            if (weatherType == "list" || weatherType.StartsWith("list"))
            {
                // For list commands, let WeatherCommandHandler handle the response directly
                return WeatherCommandHandler.HandleWeatherCommand(user, weatherType);
            }

            return WeatherCommandHandler.HandleWeatherCommand(user, weatherType);
        }

        // Placeholder methods for other event types
        // private string HandleRaidEvent(ChatMessageWrapper user, string[] args)
        // {
        //    return "Raid events coming soon!";
        // }

        private string HandleAnimalEvent(ChatMessageWrapper user, string[] args)
        {
            return "Animal events coming soon!";
        }

        private string HandleTraderEvent(ChatMessageWrapper user, string[] args)
        {
            return "Trader events coming soon!";
        }
    }

    public class Weather : ChatCommand
    {
        public override string Name => "weather";
        public override string Description => "Change the weather";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The Weather command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            if (args.Length == 0)
            {
                return "Usage: !weather <type>. Types: rain, snow, fog, thunderstorm, clear, etc.";
            }

            string weatherType = args[0].ToLower();
            return WeatherCommandHandler.HandleWeatherCommand(user, weatherType);
        }
    }

    public class PurchaseList : ChatCommand
    {
        public override string Name => "items";
        public override string Description => "List available items for purchase";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The PurchaseList command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            // TODO: Implement item listing logic
            return "Available items: !buy heal, !buy weapon, !buy food (more coming soon!)";
        }
    }

    public class ModInfo : ChatCommand
    {
        public override string Name => "modinfo";
        public override string Description => "Show information about this mod";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The ModInfo is currently disabled.";
            }

            // Get command settings
            // var settingsCommand = GetCommandSettings();
            return "[CAP] Chat Interactive v1.0 - Twitch & YouTube integration for RimWorld!";
        }
    }

    public class Instructions : ChatCommand
    {
        public override string Name => "help";
        public override string Description => "Show how to use the mod";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The Instructions is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            return "Available commands: !bal (check coins), !items (see store), !whatiskarma (learn about karma). More commands coming soon!";
        }
    }

    public class AvailableCommands : ChatCommand
    {
        public override string Name => "commands";
        public override string Description => "List all available commands";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (!IsEnabled())
            {
                return "The Instructions is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            var availableCommands = ChatCommandProcessor.GetAvailableCommands(user);
            var commandList = string.Join(", ", availableCommands.Select(cmd => $"!{cmd.Name}"));
            return $"Available commands: {commandList}";
        }
    }

    public class JoinQueue : ChatCommand
    {
        public override string Name => "join";
        public override string Description => "Join the pawn queue";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (!IsEnabled())
            {
                return "The JoinQueue is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            // TODO: Implement pawn queue logic
            return "Pawn queue functionality coming soon!";
        }
    }

    public class MyPawn : ChatCommand
    {
        public override string Name => "mypawn";
        public override string Description => "Show information about your pawn and manage it";
        public override string PermissionLevel => "everyone";

        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (!IsEnabled())
            {
                return "The MyPawn command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            if (args.Length == 0)
            {
                return ShowMyPawnHelp();
            }

            string subCommand = args[0].ToLower();
            string[] subArgs = args.Length > 1 ? args.Skip(1).ToArray() : Array.Empty<string>();

            return MyPawnCommandHandler.HandleMyPawnCommand(user, subCommand, subArgs);
        }

        private string ShowMyPawnHelp()
        {
            return "Usage: !mypawn <info-type>\n" +
                   "Available info types:\n" +
                   "• body/health - Show pawn's health and body information\n" +  
                   "• gear - Show equipped items and apparel\n" +
                   "• health [capacity] - Show health status\n" +
                   "• kills - Show kill count and history\n" +
                   "• needs - Show pawn's needs\n" +
                   "• relations [viewer] - Show relationships\n" +
                   "• skills - Show skill levels\n" +
                   "• stats [stat1] [stat2] - Show specific stats\n" +
                   "• story backstory - Show detailed backstories\n" +
                   "• story traits - Show detailed traits\n" +
                   "• story all - Show complete story info\n" +
                   "• work [worktype=priority] - Show work priorities\n" +
                   "Example: !mypawn health, !mypawn skills, !mypawn stats shooting melee";
        }
    }

    public class MilitaryAid : ChatCommand
    {
        public override string Name => "militaryaid";
        public override string Description => "Call for military reinforcements from friendly factions";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            Logger.Debug($"MilitaryAid command executed by {user.Username} with args: [{string.Join(", ", args)}]");

            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The raid command is currently disabled.";
            }

            // Get command settings
            var settings = GetCommandSettings();

            // Parse wager amount if provided, otherwise use default from settings
            int wager = settings.DefaultMilitaryAidWager;
            if (args.Length > 0 && int.TryParse(args[0], out int parsedWager))
            {
                // Clamp between min and max from settings
                wager = Math.Max(settings.MinMilitaryAidWager, Math.Min(settings.MaxMilitaryAidWager, parsedWager));
            }

            return MilitaryAidCommandHandler.HandleMilitaryAid(user, wager);
        }
    }

    public class Raid : ChatCommand
    {
        public override string Name => "raid";
        public override string Description => "Call a hostile raid on the colony. Usage: !raid [type] [strategy] [wager]";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0; // Fallback to 0 if settings not available
            }
        }

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            Logger.Debug($"Raid command executed by {user.Username} with args: [{string.Join(", ", args)}]");

            // Check if command is enabled
            if (!IsEnabled())
            {
                return "The raid command is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            // Use settings defaults
            string raidType = "standard";
            string strategy = "default";
            int wager = settingsCommand.DefaultRaidWager;

            // Use allowed types from settings, fallback to all if empty
            var validRaidTypes = settingsCommand.AllowedRaidTypes.Count > 0
                ? settingsCommand.AllowedRaidTypes
                : new List<string> { "standard", "drop", "dropcenter", "dropedge", "dropchaos", "dropgroups", "mech", "mechcluster", "manhunter", "infestation", "water", "wateredge" };

            var validStrategies = settingsCommand.AllowedRaidStrategies.Count > 0
                ? settingsCommand.AllowedRaidStrategies
                : new List<string> { "default", "immediate", "smart", "sappers", "breach", "breachsmart", "stage", "siege" };

            // Parse arguments
            foreach (var arg in args)
            {
                if (string.IsNullOrEmpty(arg)) continue;
                string lowerArg = arg.ToLower();

                if (validRaidTypes.Contains(lowerArg))
                {
                    raidType = lowerArg;
                }
                else if (validStrategies.Contains(lowerArg))
                {
                    strategy = lowerArg;
                }
                else if (int.TryParse(arg, out int parsedWager))
                {
                    wager = Math.Max(settingsCommand.MinRaidWager, Math.Min(settingsCommand.MaxRaidWager, parsedWager));
                }
                else
                {
                    return $"Unknown argument: {arg}. Use !raidinfo for available options.";
                }
            }

            // Check if this specific raid type is allowed
            if (!validRaidTypes.Contains(raidType))
            {
                return $"Raid type '{raidType}' is not allowed. Available types: {string.Join(", ", validRaidTypes)}";
            }

            // Check if this strategy is allowed
            if (!validStrategies.Contains(strategy))
            {
                return $"Strategy '{strategy}' is not allowed. Available strategies: {string.Join(", ", validStrategies)}";
            }

            return RaidCommandHandler.HandleRaidCommand(user, raidType, strategy, wager);
        }
    }

    public class RaidInfo : ChatCommand
    {
        public override string Name => "raidinfo";
        public override string Description => "Show information about available raid types and strategies";
        public override string PermissionLevel => "everyone";
        public override int CooldownSeconds
        {
            get
            {
                var settings = GetCommandSettings();
                return settings?.CooldownSeconds ?? 0;
            }
        }
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (!IsEnabled())
            {
                return "The RaidInfo is currently disabled.";
            }

            // Get command settings
            var settingsCommand = GetCommandSettings();

            try
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine("=== AVAILABLE RAID TYPES ===");

                info.AppendLine("!raid standard [strategy] [wager] - Edge walk-in raid");
                info.AppendLine("  Examples: !raid standard smart 5000, !raid standard siege 6000");
                info.AppendLine("!raid drop [strategy] [wager] - Random drop (varies in danger)");
                info.AppendLine("!raid dropcenter [strategy] [wager] - Center drop (-15% points)");
                info.AppendLine("!raid dropedge [strategy] [wager] - Edge drop");
                info.AppendLine("!raid dropchaos [strategy] [wager] - Random chaotic drop");
                info.AppendLine("!raid dropgroups [strategy] [wager] - Edge drop groups (+15% points)");
                info.AppendLine("!raid mech [wager] - Mechanoid raid");

                if (RaidCommandHandler.HasRoyaltyDLC)
                {
                    info.AppendLine("!raid mechcluster [wager] - Mech Cluster (Royalty DLC)");
                }

                info.AppendLine("!raid manhunter [wager] - Manhunter animal pack");
                info.AppendLine("!raid infestation [wager] - Insect infestation");

                if (RaidCommandHandler.HasBiotechDLC)
                {
                    info.AppendLine("!raid water [wager] - Water edge raid (Biotech DLC)");
                }

                info.AppendLine("\n=== STRATEGIES ===");
                info.AppendLine("immediate - Direct assault");
                info.AppendLine("smart - Avoids turrets and traps");
                info.AppendLine("sappers - Uses explosives to breach walls");
                info.AppendLine("breach - Focuses on breaking through defenses");
                info.AppendLine("breachsmart - Smart breaching tactics");
                info.AppendLine("stage - Waits then attacks");
                info.AppendLine("siege - Builds mortars and defenses");

                info.AppendLine("\nDefault wager: 5000, Min: 1000, Max: 20000");
                info.AppendLine("Higher wager = stronger raid, more negative karma");

                // Send as green letter for in-game reference
                MessageHandler.SendGreenLetter("Raid Commands", info.ToString());

                return "Raid commands sent to in-game letter. Use !raid [type] [strategy] [wager]";
            }
            catch (Exception ex)
            {
                Logger.Error($"Raid info error: {ex}");
                return "Error getting raid info.";
            }
        }
    }

}