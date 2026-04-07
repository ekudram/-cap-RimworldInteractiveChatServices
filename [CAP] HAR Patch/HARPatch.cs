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

        /// <summary>
        /// Determines whether the specified trait with the given degree is forced for the provided pawn's race.
        /// </summary>
        /// <remarks>A trait is considered forced if it appears in the pawn's race's forced trait entries.
        /// If the pawn's race does not define any forced traits, the method returns false.</remarks>
        /// <param name="pawn">The pawn whose race traits are to be checked. Cannot be null.</param>
        /// <param name="defName">The definition name of the trait to check for.</param>
        /// <param name="degree">The degree of the trait to check for.</param>
        /// <returns>true if the trait with the specified definition name and degree is forced for the pawn's race; otherwise,
        /// false.</returns>
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

        /// <summary>
        /// Determines whether a specified trait with a given degree is disallowed for the provided pawn based on the
        /// pawn's alien race settings.
        /// </summary>
        /// <remarks>This method only evaluates disallowed traits for pawns whose definition is of type
        /// ThingDef_AlienRace. If the pawn does not have disallowed traits defined, the method returns false.</remarks>
        /// <param name="pawn">The pawn whose disallowed traits are to be checked. Must not be null.</param>
        /// <param name="defName">The definition name of the trait to check for disallowance. Cannot be null.</param>
        /// <param name="degree">The degree of the trait to check for disallowance.</param>
        /// <returns>true if the specified trait with the given degree is disallowed for the pawn; otherwise, false.</returns>
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

        /// <summary>
        /// Determines whether the specified trait is allowed for the given pawn, considering any disallowed or forced
        /// trait rules.
        /// </summary>
        /// <remarks>A trait is considered allowed if it is neither explicitly disallowed nor forced for
        /// the specified pawn and degree.</remarks>
        /// <param name="pawn">The pawn for which to check if the trait is allowed. Cannot be null.</param>
        /// <param name="traitDef">The definition of the trait to check. Cannot be null.</param>
        /// <param name="degree">The degree of the trait to check. If set to -10, the default degree is used.</param>
        /// <returns>true if the trait is allowed for the pawn; otherwise, false.</returns>
        public bool IsTraitAllowed(Pawn pawn, TraitDef traitDef, int degree = -10)
        {
            return !IsTraitDisallowed(pawn, traitDef.defName, degree) &&
                   !IsTraitForced(pawn, traitDef.defName, degree);
        }

        /// <summary>
        /// Returns a list of xenotype names that are allowed for the specified race definition.
        /// </summary>
        /// <remarks>For human races, the allowed xenotypes are determined using global logic that may
        /// differ from non-human (alien) races. The result may vary depending on the active game modules and
        /// race-specific restrictions.</remarks>
        /// <param name="raceDef">The race definition for which to retrieve the allowed xenotypes. Must not be null.</param>
        /// <returns>A list of strings containing the names of allowed xenotypes for the given race. The list is empty if no
        /// xenotypes are allowed or if the Biotech module is not active.</returns>
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
        /// Retrieves a list of xenotype definition names that are allowed for the Human race, excluding those that are
        /// exclusive to other alien races.
        /// </summary>
        /// <remarks>If an error occurs during filtering, the method returns all xenotype definition names
        /// without applying race-based restrictions. The returned list is case-insensitive and does not include null or
        /// empty definition names.</remarks>
        /// <returns>A list of strings containing the definition names of xenotypes available to the Human race. The list is
        /// sorted alphabetically and excludes xenotypes restricted to other races.</returns>
        private List<string> GetAllowedXenotypesForHuman()
        {
            Logger.Debug($"[HARPatch] Calculating allowed xenotypes for Human and HAR without xeno lists...");

            var allowed = DefDatabase<XenotypeDef>.AllDefs
                .Where(x => !string.IsNullOrEmpty(x.defName))
                .Select(x => x.defName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int removedCount = 0;
            try
            {
                Logger.Debug($"[HARPatch] Starting xenotype filter for Human: {allowed.Count} total xenotypes");

                foreach (var alienDef in DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d is ThingDef_AlienRace && d != ThingDefOf.Human))
                {
                    var restriction = GetRaceRestriction(alienDef);
                    if (restriction == null) continue;

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
        /// Gets the list of allowed xenotype definition names for the specified alien race definition, applying any
        /// race-specific restrictions or whitelists.
        /// </summary>
        /// <remarks>If the race has no specific restriction, the method returns the allowed xenotypes for
        /// humans. Race-specific exclusive lists, whitelists, and blacklists are applied according to the race's
        /// configuration. The returned list contains distinct xenotype names, case-insensitive.</remarks>
        /// <param name="raceDef">The alien race definition for which to retrieve allowed xenotype names. Must not be null.</param>
        /// <returns>A list of xenotype definition names that are permitted for the specified alien race. Returns an empty list
        /// if Biotech is not active or if the race is human.</returns>
        public List<string> GetAllowedXenotypesForAlien(ThingDef raceDef)
        {
            if (!ModsConfig.BiotechActive || raceDef == ThingDefOf.Human)
            {
                // Logger.Debug($"[HARPatch] GetAllowedXenotypesForAlien skipped - Biotech: {ModsConfig.BiotechActive}, IsHuman: {raceDef == ThingDefOf.Human}");
                return new List<string>();
            }

            // Logger.Debug($"=== HAR PROVIDER: GetAllowedXenotypesForAlien for {raceDef.defName} ===");

            var restriction = GetRaceRestriction(raceDef);

            // Edge case: Race has NO raceRestriction at all → treat exactly like Human
            if (restriction == null)
            {
                Logger.Debug($"[HARPatch] No raceRestriction found for {raceDef.defName} → falling back to Human filtered list");
                return GetAllowedXenotypesForHuman();
            }

            // Logger.Debug($"Race restriction found for {raceDef.defName}: onlyRestricted={restriction.onlyUseRaceRestrictedXenotypes}, xenotypeList={restriction.xenotypeList?.Count ?? 0}, whiteList={restriction.whiteXenotypeList?.Count ?? 0}, blackList={restriction.blackXenotypeList?.Count ?? 0}");

            // CASE 1: xenotypeList exists → this is the exclusive list (HAR treats it as restricted)
            if (restriction.xenotypeList != null && restriction.xenotypeList.Count > 0)
            {
                var result = restriction.xenotypeList.Select(x => x.defName).ToList();
                // Logger.Debug($"[HARPatch] Returning xenotypeList (exclusive) for {raceDef.defName}: {result.Count} xenotypes");
                return result;
            }

            // CASE 2: onlyUseRaceRestrictedXenotypes = true + whiteXenotypeList → restricted to white list only (Nyaron case)
            if (restriction.onlyUseRaceRestrictedXenotypes &&
                restriction.whiteXenotypeList != null &&
                restriction.whiteXenotypeList.Count > 0)
            {
                var result = restriction.whiteXenotypeList.Select(x => x.defName).ToList();
                // Logger.Debug($"[HARPatch] Returning whiteXenotypeList (restricted mode) for {raceDef.defName}: {result.Count} xenotypes");
                return result;
            }

            // CASE 3: whiteXenotypeList exists but NOT restricted → Human list + white list (shared)
            if (restriction.whiteXenotypeList != null && restriction.whiteXenotypeList.Count > 0)
            {
                var result = GetAllowedXenotypesForHuman().ToList();
                result.AddRange(restriction.whiteXenotypeList.Select(x => x.defName));
                result = result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();   // deduplicate
                // Logger.Debug($"[HARPatch] Returning Human list + whiteXenotypeList (shared) for {raceDef.defName}: {result.Count} xenotypes");
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
                // Logger.Debug($"[HARPatch] Removed {removed} blacklisted xenotypes for {raceDef.defName}");
            }

            return finalList;
        }

        /// <summary>
        /// Determines whether the specified xenotype is allowed for the given race definition based on configured
        /// restrictions.
        /// </summary>
        /// <remarks>If no restrictions are defined for the race, all xenotypes are allowed by default.
        /// Blacklists take precedence over whitelists and exclusive lists. The method assumes that the provided
        /// definitions are valid and does not perform null checks.</remarks>
        /// <param name="raceDef">The race definition to evaluate. Must not be null.</param>
        /// <param name="xenotype">The xenotype to check for allowance. Must not be null.</param>
        /// <returns>true if the xenotype is permitted for the specified race; otherwise, false.</returns>
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
        /// Retrieves the race restriction settings for the specified race definition, if available.
        /// </summary>
        /// <remarks>This method returns restriction settings only for races defined as ThingDef_AlienRace. For other race
        /// types, the method returns null.</remarks>
        /// <param name="raceDef">The race definition for which to obtain restriction settings. Must not be null.</param>
        /// <returns>A RaceRestrictionSettings object containing the restriction settings for the specified race, or null if no
        /// restrictions are defined.</returns>
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

        /// <summary>
        /// Determines whether the specified gender is allowed for the given race definition.
        /// </summary>
        /// <remarks>For non-alien races, all genders are permitted. For alien races, the allowed genders
        /// are determined by the race's general settings, specifically the male gender probability. A gender of None is
        /// always allowed.</remarks>
        /// <param name="raceDef">The race definition to evaluate. If the race is not an alien race, all genders are allowed by default.</param>
        /// <param name="gender">The gender to check for allowance based on the race's settings.</param>
        /// <returns>true if the specified gender is allowed for the given race; otherwise, false.</returns>
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

        /// <summary>
        /// Determines the allowed genders for the specified race definition based on its gender probability settings.
        /// </summary>
        /// <remarks>For non-alien races or races without gender probability settings, both genders are
        /// allowed by default. For alien races, the allowed genders are determined by the male gender probability,
        /// rounded to two decimal places.</remarks>
        /// <param name="raceDef">The race definition to evaluate. If the race is not an alien race or does not specify gender probability,
        /// both genders are allowed.</param>
        /// <returns>A value indicating which genders are allowed for the specified race: male, female, or either.</returns>
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

        /// <summary>
        /// Calculates the probability of a character being male or female based on the specified race definition.
        /// </summary>
        /// <remarks>If the provided race definition does not represent an alien race or lacks gender
        /// probability settings, the method returns default probabilities of 0.5 for both male and female.
        /// Probabilities are rounded to two decimal places to match the expected precision.</remarks>
        /// <param name="raceDef">The race definition used to determine gender probabilities. If the race is not a supported alien race,
        /// default probabilities are used.</param>
        /// <returns>A tuple containing the probability of being male and the probability of being female, respectively. Each
        /// value is a float between 0 and 1, and their sum is 1.</returns>
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
        /// Determines whether the specified backstory is allowed for the given pawn based on race and backstory type.
        /// </summary>
        /// <remarks>Alien backstories are only allowed for non-human pawns and are subject to additional approval logic
        /// defined by the backstory. Non-alien backstories are always allowed, enabling compatibility with other
        /// mods.</remarks>
        /// <param name="backstory">The backstory definition to evaluate for the pawn. Can be an alien or non-alien backstory.</param>
        /// <param name="pawn">The pawn for which to check backstory eligibility. Must not be null.</param>
        /// <returns>true if the backstory is permitted for the pawn; otherwise, false.</returns>
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

        /// <summary>
        /// Determines whether the specified apparel can be worn by the given race.
        /// </summary>
        /// <remarks>If the apparel is null or the race is human, the method always returns true. For
        /// other races, compatibility is determined by race restriction settings, including any white or black
        /// lists.</remarks>
        /// <param name="apparel">The definition of the apparel to check. Can be null to indicate no specific apparel.</param>
        /// <param name="race">The definition of the race for which to check apparel compatibility.</param>
        /// <returns>true if the apparel can be worn by the specified race; otherwise, false.</returns>

        public bool CanWear(ThingDef apparel, ThingDef race)
        {
            if (apparel == null || race == ThingDefOf.Human)
                return true; // Humans + safety (matches HAR fallback)

            // Direct delegation to HAR's static method (respects onlyUseRaceRestrictedApparel, white/black lists)
            return RaceRestrictionSettings.CanWear(apparel, race);
        }

        /// <summary>
        /// Determines whether a weapon can be equipped by a specified race.
        /// </summary>
        /// <remarks>If the weapon is null or the race is human, the method always returns true. For other
        /// cases, equip eligibility is determined by race restriction settings.</remarks>
        /// <param name="weapon">The weapon definition to check for equip eligibility. Can be null to indicate no specific weapon.</param>
        /// <param name="race">The race definition to evaluate for weapon equip eligibility.</param>
        /// <returns>true if the weapon can be equipped by the specified race; otherwise, false.</returns>
        public bool CanEquip(ThingDef weapon, ThingDef race)
        {
            if (weapon == null || race == ThingDefOf.Human)
                return true;

            return RaceRestrictionSettings.CanEquip(weapon, race);
        }
    }
}