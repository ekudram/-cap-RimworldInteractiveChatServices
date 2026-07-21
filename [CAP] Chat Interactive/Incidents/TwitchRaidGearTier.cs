// TwitchRaidGearTier.cs
// Copyright (c) Captolamia
// Static Twitch raid gear tiers: research + player start tech → faction era.
// Storyteller threat / points scale combat power *within* the tier, not the era.

using CAP_ChatInteractive.Utilities;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public enum TwitchRaidGearTierLevel
    {
        Tribal = 0,
        Medieval = 1,
        Industrial = 2,
        Spacer = 3
    }

    public static class TwitchRaidGearTier
    {
        public const string DefTribal = "RICS_TwitchRaid_Tribal";
        public const string DefMedieval = "RICS_TwitchRaid_Medieval";
        public const string DefIndustrial = "RICS_TwitchRaid_Industrial";
        public const string DefSpacer = "RICS_TwitchRaid_Spacer";

        /// <summary>
        /// Resolve gear era from player start tech + finished weapons/armor research.
        /// Higher threat does not raise tier (that is points → combatPower within pool).
        /// </summary>
        public static TwitchRaidGearTierLevel Resolve(Map map, out string debugNotes)
        {
            var notes = new StringBuilder();
            TechLevel playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Industrial;
            notes.Append($"playerTech={playerTech}");

            // Floor from start scenario (tribal starts stay low until research lifts them)
            TwitchRaidGearTierLevel floor = playerTech switch
            {
                TechLevel.Neolithic => TwitchRaidGearTierLevel.Tribal,
                TechLevel.Medieval => TwitchRaidGearTierLevel.Medieval,
                TechLevel.Industrial => TwitchRaidGearTierLevel.Industrial,
                TechLevel.Spacer or TechLevel.Ultra or TechLevel.Archotech => TwitchRaidGearTierLevel.Spacer,
                _ => playerTech < TechLevel.Neolithic
                    ? TwitchRaidGearTierLevel.Tribal
                    : TwitchRaidGearTierLevel.Industrial
            };

            // Research ladder (weapons + armor focus). Silent-fail if def missing.
            TwitchRaidGearTierLevel fromResearch = TwitchRaidGearTierLevel.Tribal;
            var hits = new List<string>();

            if (ResearchedAny(hits, "Smithing", "ComplexClothing", "PlateArmor", "RecurveBow"))
                fromResearch = TwitchRaidGearTierLevel.Medieval;

            if (ResearchedAny(hits, "Gunsmithing", "FlakArmor", "Machining", "Electricity", "GunTurrets"))
                fromResearch = TwitchRaidGearTierLevel.Industrial;

            if (ResearchedAny(hits, "PoweredArmor", "ChargedShot", "ReconArmor"))
                fromResearch = TwitchRaidGearTierLevel.Spacer;

            // Soft wealth floor: very early colonies stay off Spacer even if one project finished
            float wealth = map?.wealthWatcher?.WealthTotal ?? 0f;
            if (fromResearch >= TwitchRaidGearTierLevel.Spacer && wealth < 80000f)
            {
                fromResearch = TwitchRaidGearTierLevel.Industrial;
                hits.Add("wealthCapSpacer→Industrial");
            }
            if (fromResearch >= TwitchRaidGearTierLevel.Industrial && wealth < 25000f &&
                floor < TwitchRaidGearTierLevel.Industrial)
            {
                fromResearch = (TwitchRaidGearTierLevel)System.Math.Min((int)fromResearch, (int)TwitchRaidGearTierLevel.Medieval);
                hits.Add("wealthCapIndustrial");
            }

            // Combine: at least floor from start tech, raised by research
            TwitchRaidGearTierLevel tier = (TwitchRaidGearTierLevel)System.Math.Max((int)floor, (int)fromResearch);

            // Ceiling: tribal start can still climb via research (no hard ceiling on player tech)
            // Spacer/Ultra starts keep Spacer floor even with little research.

            notes.Append($" floor={floor} research={fromResearch} wealth={wealth:F0}");
            if (hits.Count > 0)
                notes.Append(" signals=[").Append(string.Join(",", hits.Distinct())).Append(']');
            notes.Append($" → tier={tier}");

            debugNotes = notes.ToString();
            return tier;
        }

        public static string FactionDefNameFor(TwitchRaidGearTierLevel tier) => tier switch
        {
            TwitchRaidGearTierLevel.Tribal => DefTribal,
            TwitchRaidGearTierLevel.Medieval => DefMedieval,
            TwitchRaidGearTierLevel.Industrial => DefIndustrial,
            TwitchRaidGearTierLevel.Spacer => DefSpacer,
            _ => DefIndustrial
        };

        public static FactionDef GetFactionDef(TwitchRaidGearTierLevel tier)
        {
            string defName = FactionDefNameFor(tier);
            return DefDatabase<FactionDef>.GetNamedSilentFail(defName)
                   ?? DefDatabase<FactionDef>.GetNamedSilentFail("Pirate")
                   ?? FactionDefOf.Pirate;
        }

        /// <summary>
        /// Get or create the world faction for this tier. Applies existing Raider ideology when present.
        /// </summary>
        public static Faction GetOrCreateFaction(TwitchRaidGearTierLevel tier)
        {
            FactionDef def = GetFactionDef(tier);
            if (def == null)
            {
                Logger.Error("[TWITCH RAID] No FactionDef for tier " + tier);
                return Faction.OfAncientsHostile ?? Find.FactionManager.RandomEnemyFaction();
            }

            Faction existing = Find.FactionManager.FirstFactionOfDef(def);
            if (existing != null)
            {
                EnsureHostileToPlayer(existing);
                TryApplyExistingRaiderIdeo(existing);
                return existing;
            }

            var parms = new FactionGeneratorParms(def, default, hidden: true);
            Faction faction = FactionGenerator.NewGeneratedFaction(parms);
            if (faction == null)
            {
                Logger.Error("[TWITCH RAID] FactionGenerator failed for " + def.defName);
                return Find.FactionManager.RandomEnemyFaction();
            }

            // Prefer fixed name from def
            if (!string.IsNullOrEmpty(def.fixedName))
                faction.Name = def.fixedName;

            faction.hidden = true;
            Find.FactionManager.Add(faction);

            EnsureHostileToPlayer(faction);
            foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
            {
                if (other != faction)
                    faction.TryMakeInitialRelationsWith(other);
            }

            TryApplyExistingRaiderIdeo(faction);

            Logger.Twitch($"[TWITCH RAID] Created static faction {def.defName} name={faction.Name}");
            return faction;
        }

        /// <summary>
        /// If Ideology is active and the game already has an ideo with the Raider meme
        /// (any faction or IdeoManager), reuse it as this faction's primary ideo.
        /// </summary>
        public static void TryApplyExistingRaiderIdeo(Faction faction)
        {
            if (faction == null || !ModsConfig.IdeologyActive)
                return;

            try
            {
                MemeDef raiderMeme = DefDatabase<MemeDef>.GetNamedSilentFail("Raider");
                if (raiderMeme == null)
                    return;

                if (faction.ideos == null)
                    faction.ideos = new FactionIdeosTracker(faction);

                // Already on a Raider ideo — keep it
                if (faction.ideos.PrimaryIdeo != null &&
                    faction.ideos.PrimaryIdeo.memes != null &&
                    faction.ideos.PrimaryIdeo.memes.Contains(raiderMeme))
                {
                    return;
                }

                Ideo found = null;

                // 1) Any ideo already in the manager with Raider meme
                if (Find.IdeoManager?.IdeosListForReading != null)
                {
                    found = Find.IdeoManager.IdeosListForReading
                        .FirstOrDefault(i => i != null && i.memes != null && i.memes.Contains(raiderMeme));
                }

                // 2) Primary ideo of any existing faction that has Raider
                if (found == null)
                {
                    foreach (Faction f in Find.FactionManager.AllFactionsListForReading)
                    {
                        if (f?.ideos?.PrimaryIdeo == null) continue;
                        if (f.ideos.PrimaryIdeo.memes != null &&
                            f.ideos.PrimaryIdeo.memes.Contains(raiderMeme))
                        {
                            found = f.ideos.PrimaryIdeo;
                            break;
                        }
                    }
                }

                if (found != null)
                {
                    faction.ideos.SetPrimary(found);
                    Logger.Twitch($"[TWITCH RAID] Applied existing Raider ideo '{found.name}' to {faction.Name}");
                }
                else
                {
                    Logger.Twitch("[TWITCH RAID] No existing Raider ideo in game — keeping generated ideo");
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[TWITCH RAID] Raider ideo apply failed: {ex.Message}");
            }
        }

        private static void EnsureHostileToPlayer(Faction faction)
        {
            if (faction == null || Faction.OfPlayer == null) return;
            try
            {
                faction.TryMakeInitialRelationsWith(Faction.OfPlayer);
                faction.SetRelationDirect(Faction.OfPlayer, FactionRelationKind.Hostile, canSendHostilityLetter: false);
                Faction.OfPlayer.TryMakeInitialRelationsWith(faction);
                Faction.OfPlayer.SetRelationDirect(faction, FactionRelationKind.Hostile, canSendHostilityLetter: false);
            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[TWITCH RAID] Hostility setup: {ex.Message}");
            }
        }

        private static bool ResearchedAny(List<string> hits, params string[] defNames)
        {
            bool any = false;
            foreach (string name in defNames)
            {
                var proj = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
                if (proj != null && proj.IsFinished)
                {
                    hits.Add(name);
                    any = true;
                }
            }
            return any;
        }

        /// <summary>Combat pawn kinds listed on this faction's Combat pawnGroupMakers.</summary>
        public static HashSet<PawnKindDef> CollectCombatKinds(FactionDef factionDef)
        {
            var set = new HashSet<PawnKindDef>();
            if (factionDef?.pawnGroupMakers == null)
                return set;

            foreach (var maker in factionDef.pawnGroupMakers)
            {
                if (maker == null) continue;
                if (maker.kindDef != null && maker.kindDef.defName != "Combat")
                    continue;
                if (maker.options == null) continue;
                foreach (var opt in maker.options)
                {
                    if (opt?.kind != null)
                        set.Add(opt.kind);
                }
            }
            return set;
        }
    }
}
