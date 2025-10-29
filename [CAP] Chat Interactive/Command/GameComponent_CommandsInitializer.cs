// GameComponent_CommandsInitializer.cs
using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    public class GameComponent_CommandsInitializer : GameComponent
    {
        private bool commandsInitialized = false;

        public GameComponent_CommandsInitializer(Game game) { }

        public override void LoadedGame()
        {
            InitializeCommands();
        }

        public override void StartedNewGame()
        {
            InitializeCommands();
        }

        public override void GameComponentTick()
        {
            // Initialize on first tick to ensure all defs are loaded
            if (!commandsInitialized && Current.ProgramState == ProgramState.Playing)
            {
                InitializeCommands();
            }
        }

        private void InitializeCommands()
        {
            if (!commandsInitialized)
            {
                Logger.Debug("Initializing commands via GameComponent...");

                // Register commands from XML Defs
                RegisterDefCommands();

                commandsInitialized = true;
                Logger.Message("[CAP] Commands initialized successfully");
            }
        }

        private void RegisterDefCommands()
        {
            var defs = DefDatabase<ChatCommandDef>.AllDefsListForReading;

            // Register all ChatCommandDefs with the processor
            foreach (var commandDef in defs)
            {
                commandDef.RegisterCommand();
            }
        }
    }
}