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

// Filename: RimazonLocker.cs

/* === Rimazon Locker
 * Purpose:  To make a Locker that Chat viewers can send items they purchased in chat (twitch etc)
 * 
 * Goal:  Get Pawns to unload the box without crashing
 */

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
namespace CAP_ChatInteractive
{
    /// <summary>
    /// 
    /// </summary>
    public class LockerExtension : DefModExtension
    {
        public int maxStacks = 24;

        // Add tab types if you want
        public List<Type> inspectorTabs = new List<Type>
        {
            // typeof(ITab_ContainerStorage),
            typeof(ITab_LockerContents)
        };
    }

    /// <summary>
    /// LockerThingOwner is a custom ThingOwner that enforces the MaxStacks limit defined in LockerExtension.
    /// </summary>
    public class LockerThingOwner : ThingOwner<Thing>
    {
        // Required empty constructor for Scribe_Deep / SaveableFromNode (called with Activator.CreateInstance, no args)
        public LockerThingOwner() : base(null, false, LookMode.Deep)
        {
            // Do nothing here — RimWorld sets the owner field during deserialization because we pass "this" in Scribe_Deep
        }

        // Constructor used when a parent holder is provided during deserialization
        public LockerThingOwner(IThingHolder owner) : base(owner, false, LookMode.Deep)
        {
        }

        // Constructor used when spawning a brand-new locker
        public LockerThingOwner(IThingHolder parentHolder, bool oneStackOnly, LookMode lookMode)
            : base(parentHolder, oneStackOnly, lookMode)
        {
        }

        public override bool TryAdd(Thing thing, bool allowSpecialEffects = false)
        {
            // Extra defensive check - prevents any spawn-related NRE when multiple lockers are placed quickly
            if (Locker == null || Count >= Locker.MaxStacks || thing == null || thing.Destroyed)
                return false;

            // DO NOT set holdingOwner here — let TryAcceptThing handle ownership transfer
            return base.TryAdd(thing, allowSpecialEffects);
        }

        public bool CanAcceptAnyOf(Thing thing)
        {
            if (Locker == null || Count >= Locker.MaxStacks)
                return false;
            return base.CanAcceptAnyOf(thing);
        }

        private Building_RimazonLocker Locker => owner as Building_RimazonLocker;
    }

    /// <summary>
    /// Main Rimazon Locker – inherits from Building_Storage for stability (fixes spawn CTDs, roof/rain, temperature mods).
    /// Only keeps custom MaxStacks limit, chat delivery, rename, eject, and inspect string.
    /// </summary>
    /// <summary>
    /// Main Rimazon Locker – inherits from Building_Storage for maximum stability (fixes spawn CTDs, roof/rain, temperature mods).
    /// Only overrides what is safe and necessary: MaxStacks limit, delivery mote, custom name, inspect string, gizmos, eject.
    /// </summary>
    public class Building_RimazonLocker : Building, IThingHolder, IHaulDestination, IStoreSettingsParent
    {
        public string customName = null;

        // === Add these properties: ===
        //private ThingOwner innerContainer;
        private ThingOwner<Thing> innerContainer;
        public ThingOwner InnerContainer
        {
            //get
            //{
            //    if (innerContainer == null)
            //    {
            //        innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
            //    }
            //    return innerContainer;
            //}
            get
            {
                if (innerContainer == null)
                {
                    // Use the custom owner that respects MaxStacks
                    innerContainer = new LockerThingOwner(this, false, LookMode.Deep);
                    //Logger.Debug($"Locker {instanceId}: Created new LockerThingOwner");
                }
                return innerContainer;
            }

        }
        public int MaxStacks => def.GetModExtension<LockerExtension>().maxStacks;
        public StorageSettings settings;
        public int instanceId = 0;
        private static int instanceCounter = 0;

        // Constructor
        public Building_RimazonLocker()
        {
            instanceId = ++instanceCounter;
            //Logger.Debug($"Locker {instanceId}: Constructor called");

            // Force early initialization BEFORE any external mod (e.g. FSF Tweaks) can strip defaults.
            // This is the #1 robustness improvement.
            _ = InnerContainer;
            _ = GetStoreSettings(); // guarantees settings is never null
        }

