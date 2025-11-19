// TestCommands.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// A simple test command that responds with a greeting message
using System;

namespace CAP_ChatInteractive.Commands.TestCommands
{
    public class ModInfo : ChatCommand
    {
        public override string Name => "modinfo";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {

            // Get command settings
            // var settingsCommand = GetCommandSettings();
            return "[CAP] Rimwold Interactive Chat Service v1.0.5 beta 11/18/2023 - Twitch & YouTube integration for RimWorld!";
        }
    }
    public class Hello : ChatCommand
    {
        public override string Name =>  "hello";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return $"Hello {messageWrapper.Username}! Thanks for testing the chat system! 🎉";
        }
    }

    public class CaptoLamia : ChatCommand
    {
        public override string Name => "CaptoLamia";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Check if the user is you by username AND platform ID
            bool isCaptoLamia = user.Username == "captolamia" &&
                               user.PlatformUserId == "58513264" &&
                               user.Platform.ToLowerInvariant() == "twitch";

            if (!isCaptoLamia)
            {
                return $"Sorry {user.DisplayName}, this command is not available. 👀";
            }

            return $"😸 Hello {user.DisplayName}! Thanks for testing the chat system! 🎉 This is your special easter egg command!";
        }
    }
}