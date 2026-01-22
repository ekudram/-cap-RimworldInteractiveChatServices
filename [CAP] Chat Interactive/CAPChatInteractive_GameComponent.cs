// CAPChatInteractive_GameComponent.cs (updated with improved null safety)
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
// A game component that handles periodic tasks such as awarding coins to active viewers and managing storyteller ticks.
// Uses an efficient tick system to minimize performance impact.
// Storyteller tick logic can be expanded as needed.


using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractive_GameComponent : GameComponent
    {
        private int tickCounter = 0;
        private int saveCounter = 0;
        private const int TICKS_PER_REWARD = 120 * 60; // 2 minutes in ticks (60 ticks/sec * 120 sec)
        private bool versionCheckDone = false;

        public CAPChatInteractive_GameComponent(Game game)
        {
            if (game?.components == null) return;

            if (game.GetComponent<LootBoxComponent>() == null)
            {
                game.components.Add(new LootBoxComponent(game));
                Logger.Debug("LootBoxComponent created by GameComponent");
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            PerformVersionCheckIfNeeded(); // Check version on game load as well
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            PerformVersionCheckIfNeeded(); // Check version on new game start as well
        }

        public override void GameComponentTick()
        {
            tickCounter++;
            saveCounter++;
            
            if (tickCounter >= TICKS_PER_REWARD)
            {
                tickCounter = 0;
                Viewers.AwardActiveViewersCoins();

                Logger.Debug("2-minute coin reward tick executed - awarded coins to active viewers");
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Logger.Debug("GameComponent FinalizeInit - ensuring store is initialized");
            Store.StoreInventory.InitializeStore();
        }

        // MOD VERSION CHECKING AND UPDATE NOTIFICATIONS
        private void PerformVersionCheckIfNeeded()
        {
            if (versionCheckDone) return;
            versionCheckDone = true;

            CheckForVersionUpdate();
        }
        // Check if the mod version has changed and show update notes if needed
        private void CheckForVersionUpdate()
        {
            var mod = CAPChatInteractiveMod.Instance;
            if (mod == null)
            {
                Logger.Error("Cannot check version update - CAPChatInteractiveMod.Instance is null");
                return;
            }

            var settingsContainer = mod.Settings;
            if (settingsContainer == null)
            {
                Logger.Error("Cannot check version update - mod Settings container is null");
                return;
            }

            var globalSettings = settingsContainer.GlobalSettings;
            if (globalSettings == null)
            {
                Logger.Error("Cannot check version update - GlobalSettings is null");
                return;
            }

            string currentVersion = globalSettings.modVersion ?? "Unknown";
            string savedVersion = globalSettings.modVersionSaved;

            Logger.Debug($"Version check - Current: {currentVersion}, Saved: {savedVersion ?? "None"}");

            bool isFirstTimeOrMigration = string.IsNullOrEmpty(savedVersion);

            if (isFirstTimeOrMigration || savedVersion != currentVersion)
            {
                string previousVersion = savedVersion ?? "First install / migration";

                // Update saved version
                globalSettings.modVersionSaved = currentVersion;

                // Attempt to save settings
                if (settingsContainer != null)
                {
                    try
                    {
                        settingsContainer.Write();
                        Logger.Debug($"Updated saved version from '{previousVersion}' to '{currentVersion}'");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.Error($"Failed to save settings after version update: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Warning("Could not save settings - Settings container became null");
                }

                ShowUpdateNotification(currentVersion, previousVersion);
            }
            else
            {
                Logger.Debug("No version change detected");
            }
        }

        private void ShowUpdateNotification(string newVersion, string oldVersion)
        {
            if (Find.WindowStack == null)
            {
                Logger.Warning("Cannot show update notification - WindowStack is not available yet");
                return;
            }

            try
            {
                string updateNotes = GetUpdateNotesForVersion(newVersion, oldVersion);
                Find.WindowStack.Add(new Dialog_RICS_Updates(updateNotes));

                Logger.Message($"[RICS] Updated from version {oldVersion} to {newVersion}. Showing update notes.");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error showing update notification: {ex.Message}");
            }
        }

        private string GetUpdateNotesForVersion(string newVersion, string oldVersion)
        {
            if (VersionHistory.UpdateNotes == null)
            {
                return FallbackUpdateMessage(newVersion, oldVersion);
            }

            if (VersionHistory.UpdateNotes.TryGetValue(newVersion, out string notes))
            {
                return notes;
            }

            // Fallback for unknown versions
            return FallbackUpdateMessage(newVersion, oldVersion);
        }

        private string FallbackUpdateMessage(string newVersion, string oldVersion)
        {
            return $"RICS has been updated to version {newVersion}.\n\n" +
                   $"Previous version: {(string.IsNullOrEmpty(oldVersion) ? "First install / unknown" : oldVersion)}\n\n" +
                   "Please check the mod's documentation or Steam Workshop page for the detailed changelog.";
        }
    }
}