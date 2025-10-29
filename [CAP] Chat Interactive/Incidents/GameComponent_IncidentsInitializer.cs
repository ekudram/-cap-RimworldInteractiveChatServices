// GameComponent_IncidentsInitializer.cs - UPDATED
using CAP_ChatInteractive.Commands.ViewerCommands;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public class GameComponent_IncidentsInitializer : GameComponent
    {
        private bool incidentsInitialized = false;
        private bool weatherInitialized = false;

        public GameComponent_IncidentsInitializer(Game game) { }

        public override void LoadedGame()
        {
            InitializeSystems();
        }

        public override void StartedNewGame()
        {
            InitializeSystems();
        }

        public override void GameComponentTick()
        {
            if ((!incidentsInitialized || !weatherInitialized) && Current.ProgramState == ProgramState.Playing)
            {
                InitializeSystems();
            }
        }

        private void InitializeSystems()
        {
            if (!incidentsInitialized)
            {
                IncidentsManager.InitializeIncidents();
                incidentsInitialized = true;
            }

            if (!weatherInitialized)
            {
                Weather.BuyableWeatherManager.InitializeWeather();
                weatherInitialized = true;
            }
        }
    }
}