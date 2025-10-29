using System;

namespace CAP_ChatInteractive.Commands.TestCommands
{
    public class HelloWorld : ChatCommand
    {
        public override string Name => "hello";
        public override string Description => "A simple test command";
        public override string PermissionLevel => "everyone";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return $"Hello {user.Username}! Thanks for testing the chat system! 🎉";
        }
    }
}