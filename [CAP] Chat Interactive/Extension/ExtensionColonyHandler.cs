// File: ExtensionColonyHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Structured colony snapshot for the Viewer Hub. Not the chat command processor.

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Extension
{
    public static class ExtensionColonyHandler
    {
        private const float ThreatPointsNormalMax = 10000f;

        public static string HandleGet(ExtensionJob job)
        {
            if (Current.Game == null)
                return ExtensionEnvelope.Fail("NoGame", "Load a colony first.");

            Map map = PlayerHomeMap();
            if (map == null)
                return ExtensionEnvelope.Fail("NoColonyData", "Colony data is currently unavailable.");

            try
            {
                return ExtensionEnvelope.Ok(Build(map));
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Colony: {ex}");
                return ExtensionEnvelope.Fail("NoColonyData", "Colony data is currently unavailable.");
            }
        }

        private static object Build(Map map)
        {
            string colonyName = "Colony";
            if (map.Parent != null)
                colonyName = Plain(map.Parent.LabelCap);
            else if (map.info != null && map.info.parent != null)
                colonyName = Plain(map.info.parent.LabelCap);
            string factionName = Plain(Faction.OfPlayer != null ? Faction.OfPlayer.Name : "Player");

            Vector2 longLat = default;
            try { longLat = Find.WorldGrid.LongLatOf(map.Tile); }
            catch { /* tile  */ }
            int ticks = Find.TickManager?.TicksAbs ?? 0;
            int year = GenDate.Year(ticks, longLat.x);
            Quadrum quadrum = GenDate.Quadrum(ticks, longLat.x);
            int day = GenDate.DayOfQuadrum(ticks, longLat.x) + 1;

            string storyteller = Find.Storyteller?.def != null
                ? Plain(Find.Storyteller.def.LabelCap)
                : "Unknown";
            string difficulty = Find.Storyteller?.difficultyDef != null
                ? Plain(Find.Storyteller.difficultyDef.LabelCap)
                : "Unknown";
            int daysPassed = GenDate.DaysPassed;
            float wealth = 0f;
            try { wealth = map.wealthWatcher?.WealthTotal ?? 0f; } catch { /* ignore */ }

            float threatPoints = 0f;
            try { threatPoints = StorytellerUtility.DefaultThreatPointsNow(map); } catch { /* ignore */ }
            bool threatMaxed = threatPoints >= ThreatPointsNormalMax - 0.5f;

            var viewerSet = ViewerPawnSet();
            var colonists = map.mapPawns?.FreeColonists?.Where(p => p != null && !p.Dead).ToList()
                ?? new List<Pawn>();
            int viewerCount = colonists.Count(p => viewerSet.Contains(p));
            int colonistTotal = colonists.Count;

            var animals = CountByKind(
                PlayerFactionPawns(map).Where(p => p.RaceProps != null && p.RaceProps.Animal),
                top: 10);
            var mechs = CountByKind(
                PlayerFactionPawns(map).Where(p => p.RaceProps != null && p.RaceProps.IsMechanoid),
                top: 0);

            string researchName = null;
            int researchPct = 0;
            try
            {
                ResearchProjectDef proj = Find.ResearchManager?.GetProject();
                if (proj != null)
                {
                    researchName = Plain(proj.LabelCap);
                    researchPct = Mathf.RoundToInt(proj.ProgressPercent * 100f);
                }
            }
            catch { /* ignore */ }

            var threats = CollectThreats(map);

            return new
            {
                name = colonyName,
                colonyName,
                faction = factionName,
                factionName,
                date = new { year, quadrum = Plain(quadrum.Label()), day },
                snapshot = new
                {
                    storyteller,
                    difficulty,
                    daysPassed,
                    wealth = Mathf.RoundToInt(wealth),
                    threatPoints = Mathf.RoundToInt(threatPoints)
                },
                storyteller,
                difficulty,
                daysPassed,
                wealth = Mathf.RoundToInt(wealth),
                threatPoints = Mathf.RoundToInt(threatPoints),
                threatPointsIsMaxed = threatMaxed,
                population = new
                {
                    colonists = colonistTotal,
                    viewerColonists = viewerCount,
                    nonViewerColonists = Math.Max(0, colonistTotal - viewerCount),
                    animals = animals.items,
                    animalTotal = animals.total,
                    mechs = mechs.items,
                    mechTotal = mechs.total
                },
                colonists = new
                {
                    total = colonistTotal,
                    viewer = viewerCount,
                    nonViewer = Math.Max(0, colonistTotal - viewerCount)
                },
                animals = new { total = animals.total, byType = animals.items },
                mechanoids = new { total = mechs.total, byType = mechs.items },
                research = new
                {
                    current = researchName,
                    progressPercent = researchPct,
                    progress = researchPct / 100f,
                    queueCount = 0
                },
                threats
            };
        }

        private static List<object> CollectThreats(Map map)
        {
            var list = new List<object>();
            try
            {
                var conditions = new List<GameCondition>();
                map.GameConditionManager?.GetAllGameConditionsAffectingMap(map, conditions);
                foreach (GameCondition c in conditions)
                {
                    if (c == null || c.def == null)
                        continue;
                    if (!c.def.displayOnUI)
                        continue;
                    if (c.HiddenByOtherCondition(map))
                        continue;
                    string detail = null;
                    if (!c.Permanent && c.TicksLeft > 0 && c.TicksLeft < 100000000)
                        detail = c.TicksLeft.ToStringTicksToPeriod() + " left";
                    string sev = "medium";
                    if (c.def.letterDef == LetterDefOf.ThreatBig || c.def.letterDef == LetterDefOf.NegativeEvent)
                        sev = "high";
                    else if (c.def.letterDef == LetterDefOf.PositiveEvent || c.def.letterDef == LetterDefOf.NeutralEvent)
                        sev = "low";
                    list.Add(new
                    {
                        label = Plain(c.LabelCap),
                        detail,
                        severity = sev
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[RICS Extension] Colony conditions: {ex.Message}");
            }

            try
            {
                int hostiles = map.mapPawns?.AllPawnsSpawned?
                    .Count(p =>
                        p != null
                        && !p.Dead
                        && !p.Downed
                        && p.HostileTo(Faction.OfPlayer))
                    ?? 0;
                if (hostiles > 0)
                {
                    list.Insert(0, new
                    {
                        label = hostiles + (hostiles == 1 ? " hostile on the map" : " hostiles on the map"),
                        detail = (string)null,
                        severity = "high"
                    });
                }
            }
            catch { /* ignore */ }

            return list;
        }

        private static HashSet<Pawn> ViewerPawnSet()
        {
            var set = new HashSet<Pawn>();
            try
            {
                var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                var list = mgr?.GetAllViewerPawns();
                if (list != null)
                {
                    foreach (Pawn p in list)
                    {
                        if (p != null)
                            set.Add(p);
                    }
                }
            }
            catch { /* ignore */ }
            return set;
        }

        private static IEnumerable<Pawn> PlayerFactionPawns(Map map)
        {
            var list = map.mapPawns?.PawnsInFaction(Faction.OfPlayer);
            if (list == null)
                yield break;
            foreach (Pawn p in list)
            {
                if (p != null && !p.Dead && !p.Destroyed)
                    yield return p;
            }
        }

        private static (int total, List<object> items) CountByKind(IEnumerable<Pawn> pawns, int top)
        {
            var groups = pawns
                .GroupBy(p => KindLabel(p))
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ThenBy(x => x.label)
                .ToList();
            int total = groups.Sum(x => x.count);
            var items = groups
                .Take(top > 0 ? top : groups.Count)
                .Select(x => (object)new { x.label, x.count })
                .ToList();
            return (total, items);
        }

        private static Map PlayerHomeMap()
        {
            if (Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome)
                return Find.CurrentMap;
            if (Find.Maps == null)
                return Find.CurrentMap;
            foreach (Map map in Find.Maps)
            {
                if (map != null && map.IsPlayerHome)
                    return map;
            }
            foreach (Map map in Find.Maps)
            {
                if (map != null && map.ParentFaction == Faction.OfPlayer)
                    return map;
            }
            return Find.CurrentMap;
        }

        private static string KindLabel(Pawn p)
        {
            if (p?.kindDef != null)
            {
                string kind = Plain(p.kindDef.LabelCap);
                if (!string.IsNullOrEmpty(kind))
                    return kind;
            }
            if (p?.def != null)
            {
                string def = Plain(p.def.label);
                if (!string.IsNullOrEmpty(def))
                    return def;
            }
            return "Unknown";
        }

        private static string Plain(TaggedString text)
        {
            return Plain(text.ToString());
        }

        private static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            string s = text;
            try { s = s.StripTags(); } catch { /* keep */ }
            s = Regex.Replace(s, @"<[^>]+>", string.Empty);
            return s.Trim();
        }
    }
}
