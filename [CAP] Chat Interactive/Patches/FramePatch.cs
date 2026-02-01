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

using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;  // For OpCodes and Label

namespace CAP_ChatInteractive
{
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    [HarmonyPriority(Priority.VeryHigh)]
    [HarmonyAfter(new string[] { "OskarPotocki.VEF" })]  // Run after VEF transpiler to inject on top
    public static class Frame_CompleteConstruction_Transpiler
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var getStoreSettings = AccessTools.Method(typeof(IStoreSettingsParent), nameof(IStoreSettingsParent.GetStoreSettings));
            var copyFrom = AccessTools.Method(typeof(StorageSettings), nameof(StorageSettings.CopyFrom));

            bool foundGet = false;
            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].Calls(getStoreSettings))
                {
                    foundGet = true;
                    // Look for next CopyFrom after GetStoreSettings
                    for (int j = i + 1; j < codes.Count; j++)
                    {
                        if (codes[j].Calls(copyFrom))
                        {
                            Log.Message($"[RICS] Found GetStoreSettings -> CopyFrom sequence at {j}. Injecting null guard.");

                            Label skipLabel = new Label();
                            codes[j + 1].labels.Add(skipLabel);  // Assume next instr after call is where to jump if skip

                            var guard = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Dup),                     // Dup 'this.settings' (other)
                        new CodeInstruction(OpCodes.Brtrue_S, codes[j].operand), // If not null, proceed to call
                        new CodeInstruction(OpCodes.Pop),                     // Pop null
                        new CodeInstruction(OpCodes.Pop),                     // Pop receiver (GetStoreSettings result)
                        new CodeInstruction(OpCodes.Br_S, skipLabel)          // Skip the call
                    };

                            codes.InsertRange(j, guard);
                            return codes;
                        }
                    }
                }
            }

            if (!foundGet) Log.Warning("[RICS] Could not find GetStoreSettings in CompleteConstruction – transpiler skipped.");
            return codes;
        }
    }
}