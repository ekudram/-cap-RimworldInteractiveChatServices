// TestCommands.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// A simple test command that responds with a greeting message
using System;

namespace CAP_ChatInteractive.Commands.TestCommands
{
    public class Hello : ChatCommand
    {
        public override string Name => "hello";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return $"Hello {user.Username}! Thanks for testing the chat system! 🎉";
        }
    }
}