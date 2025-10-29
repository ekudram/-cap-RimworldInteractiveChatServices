using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractive_GameComponent : GameComponent
    {
        private int tickCounter = 0;
        private const int TICKS_PER_REWARD = 120 * 60; // 2 minutes in ticks (60 ticks/sec * 120 sec)

        public CAPChatInteractive_GameComponent(Game game) { }

        public override void GameComponentTick()
        {
            tickCounter++;
            if (tickCounter >= TICKS_PER_REWARD)
            {
                tickCounter = 0;
                Viewers.AwardActiveViewersCoins();

                // Debug logging to verify it's working
                Logger.Debug("2-minute coin reward tick executed - awarded coins to active viewers");
            }
            StorytellerTick();
        }
        private void StorytellerTick()
        {
            // Your custom storyteller logic here
            // This could trigger events, manage story arcs, etc.
            // All tied to the same efficient tick system
        }
    }
}