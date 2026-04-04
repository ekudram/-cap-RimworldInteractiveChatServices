// TestCommands.cs
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
// A simple test command that responds with a greeting message
using System;

namespace CAP_ChatInteractive.Commands.TestCommands
{
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

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var globalChatSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            // Check if the user is you by username AND platform ID
            bool isCaptoLamia = messageWrapper.Username == "captolamia" &&
                               messageWrapper.PlatformUserId == "58513264" &&
                               messageWrapper.Platform.ToLowerInvariant() == "twitch";

            if (!isCaptoLamia)
            {
                return $"Sorry {messageWrapper.DisplayName}, this command is not available. 👀";
            }

            return $"😸 Hello {messageWrapper.DisplayName}! RICS {globalChatSettings.modVersion}. The MOD Developer is present in chat! ";
        }
    }
}