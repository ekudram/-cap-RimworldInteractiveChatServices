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
                    info.MaxPsyfocus = pe.MaxPsyfocus;
                    info.CurrentHeat = pe.CurrentEntropy;
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
    }
}
