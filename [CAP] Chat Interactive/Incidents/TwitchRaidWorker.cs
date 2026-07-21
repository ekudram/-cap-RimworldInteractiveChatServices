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
//
// Named Twitch raiders: wealth/points-scaled combat kinds + vacuum gear on space maps.

using _CAP__Chat_Interactive.Command.CommandHelpers;
using RimWorld;
using System;
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
            Logger.Twitch(
                $"[TWITCH RAID WORKER] Starting custom raid | Names: {CurrentRaidUsernames.Count} | " +
                $"Faction: {parms.faction?.Name ?? "null"} | points={parms.points:F0}");

            if (parms.target is not Map map)
            {
                Logger.Twitch("WARNING: No valid map target for raid.");
                CurrentRaidUsernames.Clear();
                return false;
            }

            // Default to edge walk-in first
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;

            if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                Logger.Twitch("EdgeWalkIn spawn failed → falling back to weighted drop mode");

                var dropOptions = new List<(PawnsArrivalModeDef mode, float weight)>
                {
                    (PawnsArrivalModeDefOf.CenterDrop, 1.0f),
                    (PawnsArrivalModeDefOf.EdgeDrop, 2.5f),
                    (PawnsArrivalModeDefOf.RandomDrop, 1.5f)
                };

                var chosen = dropOptions.RandomElementByWeight(t => t.weight);
                parms.raidArrivalMode = chosen.mode;
                parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);
                Logger.Twitch($"Fallback arrival mode: {parms.raidArrivalMode.defName}");
            }

            var raidNames = CurrentRaidUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();

            if (raidNames.Count == 0)
            {
                Logger.Twitch("No raid names → falling back to vanilla raid");
                CurrentRaidUsernames.Clear();
                return base.TryExecuteWorker(parms);
            }

            var onlyRaiders = CAPChatInteractiveMod.Instance.Settings.GlobalSettings.TwitchRaidsOnlyRaiders;
            int maxRaiders = onlyRaiders ? raidNames.Count : Math.Min(10, raidNames.Count);
            if (maxRaiders <= 0) maxRaiders = raidNames.Count;

            float colonyWealth = map.wealthWatcher?.WealthTotal ?? 0f;
            float pointsPerRaider = parms.points / Math.Max(1, maxRaiders);
            bool vacuumMap = ItemDeliveryHelper.IsSpaceMap(map) || (map.Biome?.inVacuum == true);

            // Prefer kinds from the static Twitch tier faction pool (era kit)
            HashSet<PawnKindDef> tierKinds = null;
            if (parms.faction?.def != null)
                tierKinds = TwitchRaidGearTier.CollectCombatKinds(parms.faction.def);

            Logger.Twitch(
                $"[TWITCH RAID WORKER] Wealth={colonyWealth:F0} points={parms.points:F0} " +
                $"perRaider={pointsPerRaider:F0} vacuumMap={vacuumMap} maxRaiders={maxRaiders} " +
                $"faction={parms.faction?.def?.defName ?? "null"} tierKinds={tierKinds?.Count ?? 0}");

            var spawnedPawns = new List<Pawn>();

            for (int i = 0; i < maxRaiders && i < raidNames.Count; i++)
            {
                string viewer = raidNames[i];

                PawnKindDef kind = PickCombatKindForRaid(map, parms.faction, pointsPerRaider, colonyWealth, tierKinds);
                float biocodeChance = BiocodeChanceForWealth(colonyWealth, pointsPerRaider);

                var request = new PawnGenerationRequest(
                    kind: kind,
                    faction: parms.faction,
                    context: PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: true,
                    colonistRelationChanceFactor: 0f,
                    forceAddFreeWarmLayerIfNeeded: true,
                    allowGay: true,
                    allowPregnant: false,
                    allowFood: true,
                    allowAddictions: true,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: true,
                    worldPawnFactionDoesntMatter: true,
                    biocodeWeaponChance: biocodeChance,
                    biocodeApparelChance: biocodeChance * 0.85f);

                Pawn pawn = PawnGenerator.GeneratePawn(request);

                // Name injection — first list entry is typically the raiding streamer
                if (pawn.Name is NameTriple triple)
                    pawn.Name = new NameTriple(triple.First, viewer, triple.Last);
                else
                    pawn.Name = new NameSingle(viewer);

                // Vacuum / space: vacsuit unless already in Vac-rated armor (Recon, Cataphract, etc.)
                if (vacuumMap)
                    ItemDeliveryHelper.EquipVacsuitIfNeeded(pawn);

                spawnedPawns.Add(pawn);

                string weapon = pawn.equipment?.Primary?.LabelCap ?? "unarmed";
                Logger.Twitch(
                    $"Generated @{viewer} kind={kind.defName} combatPower={kind.combatPower:F0} " +
                    $"weapon={weapon} biocode={biocodeChance:F2}");
            }

            parms.pawnGroups = null;
            parms.raidArrivalMode.Worker.Arrive(spawnedPawns, parms);

            if (spawnedPawns.Count > 0)
            {
                LordMaker.MakeNewLord(
                    parms.faction,
                    new LordJob_AssaultColony(parms.faction, canKidnap: true),
                    map,
                    spawnedPawns);
                Logger.Twitch(
                    $"[TWITCH RAID WORKER] AssaultColony lord for {spawnedPawns.Count} named raiders");
            }

            Logger.Twitch($"[TWITCH RAID WORKER] Spawned {spawnedPawns.Count} named raiders");
            CurrentRaidUsernames.Clear();
            return true;
        }

        /// <summary>
        /// Combat kind near target combat power from raid threat points + wealth.
        /// Prefer kinds from the static Twitch tier faction pool (era-appropriate kit).
        /// Threat raises power within the pool — does not change tech era.
        /// </summary>
        private static PawnKindDef PickCombatKindForRaid(
            Map map, Faction faction, float pointsPerRaider, float colonyWealth,
            HashSet<PawnKindDef> tierKinds)
        {
            float targetPower = Mathf.Clamp(pointsPerRaider * 0.5f, 45f, 280f);

            // Wealth / threat pulls combat power up within the tier (not tech era)
            if (colonyWealth >= 600000f) targetPower = Mathf.Max(targetPower, 180f);
            else if (colonyWealth >= 300000f) targetPower = Mathf.Max(targetPower, 130f);
            else if (colonyWealth >= 120000f) targetPower = Mathf.Max(targetPower, 90f);
            else if (colonyWealth >= 40000f) targetPower = Mathf.Max(targetPower, 65f);

            List<PawnKindDef> combatKinds;
            if (tierKinds != null && tierKinds.Count > 0)
            {
                combatKinds = tierKinds
                    .Where(k => k != null && k.race != null)
                    .Where(k => k.RaceProps.Humanlike && !k.RaceProps.IsMechanoid)
                    .ToList();
            }
            else
            {
                combatKinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                    .Where(k => k != null && k.race != null)
                    .Where(k => k.RaceProps.Humanlike && !k.RaceProps.IsMechanoid)
                    .Where(k => k.combatPower >= 40f && k.combatPower <= 320f)
                    .Where(IsCombatCapableKind)
                    .ToList();
            }

            if (combatKinds.Count == 0)
            {
                Logger.Twitch("[TWITCH RAID WORKER] No combat kinds found — fallback Pirate");
                return PawnKindDefOf.Pirate ?? PawnKindDefOf.Colonist;
            }

            // Prefer kinds near target combat power (threat scaling within tier)
            var band = combatKinds
                .Where(k => k.combatPower >= targetPower * 0.55f && k.combatPower <= targetPower * 1.4f)
                .ToList();

            if (band.Count == 0)
            {
                band = combatKinds
                    .OrderBy(k => Mathf.Abs(k.combatPower - targetPower))
                    .Take(12)
                    .ToList();
            }

            PawnKindDef pick = band.RandomElement();
            Logger.Twitch(
                $"[TWITCH RAID WORKER] Kind pick targetPower={targetPower:F0} → {pick.defName} " +
                $"(cp={pick.combatPower:F0}, band={band.Count}, tierPool={combatKinds.Count})");
            return pick;
        }

        private static bool IsCombatCapableKind(PawnKindDef k)
        {
            if (k == null) return false;
            // Prefer fighters / weapon-tagged kinds; exclude pure civilians when possible
            if (k.isFighter) return true;
            if (k.weaponTags != null && k.weaponTags.Count > 0) return true;
            if (k.modExtensions != null && k.combatPower >= 60f) return true;
            // Name heuristics for pirate / mercenary / scavenger packs
            string n = k.defName ?? "";
            if (n.IndexOf("Pirate", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Mercenary", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Raider", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Scavenger", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Drifter", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Soldier", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return k.combatPower >= 80f;
        }

        /// <summary>Higher wealth / points → more biocoded gear rolls on generate.</summary>
        private static float BiocodeChanceForWealth(float colonyWealth, float pointsPerRaider)
        {
            float chance = 0.05f;
            if (colonyWealth >= 40000f || pointsPerRaider >= 80f) chance = 0.12f;
            if (colonyWealth >= 120000f || pointsPerRaider >= 150f) chance = 0.22f;
            if (colonyWealth >= 300000f || pointsPerRaider >= 250f) chance = 0.38f;
            if (colonyWealth >= 600000f || pointsPerRaider >= 350f) chance = 0.55f;
            return Mathf.Clamp01(chance);
        }

        // Kept for optional future setting — not applied by default
        private void LimitRaiderGearToColonyTech(Pawn pawn)
        {
            if (pawn == null) return;

            TechLevel colonyTech = Faction.OfPlayer.def.techLevel;
            int itemsStripped = 0;

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

            if (pawn.equipment?.Primary != null &&
                pawn.equipment.Primary.def.techLevel > colonyTech)
            {
                pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
                itemsStripped++;
            }

            if (itemsStripped > 0)
            {
                Logger.Twitch(
                    $"Gear limited to {colonyTech} | Stripped {itemsStripped} over-tech items from @{pawn.Name}");
            }
        }
    }
}