        // === IThingHolder
        public ThingOwner GetDirectlyHeldThings() => innerContainer;
        // Update your IThingHolder implementation to properly handle position requests
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            // This is the CORRECT way - ThingOwnerUtility handles the position chain
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }
        public new IThingHolder ParentHolder => null; // As a building on map, we're top-level for our items

        public IntVec3 GetPositionForHeldItems()
        {
            // Return our position for any items we contain
            return Position;
        }


        // === IStoreSettingsParent
        public bool StorageTabVisible => Spawned && Map != null;

        public StorageSettings GetStoreSettings()
        {
            if (settings != null)
                return settings;

            settings = new StorageSettings(this);

            bool copied = false;

            // 1. Vanilla-style copy (what Building_Storage does)
            var parentSettings = GetParentStoreSettings();
            if (parentSettings != null)
            {
                try
                {
                    settings.CopyFrom(parentSettings);
                    copied = true;
                    //Logger.Debug($"[RICS Locker] Copied default storage settings for locker at {Position}");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[RICS Locker] CopyFrom failed (possible mod interference): {ex.Message}");
                }
            }

            // 2. Ultra-robust fallback for [FSF] FrozenSnowFox Tweaks "no default storage"
            if (!copied)
            {
                try
                {
                    settings.filter = new ThingFilter();
                    settings.filter.SetAllowAll(null);
                    settings.filter.ResolveReferences();
                    settings.Priority = StoragePriority.Unstored;   // CRITICAL: Unstored so pawns haul OUT (matches vanilla drop-box pattern)

                    copied = true;
                    Logger.Warning($"[RICS Locker] FSF Tweaks / no-default-storage detected — using robust Unstored fallback");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[RICS Locker] Even fallback filter creation failed: {ex.Message}");
                    settings.filter = new ThingFilter(); // absolute last resort
                }
            }

            // FINAL FORCE — survives every mod interference, save/load, and reload
            if (settings != null)
            {
                settings.Priority = StoragePriority.Unstored;
                //Logger.Debug($"[RICS Locker] Priority locked to Unstored at {Position}");
            }

            return settings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            // Extra safety — many storage tweak mods touch this field
            if (def?.building?.defaultStorageSettings != null)
                return def.building.defaultStorageSettings;

            //Logger.Debug($"[RICS] No defaultStorageSettings on def (common with storage tweak mods)");
            return null;
        }

        public void Notify_SettingsChanged()
        {
            // Refresh haul jobs if needed
        }

        // === PostMake
        public override void PostMake()
        {
            base.PostMake();

            // GetStoreSettings will initialize settings if null
            var s = GetStoreSettings();

            // Force Unstored immediately (survives early mod hooks)
            if (s != null)
                s.Priority = StoragePriority.Unstored;
        }

        // === SpawnSetup
        public override void SpawnSetup(Map map, bool respawningAfterReload)
        {
            base.SpawnSetup(map, respawningAfterReload);

            _ = InnerContainer;
            //Logger.Debug($"[RICS] Locker has {innerContainer.Count} items after spawn");

            _ = GetStoreSettings();

            // Enforce Unstored priority every spawn (survives mod conflicts & reload)
            if (settings != null)
            {
                settings.Priority = StoragePriority.Unstored;
                //Logger.Debug($"[RICS Locker] Priority forced to Unstored at {Position} | Stacks: {innerContainer.Count}/{MaxStacks}");
            }
        }

        //  === IHaulDestination
        public new IntVec3 Position => base.Position;           // Inherited from Thing, but explicit for clarity
        public new Map Map => base.Map;                         // Inherited from Thing
        // Testing Note.  tested as false and was unable to drop stuff into locker, so this must be true
        public bool HaulDestinationEnabled => true;


        // === Rename Locker
        public void RenameLocker(string newName)
        {
            customName = newName.NullOrEmpty() ? null : newName.Trim();
        }

