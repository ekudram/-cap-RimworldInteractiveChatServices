// Windows/Window_Commands.cs
using RimWorld;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Windows
{
    public class Window_Commands : Window
    {
        public Window_Commands()
        {
            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Chat Commands");
            listing.GapLine();

            listing.Label("Available commands:");
            listing.Label("!help - Show available commands");
            listing.Label("!points - Check your coin balance");
            listing.Label("More commands coming soon...");

            listing.Gap();
            listing.Label("Command management interface coming soon...");

            listing.End();
        }
    }
}