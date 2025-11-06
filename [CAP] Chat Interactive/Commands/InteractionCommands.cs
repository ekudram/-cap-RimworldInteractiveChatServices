// InteractionCommands.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.

using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.InteractionCommands
{
    public class Chitchat : ChatCommand
    {
        public override string Name => "chitchat";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return InteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.Chitchat, args);
        }
    }

    public class DeepTalk : ChatCommand
    {
        public override string Name => "deeptalk";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return InteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.DeepTalk, args);
        }
    }

    public class Insult : ChatCommand
    {
        public override string Name => "insult";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return InteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.Insult, args);
        }
    }

    public class Flirt : ChatCommand
    {
        public override string Name => "flirt";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return InteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.RomanceAttempt, args);
        }
    }

    public class Reassure : ChatCommand
    {
        public override string Name => "reassure";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if Ideology is available
            if (InteractionDefOf.Reassure == null)
            {
                return "The 'reassure' interaction requires the Ideology DLC.";
            }
            return InteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.Reassure, args);
        }
    }

    public class Nuzzle : ChatCommand
    {
        public override string Name => "nuzzle";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return InteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.Nuzzle, args);
        }
    }

    public class AnimalChat : ChatCommand
    {
        public override string Name => "animalchat";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return InteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.AnimalChat, args);
        }
    }
}