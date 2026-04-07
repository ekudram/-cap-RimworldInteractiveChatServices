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
// Provides compatibility with the Human and Alien Races (HAR) mod for trait and xenotype restrictions.
using _CAP__Chat_Interactive.Interfaces;
using AlienRace;
using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Patch.HAR
{
    [UsedImplicitly]
    // [StaticConstructorOnStartup]
    public class HARPatch : IAlienCompatibilityProvider
    {
        public string ModId => "erdelf.HumanoidAlienRaces";

        static HARPatch()
        {
            Logger.Message("[CAP] HAR Patch Assembly Loaded!");
            Logger.Debug("[CAP] HAR Patch static constructor executed");
        }

        public HARPatch()
        {
            Logger.Message("[CAP] HAR Patch Instance Created!");
        }

        public static class HARPatchVerifier
        {
            public static void VerifyLoaded()
            {
                Logger.Message("[CAP] HAR Patch Verification Called - Assembly is LOADED!");
            }

            public static bool IsAvailable()
            {
                Logger.Debug("[CAP] HAR Patch Availability Check - YES");
                return true;
            }
        }

        public bool IsTraitForced(Pawn pawn, string defName, int degree)
        {
            if (pawn.def is not ThingDef_AlienRace alienRace ||
                alienRace.alienRace.generalSettings.forcedRaceTraitEntries.NullOrEmpty())
            {
                return false;
            }

            foreach (AlienChanceEntry<TraitWithDegree> entry in alienRace.alienRace.generalSettings.forcedRaceTraitEntries)
            {
                if (string.Equals(entry.entry.def.defName, defName) && entry.entry.degree == degree)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsTraitDisallowed(Pawn pawn, string defName, int degree)
        {
            if (pawn.def is not ThingDef_AlienRace alienRace ||
                alienRace.alienRace.generalSettings.disallowedTraits.NullOrEmpty())
            {
                return false;
            }

            foreach (AlienChanceEntry<TraitWithDegree> entry in alienRace.alienRace.generalSettings.disallowedTraits)
            {
                if (string.Equals(entry.entry.def.defName, defName) && entry.entry.degree == degree)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsTraitAllowed(Pawn pawn, TraitDef traitDef, int degree = -10)
        {
            return !IsTraitDisallowed(pawn, traitDef.defName, degree) &&
                   !IsTraitForced(pawn, traitDef.defName, degree);
        }

        /// <summary>
        /// Returns xenotypes allowed for the given race according to HAR's RaceRestrictionSettings.
        /// Now correctly handles Human + HAR (excludes alien-only xenotypes).
        /// </summary>
        public List<string> GetAllowedXenotypes(ThingDef raceDef)
        {
            if (!ModsConfig.BiotechActive)
                return new List<string>();

            Logger.Debug($"=== HAR PROVIDER: GetAllowedXenotypes for {raceDef.defName} ===");

            // For Humans we must filter using global HAR logic (reverse of alien restrictions)
            if (raceDef == ThingDefOf.Human)
            {
                return GetAllowedXenotypesForHuman();
            }

            // Non-Human (alien) races - use your existing logic (already working)
            return GetAllowedXenotypesForAlien(raceDef);
        }

        /// <summary>
        /// Returns xenotypes that Humans are allowed to use when HAR is active.
        /// 
        /// Corrected logic:
        /// - Start with ALL xenotypes
        /// - Remove ONLY those in any alien race's blackXenotypeList
        /// - For races with onlyUseRaceRestrictedXenotypes = true:
        ///   → Remove a xenotype ONLY if it is in that race's xenotypeList/whiteXenotypeList
        ///     (i.e. it is explicitly exclusive to that alien race)
        /// 
        /// This keeps all Biotech core xenotypes (Baseliner, Highmate, etc.) while correctly hiding
        /// race-exclusive alien xenotypes (like Nyarron-specific or Vampire Nyarron).
        /// </summary>
        private List<string> GetAllowedXenotypesForHuman()
        {
            Logger.Debug($"[HARPatch] Calculating allowed xenotypes for Human and HAR without xeno lists...");

            var allowed = DefDatabase<XenotypeDef>.AllDefs
                .Where(x => !string.IsNullOrEmpty(x.defName))
                .Select(x => x.defName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // if HAR is not active then we should not be here.
            //const string harModId = "erdelf.HumanoidAlienRaces";
            //if (!ModsConfig.IsActive(harModId))
            //{
            //    Logger.Debug("[HARPatch] HAR not active - Human can use all xenotypes");
            //    return allowed.OrderBy(x => x).ToList();
            //}

            int removedCount = 0;
            try
            {
                Logger.Debug($"[HARPatch] Starting xenotype filter for Human: {allowed.Count} total xenotypes");

                foreach (var alienDef in DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d is ThingDef_AlienRace && d != ThingDefOf.Human))
                {
                    var restriction = GetRaceRestriction(alienDef);
                    if (restriction == null) continue;

                    // 1. Blacklist - never allowed on Humans - Test Blaclist is for HAR Races only, not global, so we don't remove from allowed if it's
                    //if (restriction.blackXenotypeList != null)
                    //{
                    //    foreach (var entry in restriction.blackXenotypeList)
                    //    {
                    //        if (allowed.Remove(entry.defName))
                    //            removedCount++;
                    //    }
                    //}

                    // 1. Exclusive xenotypes (onlyUseRaceRestrictedXenotypes = true)
                    //    These belong ONLY to the alien race → remove them from Human list or HAR race without whitelist (they are not shared)
                    if (restriction.onlyUseRaceRestrictedXenotypes)
                    {
                        var raceExclusive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        if (restriction.xenotypeList != null)
                            raceExclusive.UnionWith(restriction.xenotypeList.Select(x => x.defName));

                        if (restriction.whiteXenotypeList != null)
                            raceExclusive.UnionWith(restriction.whiteXenotypeList.Select(x => x.defName));

                        // Remove ONLY the ones that are exclusive to this alien race
                        foreach (var xeno in raceExclusive)
                        {
                            if (allowed.Remove(xeno))
                                removedCount++;
                        }
                    }
                }

                Logger.Debug($"[HARPatch] Human xenotype filter: started with {allowed.Count + removedCount}, removed {removedCount} exclusive/blacklisted, final = {allowed.Count}");
                return allowed.OrderBy(x => x).ToList();
            }
            catch (Exception ex)
            {
                Logger.Warning($"[HARPatch] Error while filtering xenotypes for Human: {ex.Message}");
                return DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName))
                    .Select(x => x.defName)
                    .OrderBy(x => x)
                    .ToList();
            }
        }

        /// <summary>
        /// Returns xenotypes allowed for a non-Human (alien) race according to the official HAR wiki rules.
        /// 
        /// Exact behavior:
        /// • xenotypeList          → exclusive list (always used if present, ignores onlyUseRaceRestrictedXenotypes flag)
        /// • whiteXenotypeList + onlyUseRaceRestrictedXenotypes = true  → restricted to white list only
        /// • whiteXenotypeList + onlyUseRaceRestrictedXenotypes = false → Human filtered list + white list (shared)
        /// • blackXenotypeList     → always removed from whatever list we build
        /// • No restriction at all → fall back to Human filtered list
        /// </summary>
        public List<string> GetAllowedXenotypesForAlien(ThingDef raceDef)
        {
            if (!ModsConfig.BiotechActive || raceDef == ThingDefOf.Human)
            {
                Logger.Debug($"[HARPatch] GetAllowedXenotypesForAlien skipped - Biotech: {ModsConfig.BiotechActive}, IsHuman: {raceDef == ThingDefOf.Human}");
                return new List<string>();
            }

            Logger.Debug($"=== HAR PROVIDER: GetAllowedXenotypesForAlien for {raceDef.defName} ===");

            var restriction = GetRaceRestriction(raceDef);

            // Edge case: Race has NO raceRestriction at all → treat exactly like Human
            if (restriction == null)
            {
                Logger.Debug($"[HARPatch] No raceRestriction found for {raceDef.defName} → falling back to Human filtered list");
                return GetAllowedXenotypesForHuman();
            }

            Logger.Debug($"Race restriction found for {raceDef.defName}: onlyRestricted={restriction.onlyUseRaceRestrictedXenotypes}, xenotypeList={restriction.xenotypeList?.Count ?? 0}, whiteList={restriction.whiteXenotypeList?.Count ?? 0}, blackList={restriction.blackXenotypeList?.Count ?? 0}");

            // CASE 1: xenotypeList exists → this is the exclusive list (HAR treats it as restricted)
            if (restriction.xenotypeList != null && restriction.xenotypeList.Count > 0)
            {
                var result = restriction.xenotypeList.Select(x => x.defName).ToList();
                Logger.Debug($"[HARPatch] Returning xenotypeList (exclusive) for {raceDef.defName}: {result.Count} xenotypes");
                return result;
            }

            // CASE 2: onlyUseRaceRestrictedXenotypes = true + whiteXenotypeList → restricted to white list only (Nyaron case)
            if (restriction.onlyUseRaceRestrictedXenotypes &&
                restriction.whiteXenotypeList != null &&
                restriction.whiteXenotypeList.Count > 0)
            {
                var result = restriction.whiteXenotypeList.Select(x => x.defName).ToList();
                Logger.Debug($"[HARPatch] Returning whiteXenotypeList (restricted mode) for {raceDef.defName}: {result.Count} xenotypes");
                return result;
            }

            // CASE 3: whiteXenotypeList exists but NOT restricted → Human list + white list (shared)
            if (restriction.whiteXenotypeList != null && restriction.whiteXenotypeList.Count > 0)
            {
                var result = GetAllowedXenotypesForHuman().ToList();
                result.AddRange(restriction.whiteXenotypeList.Select(x => x.defName));
                result = result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();   // deduplicate
                Logger.Debug($"[HARPatch] Returning Human list + whiteXenotypeList (shared) for {raceDef.defName}: {result.Count} xenotypes");
                return result;
            }

            // CASE 4: No lists or only blacklist → Human list minus blacklist (Kurin case)
            Logger.Debug($"[HARPatch] No xenotypeList or whiteList → starting from Human list and removing blacklist for {raceDef.defName}");
            var finalList = GetAllowedXenotypesForHuman().ToList();

            if (restriction.blackXenotypeList != null && restriction.blackXenotypeList.Count > 0)
            {
                int removed = 0;
                finalList.RemoveAll(x =>
                {
                    bool remove = restriction.blackXenotypeList.Any(b => b.defName == x);
                    if (remove) removed++;
                    return remove;
                });
                Logger.Debug($"[HARPatch] Removed {removed} blacklisted xenotypes for {raceDef.defName}");
            }

            return finalList;
        }

        public bool IsXenotypeAllowed(ThingDef raceDef, XenotypeDef xenotype)
        {
            if (raceDef == ThingDefOf.Human)
                return true;

            var restriction = GetRaceRestriction(raceDef);
            if (restriction == null)
            {
                return true; // No restrictions = all allowed
            }

            // Check blacklist first - if it's in blacklist, it's not allowed
            if (restriction.blackXenotypeList != null &&
                restriction.blackXenotypeList.Any(x => x.defName == xenotype.defName))
            {
                return false;
            }

            // If whitelist exists, check if it's in the whitelist
            if (restriction.whiteXenotypeList != null && restriction.whiteXenotypeList.Count > 0)
            {
                return restriction.whiteXenotypeList.Any(x => x.defName == xenotype.defName);
            }

            // If only race-restricted xenotypes are allowed, check the exclusive list
            if (restriction.onlyUseRaceRestrictedXenotypes)
            {
                if (restriction.xenotypeList != null)
                {
                    return restriction.xenotypeList.Any(x => x.defName == xenotype.defName);
                }
                return false; // No exclusive list = no xenotypes allowed
            }

            // If whitelist exists, check if it's in the whitelist
            if (restriction.whiteXenotypeList != null && restriction.whiteXenotypeList.Count > 0)
            {
                return restriction.whiteXenotypeList.Any(x => x.defName == xenotype.defName);
            }

            // If no whitelist but exclusive list exists, check that too
            if (restriction.xenotypeList != null && restriction.xenotypeList.Count > 0)
            {
                return restriction.xenotypeList.Any(x => x.defName == xenotype.defName);
            }

            // No restrictions = all allowed
            return true;
        }

        /// <summary>
        /// Helper method to get the RaceRestrictionSettings from a race def
        /// </summary>
        private RaceRestrictionSettings GetRaceRestriction(ThingDef raceDef)
        {
            if (raceDef is ThingDef_AlienRace alienRace)
            {
                // Race restrictions are directly on the alienRace field
                return alienRace.alienRace?.raceRestriction;
            }

            // If we get here, it's not a HAR race - no need for reflection fallback
            // Logger.Warn($"Race {raceDef.defName} is not a ThingDef_AlienRace - skipping HAR xenotype restrictions");
            return null;
        }

        //  Gender

        public bool IsGenderAllowed(ThingDef raceDef, Gender gender)
        {
            if (raceDef is not ThingDef_AlienRace alienRace)
            {
                return true; // Non-HAR races allow all genders by default
            }

            var raceSettings = alienRace.alienRace.generalSettings;
            if (raceSettings == null)
            {
                return true;
            }

            // Check gender probability to determine allowed genders
            return gender switch
            {
                Gender.Male => raceSettings.maleGenderProbability > 0f,
                Gender.Female => raceSettings.maleGenderProbability < 1f,
                Gender.None => true, // Usually "None" is allowed
                _ => true
            };
        }

        public GenderPossibility GetAllowedGenders(ThingDef raceDef)
        {
            if (raceDef is not ThingDef_AlienRace alienRace)
            {
                return GenderPossibility.Either; // Non-HAR races allow both by default
            }

            var raceSettings = alienRace.alienRace.generalSettings;
            if (raceSettings == null)
            {
                return GenderPossibility.Either;
            }

            // Round to 2 decimal places to match HAR's precision
            float roundedProbability = (float)Math.Round(raceSettings.maleGenderProbability, 2);

            // Convert maleGenderProbability to GenderPossibility
            if (roundedProbability <= 0f)
                return GenderPossibility.Female; // Only female allowed
            else if (roundedProbability >= 1f)
                return GenderPossibility.Male; // Only male allowed
            else
                return GenderPossibility.Either; // Both allowed
        }

        public (float maleProbability, float femaleProbability) GetGenderProbabilities(ThingDef raceDef)
        {
            if (raceDef is not ThingDef_AlienRace alienRace)
            {
                return (0.5f, 0.5f); // Default 50/50 for non-HAR races
            }

            var raceSettings = alienRace.alienRace.generalSettings;
            if (raceSettings == null)
            {
                return (0.5f, 0.5f);
            }

            // Round to 2 decimal places to match HAR's precision
            float maleProb = (float)Math.Round(raceSettings.maleGenderProbability, 2);
            float femaleProb = 1f - maleProb;

            return (maleProb, femaleProb);
        }

        /// <summary>
        /// HAR-specific backstory compatibility (uses AlienBackstoryDef.Approved(Pawn) from provided BackstoryDef.cs).
        /// Respects race restrictions, gender commonality, age ranges, and linked backstories.
        /// Vanilla backstories always allowed (RimWorld default).
        /// </summary>
        public bool IsBackstoryAllowed(BackstoryDef backstory, Pawn pawn)
        {
            if (backstory == null || pawn == null) return false;

            // === HAR Alien Backstory Handling ===
            if (backstory is AlienRace.AlienBackstoryDef alienBackstory)
            {
                // Extra protection: Never allow alien backstories on base humans
                if (pawn.def == ThingDefOf.Human)
                {
                    Logger.Debug($"[HAR Backstory Filter] Blocked alien backstory '{backstory.defName}' for human pawn");
                    return false;
                }

                // Use HAR's own approval logic (gender commonality, age ranges, etc.)
                return alienBackstory.Approved(pawn);
            }

            // === Vanilla or other mod backstories ===
            // Always allow non-alien backstories (this lets other mods that add backstories still work)
            return true;
        }

        // === NEW: HAR race restriction checks (uses exact statics from ThingDef_AlienRace.cs) ===
        // Prevents coin loss + silent item disappearance for !wear / !equip
        public bool CanWear(ThingDef apparel, ThingDef race)
        {
            if (apparel == null || race == ThingDefOf.Human)
                return true; // Humans + safety (matches HAR fallback)

            // Direct delegation to HAR's static method (respects onlyUseRaceRestrictedApparel, white/black lists)
            return RaceRestrictionSettings.CanWear(apparel, race);
        }

        public bool CanEquip(ThingDef weapon, ThingDef race)
        {
            if (weapon == null || race == ThingDefOf.Human)
                return true;

            return RaceRestrictionSettings.CanEquip(weapon, race);
        }
    }
}