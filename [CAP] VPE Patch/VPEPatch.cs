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
// Provides compatibility with Vanilla Psycasts Expanded (VPE).
// Kept completely separate from the HAR patch system for readability.

using _CAP__Chat_Interactive.Interfaces;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using RimWorld;
using System;
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

        /// <summary>
        /// Phase 1: Returns basic psycast stats for !mypawn psycasts
        /// (level, psyfocus current+needed, heat current+max).
        /// Heavy logging + try/catch for debugging.
        /// </summary>
        public VPEBasicPsycastInfo GetBasicPsycastInfo(Pawn pawn)
        {
            Logger.Debug($"[CAP] VPE Patch: GetBasicPsycastInfo called for {(pawn?.LabelShort ?? "null")}");

            try
            {
                if (pawn == null)
                {
                    Logger.Debug("[CAP] VPE Patch: Pawn is null, returning empty info.");
                    return new VPEBasicPsycastInfo();
                }

                var implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
                if (implantDef == null)
                {
                    Logger.Debug("[CAP] VPE Patch: VPE_PsycastAbilityImplant def not found.");
                    return new VPEBasicPsycastInfo();
                }

                var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(implantDef, false) as Hediff_PsycastAbilities;
                if (hediff == null)
                {
                    Logger.Debug("[CAP] VPE Patch: No VPE_PsycastAbilityImplant hediff on pawn.");
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
                    info.MaxPsyfocus = pe.MaxPsyfocus;
                    info.CurrentHeat = pe.CurrentEntropy;
                    info.MaxHeat = pe.MaxEntropy;

                    // Try to compute "needed for next level" using experience
                    try
                    {
                        if (hediff.level < 30) // safe upper bound
                        {
                            int nextLevel = hediff.level + 1;
                            float required = Hediff_PsycastAbilities.ExperienceRequiredForLevel(nextLevel);
                            info.PsyfocusNeededForNextLevel = Math.Max(0f, required - hediff.experience);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"[CAP] VPE Patch: Could not compute next-level experience: {ex.Message}");
                    }
                }

                Logger.Debug($"[CAP] VPE Patch: level={info.Level}, psyfocus={info.CurrentPsyfocus:F2}/{info.MaxPsyfocus:F2}, heat={info.CurrentHeat:F1}/{info.MaxHeat:F1}, neededForNext={info.PsyfocusNeededForNextLevel:F1}");
                return info;
            }
            catch (Exception ex)
            {
                Logger.Error($"[CAP] VPE Patch GetBasicPsycastInfo error: {ex}");
                return new VPEBasicPsycastInfo();
            }
        }
    }
}
