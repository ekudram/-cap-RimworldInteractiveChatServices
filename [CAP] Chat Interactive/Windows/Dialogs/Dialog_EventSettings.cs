// Dialog_EventSettings.cs
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

using CAP_ChatInteractive.Incidents;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_EventSettings : Window
    {
        private CAPGlobalChatSettings settings;
        private Vector2 scrollPosition = Vector2.zero;
        private Dictionary<string, string> numericBuffers = new Dictionary<string, string>();

        public override Vector2 InitialSize => new Vector2(500f, 600f);

        public Dialog_EventSettings()
        {
            settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Rect titleRect = new Rect(0f, 0f, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            // Widgets.Label(titleRect, "Event Settings");
            Widgets.Label(titleRect, "RICS.Header.Event".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Content area
            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
            DrawSettings(contentRect);
        }

        private void DrawSettings(Rect rect)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, 800f); // Enough height for all content

            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // Event Statistics
            DrawEventStatistics(listing);

            listing.Gap(12f);

            // Display Settings
            DrawDisplaySettings(listing);

            listing.Gap(12f);

            // Cooldown Settings
            DrawCooldownSettings(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawEventStatistics(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            // listing.Label("Event Statistics");
            listing.Label("RICS.Header.EventStat".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            int totalEvents = IncidentsManager.AllBuyableIncidents?.Count ?? 0;
            int enabledEvents = IncidentsManager.AllBuyableIncidents?.Values.Count(e => e.Enabled) ?? 0;
            int availableEvents = IncidentsManager.AllBuyableIncidents?.Values.Count(e => e.IsAvailableForCommands) ?? 0;

            // listing.Label($"Total Events: {totalEvents}");
            // listing.Label($"Enabled Events: {enabledEvents}");
            // listing.Label($"Available for Commands: {availableEvents}");
            // listing.Label($"Unavailable Events: {totalEvents - availableEvents}");

            listing.Label("RICS.Message.TotalEvents".Translate(totalEvents)); 
            listing.Label("RICS.Message.EnabledEvents".Translate(enabledEvents) );
            listing.Label("RICS.Message.AvailableforCommands".Translate(availableEvents));
            int unavailableEvents = totalEvents - availableEvents;
            listing.Label("RICS.Message.UnavailableEvents".Translate(unavailableEvents));

            // Breakdown by karma type
            if (IncidentsManager.AllBuyableIncidents != null)
            {
                var karmaGroups = IncidentsManager.AllBuyableIncidents.Values
                    .Where(e => e.IsAvailableForCommands)
                    .GroupBy(e => e.KarmaType)
                    .OrderByDescending(g => g.Count());

                listing.Gap(4f);
                Text.Font = GameFont.Tiny;
                // listing.Label("Available events by karma type:");
                listing.Label("RICS.Message.EventsbyKarma".Translate());
                // WE ARE HERE
                foreach (var group in karmaGroups)
                {
                    string karmaType = $"RICS.KarmaType.{group.Key}".Translate();
                    listing.Label("RICS.Message.KarmaGroup".Translate(karmaType, group.Count()));
                }
                Text.Font = GameFont.Small;
            }
        }

        private void DrawDisplaySettings(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            // listing.Label("Display Settings");
            listing.Label("RICS.Header.Display".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            // Show unavailable events setting
            bool showUnavailable = settings.GetType().GetField("ShowUnavailableEvents")?.GetValue(settings) as bool? ?? true;
            bool newShowUnavailable = showUnavailable;

            // listing.CheckboxLabeled("Show unavailable events", ref newShowUnavailable,
            // "When enabled, events that are not available via commands will still be visible in the editor");
            listing.CheckboxLabeled("RICS.Message.ShowUnavailable".Translate(),
                ref newShowUnavailable,
                "RICS.Message.ShowUnavailableDesc".Translate());

            if (newShowUnavailable != showUnavailable)
            {
                var field = settings.GetType().GetField("ShowUnavailableEvents");
                if (field != null)
                {
                    field.SetValue(settings, newShowUnavailable);
                }
                else
                {
                    // If the field doesn't exist, we need to add it to the settings class
                    Logger.Warning("ShowUnavailableEvents field not found in settings");
                }
            }

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            // listing.Label("Unavailable events are grayed out and cannot be enabled");
            listing.Label("RICS.Message.ShowUnavailableDesc".Translate());
            Text.Font = GameFont.Small;
        }

        private void DrawCooldownSettings(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            // listing.Label("Cooldown Settings");
            listing.Label("RICS.Header.Cooldown".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            // Event cooldown toggle
            // listing.CheckboxLabeled("Enable event cooldowns", ref settings.EventCooldownsEnabled,"When enabled, events will go on cooldown after being purchased");
            listing.CheckboxLabeled("RICS.Message.EnableCooldown".Translate(),
                ref settings.EventCooldownsEnabled,
                "RICS.Message.EnableCooldownDesc".Translate());

            // Cooldown days
            // NumericField(listing, "Event cooldown duration (days):", ref settings.EventCooldownDays, 1f, 30f);
            NumericField(listing, "RICS.Message.CooldownDuration".Translate(), ref settings.EventCooldownDays, 1f, 30f);
            Text.Font = GameFont.Tiny;
            // listing.Label($"Events will be unavailable for {settings.EventCooldownDays} in-game days after purchase");
            listing.Label("RICS.Message.CooldownDurationDesc".Translate(settings.EventCooldownDays));
            Text.Font = GameFont.Small;

            // Events per cooldown period
            //NumericField(listing, "Events per cooldown period:", ref settings.EventsperCooldown, 1f, 50f);
            NumericField(listing, "RICS.Message.EventsPerCooldown".Translate(), ref settings.EventsperCooldown, 1f, 50f);
            Text.Font = GameFont.Tiny;
            // listing.Label($"Limit of {settings.EventsperCooldown} event purchases per cooldown period");
            listing.Label("RICS.Message.EventsPerCooldownDesc".Translate(settings.EventsperCooldown));
            Text.Font = GameFont.Small;

            listing.Gap(12f);

            // Karma type limits toggle
            // listing.CheckboxLabeled("Limit events by karma type", ref settings.KarmaTypeLimitsEnabled,
            // "Restrict how many events of each karma type can be purchased within a period");
            listing.CheckboxLabeled("RICS.Message.KarmaLimits".Translate(),
                ref settings.KarmaTypeLimitsEnabled,
                "RICS.Message.KarmaLimitsDesc".Translate());

            if (settings.KarmaTypeLimitsEnabled)
            {
                listing.Gap(4f);
                // NumericField(listing, "Max bad event purchases:", ref settings.MaxBadEvents, 1f, 20f);
                // NumericField(listing, "Max good event purchases:", ref settings.MaxGoodEvents, 1f, 20f);
                // NumericField(listing, "Max neutral event purchases:", ref settings.MaxNeutralEvents, 1f, 20f);
                NumericField(listing, "RICS.Message.MaxNeutralEvents".Translate(), ref settings.MaxNeutralEvents, 1f, 20f);
                NumericField(listing, "RICS.Message.MaxBadEvents".Translate(), ref settings.MaxBadEvents, 1f, 20f);
                NumericField(listing, "RICS.Message.MaxGoodEvents".Translate(), ref settings.MaxGoodEvents, 1f, 20f);
            }

            listing.Gap(12f);

            // Store purchase limits
            // NumericField(listing, "Max item purchases per day:", ref settings.MaxItemPurchases, 1, 50);
            NumericField(listing, "RICS.Message.MaxItemPurchases".Translate(), ref settings.MaxItemPurchases, 1, 50);
            Text.Font = GameFont.Tiny;
            // listing.Label($"Viewers can purchase up to {settings.MaxItemPurchases} items per game day before cooldown");
            listing.Label("RICS.Message.MaxItemPurchasesDesc".Translate(settings.MaxItemPurchases));
            Text.Font = GameFont.Small;
        }

        private void NumericField(Listing_Standard listing, string label, ref int value, float min, float max)
        {
            Rect rect = listing.GetRect(30f);
            Rect leftRect = rect.LeftHalf().Rounded();
            Rect rightRect = rect.RightHalf().Rounded();

            Widgets.Label(leftRect, label);

            // Create a unique key based on the label
            string bufferKey = $"Settings_{label.GetHashCode()}";
            if (!numericBuffers.ContainsKey(bufferKey))
            {
                numericBuffers[bufferKey] = value.ToString();
            }

            string _numBufferString = numericBuffers[bufferKey];
            Widgets.TextFieldNumeric(rightRect, ref value, ref _numBufferString, min, max);

            // CRITICAL: Store the modified buffer back into the dictionary
            numericBuffers[bufferKey] = _numBufferString;

            listing.Gap(2f);
        }
    }
}