        // === ExposeData
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref customName, "customName");

            // Important: pass "this" as the IThingHolder so RimWorld wires the owner correctly on load
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);

            Scribe_Deep.Look(ref settings, "settings", this);

            // Re-apply Unstored priority after loading (survives old saves and storage tweak mods)
            if (settings != null)
            {
                settings.Priority = StoragePriority.Unstored;
            }

            // Post-load safety
            if (innerContainer != null)
            {
                // Ensure every item knows its holding owner (vanilla pattern)
                foreach (Thing t in innerContainer)
                {
                    if (t != null && t.holdingOwner == null)
                        t.holdingOwner = innerContainer;
                }
            }

            //Logger.Debug($"Locker {instanceId} loaded — stacks: {innerContainer?.Count ?? 0}/{MaxStacks}");
        }

        // === DeSpawn
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // Save items before despawn
            // GameComponent_LockerManager.SaveLockerInventory(this);

            base.DeSpawn(mode);
        }

        /// <summary>
        /// This is how we spawn stuff into the locker
        /// From CHAT viewers buying items.
        /// 5 Referances
        /// </summary>
        /// <param name="thing"></param>
        /// <returns></returns>
        public virtual bool Accepts(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing is Pawn)
                return false;

            if (settings == null || !settings.AllowedToAccept(thing))
                return false;

            try
            {
                int existingStacksOfSameType = 0;
                int totalSpaceForThisType = 0;
                var snapshot = innerContainer.ToList();

                foreach (var existingThing in snapshot)
                {
                    if (existingThing == null || existingThing.Destroyed) continue;

                    if (existingThing.def == thing.def && existingThing.CanStackWith(thing))
                    {
                        existingStacksOfSameType++;
                        int spaceLeft = existingThing.def.stackLimit - existingThing.stackCount;
                        totalSpaceForThisType += Mathf.Max(0, spaceLeft);
                    }
                }

                if (existingStacksOfSameType > 0)
                {
                    if (totalSpaceForThisType >= thing.stackCount)
                        return true;

                    int itemsRemainingAfterMerge = thing.stackCount - totalSpaceForThisType;
                    int stacksNeededForRemaining = Mathf.CeilToInt((float)itemsRemainingAfterMerge / thing.def.stackLimit);

                    int emptyStackSlots = MaxStacks - innerContainer.Count;
                    return emptyStackSlots >= stacksNeededForRemaining;
                }

                int stacksRequired = Mathf.CeilToInt((float)thing.stackCount / thing.def.stackLimit);
                int availableStackSlots = MaxStacks - innerContainer.Count;

                return availableStackSlots >= stacksRequired;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[RimazonLocker] Accepts crash prevented for {thing?.LabelShort ?? "null"}: {ex.Message}\nStack: {ex.StackTrace}", "LockerAcceptsCrash".GetHashCode());
                return false;
            }
        }

        /// <summary>
        /// Try to accept a thing
        /// 2 Referances
        /// </summary>
        /// <param name="thing"></param>
        /// <param name="allowSpecialEffects"></param>
        /// <returns></returns>
        // In RimazonLocker.cs, inside Building_RimazonLocker class
        // Replace the current TryAcceptThing method with this original working version:
        public virtual bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (!Spawned || Map == null || thing == null || thing.Destroyed)
                return false;

            if (!Accepts(thing))
                return false;

            try
            {
                bool merged = false;
                if (innerContainer.Count > 0)
                {
                    var snapshot = innerContainer.ToList();
                    foreach (var existingThing in snapshot)
                    {
                        if (existingThing == null || existingThing.Destroyed) continue;

                        if (existingThing.def == thing.def && existingThing.CanStackWith(thing))
                        {
                            int spaceLeft = existingThing.def.stackLimit - existingThing.stackCount;
                            if (spaceLeft > 0)
                            {
                                int amountToMerge = Mathf.Min(spaceLeft, thing.stackCount);
                                if (amountToMerge > 0)
                                {
                                    existingThing.stackCount += amountToMerge;
                                    thing.stackCount -= amountToMerge;

                                    if (thing.stackCount <= 0)
                                    {
                                        thing.Destroy();
                                        if (allowSpecialEffects)
                                        {
                                            MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.25f), Map, "Delivery Received", Color.white, 2f);
                                        }
                                        return true;
                                    }
                                    merged = true;
                                }
                            }
                        }
                    }
                }

                if (thing.stackCount > 0)
                {
                    if (thing.ParentHolder != null && thing.ParentHolder != this)
                    {
                        thing.ParentHolder.GetDirectlyHeldThings()?.Remove(thing);
                    }

                    if (thing.stackCount > 0)
                    {
                        bool added = innerContainer.TryAdd(thing, allowSpecialEffects);
                        if (added)
                        {
                            if (thing is Thing t)
                            {
                                t.holdingOwner = innerContainer;
                            }

                            if (allowSpecialEffects)
                            {
                                MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.25f), Map, "Delivery Received", Color.white, 2f);
                            }
                            return true;
                        }
                        else
                        {
                            Log.Warning($"[Locker] TryAdd returned false for {thing.LabelShort} x{thing.stackCount}");
                            return false;
                        }
                    }
                }

                if (merged && allowSpecialEffects)
                {
                    MoteMaker.ThrowText(this.DrawPos + new Vector3(0f, 0f, 0.25f), Map, "RICS.Locker.DeliveryReceived".Translate(), Color.white, 2f);
                }

                return merged;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Locker CRASH CAUGHT in TryAcceptThing] For {thing?.LabelShort ?? "null"} x{thing?.stackCount ?? 0}:\n{ex.Message}\nStackTrace:\n{ex.StackTrace}");
                return false;
            }
        }
        // === IAcceptDropPod interface implementation

        public void AcceptDropPod(DropPodIncoming dropPod, Thing[] contents)
        {
            foreach (Thing thing in contents)
            {
                if (Accepts(thing))
                {
                    TryAcceptThing(thing, true);   // Use full merge + detach logic
                }
                else
                {
                    // Drop items that can't fit
                    GenPlace.TryPlaceThing(thing, Position, Map, ThingPlaceMode.Near);
                }
            }

            // Show delivery effect
            MoteMaker.ThrowText(this.DrawPos + new Vector3(0f, 0f, 0.25f), Map, "Delivery Received", Color.white, 2f);
        }

        // === Get Gizmos, How we rename our Locker.
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            // Rename button
            yield return new Command_Action
            {
                defaultLabel = "Rename locker",
                defaultDesc = "Give this Rimazon locker a unique name for chat deliveries.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Rename", true),
                action = () =>
                {
                    Find.WindowStack.Add(new Dialog_RenameLocker(this));
                }
            };

            // OPEN LOCKER BUTTON - This is the key!
            yield return new Command_Action
            {
                defaultLabel = "Open locker",
                defaultDesc = "View and access items in the locker.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_OpenLocker", true),
                action = () => OpenLocker()
            };

            // STORAGE SETTINGS BUTTON (optional)
            yield return new Command_Action
            {
                defaultLabel = "Storage settings",
                defaultDesc = "Configure what can be stored in this locker.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Settings", true), // Or use a custom icon
                action = () => OpenStorageSettings()
            };

            // Eject button with safe placement
            if (innerContainer.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Eject all contents",
                    defaultDesc = "Drop all items from the locker to the ground nearby.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Eject"),
                    action = () => SafeEjectAllContents()
                };
            }
        }


        /// <summary>
        /// Safely ejects all contents without placing them on the locker itself
        /// </summary>
        public void SafeEjectAllContents()
        {
            if (innerContainer.Count == 0 || Map == null)
            {
                // Logger.Debug("SafeEjectAllContents: Container empty or no map");
                return;
            }

            // Logger.Debug($"SafeEjectAllContents: Attempting to eject {innerContainer.Count} items from locker at {Position}");

            try
            {
                // Find a valid cell to drop items near the locker
                IntVec3 dropCell = FindValidDropCell(Position, Map);

                if (dropCell.IsValid)
                {
                    // Logger.Debug($"SafeEjectAllContents: Dropping items at {dropCell}");
                    bool success = innerContainer.TryDropAll(dropCell, Map, ThingPlaceMode.Near);
                    //Logger.Debug($"SafeEjectAllContents: TryDropAll result = {success}");

                    if (!success)
                    {
                        Logger.Warning("SafeEjectAllContents: TryDropAll failed, trying individual drops");

                        SafeDropItemsIndividually();
                    }
                }
                else
                {
                    Logger.Warning("SafeEjectAllContents: No valid drop cell found near locker, trying individual drops");

                    SafeDropItemsIndividually();
                }

                // Logger.Debug($"SafeEjectAllContents: After ejection, container count = {innerContainer.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error($"SafeEjectAllContents ERROR: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");

                // Emergency fallback: try to save items

                EmergencyEject();
            }

            // Show effect if we're still spawned
            if (Spawned && Map != null)
            {
                try
                {
                    MoteMaker.ThrowText(this.DrawPos + new Vector3(0f, 0f, 0.25f), Map, "RICS.Locker.ItemsEjected".Translate(), Color.white, 2f);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to create mote: {ex.Message}");
                }
            }
        }

        private IntVec3 FindValidDropCell(IntVec3 center, Map map, int radius = 3)
        {
            for (int r = 1; r <= radius; r++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, r, true))
                {
                    if (!cell.InBounds(map) || cell.Fogged(map))
                        continue;

                    // Check if the cell is walkable
                    if (!cell.Walkable(map))
                        continue;

                    // Check if there's a building that blocks placement
                    Building building = cell.GetEdifice(map);
                    if (building != null && building.def.passability == Traversability.Impassable)
                        continue;

                    // Cell is valid
                    return cell;
                }
            }

            // If no ideal cell, return the center (fallback)
            return center;
        }

        private void SafeDropItemsIndividually()
        {
            if (innerContainer.Count == 0 || Map == null)
                return;

            // Create a copy of the list to avoid modification during iteration
            List<Thing> itemsToDrop = new List<Thing>();
            foreach (Thing thing in innerContainer)
            {
                itemsToDrop.Add(thing);
            }

            foreach (Thing thing in itemsToDrop)
            {
                if (thing == null || thing.Destroyed || !innerContainer.Contains(thing))
                    continue;

                try
                {
                    // Find a valid cell for this specific item
                    IntVec3 dropCell = FindValidDropCell(Position, Map, 5);

                    if (dropCell.IsValid)
                    {

                        // Logger.Debug($"Dropping {thing.LabelCap} at {dropCell}");
                        bool dropped = innerContainer.TryDrop(thing, dropCell, Map, ThingPlaceMode.Direct, out Thing result);
                        // Logger.Debug($"  - Drop result: {dropped}, result: {result?.LabelCap ?? "null"}");
                    }
                    else
                    {

                        Logger.Warning($"No valid drop cell found for {thing.LabelCap}, forcing drop at position");
                        innerContainer.TryDrop(thing, Position, Map, ThingPlaceMode.Near, out Thing result);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error dropping {thing?.LabelCap ?? "unknown item"}: {ex.Message}");
                }
            }
        }

        private void EmergencyEject()
        {
            // Last resort: destroy items to prevent crash
            Logger.Error("EmergencyEject: Destroying items to prevent crash");

            while (innerContainer.Count > 0)
            {
                try
                {
                    Thing thing = innerContainer[0];
                    if (thing != null)
                    {
                        Logger.Error($"Destroying: {thing.LabelCap} x{thing.stackCount}");
                        thing.Destroy();
                    }

                    innerContainer.Remove(thing);
                }
                catch
                {

                    // If even this fails, clear the container forcefully
                    innerContainer.Clear();
                    break;
                }
            }
        }

        /// <summary>
        /// Ejects ONE specific item from the locker to a nearby valid cell.
        /// Called from the new per-item eject button in Dialog_LockerContents.
        /// Reuses the exact same FindValidDropCell + TryDrop pattern already used by
        /// SafeEjectAllContents / SafeDropItemsIndividually so placement rules stay consistent
        /// with vanilla (and with your existing Unstored priority + haul-destination behavior).
        /// WHY this change: Viewers/streamers requested the ability to remove individual
        /// purchased items instead of being forced to eject everything.
        /// Edge cases: null thing, thing no longer in container, no Map/spawned, invalid drop cell
        /// (falls back to Position), exceptions logged via Logger (graceful degradation, no crash).
        /// </summary>
        public void EjectSingleItem(Thing thing)
        {
            if (thing == null || innerContainer == null || !innerContainer.Contains(thing) || Map == null || !Spawned)
                return;

            try
            {
                IntVec3 dropCell = FindValidDropCell(Position, Map, 3);
                if (!dropCell.IsValid)
                {
                    dropCell = Position; // same fallback used in SafeDropItemsIndividually
                }

                bool dropped = innerContainer.TryDrop(thing, dropCell, Map, ThingPlaceMode.Near, out Thing _);

                if (dropped && Spawned && Map != null)
                {
                    MoteMaker.ThrowText(this.DrawPos + new Vector3(0f, 0f, 0.25f), Map, "Ejected", Color.white, 1.5f);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"EjectSingleItem ERROR for {thing?.LabelShort ?? "null"}: {ex.Message}");
            }
        }

        public bool CanOpen => true;

        // === How we access the contents
        public void OpenLocker()
        {
            Find.WindowStack.Add(new Dialog_LockerContents(this));
        }

        // === Open Storage Settings Method ===
        public void OpenStorageSettings()
        {
            // Create a simple window for storage settings
            Find.WindowStack.Add(new Dialog_StorageSettings(this));
        }

        // === Inspect String
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (!customName.NullOrEmpty())
            {
                if (!text.NullOrEmpty()) text += "\n";
                text += "RICS_Named".Translate(customName);  // Uses translation
            }
            if (!text.NullOrEmpty())
            {
                text += "\n";
            }
            if (innerContainer.Count == 0)
            {
                text += "RICS_LockerEmpty".Translate();  // Uses translation
            }
            else
            {
                text += "RICS_Contains".Translate() + innerContainer.ContentsString.CapitalizeFirst();
                text += "\n" + "RICS_StackSlots".Translate(innerContainer.Count, MaxStacks);
                text += "\n" + "RICS_TotalItems".Translate(innerContainer.TotalStackCount);
            }
            return text;
        }

        public override string Label
        {
            get
            {
                if (!customName.NullOrEmpty())
                {
                    return customName + " (" + def.label + ")";
                }
                return base.Label;
            }
        }

        public override void PostMapInit()
        {
            base.PostMapInit();

            // Fix position tracking for items in our container
            if (innerContainer != null && innerContainer.Count > 0)
            {
                foreach (Thing thing in innerContainer)
                {
                    if (thing != null)
                    {
                        // Ensure proper position tracking
                        thing.holdingOwner = innerContainer;

                        // If the thing has a comp that needs position, update it
                        if (thing is ThingWithComps twc)
                        {
                            // Update any comps that need position info
                        }
                    }
                }
            }
        }

    }

    public class Dialog_RenameLocker : Window
    {
        private string curName;
        private Building_RimazonLocker locker;

        public Dialog_RenameLocker(Building_RimazonLocker locker)
        {
            this.locker = locker;
            curName = locker.customName ?? "";
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 175f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Rename locker (leave blank to reset):");

            curName = Widgets.TextField(new Rect(0f, 40f, inRect.width, 35f), curName);

            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f, inRect.width / 2 - 20f, 35f), "OK"))
            {
                locker.RenameLocker(curName);
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 5f, inRect.height - 35f - 15f, inRect.width / 2 - 20f, 35f), "Cancel"))
            {
                Close();
            }
        }
    }

    public class Dialog_LockerContents : Window
    {
        private Building_RimazonLocker locker;
        private Vector2 scrollPosition;
        private List<Thing> cachedContents;

        public Dialog_LockerContents(Building_RimazonLocker locker)
        {
            this.locker = locker;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            CacheContents();
        }

        private void CacheContents()
        {
            cachedContents = new List<Thing>();
            if (locker != null && locker.InnerContainer != null)
            {
                cachedContents.AddRange(locker.InnerContainer);
            }
        }

        public override Vector2 InitialSize => new Vector2(720f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            string title = locker.customName.NullOrEmpty()
                ? "RICS_LockerContents".Translate()  // Use translation
                : "RICS_ContentsOf".Translate(locker.customName);  // Use translation
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), title);

            Text.Font = GameFont.Small;
            // Widgets.Label(new Rect(0f, 40f, inRect.width, 25f), $"Stack slots: {locker.InnerContainer.Count}/{locker.MaxStacks}");
            Widgets.Label(new Rect(0f, 40f, inRect.width, 25f),
                "RICS.Storage.StackSlots".Translate(locker.InnerContainer.Count, locker.MaxStacks));

            // We add a 28px left margin for the eject widget button.
            // All existing column x-offsets are shifted +28px so nothing overlaps.
            // Icon column now starts at ~28f, name at 58f, etc.
            // The eject button uses Widgets.ButtonImage + ContentFinder exactly like your
            // existing "Eject all" / rename gizmos. You will add the RICS_EjectItem asset.

            //Widgets.Label(new Rect(0f, 60f, inRect.width, 25f), $"Total items: {locker.InnerContainer.TotalStackCount}");
            Widgets.Label(new Rect(0f, 60f, inRect.width, 25f),
                            "RICS.Storage.TotalItems".Translate(locker.InnerContainer.TotalStackCount));

            // === COLUMN HEADERS (fixed position) ===
            // These must be drawn *before* BeginScrollView and at a fixed y,
            // NOT using viewRect.y. Otherwise the scroll content starts at the
            // same y and overlaps the headers.
            // Header block ends at y ≈ 144f (120f + 25f height + line).
            Rect headerRect = new Rect(0f, 120f, inRect.width, 25f);
            if (cachedContents.Count > 0)
            {
                GUI.color = Color.gray;
                Widgets.DrawLineHorizontal(headerRect.x, headerRect.y + 24f, headerRect.width);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.MiddleLeft;
                // Eject column has no text header (icon-only)
                Widgets.Label(new Rect(headerRect.x + 58f, headerRect.y, 180f, 25f), "RICS.Storage.Item".Translate());
                Widgets.Label(new Rect(headerRect.x + 250f, headerRect.y, 70f, 25f), "RICS.Storage.Quantity".Translate());
                Widgets.Label(new Rect(headerRect.x + 330f, headerRect.y, 90f, 25f), "RICS.Storage.EachValue".Translate());
                Widgets.Label(new Rect(headerRect.x + 430f, headerRect.y, 120f, 25f), "RICS.Storage.TotalValue".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // === SCROLL VIEW SETUP (now correctly below headers) ===
            // Widgets.BeginScrollView(viewRect, ref scrollPosition, listRect)
            // - viewRect  = the visible viewport rectangle (what the player sees)
            // - listRect  = the full content size (height = all rows * 35f)
            // - scrollPosition = RimWorld automatically updates when you scroll
            //
            // We start the viewport at 155f (10f gap after header line) and leave
            // 40f at the bottom for the Eject All + Close buttons.
            float scrollStartY = 155f;
            float buttonAreaHeight = 40f;
            Rect viewRect = new Rect(0f, scrollStartY, inRect.width, inRect.height - scrollStartY - buttonAreaHeight);
            Rect listRect = new Rect(0f, 0f, viewRect.width - 20f, cachedContents.Count * 35f);

            Widgets.BeginScrollView(viewRect, ref scrollPosition, listRect); float y = 25f; // Start below header

            for (int i = 0; i < cachedContents.Count; i++)
            {
                Thing thing = cachedContents[i];
                Rect rowRect = new Rect(0f, y, listRect.width, 32f);

                // Highlight alternate rows
                if (i % 2 == 0)
                {
                    Widgets.DrawLightHighlight(rowRect);
                }

                // === NEW: Eject single item button (left side, caravan-menu widget style) ===
                // 24x24 icon button. Uses same ContentFinder pattern as your other RICS commands.
                // "UI/Commands/RICS_Eject" is a custom "eject" icon included in the mod's content.
                // "UI/Buttons/Abandon" is the vanilla "abandon item" icon used in caravans/
                Rect ejectRect = new Rect(2f, y + 4f, 24f, 24f);
                Texture2D ejectIcon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Eject", true);
                if (Widgets.ButtonImage(ejectRect, ejectIcon))
                {
                    if (thing != null && locker.InnerContainer != null && locker.InnerContainer.Contains(thing))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        locker.EjectSingleItem(thing);
                        CacheContents(); // refresh list immediately so the item disappears from UI
                    }
                }
                if (Mouse.IsOver(ejectRect))
                {
                    TooltipHandler.TipRegion(ejectRect, "RICS.Locker.EjectThisItem".Translate());
                }

                // Icon — shifted right by 28px to make room for eject button (was 0f)
                Widgets.ThingIcon(new Rect(28f, y + 2f, 28f, 28f), thing);

                // Name — shifted (was 30f, now 58f)
                Text.Anchor = TextAnchor.MiddleLeft;
                string itemName = thing.LabelCapNoCount ?? thing.def?.label ?? "RICS.Unknown".Translate();
                Widgets.Label(new Rect(58f, y, 190f, 32f), itemName);

                // Quantity — shifted (was 220f, now 250f)
                string quantityText = thing.stackCount.ToString();
                Widgets.Label(new Rect(250f, y, 80f, 32f), quantityText);

                // Individual item value (per unit) — shifted (was 300f, now 330f)
                string eachValue = thing.MarketValue.ToStringMoney();
                Widgets.Label(new Rect(330f, y, 100f, 32f), eachValue);

                // Total value (item value × quantity) — shifted (was 400f, now 430f)
                float totalValue = thing.MarketValue * thing.stackCount;
                string totalValueText = totalValue.ToStringMoney();
                Widgets.Label(new Rect(430f, y, 130f, 32f), totalValueText);

                // Info button (right side, unchanged)
                if (Widgets.ButtonImage(new Rect(listRect.width - 24f, y + 4f, 24f, 24f), TexButton.Info))
                {
                    if (thing?.def != null)
                    {
                        Find.WindowStack.Add(new Dialog_InfoCard(thing.def, thing.Stuff));
                    }
                    else
                    {
                        Messages.Message("RICS.CannotShowInfo".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }

                // Tooltip — shows detailed info including stack count
                string tooltip = thing.GetInspectString();
                if (!string.IsNullOrEmpty(tooltip))
                {
                    TooltipHandler.TipRegion(rowRect, tooltip);
                }

                y += 35f;
            }

            Widgets.EndScrollView();
            Text.Anchor = TextAnchor.UpperLeft;

            Rect buttonArea = new Rect(10f, inRect.height - 35f, inRect.width - 20f, 30f);
            float ejectAllWidth = 150f;
            float closeWidth = 110f;
            Rect ejectAllRect = new Rect(buttonArea.x, buttonArea.y, ejectAllWidth, 30f);
            Rect closeRect = new Rect(buttonArea.xMax - closeWidth, buttonArea.y, closeWidth, 30f);

            if (cachedContents.Count > 0)
            {
                if (Widgets.ButtonText(ejectAllRect, "RICS.EjectAll".Translate()))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    locker.SafeEjectAllContents();
                    CacheContents(); // keep list fresh for rapid successive actions
                }
            }

            if (Widgets.ButtonText(closeRect, "Close".Translate()))
            {
                Close();
            }
        }
    }
    

    public class Dialog_StorageSettings : Window
    {
        private Building_RimazonLocker locker;
        private ThingFilterUI.UIState uiState = new ThingFilterUI.UIState();

        public Dialog_StorageSettings(Building_RimazonLocker locker)
        {
            this.locker = locker;
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = true;
            this.closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(420f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                // Force initialization — survives any storage tweak mod
                if (locker?.settings == null)
                    locker.GetStoreSettings();

                if (locker?.settings == null || locker.settings.filter == null)
                {
                    Widgets.Label(inRect, "RICS.Locker.StorageSettingsUnavailable".Translate());
                    return;
                }

                // Reset GUI state
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                Rect mainRect = new Rect(0f, 0f, inRect.width, inRect.height).ContractedBy(10f);

                // Draw priority
                DrawPriority(new Rect(mainRect.x, mainRect.y, mainRect.width, 30f), locker.settings);

                // Draw filter — use our own filter as parent if def was stripped
                Rect filterRect = new Rect(mainRect.x, mainRect.y + 35f, mainRect.width, mainRect.height - 35f);
                ThingFilter parentFilter = locker.def?.building?.defaultStorageSettings?.filter ?? locker.settings.filter;
                DrawFilter(filterRect, locker.settings.filter, parentFilter);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in storage settings window: {ex}");
            }
        }

        private void DrawPriority(Rect rect, StorageSettings settings)
        {
            Widgets.Label(rect.LeftHalf(), "Priority".Translate() + ":");

            // In DrawPriority (Dialog_StorageSettings or ITab)
            // Remove Button to prevent Crash
            // Keep Player/Streamer from using the Locker as a storage point
            // prevents pawns from delivering to the box.
            Widgets.Label(rect.LeftHalf(), "RICS.Priority".Translate() + ":");
            Widgets.Label(rect.RightHalf(), "RICS.Unstored".Translate());
        }

        private void DrawFilter(Rect rect, ThingFilter filter, ThingFilter parentFilter)
        {
            ThingFilterUI.DoThingFilterConfigWindow(
                rect: rect,
                state: uiState,
                filter: filter,
                parentFilter: parentFilter,
                openMask: 1,
                forceHiddenDefs: null,
                forceHiddenFilters: null,
                forceHideHitPointsConfig: false,
                forceHideQualityConfig: false,
                showMentalBreakChanceRange: false,
                suppressSmallVolumeTags: null,
                map: Find.CurrentMap
            );
        }
    }
}
