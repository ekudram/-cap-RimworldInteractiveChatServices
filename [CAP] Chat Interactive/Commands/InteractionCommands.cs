// InteractionCommands.cs

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
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Commands.InteractionCommands
{
    public class Chitchat : ChatCommand
    {
        public override string Name => "chitchat";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.Chitchat, args);
        }
    }

    public class DeepTalk : ChatCommand
    {
        public override string Name => "deeptalk";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.DeepTalk, args);
        }
    }

    public class Insult : ChatCommand
    {
        public override string Name => "insult";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.Insult, args);
        }
    }

    public class Flirt : ChatCommand
    {
        public override string Name => "flirt";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.RomanceAttempt, args);
        }
    }

    public class Reassure : ChatCommand
    {
        public override string Name => "reassure";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (InteractionDefOf.Reassure == null)
                // return "The 'reassure' interaction requires the Ideology DLC.";
                return "RICS.CC.common.interaction.dlc_required".Translate();
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.Reassure, args);
        }
    }

    public class Nuzzle : ChatCommand
    {
        public override string Name => "nuzzle";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.Nuzzle, args);
        }
    }

    public class AnimalChat : ChatCommand
    {
        public override string Name => "animalchat";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.AnimalChat, args);
        }
    }

    public class MarriageProposal : ChatCommand
    {
        public override string Name => "marry";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.MarriageProposal, args);
        }
    }

    public class BuildRapport : ChatCommand
    {
        public override string Name => "buildrapport";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (InteractionDefOf.BuildRapport == null)
                return "RICS.CC.common.interaction.dlc_required".Translate();
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.BuildRapport, args);
        }
    }

    public class ConvertIdeo : ChatCommand
    {
        public override string Name => "convert";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (InteractionDefOf.ConvertIdeoAttempt == null)
                return "RICS.CC.common.interaction.dlc_required".Translate();
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(messageWrapper, InteractionDefOf.ConvertIdeoAttempt, args);
        }
    }
}