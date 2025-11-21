// InteractionCommands.cs
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using System;

namespace CAP_ChatInteractive.Commands.InteractionCommands
{
    public class Chitchat : ChatCommand
    {
        public override string Name => "chitchat";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.Chitchat, args);
        }
    }

    public class DeepTalk : ChatCommand
    {
        public override string Name => "deeptalk";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.DeepTalk, args);
        }
    }

    public class Insult : ChatCommand
    {
        public override string Name => "insult";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.Insult, args);
        }
    }

    public class Flirt : ChatCommand
    {
        public override string Name => "flirt";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.RomanceAttempt, args);
        }
    }

    public class Reassure : ChatCommand
    {
        public override string Name => "reassure";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (InteractionDefOf.Reassure == null)
                return "The 'reassure' interaction requires the Ideology DLC.";
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.Reassure, args);
        }
    }

    public class Nuzzle : ChatCommand
    {
        public override string Name => "nuzzle";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.Nuzzle, args);
        }
    }

    public class AnimalChat : ChatCommand
    {
        public override string Name => "animalchat";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.AnimalChat, args);
        }
    }

    public class MarriageProposal : ChatCommand
    {
        public override string Name => "marry";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.MarriageProposal, args);
        }
    }

    public class BuildRapport : ChatCommand
    {
        public override string Name => "buildrapport";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.BuildRapport, args);
        }
    }

    public class ConvertIdeo : ChatCommand
    {
        public override string Name => "convert";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (InteractionDefOf.ConvertIdeoAttempt == null)
                return "The 'convert' interaction requires the Ideology DLC.";
            return EnhancedInteractionCommandHandler.HandleInteractionCommand(user, InteractionDefOf.ConvertIdeoAttempt, args);
        }
    }
}