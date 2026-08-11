// File: PawnItemHelper.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Equip/wear helpers and viewer pawn lookup for store commands.
using CAP_ChatInteractive;
using RimWorld;
using System;
using Verse;

namespace _CAP__Chat_Interactive.Command.CommandHelpers
{
    public static class PawnItemHelper
    {
        public static bool EquipItemOnPawn(Thing item, Verse.Pawn pawn)
        {
            try
            {
                if (pawn == null || item == null || item.def == null)
                    return false;

                if (!item.def.IsWeapon)
                    return false;

                if (!(item is ThingWithComps weapon))
                    return false;

                if (pawn.equipment == null)
                    return false;

                if (!EquipmentUtility.CanEquip(weapon, pawn))
                    return false;

                if (!MassUtility.CanEverCarryAnything(pawn))
                    return false;

                ThingWithComps oldWeapon = null;

                if (pawn.equipment.Primary != null)
                {
                    if (pawn.inventory?.innerContainer == null ||
                        !pawn.equipment.TryTransferEquipmentToContainer(
                            pawn.equipment.Primary, pawn.inventory.innerContainer))
                    {
                        if (!pawn.equipment.TryDropEquipment(
                                pawn.equipment.Primary, out oldWeapon, pawn.Position))
                        {
                            Logger.Warning(
                                $"[PawnItem] Could not make room for {pawn.LabelShort}'s new weapon.");
                        }
                    }
                }

                if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, weapon, 1) && oldWeapon != null)
                {
                    pawn.equipment.AddEquipment(oldWeapon);
                    return false;
                }

                pawn.equipment.AddEquipment(weapon);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnItem] EquipItemOnPawn: {ex.Message}");
                return false;
            }
        }

        public static Pawn GetViewerPawn(ChatMessageWrapper messageWrapper)
        {
            try
            {
                if (messageWrapper == null)
                    return null;

                var manager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (manager?.viewerPawnAssignments == null)
                    return null;

                if (string.IsNullOrEmpty(messageWrapper.PlatformUserId))
                    return null;

                string plat = messageWrapper.Platform?.ToLowerInvariant() ?? "unknown";
                string key = $"{plat}:{messageWrapper.PlatformUserId}";

                if (manager.viewerPawnAssignments.TryGetValue(key, out string thingId))
                    return GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);

                // Fallback: assignment manager full lookup (legacy keys)
                return manager.GetAssignedPawn(messageWrapper);
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnItem] GetViewerPawn: {ex.Message}");
                return null;
            }
        }

        public static Pawn GetViewerPawn(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager == null)
                    return null;

                if (assignmentManager.HasAssignedPawn(username))
                    return assignmentManager.GetAssignedPawn(username);

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnItem] GetViewerPawn(username): {ex.Message}");
                return null;
            }
        }

        public static bool WearApparelOnPawn(Thing item, Verse.Pawn pawn)
        {
            try
            {
                if (pawn == null || item == null || item.def == null || pawn.apparel == null)
                    return false;

                if (!item.def.IsApparel)
                    return false;

                if (!(item is Apparel apparel))
                    return false;

                if (!ApparelUtility.HasPartsToWear(pawn, item.def))
                    return false;

                if (pawn.apparel.WouldReplaceLockedApparel(apparel))
                    return false;

                if (!EquipmentUtility.CanEquip(apparel, pawn))
                    return false;

                pawn.apparel.Wear(apparel, dropReplacedApparel: true);
                if (pawn.outfits?.forcedHandler != null)
                    pawn.outfits.forcedHandler.SetForced(apparel, true);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnItem] WearApparelOnPawn: {ex.Message}");
                return false;
            }
        }
    }
}
