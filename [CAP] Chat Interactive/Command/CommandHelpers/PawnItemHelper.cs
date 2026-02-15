
// StoreCommandHelper.cs
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
//
// Helper methods for store command handling

using CAP_ChatInteractive;
using RimWorld;
using System;
using Verse;

namespace _CAP__Chat_Interactive.Command.CommandHelpers
{
    public static class PawnItemHelper
    {

        // === PawnItemHelper

        public static bool EquipItemOnPawn(Thing item, Verse.Pawn pawn)
        {
            try
            {
                if (pawn == null || item == null) return false;

                // Check if it's a weapon
                if (item.def.IsWeapon)
                {
                    var weapon = item as ThingWithComps;
                    if (weapon != null)
                    {
                        // Check if pawn can equip this weapon
                        if (!EquipmentUtility.CanEquip(weapon, pawn))
                        {
                            Logger.Debug($"Pawn cannot equip {weapon.def.defName}");
                            return false;
                        }

                        // Check if pawn can carry anything
                        if (!MassUtility.CanEverCarryAnything(pawn))
                        {
                            Logger.Debug($"Pawn cannot carry anything");
                            return false;
                        }

                        ThingWithComps oldWeapon = null;

                        // Try to handle current equipment
                        if (pawn.equipment.Primary != null)
                        {
                            // Try to move current weapon to inventory
                            if (!pawn.equipment.TryTransferEquipmentToContainer(pawn.equipment.Primary, pawn.inventory.innerContainer))
                            {
                                // If inventory full, try to drop it
                                if (!pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out oldWeapon, pawn.Position))
                                {
                                    Logger.Warning($"Could not make room for {pawn.Name}'s new weapon.");
                                }
                            }
                        }

                        // Check if pawn would be over encumbered
                        if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, weapon, 1) && oldWeapon != null)
                        {
                            // Re-equip old weapon and spawn new one
                            pawn.equipment.AddEquipment(oldWeapon);
                            Logger.Debug($"Pawn would be over encumbered, spawning weapon instead");
                            return false;
                        }

                        // Equip the new weapon
                        pawn.equipment.AddEquipment(weapon);
                        Logger.Debug($"Equipped weapon: {item.def.defName} on pawn {pawn.Name}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error equipping item on pawn: {ex}");
                return false;
            }
        }

        public static Pawn GetViewerPawn(ChatMessageWrapper messageWrapper)
        {
            try
            {
                var manager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (manager == null || messageWrapper == null) return null;

                string key = $"{messageWrapper.Platform?.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
                if (string.IsNullOrEmpty(key) || key == ":") return null;

                if (manager.viewerPawnAssignments.TryGetValue(key, out string thingId))
                    return GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting viewer pawn: {ex}");
                return null;
            }
        }

        public static Pawn GetViewerPawn(string username)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            if (assignmentManager != null && assignmentManager.HasAssignedPawn(username))
            {
                return assignmentManager.GetAssignedPawn(username);
            }
            return null;
        }

        public static bool WearApparelOnPawn(Thing item, Verse.Pawn pawn)
        {
            try
            {
                if (pawn == null || item == null) return false;

                // Check if it's apparel
                if (item.def.IsApparel)
                {
                    var apparel = item as Apparel;
                    if (apparel != null)
                    {
                        // Check if pawn has body parts to wear this apparel
                        if (!ApparelUtility.HasPartsToWear(pawn, item.def))
                        {
                            Logger.Debug($"Pawn lacks body parts to wear {item.def.defName}");
                            return false;
                        }

                        // Check if this would replace locked apparel
                        if (pawn.apparel.WouldReplaceLockedApparel(apparel))
                        {
                            Logger.Debug($"Would replace locked apparel with {item.def.defName}");
                            return false;
                        }

                        // Check if pawn can equip this apparel
                        if (!EquipmentUtility.CanEquip(apparel, pawn))
                        {
                            Logger.Debug($"Pawn cannot equip {item.def.defName}");
                            return false;
                        }

                        // Wear the apparel and force it to be worn
                        pawn.apparel.Wear(apparel, dropReplacedApparel: true);
                        pawn.outfits.forcedHandler.SetForced(apparel, true);
                        Logger.Debug($"Wore apparel: {item.def.defName} on pawn {pawn.Name}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error wearing apparel on pawn: {ex}");
                return false;
            }
        }
    }
}