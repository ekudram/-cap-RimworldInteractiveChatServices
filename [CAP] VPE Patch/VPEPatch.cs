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
//
// IMPORTANT: Do not put VEF.Abilities.AbilityDef (or other VEF types) in method
// signatures, fields, or lambda captures. RimWorld's type scanner calls GetFields
// on compiler-generated display classes; if VEF is missing/mismatched that throws
// TypeLoadException even when the method is never called.

using _CAP__Chat_Interactive.Interfaces;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VanillaPsycastsExpanded;
using Verse;

namespace CAP_ChatInteractive.Patch.VPE
{
    [UsedImplicitly]
    public class VPEPatch : IVPEPsycastProvider
    {
        public string ModId => "VanillaExpanded.VPsycastsE";

        // Cached reflection — resolved only when first needed (VPE+VEF must be present).
        private static Type _compAbilitiesType;
        private static MethodInfo _hasAbilityMethod;
        private static PropertyInfo _learnedAbilitiesProp;
        private static bool _vefReflectionReady;
        private static bool _vefReflectionFailed;

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
                    catch { /* optional VPE level-exp path */ }
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

                // Use Def (not VEF.Abilities.AbilityDef) so nested display classes never
                // store a field typed as AbilityDef — that was the TypeLoadException source.
                List<Def> pathAbilities = GetPathAbilitiesAsDefs(matchedPath);
                object compAbilities = GetCompAbilitiesInstance(pawn);

                foreach (Def ad in pathAbilities)
                {
                    if (ad == null) continue;

                    // Snapshot strings so lambdas never capture Def/AbilityDef instances.
                    string adDefName = ad.defName;
                    string adLabel = ad.LabelCap.ToString() ?? ad.label ?? adDefName;

                    var ext = ad.GetModExtension<AbilityExtension_Psycast>();
                    int reqLevel = ext?.level ?? 0;
                    if (reqLevel > currentLevel) continue;

                    bool owns = false;
                    if (compAbilities != null)
                        owns = CompHasAbility(compAbilities, ad);

                    if (!owns && pawn.abilities != null)
                    {
                        try
                        {
                            // Capture string only — avoids DisplayClass field of type AbilityDef.
                            owns = pawn.abilities.AllAbilitiesForReading.Any(ab => ab?.def?.defName == adDefName);
                        }
                        catch { /* vanilla tracker may be unavailable */ }
                    }

                    if (!owns && compAbilities != null)
                        owns = CompLearnedContains(compAbilities, adDefName);

                    if (owns)
                    {
                        result.Abilities.Add(new VPEOwnedAbility
                        {
                            Label = adLabel,
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

        /// <summary>
        /// Reads PsycasterPathDef.abilities without naming VEF.Abilities.AbilityDef in signatures.
        /// Returns Verse.Def list so callers stay free of VEF field types.
        /// </summary>
        private static List<Def> GetPathAbilitiesAsDefs(PsycasterPathDef path)
        {
            var list = new List<Def>();
            if (path == null) return list;

            try
            {
                FieldInfo abilitiesField = AccessTools.Field(typeof(PsycasterPathDef), "abilities");
                if (abilitiesField == null) return list;

                object raw = abilitiesField.GetValue(path);
                if (raw is IEnumerable enumerable)
                {
                    foreach (object item in enumerable)
                    {
                        if (item is Def def)
                            list.Add(def);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[CAP] VPE Patch: GetPathAbilitiesAsDefs failed: {ex.Message}");
            }

            return list;
        }

        private static void EnsureVefReflection()
        {
            if (_vefReflectionReady || _vefReflectionFailed) return;

            try
            {
                _compAbilitiesType = AccessTools.TypeByName("VEF.Abilities.CompAbilities");
                if (_compAbilitiesType == null)
                {
                    _vefReflectionFailed = true;
                    Logger.Warning("[CAP] VPE Patch: VEF.Abilities.CompAbilities type not found");
                    return;
                }

                _hasAbilityMethod = AccessTools.Method(_compAbilitiesType, "HasAbility", new[] { typeof(Def) })
                    ?? AccessTools.Method(_compAbilitiesType, "HasAbility");
                _learnedAbilitiesProp = AccessTools.Property(_compAbilitiesType, "LearnedAbilities");
                _vefReflectionReady = true;
            }
            catch (Exception ex)
            {
                _vefReflectionFailed = true;
                Logger.Warning($"[CAP] VPE Patch: VEF reflection init failed: {ex.Message}");
            }
        }

        private static object GetCompAbilitiesInstance(Pawn pawn)
        {
            EnsureVefReflection();
            if (_compAbilitiesType == null || pawn == null) return null;

            try
            {
                // ThingWithComps.GetComp&lt;T&gt; via non-generic path
                MethodInfo getComp = AccessTools.Method(typeof(ThingWithComps), "GetComp");
                if (getComp == null) return null;
                MethodInfo generic = getComp.MakeGenericMethod(_compAbilitiesType);
                return generic.Invoke(pawn, null);
            }
            catch
            {
                // Fallback: walk AllComps by type name
                try
                {
                    if (pawn.AllComps == null) return null;
                    foreach (ThingComp c in pawn.AllComps)
                    {
                        if (c != null && c.GetType() == _compAbilitiesType)
                            return c;
                    }
                }
                catch { /* ignore */ }
                return null;
            }
        }

        private static bool CompHasAbility(object compAbilities, Def abilityDef)
        {
            EnsureVefReflection();
            if (compAbilities == null || abilityDef == null || _hasAbilityMethod == null) return false;

            try
            {
                object result = _hasAbilityMethod.Invoke(compAbilities, new object[] { abilityDef });
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static bool CompLearnedContains(object compAbilities, string abilityDefName)
        {
            EnsureVefReflection();
            if (compAbilities == null || string.IsNullOrEmpty(abilityDefName) || _learnedAbilitiesProp == null)
                return false;

            try
            {
                object learned = _learnedAbilitiesProp.GetValue(compAbilities);
                if (learned is not IEnumerable enumerable) return false;

                foreach (object ability in enumerable)
                {
                    if (ability == null) continue;
                    // VEF.Abilities.Ability has .def
                    object defObj = AccessTools.Field(ability.GetType(), "def")?.GetValue(ability)
                        ?? AccessTools.Property(ability.GetType(), "def")?.GetValue(ability);
                    if (defObj is Def d && d.defName == abilityDefName)
                        return true;
                }
            }
            catch { /* ignore */ }

            return false;
        }
    }
}
