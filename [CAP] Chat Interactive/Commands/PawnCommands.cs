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

            // Handle mypawn command
            if (firstArg == "mypawn")
            {
                return HandleMyPawnCommand(user);
            }

            // Handle purchase - delegate ALL parsing to BuyPawnCommandHandler
            return BuyPawnCommandHandler.HandleBuyPawnCommand(user, args);
        }

        private string ShowPawnHelp()
        {
            return "Usage: !pawn <race> [xenotype] [gender] [age] OR !pawn list <races|xenotypes> OR !pawn mypawn";
        }

        private string HandleListCommand(ChatMessageWrapper user, string[] args)
        {
            if (args.Length > 1)
            {
                string listType = args[1].ToLower();
                switch (listType)
                {
                    case "races":
                        return BuyPawnCommandHandler.ListAvailableRaces();
                    case "xenotypes":
                        // Support: !pawn list xenotypes human
                        string raceName = args.Length > 2 ? args[2] : null;
                        return BuyPawnCommandHandler.ListAvailableXenotypes(raceName);
                    default:
                        return $"Unknown list type: {listType}. Use: races, xenotypes";
                }
            }

            return "List types: races, xenotypes [race]. Examples: !pawn list races, !pawn list xenotypes human";
        }

        private string HandleMyPawnCommand(ChatMessageWrapper user)
        {
            return BuyPawnCommandHandler.HandleMyPawnCommand(user);
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

    public class Trait : ChatCommand
    {
        public override string Name => "trait";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return TraitsCommandHandler.HandleLookupTraitCommand(user, args);
        }
    }

    public class AddTrait : ChatCommand
    {
        public override string Name => "addtrait";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return TraitsCommandHandler.HandleAddTraitCommand(user, args);
        }
    }

    public class RemoveTrait : ChatCommand
    {
        public override string Name => "removetrait";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return TraitsCommandHandler.HandleRemoveTraitCommand(user, args);
        }
    }

    public class ListTraits : ChatCommand
    {
        public override string Name => "traits";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return TraitsCommandHandler.HandleListTraitsCommand(user, args);
        }
    }

    public class Leave : ChatCommand
    {
        public override string Name => "leave";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
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
        public override string Name => "JoinQueue";

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
        public override string Name => "healpawn";

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