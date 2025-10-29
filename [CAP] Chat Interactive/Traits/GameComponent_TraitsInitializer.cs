// GameComponent_TraitsInitializer.cs
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Traits
{
    public class GameComponent_TraitsInitializer : GameComponent
    {
        private bool traitsInitialized = false;

        public GameComponent_TraitsInitializer(Game game) { }

        public override void LoadedGame()
        {
            InitializeTraits();
        }

        public override void StartedNewGame()
        {
            InitializeTraits();
        }

        public override void GameComponentTick()
        {
            // Initialize on first tick to ensure all defs are loaded
            if (!traitsInitialized && Current.ProgramState == ProgramState.Playing)
            {
                InitializeTraits();
            }
        }

        private void InitializeTraits()
        {
            if (!traitsInitialized)
            {
                TraitsManager.InitializeTraits();
                traitsInitialized = true;
            }
        }
    }
}