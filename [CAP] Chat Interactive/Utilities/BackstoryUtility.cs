// BackstoryUtility.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
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

using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    /// <summary>
    /// Static helper for properly updating a pawn after a backstory swap.
    /// Handles skill gains, work-type disabling, cache clearing, and all vanilla notifications.
    /// Used by both ShuffleChildhoodCommandHandler and ShuffleAdulthoodCommandHandler.
    /// </summary>
    public static class BackstoryUtility
    {
        /// <summary>
        /// Full backstory effect restoration — exact CharacterEditor + vanilla behavior.
        /// Clears old effects, applies new ones from BOTH backstories, and forces UI/Stat updates.
        /// </summary>
        public static void RestoreBackstoryEffects(Verse.Pawn pawn, BackstoryDef oldBackstory, BackstoryDef newBackstory)
        {
            if (pawn == null) return;

            try
            {
                Logger.Debug("=== BackstoryUtility.RestoreBackstoryEffects started ===");

                // ==================== 1. SKILL GAINS ====================
                if (pawn.skills != null)
                {
                    // Remove old backstory skill gains
                    if (oldBackstory?.skillGains != null)
                    {
                        foreach (SkillGain gain in oldBackstory.skillGains)
                        {
                            if (gain.skill != null)
                            {
                                var skillRecord = pawn.skills.GetSkill(gain.skill);
                                skillRecord.Level = Mathf.Clamp(skillRecord.Level - gain.amount, 0, 20);
                                Logger.Debug($"[BackstoryShuffle] Removed old skill gain: {gain.skill.defName} -{gain.amount}");
                            }
                        }
                    }

                    // Apply new backstory skill gains
                    if (newBackstory?.skillGains != null)
                    {
                        foreach (SkillGain gain in newBackstory.skillGains)
                        {
                            if (gain.skill != null)
                            {
                                var skillRecord = pawn.skills.GetSkill(gain.skill);
                                skillRecord.Level = Mathf.Clamp(skillRecord.Level + gain.amount, 0, 20);
                                Logger.Debug($"[BackstoryShuffle] Applied new skill gain: {gain.skill.defName} +{gain.amount}");
                            }
                        }
                    }
                }

                // ==================== 2. CLEAR ALL CACHES ====================
                // Clear skill totally-disabled cache
                if (pawn.skills != null)
                {
                    var cachedTotallyDisabledField = AccessTools.Field(typeof(SkillRecord), "cachedTotallyDisabled");
                    foreach (SkillRecord skill in pawn.skills.skills)
                    {
                        if (skill != null && cachedTotallyDisabledField != null)
                            cachedTotallyDisabledField.SetValue(skill, (BoolUnknown)1); // reset
                    }
                }

                // Clear workSettings caches
                if (pawn.workSettings != null)
                {
                    var cachedDisabledField = AccessTools.Field(typeof(Pawn_WorkSettings), "cachedDisabledWorkTypes");
                    var cachedPermanentField = AccessTools.Field(typeof(Pawn_WorkSettings), "cachedDisabledWorkTypesPermanent");

                    if (cachedDisabledField != null)
                        cachedDisabledField.SetValue(pawn.workSettings, new List<WorkTypeDef>());
                    if (cachedPermanentField != null)
                        cachedPermanentField.SetValue(pawn.workSettings, new List<WorkTypeDef>());
                }

                // ==================== 3. RE-APPLY DISABLES FROM BOTH BACKSTORIES ====================
                if (pawn.skills != null)
                {
                    var cachedTotallyDisabledField = AccessTools.Field(typeof(SkillRecord), "cachedTotallyDisabled");

                    foreach (SkillRecord skill in pawn.skills.skills)
                    {
                        // Childhood
                        var childhood = pawn.story?.Childhood;
                        if (childhood != null && skill.def.IsDisabled(childhood.workDisables, childhood.DisabledWorkTypes ?? Enumerable.Empty<WorkTypeDef>()))
                            cachedTotallyDisabledField?.SetValue(skill, (BoolUnknown)0);

                        // Adulthood
                        var adulthood = pawn.story?.Adulthood;
                        if (adulthood != null && skill.def.IsDisabled(adulthood.workDisables, adulthood.DisabledWorkTypes ?? Enumerable.Empty<WorkTypeDef>()))
                            cachedTotallyDisabledField?.SetValue(skill, (BoolUnknown)0);
                    }
                }

                // ==================== 4. FINAL NOTIFICATIONS (CRITICAL) ====================
                pawn.skills?.Notify_SkillDisablesChanged();

                if (pawn.workSettings != null)
                {
                    pawn.workSettings.Notify_DisabledWorkTypesChanged();
                    pawn.Notify_DisabledWorkTypesChanged();           // extra safety
                }

                MeditationFocusTypeAvailabilityCache.ClearFor(pawn);
                StatsReportUtility.Reset();
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();         // visual refresh

                Logger.Debug($"[BackstoryShuffle] Fully restored effects for {pawn.LabelShort}. Old → New applied.");
            }
            catch (Exception ex)
            {
                Logger.Error($"[BackstoryShuffle] Failed to restore backstory effects: {ex}");
            }
        }
    }
}
