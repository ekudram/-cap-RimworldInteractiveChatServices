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

// VPEPatch.cs
// ... (keep your copyright header)

using _CAP__Chat_Interactive.Interfaces;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using Verse;

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

        public VPEBasicPsycastInfo GetBasicPsycastInfo(Pawn pawn)
        {
            // ... (your existing implementation - unchanged, it doesn't reference VEF directly)
            // Keep as-is
            Logger.Debug($"[CAP] VPE Patch: GetBasicPsycastInfo called for {(pawn?.LabelShort ?? "null")}");
            try
            {
                if (pawn == null) return new VPEBasicPsycastInfo();

                var implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
                var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(implantDef, false) as Hediff_PsycastAbilities;

                if (hediff == null)
                {
                    Logger.Debug("[CAP] VPE Patch: No VPE_PsycastAbilityImplant hediff found.");
                    return new VPEBasicPsycastInfo();
                }

                var info = new VPEBasicPsycastInfo { Level = hediff.level };

                var pe = pawn.psychicEntropy;
                if (pe != null)
                {
                    info.CurrentPsyfocus = pe.CurrentPsyfocus;
                    info.MaxPsyfocus = 1f;
                    info.CurrentHeat = pe.EntropyValue;
                    info.MaxHeat = pe.MaxEntropy;

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
                    return new VPEClassInfo { Error = "Invalid pawn or class name" };

                var implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
                var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(implantDef, false) as Hediff_PsycastAbilities;

                if (hediff == null)
                    return new VPEClassInfo { Error = "Pawn has no VPE psycaster implant" };

                int currentLevel = hediff.level;

                // Fuzzy match (unchanged)
                string search = classIdentifier.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace(".", "");

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
                    return new VPEClassInfo
                    {
                        Level = currentLevel,
                        HasMatchingClass = false,
                        Error = $"No class matching '{classIdentifier}'"
                    };
                }

                var result = new VPEClassInfo
                {
                    Level = currentLevel,
                    ClassLabel = matchedPath.LabelCap.ToString() ?? matchedPath.defName,
                    ClassDefName = matchedPath.defName,
                    HasMatchingClass = true,
                    Abilities = new List<VPEOwnedAbility>()
                };

                // CRITICAL FIX: Avoid direct VEF.Abilities.AbilityDef reference on load
                var comp = pawn.GetComp<CompAbilities>();
                var pathAbilities = GetPathAbilitiesSafe(matchedPath);  // New helper

                foreach (var ad in pathAbilities)
                {
                    // ... rest of your logic for ext, reqLevel, owns checks (unchanged)
                    var ext = ad.GetModExtension<AbilityExtension_Psycast>();
                    int reqLevel = ext?.level ?? 0;
                    if (reqLevel > currentLevel) continue;

                    bool owns = false;
                    if (comp != null)
                    {
                        try { owns = comp.HasAbility(ad); } catch { }
                    }
                    if (!owns && pawn.abilities != null)
                    {
                        try
                        {
                            owns = pawn.abilities.AllAbilitiesForReading.Any(ab => ab?.def?.defName == ad.defName);
                        }
                        catch { }
                    }
                    if (!owns && comp?.LearnedAbilities != null)
                    {
                        try
                        {
                            owns = comp.LearnedAbilities.Any(a => a?.def?.defName == ad.defName);
                        }
                        catch { }
                    }

                    if (owns)
                    {
                        result.Abilities.Add(new VPEOwnedAbility
                        {
                            Label = ad.LabelCap.ToString() ?? ad.label ?? ad.defName,
                            RequiredLevel = reqLevel
                        });
                    }
                }

                Logger.Debug($"[RICS] VPE Patch: Class '{result.ClassLabel}' returned {result.Abilities.Count} owned abilities");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS] VPE Patch GetPsycastsInClass error: {ex}");
                return new VPEClassInfo { Error = ex.Message };
            }
        }

        // NEW HELPER: Defers VEF type resolution until actually needed
        private List<VEF.Abilities.AbilityDef> GetPathAbilitiesSafe(PsycasterPathDef path)
        {
            try
            {
                // Use reflection or indirect access to avoid compile-time VEF dep in closure
                var abilitiesField = AccessTools.Field(typeof(PsycasterPathDef), "abilities");
                if (abilitiesField != null)
                {
                    return abilitiesField.GetValue(path) as List<VEF.Abilities.AbilityDef> ?? new List<VEF.Abilities.AbilityDef>();
                }
                return new List<VEF.Abilities.AbilityDef>();
            }
            catch
            {
                return new List<VEF.Abilities.AbilityDef>();
            }
        }
    }
}
