// CAPChatInteractive_GameComponent.cs (updated)
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
        private const int TICKS_PER_REWARD = 120 * 60; // 2 minutes in ticks (60 ticks/sec * 120 sec)
        private bool versionCheckDone = false;

        public CAPChatInteractive_GameComponent(Game game)
        {
            // Ensure lootbox component exists when this game component is created
            if (game != null && game.components != null)
            {
                var existingLootboxComponent = game.GetComponent<LootBoxComponent>();
                if (existingLootboxComponent == null)
                {
                    game.components.Add(new LootBoxComponent(game));
                    Logger.Debug("LootBoxComponent created by GameComponent");
                }
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();

            // Check for version updates when game is loaded
            if (!versionCheckDone)
            {
                CheckForVersionUpdate();
                versionCheckDone = true;
            }
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();

            // Check for version updates when new game is started
            if (!versionCheckDone)
            {
                CheckForVersionUpdate();
                versionCheckDone = true;
            }
        }

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
        }

        private void CheckForVersionUpdate()
        {
            try
            {
                // In CheckForVersionUpdate() - for testing only
                // Force it to show the update dialog
                /*
                if (true) // Always show for testing
                {
                    ShowUpdateNotification("1.0.14", "1.0.13");
                }
                */


                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null)
                {
                    Logger.Error("Cannot check version update - settings not available");
                    return;
                }

                string currentVersion = settings.modVersion;
                string savedVersion = settings.modVersionSaved;

                Logger.Debug($"Version check - Current: {currentVersion}, Saved: {savedVersion}");

                // Special handling for first-time/migration from empty version
                bool isFirstTimeOrMigration = string.IsNullOrEmpty(savedVersion);

                // If versions don't match, show update notification
                if (isFirstTimeOrMigration || savedVersion != currentVersion)
                {
                    // Update the saved version
                    string previousVersion = savedVersion;
                    settings.modVersionSaved = currentVersion;

                    // Force save the settings
                    if (CAPChatInteractiveMod.Instance?.Settings != null)
                    {
                        CAPChatInteractiveMod.Instance.Settings.Write();
                        Logger.Debug($"Updated saved version from '{previousVersion}' to '{currentVersion}'");
                    }

                    // Show update notification window
                    ShowUpdateNotification(currentVersion, previousVersion);
                }
                else
                {
                    Logger.Debug("No version change detected");
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error checking version update: {ex}");
            }
        }

        private void ShowUpdateNotification(string newVersion, string oldVersion)
        {
            try
            {
                // Get update notes based on version
                string updateNotes = GetUpdateNotesForVersion(newVersion, oldVersion);

                // Show the update dialog
                Find.WindowStack.Add(new Dialog_RICS_Updates(updateNotes));

                Logger.Message($"[RICS] Updated from version {oldVersion} to {newVersion}. Showing update notes.");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error showing update notification: {ex}");
            }
        }

        private string GetUpdateNotesForVersion(string newVersion, string oldVersion)
        {
            // Check if we have specific notes for this version
            if (VersionHistory.UpdateNotes.ContainsKey(newVersion))
            {
                // Special handling for critical migrations
                if (newVersion == "1.0.14" && (string.IsNullOrEmpty(oldVersion) || oldVersion != "1.0.14"))
                {
                    return VersionHistory.GetMigrationNotes(oldVersion, newVersion);
                }

                return VersionHistory.UpdateNotes[newVersion];
            }

            // Fallback for unknown versions
            return $"RICS has been updated to version {newVersion}.\n\n" +
                   $"Previous version: {oldVersion}\n\n" +
                   "Please check the mod's documentation for detailed changelog.";
        }
    }
}