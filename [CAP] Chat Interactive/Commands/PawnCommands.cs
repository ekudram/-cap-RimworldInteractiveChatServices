// PawnCommands.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles pawn-related chat commands: !pawn, !mypawn, trait commands, and queue management.
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive.Commands.CommandHandlers;
using CAP_ChatInteractive.Traits;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    public class Pawn : ChatCommand
    {
        public override string Name => "pawn";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
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
                int traitCount = pawn.story.traits.allTraits.Count;
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                int maxTraits = settings?.MaxTraits ?? 4;

                return $"Your pawn {pawn.Name} is {status}. Health: {health}, Age: {pawn.ageTracker.AgeBiologicalYears}, Traits: {traitCount}/{maxTraits}";
            }
            else
            {
                // Clear the assignment if pawn is dead
                // assignmentManager.UnassignPawn(user.Username); lets not do this automatically to preserve history
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

    public class MyPawn : ChatCommand
    {
        public override string Name => "mypawn";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
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
            return "!mypawn <type>: body, gear, kills, needs, relations, skills, stats, story, traits, work\n" +
                "Ex: !mypawn health, !mypawn skills, !mypawn stats shooting melee";
        }
    }

    public class TraitCommand : ChatCommand
    {
        public override string Name => "trait";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return TraitsCommandHandler.HandleLookupTraitCommand(user, args);
        }
    }

    public class AddTraitCommand : ChatCommand
    {
        public override string Name => "addtrait";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return TraitsCommandHandler.HandleAddTraitCommand(user, args);
        }
    }

    public class RemoveTraitCommand : ChatCommand
    {
        public override string Name => "removetrait";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return TraitsCommandHandler.HandleRemoveTraitCommand(user, args);
        }
    }

    public class ListTraitsCommand : ChatCommand
    {
        public override string Name => "traits";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return TraitsCommandHandler.HandleListTraitsCommand(user, args);
        }
    }

    public class LeaveCommand : ChatCommand
    {
        public override string Name => "leave";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (!IsEnabled())
            {
                return "The Leave command is currently disabled.";
            }

            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

            // Check if user has a pawn assigned
            if (!assignmentManager.HasAssignedPawn(user.Username))
            {
                return "You don't have a pawn assigned to release.";
            }

            // Get the pawn info before unassigning for the message
            Verse.Pawn pawn = assignmentManager.GetAssignedPawn(user.Username);
            string pawnName = pawn?.Name?.ToStringShort ?? "your pawn";
            string pawnStatus = (pawn == null || pawn.Dead) ? " (deceased)" : "";

            // Handle live pawn departure
            if (pawn != null && !pawn.Dead && pawn.Spawned)
            {
                PreparePawnForDeparture(pawn);
            }

            // Release the pawn
            assignmentManager.UnassignPawn(user.Username);

            // Send storytelling letter
            SendDepartureLetter(user.Username, pawn, pawnName);

            return $"✅ You have released {pawnName}{pawnStatus}. You can now get a new pawn with !pawn command.";
        }

        private void PreparePawnForDeparture(Verse.Pawn pawn)
        {
            try
            {
                // Remove from faction
                if (pawn.Faction != null && pawn.Faction.IsPlayer)
                {
                    pawn.SetFaction(null);
                }

                // Stop all jobs and clear surgery bills
                pawn.jobs.StopAll();
                pawn.health.surgeryBills.Clear();

                Logger.Debug($"Prepared pawn {pawn.Name} for departure");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error preparing pawn for departure: {ex.Message}");
            }
        }

        private void SendDepartureLetter(string username, Verse.Pawn pawn, string pawnName)
        {
            string label = $"Viewer Departure";

            string message;
            if (pawn == null || pawn.Dead)
            {
                message = $"{username} has released their connection to the deceased {pawnName}. Their story in the colony has come to a close.";
                MessageHandler.SendBlueLetter(label, message);
            }
            else
            {
                message = $"{username} has decided to part ways with {pawnName}. The colonist, feeling the severed bond, has chosen to leave the settlement behind.";
                MessageHandler.SendPinkLetter(label, message);
            }

            Logger.Debug($"Sent departure letter: {message}");
        }
    }

    public class JoinQueue : ChatCommand
    {
        public override string Name => "join";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return PawnQueueCommandHandler.HandleJoinQueueCommand(user);
        }
    }

    public class LeaveQueue : ChatCommand
    {
        public override string Name => "leavequeue";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return PawnQueueCommandHandler.HandleLeaveQueueCommand(user);
        }
    }

    public class QueueStatus : ChatCommand
    {
        public override string Name => "queuestatus";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return PawnQueueCommandHandler.HandleQueueStatusCommand(user);
        }
    }

    public class AcceptPawn : ChatCommand
    {
        public override string Name => "acceptpawn";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return PawnQueueCommandHandler.HandleAcceptPawnCommand(user);
        }
    }

    public class RevivePawn : ChatCommand
    {
        public override string Name => "revive pawn";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (!IsEnabled())
            {
                return "The Revivepawn command is currently disabled.";
            }
            return RevivePawnCommandHandler.HandleRevivePawn(user, args);
        }
    }

    public class HealPawn : ChatCommand
    {
        public override string Name => "heal pawn";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return HealPawnCommandHandler.HandleHealPawn(user, args);
        }
    }
    
    public class DebugRaces : ChatCommand
    {
        public override string Name => "debugraces";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            var excluded = RaceUtils.GetExcludedRaceList();
            var allHumanlike = DefDatabase<ThingDef>.AllDefs.Where(d => d.race?.Humanlike ?? false).Count();
            var available = RaceUtils.GetAllHumanlikeRaces().Count();

            string result = $"Races - Total Humanlike: {allHumanlike}, Available: {available}, Excluded: {excluded.Count}\n";
            result += "Excluded races: " + string.Join(", ", excluded.Take(5)); // Show first 5 for chat

            return result;
        }
    }
}