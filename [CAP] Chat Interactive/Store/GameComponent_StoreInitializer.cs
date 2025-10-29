// GameComponent_StoreInitializer.cs
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Store
{
    public class GameComponent_StoreInitializer : GameComponent
    {
        private bool storeInitialized = false;

        public GameComponent_StoreInitializer(Game game) { }

        public override void LoadedGame()
        {
            InitializeStore();
        }

        public override void StartedNewGame()
        {
            InitializeStore();
        }

        public override void GameComponentTick()
        {
            // Initialize on first tick to ensure all defs are loaded
            if (!storeInitialized && Current.ProgramState == ProgramState.Playing)
            {
                InitializeStore();
            }
        }

        private void InitializeStore()
        {
            if (!storeInitialized)
            {
                StoreInventory.InitializeStore();
                storeInitialized = true;
            }
        }
    }
}