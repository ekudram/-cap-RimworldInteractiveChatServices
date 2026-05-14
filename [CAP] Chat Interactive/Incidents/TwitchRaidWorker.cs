// Source/RICS/Incidents/TwitchRaidWorker.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive. RICS Rimworld Chat Interactive Service
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
// Source/RICS/Incidents/TwitchRaidWorker.cs
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace CAP_ChatInteractive.Incidents
{
    public class IncidentWorker_TwitchRaid : IncidentWorker_RaidEnemy
    {
        public static List<string> CurrentRaidUsernames { get; set; } = new List<string>();

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Logger.Twitch($"[TWITCH RAID WORKER] Starting custom raid | Names in list: {CurrentRaidUsernames.Count} | Faction: {parms.faction?.Name ?? "null"}");

            if (parms.target is not Map map)
            {
                Logger.Twitch("WARNING: No valid map target for raid.");
                return false;
            }

            // Default to edge walk-in first (as you requested)
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;

            // Resolve spawn center (fixes -1000,-1000 out-of-bounds)
            if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                Logger.Twitch("EdgeWalkIn spawn failed → falling back to weighted drop mode");

                var dropOptions = new List<(PawnsArrivalModeDef mode, float weight)>
                {
                    (PawnsArrivalModeDefOf.CenterDrop, 1.0f),   // High danger - rare
                    (PawnsArrivalModeDefOf.EdgeDrop,   2.5f),   // Medium danger - common
                    (PawnsArrivalModeDefOf.RandomDrop, 1.5f)    // Chaos - medium
                };

                var chosen = dropOptions.RandomElementByWeight(t => t.weight);
                parms.raidArrivalMode = chosen.mode;
                parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);
                Logger.Twitch($"Fallback arrival mode: {parms.raidArrivalMode.defName}");
            }

            // === Generate named raiders ===
            var raidNames = CurrentRaidUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();

            if (raidNames.Count == 0)
            {
                Logger.Twitch("No raid names → falling back to vanilla raid");
                return base.TryExecuteWorker(parms);
            }

            var onlyRaiders = CAPChatInteractiveMod.Instance.Settings.GlobalSettings.TwitchRaidsOnlyRaiders;
            int maxRaiders = onlyRaiders ? raidNames.Count : 10;

            var spawnedPawns = new List<Pawn>();

            for (int i = 0; i < maxRaiders && i < raidNames.Count; i++)
            {
                string viewer = raidNames[i];

                PawnKindDef kind = GetAppropriateRaidKind();

                var request = new PawnGenerationRequest(kind, parms.faction,
                    forceGenerateNewPawn: true,
                    forceRedressWorldPawnIfFormerColonist: true);

                Pawn pawn = PawnGenerator.GeneratePawn(request);

                // Name injection - first raider is always the streamer
                if (pawn.Name is NameTriple triple)
                {
                    pawn.Name = new NameTriple(triple.First, viewer, triple.Last);
                }
                else
                {
                    pawn.Name = new NameSingle(viewer);
                }

                LimitRaiderGearToColonyTech(pawn);

                spawnedPawns.Add(pawn);

                Logger.Twitch($"Generated pawn → @{viewer} (kind: {kind.defName})");
            }

            // Let vanilla arrival system spawn them
            parms.pawnGroups = null;
            parms.raidArrivalMode.Worker.Arrive(spawnedPawns, parms);

            // === CRITICAL: Create the raid lord so they actually attack the colony ===
            // This is what was missing — without a Lord they just wander off
            if (spawnedPawns.Count > 0)
            {
                LordMaker.MakeNewLord(parms.faction, new LordJob_AssaultColony(parms.faction, canKidnap: true), map, spawnedPawns);
                Logger.Twitch($"[TWITCH RAID WORKER] Created AssaultColony lord for {spawnedPawns.Count} named raiders");
            }

            Logger.Twitch($"[TWITCH RAID WORKER] Successfully spawned {spawnedPawns.Count} named raiders");
            CurrentRaidUsernames.Clear();

            return true;
        }

        private PawnKindDef GetAppropriateRaidKind()
        {
            // Official, stable way to read colony tech level in RimWorld 1.6
            TechLevel colonyTech = Faction.OfPlayer.def.techLevel;

            Logger.Twitch($"[TWITCH RAID WORKER] Colony tech level detected: {colonyTech} (using Faction.OfPlayer.def.techLevel)");

            if (colonyTech >= TechLevel.Spacer)
                return PawnKindDefOf.SpaceRefugee;

            if (colonyTech >= TechLevel.Industrial)
                return PawnKindDefOf.Pirate;

            // Neolithic / Tribal
            return PawnKindDefOf.Villager;
        }

        private void LimitRaiderGearToColonyTech(Pawn pawn)
        {
            if (pawn == null) return;

            // Official, stable colony tech level (same as GetAppropriateRaidKind)
            TechLevel colonyTech = Faction.OfPlayer.def.techLevel;

            int itemsStripped = 0;

            // Strip over-tech apparel
            if (pawn.apparel != null)
            {
                foreach (var apparel in pawn.apparel.WornApparel.ToList())
                {
                    if (apparel.def.techLevel > colonyTech)
                    {
                        pawn.apparel.Remove(apparel);
                        apparel.Destroy();
                        itemsStripped++;
                    }
                }
            }

            // Strip over-tech primary weapon
            if (pawn.equipment?.Primary != null)
            {
                if (pawn.equipment.Primary.def.techLevel > colonyTech)
                {
                    pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
                    itemsStripped++;
                }
            }

            if (itemsStripped > 0)
            {
                Logger.Twitch($"Gear limited to {colonyTech} | Stripped {itemsStripped} over-tech items from @{pawn.Name}");
            }
        }

    }
}
