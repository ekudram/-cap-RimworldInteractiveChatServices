using _CAP__Chat_Interactive.Interfaces;
using AlienRace;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Patch.HAR
{
    public class HARPatch : IAlienCompatibilityProvider
    {
        public string ModId => "erdelf.HumanoidAlienRaces";

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

        public List<string> GetAllowedXenotypes(ThingDef raceDef)
        {
            if (!ModsConfig.BiotechActive || raceDef == ThingDefOf.Human)
            {
                return new List<string>();
            }

            // Get the race restriction settings
            var restriction = GetRaceRestriction(raceDef);
            if (restriction == null)
            {
                return new List<string>();
            }

            // If only race-restricted xenotypes are allowed, return the exclusive list
            if (restriction.onlyUseRaceRestrictedXenotypes && restriction.xenotypeList != null)
            {
                return restriction.xenotypeList.Select(x => x.defName).ToList();
            }

            // Otherwise, get all xenotypes that pass the race restriction check
            return DefDatabase<XenotypeDef>.AllDefs
                .Where(xenotype => IsXenotypeAllowed(raceDef, xenotype))
                .Select(xenotype => xenotype.defName)
                .ToList();
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
    }
}