// VPEPatch.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at an option) any later version.
// 
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.
//
// VPE compatibility patch. Kept separate from the HAR patch for readability.
// Implements IVPEPsycastProvider so the main mod can get clean VPE psycast data.

using _CAP__Chat_Interactive.Interfaces;
using VanillaPsycastsExpanded;
using RimWorld;
using System;
using Verse;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Linq;
using VEF.Abilities;

namespace CAP_ChatInteractive.Patch.VPE
{
    [UsedImplicitly]
    public class VPEPatch : IVPEPsycastProvider
    {
        public string ModId => "VanillaExpanded.VPsycastsE";

        static VPEPatch()
        {
            Logger.Message("[CAP] VPE Patch Assembly Loaded!");
            Logger.Debug("[CAP] VPE Patch static constructor executed");
        }

        public VPEPatch()
        {
            Logger.Message("[CAP] VPE Patch Instance Created!");
        }

        public static class VPEPatchVerifier
        {
            public static void VerifyLoaded()
            {
                Logger.Message("[CAP] VPE Patch Verification Called - Assembly is LOADED!");
            }

            public static bool IsAvailable()
            {
                Logger.Debug("[CAP] VPE Patch Availability Check - YES");
                return true;
            }
        }

        public VPEBasicPsycastInfo GetBasicPsycastInfo(Pawn pawn)
        {
            Logger.Debug($"[CAP] VPE Patch: GetBasicPsycastInfo called for {(pawn?.LabelShort ?? "null")}");

            try
            {
                if (pawn == null)
                {
                    return new VPEBasicPsycastInfo();
                }

                var implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
                var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(implantDef, false) as Hediff_PsycastAbilities;

                if (hediff == null)
                {
                    Logger.Debug("[CAP] VPE Patch: No VPE_PsycastAbilityImplant hediff found.");
                    return new VPEBasicPsycastInfo();
                }

                var info = new VPEBasicPsycastInfo
                {
                    Level = hediff.level
                };

                var pe = pawn.psychicEntropy;
                if (pe != null)
                {
                    info.CurrentPsyfocus = pe.CurrentPsyfocus;
                    info.MaxPsyfocus = 1f;  // psyfocus is always 0-1 range
                    info.CurrentHeat = pe.EntropyValue;
                    info.MaxHeat = pe.MaxEntropy;

                    // Compute experience needed for next level (Phase 1)
                    try
                    {
                        if (hediff.level < 30)
                        {
                            int next = hediff.level + 1;
                            float required = Hediff_PsycastAbilities.ExperienceRequiredForLevel(next);
                            info.PsyfocusNeededForNextLevel = Math.Max(0f, required - hediff.experience);
                        }
                    }
                    catch { }
                }

                Logger.Debug($"[CAP] VPE Patch: level={info.Level}, psyfocus={info.CurrentPsyfocus:F2}/{info.MaxPsyfocus:F2}, heat={info.CurrentHeat:F1}/{info.MaxHeat:F1}");
                return info;
            }
            catch (Exception ex)
            {
                Logger.Error($"[CAP] VPE Patch GetBasicPsycastInfo error: {ex}");
                return new VPEBasicPsycastInfo();
            }
        }

        public VPEClassInfo GetPsycastsInClass(Pawn pawn, string classIdentifier)
        {
            Logger.Debug($"[CAP] VPE Patch: GetPsycastsInClass called for {(pawn?.LabelShort ?? "null")} class='{classIdentifier}'");

            try
            {
                if (pawn == null || string.IsNullOrWhiteSpace(classIdentifier))
                {
                    return new VPEClassInfo { Error = "Invalid pawn or class name" };
                }

                var implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
                var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(implantDef, false) as Hediff_PsycastAbilities;

                if (hediff == null)
                {
                    Logger.Debug("[CAP] VPE Patch: GetPsycastsInClass - no VPE implant hediff");
                    return new VPEClassInfo { Error = "Pawn has no VPE psycaster implant" };
                }

                int currentLevel = hediff.level;

                // Fuzzy match on PsycasterPathDef label or defName
                string search = classIdentifier.ToLowerInvariant()
                    .Replace(" ", "").Replace("_", "").Replace("-", "").Replace(".", "");

                PsycasterPathDef matchedPath = null;
                foreach (var p in DefDatabase<PsycasterPathDef>.AllDefsListForReading)
                {
                    if (p == null) continue;
                    string dn = (p.defName ?? "").ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
                    string lb = (p.LabelCap.ToString() ?? "").ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
                    if (dn.Contains(search) || lb.Contains(search) || search.Contains(dn) || search.Contains(lb))
                    {
                        matchedPath = p;
                        break;
                    }
                }

                if (matchedPath == null)
                {
                    Logger.Debug($"[CAP] VPE Patch: No matching PsycasterPathDef for '{classIdentifier}'");
                    return new VPEClassInfo
                    {
                        Level = currentLevel,
                        HasMatchingClass = false,
                        Error = $"No class matching '{classIdentifier}'"
                    };
                }

                Logger.Debug($"[CAP] VPE Patch: Matched path {matchedPath.defName} ({matchedPath.LabelCap})");

                var result = new VPEClassInfo
                {
                    Level = currentLevel,
                    ClassLabel = matchedPath.LabelCap.ToString() ?? matchedPath.defName,
                    ClassDefName = matchedPath.defName,
                    HasMatchingClass = true,
                    Abilities = new List<VPEOwnedAbility>()
                };

                // Get abilities for this path via AbilityExtension_Psycast
                var comp = pawn.GetComp<CompAbilities>();

                var candidates = DefDatabase<RimWorld.AbilityDef>.AllDefsListForReading
                    .Where(ad =>
                    {
                        if (ad == null) return false;
                        var ext = ad.GetModExtension<AbilityExtension_Psycast>();
                        return ext != null && ext.path == matchedPath;
                    })
                    .OrderBy(ad =>
                    {
                        var ext = ad.GetModExtension<AbilityExtension_Psycast>();
                        return ext?.level ?? 99;
                    })
                    .ToList();

                foreach (var ad in candidates)
                {
                    var ext = ad.GetModExtension<AbilityExtension_Psycast>();
                    int reqLevel = ext?.level ?? 0;
                    if (reqLevel > currentLevel) continue;

                    bool owns = false;

                    // Prefer abilities tracker (populated for actual granted psycasts). Avoid VEF.AbilityDef type conflict.
                    if (pawn.abilities != null)
                    {
                        try
                        {
                            owns = pawn.abilities.AllAbilitiesForReading.Any(ab => ab != null && ab.def == ad);
                        }
                        catch { }
                    }

                    // Extra: if CompAbilities present, try via the VEF AbilityDef if available (best effort)
                    if (!owns && comp != null)
                    {
                        try
                        {
                            // VEF CompAbilities.HasAbility expects VEF.Abilities.AbilityDef in some builds; attempt via name match fallback
                            owns = comp.LearnedAbilities != null && comp.LearnedAbilities.Any(a => a != null && a.def != null && a.def.defName == ad.defName);
                        }
                        catch { }
                    }

                    if (owns)
                    {
                        result.Abilities.Add(new VPEOwnedAbility
                        {
                            Label = ad.LabelCap.Resolve() ?? ad.label ?? ad.defName,
                            RequiredLevel = reqLevel
                        });
                    }
                }

                Logger.Debug($"[CAP] VPE Patch: Class '{result.ClassLabel}' returned {result.Abilities.Count} owned abilities for level {currentLevel}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[CAP] VPE Patch GetPsycastsInClass error: {ex}");
                return new VPEClassInfo { Error = ex.Message };
            }
        }
    }
}
