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

            return $"😸 Hello {user.DisplayName}! The MOD DEV is present!";
        }
    }
}