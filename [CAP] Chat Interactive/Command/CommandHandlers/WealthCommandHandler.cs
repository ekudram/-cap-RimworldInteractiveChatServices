// File: WealthCommandHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// !wealth [raid|trend] — colony wealth breakdown for chat
using RimWorld;
using System;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class WealthCommandHandler
    {
        private const string ReturnDivider = " | ";

        public static string HandleWealthCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();

                var map = GetPlayerHomeMap();
                if (map == null)
                    return "RICS.Wealth.NoMap".Translate();

                var wealthWatcher = map.wealthWatcher;
                if (wealthWatcher == null)
                    return "RICS.Wealth.Error".Translate();

                // Force a refresh so chat reflects current values
                try
                {
                    wealthWatcher.ForceRecount();
                }
                catch
                {
                    // Older/alternate builds may not expose ForceRecount the same way
                }

                var report = new StringBuilder();
                report.Append("RICS.Wealth.Header".Translate());
                report.Append(ReturnDivider);
                report.Append("RICS.Wealth.Total".Translate(FormatWealth(wealthWatcher.WealthTotal)));
                report.Append(ReturnDivider);
                report.Append("RICS.Wealth.BreakdownHeader".Translate());
                report.Append(ReturnDivider);
                report.Append("RICS.Wealth.Items".Translate(FormatWealth(wealthWatcher.WealthItems)));
                report.Append(ReturnDivider);
                report.Append("RICS.Wealth.Buildings".Translate(FormatWealth(wealthWatcher.WealthBuildings)));
                report.Append(ReturnDivider);
                report.Append("RICS.Wealth.Pawns".Translate(FormatWealth(wealthWatcher.WealthPawns)));

                string mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;

                if (mode == "raid")
                {
                    report.Append(ReturnDivider);
                    report.Append("RICS.Wealth.RaidHeader".Translate());
                    report.Append(ReturnDivider);
                    report.Append("RICS.Wealth.RaidPoints".Translate(CalculateRaidPoints(map).ToString("F0")));
                    report.Append(ReturnDivider);
                    report.Append("RICS.Wealth.RaidNote".Translate());
                }
                else if (mode == "trend")
                {
                    report.Append(ReturnDivider);
                    report.Append("RICS.Wealth.TrendHeader".Translate());
                    report.Append(ReturnDivider);
                    report.Append("RICS.Wealth.Tip1".Translate());
                    report.Append(ReturnDivider);
                    report.Append("RICS.Wealth.Tip2".Translate());
                    report.Append(ReturnDivider);
                    report.Append("RICS.Wealth.Tip3".Translate());
                }

                return report.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Wealth] Error in HandleWealthCommand: {ex}");
                return "RICS.Wealth.Error".Translate();
            }
        }

        private static string FormatWealth(float wealth)
        {
            string currency = "RICS.Wealth.Currency".Translate();
            return wealth.ToString("N0") + " " + currency;
        }

        private static Map GetPlayerHomeMap()
        {
            if (Current.Game == null || Find.Maps == null)
                return null;

            foreach (var map in Find.Maps)
            {
                if (map != null && map.IsPlayerHome)
                    return map;
            }

            foreach (var map in Find.Maps)
            {
                if (map != null && map.ParentFaction == Faction.OfPlayer)
                    return map;
            }

            return Find.CurrentMap;
        }

        private static float CalculateRaidPoints(Map map)
        {
            try
            {
                if (map?.wealthWatcher == null)
                    return 0f;

                float wealthFactor = map.wealthWatcher.WealthTotal / 10000f;
                float points = 35f + (wealthFactor * 40f);

                var difficulty = Find.Storyteller?.difficulty;
                if (difficulty != null)
                    points *= difficulty.threatScale;

                var tickManager = Find.TickManager;
                if (tickManager != null)
                {
                    float daysPassed = tickManager.TicksGame / 60000f;
                    points += daysPassed * 1.4f;
                }

                return Math.Max(points, 35f);
            }
            catch
            {
                return 0f;
            }
        }
    }
}
