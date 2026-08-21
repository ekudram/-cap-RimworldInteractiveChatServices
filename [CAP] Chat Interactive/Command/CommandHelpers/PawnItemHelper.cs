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
                if (pawn == null || item == null || item.def == null || item.Destroyed)
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

                // Clear map spawn so AddEquipment can take ownership.
                if (weapon.Spawned)
                {
                    if (pawn.Map == null)
                        return false;
                    weapon.DeSpawn();
                }

                // If still in a container (e.g. temp holder), try to extract without destroying.
                if (weapon.holdingOwner != null)
                {
                    var owner = weapon.holdingOwner;
                    if (!owner.TryDrop(weapon, pawn.PositionHeld, pawn.MapHeld ?? pawn.Map, ThingPlaceMode.Near, 1, out Thing dropped))
                    {
                        // Last resort: remove from owner list if API allows via TryDrop failed
                        Logger.Warning(
                            $"[PawnItem] EquipItemOnPawn: could not release {weapon.LabelShort} from holder for {pawn.LabelShort}.");
                        return false;
                    }
                    weapon = dropped as ThingWithComps ?? weapon;
                    if (weapon == null || weapon.Destroyed)
                        return false;
                    if (weapon.Spawned)
                        weapon.DeSpawn();
                }

                ThingWithComps oldWeapon = null;

                if (pawn.equipment.Primary != null)
                {
                    ThingWithComps primary = pawn.equipment.Primary;
                    bool moved = false;
                    if (pawn.inventory?.innerContainer != null)
                    {
                        moved = pawn.equipment.TryTransferEquipmentToContainer(
                            primary, pawn.inventory.innerContainer);
                    }

                    if (!moved)
                    {
                        if (!pawn.equipment.TryDropEquipment(primary, out oldWeapon, pawn.Position))
                        {
                            Logger.Warning(
                                $"[PawnItem] Could not make room for {pawn.LabelShort}'s new weapon.");
                            return false;
                        }
                    }
                }

                if (weapon.Destroyed || weapon.holdingOwner != null)
                {
                    Logger.Warning(
                        $"[PawnItem] EquipItemOnPawn: weapon not free for AddEquipment ({pawn.LabelShort}).");
                    // Best-effort restore dropped old weapon
                    if (oldWeapon != null && !oldWeapon.Destroyed && oldWeapon.holdingOwner == null && !oldWeapon.Spawned)
                    {
                        try { pawn.equipment.AddEquipment(oldWeapon); }
                        catch { /* ignore restore failure */ }
                    }
                    return false;
                }

                if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, weapon, 1))
                {
                    if (oldWeapon != null && !oldWeapon.Destroyed && oldWeapon.holdingOwner == null && !oldWeapon.Spawned)
                    {
                        try { pawn.equipment.AddEquipment(oldWeapon); }
                        catch (Exception restoreEx)
                        {
                            Logger.Warning($"[PawnItem] Failed restoring old weapon: {restoreEx.Message}");
                        }
                    }
                    return false;
                }

                pawn.equipment.AddEquipment(weapon);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnItem] EquipItemOnPawn: {ex}");
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
