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

    // Add this new class near the top of the file, after the LockerExtension
    public class LockerThingOwner : ThingOwner<Thing>
    {
        private Building_RimazonLocker Locker => (Building_RimazonLocker)owner;

        public LockerThingOwner(IThingHolder parentHolder, bool oneStackOnly, LookMode lookMode)
                : base(parentHolder, oneStackOnly, lookMode)
        {
        }

        public override bool TryAdd(Thing thing, bool allowSpecialEffects = false)
        {
            if (Count >= Locker.MaxStacks)
                return false;
            return base.TryAdd(thing, allowSpecialEffects);
        }

        public bool CanAcceptAnyOf(Thing thing)
        {
            if (Count >= Locker.MaxStacks)
                return false;
            return base.CanAcceptAnyOf(thing);
        }
    }
    // Main Class
    // public class Building_RimazonLocker : v, IThingHolder, IHaulDestination, IStoreSettingsParent
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
                    innerContainer = new LockerThingOwner(this, false, LookMode.Deep);
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
            Logger.Debug($"Locker {instanceId}: Constructor called");

            // The key is using the correct constructor parameters:
            // 1. parentHolder (this)
            // 2. oneStackOnly (false - we want multiple stacks)
            // 3. lookMode (LookMode.Deep for proper serialization)
            // innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
            // innerContainer.maxStacks = MaxStacks; Set in Def         <maxStacks>24</maxStacks>
        }

        // === IThingHolder
        public ThingOwner GetDirectlyHeldThings() => innerContainer;
        // Update your IThingHolder implementation to properly handle position requests
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            // This is the CORRECT way - ThingOwnerUtility handles the position chain
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }
        public IThingHolder ParentHolder => null; // As a building on map, we're top-level for our items

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
            {
                return settings;
            }

            // Lazy creation
            settings = new StorageSettings(this);

            bool copied = false;

            // 1. Try the normal parent/defaults copy (what vanilla would have done)
            var parentSettings = GetParentStoreSettings();
            if (parentSettings != null)
            {
                try
                {
                    settings.CopyFrom(parentSettings);
                    copied = true;
                    // Logger.Debug($"[RICS Locker] Successfully copied settings from parent/defaults for {this.def.defName} at {this.Position}");
                }
                catch (Exception ex)
                {
                    // Logger.Error($"[RICS Locker] Failed to copy parent settings during lazy init: {ex.Message}. Using fallback.");
                }
            }

            // 2. If no parent or copy failed, explicitly grab the def's defaultStorageSettings (your XML block)
            if (!copied && def?.building?.defaultStorageSettings != null)
            {
                try
                {
                    settings.CopyFrom(def.building.defaultStorageSettings);
                    copied = true;
                    // Logger.Debug($"[RICS Locker] Recovered by copying directly from def.defaultStorageSettings for {this.def.defName}");
                }
                catch (Exception ex)
                {
                    // Logger.Debug($"[RICS Locker] Failed to copy def defaults: {ex.Message}. Using allow-all fallback.");
                }
            }

            // 3. Ultimate fallback: make sure we have a usable filter (allow everything, low priority)
            if (!copied)
            {
                settings.filter = new ThingFilter();
                //settings.filter.SetAllowAllWhoCanHold(this);  // Or just SetAllowEverything() if you prefer broader
                settings.Priority = StoragePriority.Low;      // Matches your XML intent
                // Logger.Warning($"[RICS Locker] No valid settings source found for {this.def.defName} at {this.Position}. Using full allow-all fallback.");
            }

            // Optional: enforce any fixed restrictions from your XML <fixedStorageSettings> if you want
            // (you can merge them here if needed)

            return settings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            if (def?.building?.defaultStorageSettings != null)
            {
                return def.building.defaultStorageSettings;
            }

            // If def is null or no defaults (very rare during normal play)
            // Logger.Warning($"[RICS] No defaultStorageSettings found on def for {def?.defName ?? "unknown"}");
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
            // Logger.Debug($"ThingOwner: {this.GetDirectlyHeldThings() != null}");
            // Logger.Debug($"Locker {this} settings after init: {(settings != null ? "exists" : "NULL")} | Parent defaults: {(def.building?.defaultStorageSettings != null ? "exists" : "NULL")}");
            // Initialize innerContainer if null (shouldn't be, but just in case)
            //if (innerContainer == null)
            //{
            //    innerContainer = new ThingOwner<Thing>(this, LookMode.Deep);
            //}

            // GetStoreSettings will initialize settings if null
            var s = GetStoreSettings();
        }

        // === SpawnSetup
        public override void SpawnSetup(Map map, bool respawningAfterReload)
        {
            base.SpawnSetup(map, respawningAfterReload);

            //if (innerContainer == null)
            //{
            //    innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
            //}
            _ = InnerContainer;
            Logger.Debug($"[RICS] Locker has {innerContainer.Count} items after spawn");

            _ = GetStoreSettings();
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
            Scribe_Deep.Look(ref settings, "settings", this);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
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
            // Logger.Debug($"[DEBUG-ACCEPTS] Called for {thing?.LabelShort ?? "null"}" +
            //    $" stack {thing?.stackCount}, pawn job? {Find.TickManager?.Paused == false}," +
            //    $" container count {innerContainer.Count}, total items {innerContainer.TotalStackCount}");

            if (thing == null || thing.Destroyed || thing is Pawn)
                return false;

            if (settings == null || !settings.AllowedToAccept(thing))
            {
                // Logger.Debug($"[DEBUG-ACCEPTS] !settings.AllowedToAccept({thing?.LabelShort ?? "null"})");
                return false;
            }

            try
            {
                // Calculate how many stacks we currently have of this item type
                int existingStacksOfSameType = 0;
                int totalSpaceForThisType = 0;

                // Create a snapshot to avoid modification during iteration
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

                // If we have existing stacks of this type, check if they have space
                if (existingStacksOfSameType > 0)
                {
                    // We can merge into existing stacks if they have space
                    if (totalSpaceForThisType >= thing.stackCount)
                    {
                        return true; // Can merge completely into existing stacks
                    }
                    else if (totalSpaceForThisType > 0)
                    {
                        // We can partially merge, need to check if we have room for remaining items
                        int itemsRemainingAfterMerge = thing.stackCount - totalSpaceForThisType;
                        int stacksNeededForRemaining = Mathf.CeilToInt((float)itemsRemainingAfterMerge / thing.def.stackLimit);

                        // Check if we have enough empty stack slots
                        int emptyStackSlots = MaxStacks - innerContainer.Count;
                        return emptyStackSlots >= stacksNeededForRemaining;
                    }
                }

                // No existing stacks to merge with, need new stack(s)
                int stacksRequired = Mathf.CeilToInt((float)thing.stackCount / thing.def.stackLimit);
                int availableStackSlots = MaxStacks - innerContainer.Count;

                return availableStackSlots >= stacksRequired;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[RimazonLocker] Accepts crash prevented for {thing?.LabelShort ?? "null"}: {ex.Message}\nStack: {ex.StackTrace}", "LockerAcceptsCrash".GetHashCode());
                return false;  // Fail closed: reject instead of crash game
            }
        }

        /// <summary>
        /// Try to accept a thing
        /// 2 Referances
        /// </summary>
        /// <param name="thing"></param>
        /// <param name="allowSpecialEffects"></param>
        /// <returns></returns>
        public virtual bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (!Spawned || Map == null || thing == null || thing.Destroyed)
            {
                //Logger.Warning("[Locker TryAcceptThing] Early reject: invalid state/thing");
                return false;
            }

            if (!Accepts(thing))
            {
                //Logger.Message($"[Locker] Rejected {thing.LabelShort} x{thing.stackCount} - Accepts() returned false");
                //Logger.Message($"[Locker] Container status: {innerContainer.Count}/{MaxStacks} stacks, {innerContainer.TotalStackCount} total items");
                return false;
            }

            //Logger.Message($"[CRITICAL-DEBUG] Reached TryAcceptThing for {thing.LabelShort} x{thing.stackCount} | Current: {innerContainer.Count}/{MaxStacks} stacks");

            try
            {
                // First, try to merge with existing stacks
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
                                        // Logger.Debug($"[Locker] SUCCESS: Merged all items into existing stack");
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

                // If we still have items to add after merging
                if (thing.stackCount > 0)
                {
                    // Force remove from any prior owner
                    if (thing.ParentHolder != null && thing.ParentHolder != this)
                    {
                        thing.ParentHolder.GetDirectlyHeldThings()?.Remove(thing);
                        // Logger.Debug("[CRITICAL-DEBUG] Detached thing from previous holder");
                    }

                    bool added = innerContainer.TryAdd(thing, allowSpecialEffects);

                    if (added)
                    {
                        if (thing is Thing t)
                        {
                            // This helps with position tracking
                            t.holdingOwner = innerContainer;
                        }

                        // Logger.Debug($"[Locker] SUCCESS: Added {thing.LabelShort} x{thing.stackCount}" + (merged ? " (partial merge)" : ""));
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

                // If we merged completely but had no items left to add
                if (merged)
                {
                    // Logger.Debug($"[Locker] SUCCESS: Merged all items into existing stack(s)");
                    if (allowSpecialEffects)
                    {
                        // MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.25f), Map, "Delivery Received", Color.white, 2f);
                        MoteMaker.ThrowText(this.DrawPos + new Vector3(0f, 0f, 0.25f), Map, "RICS.Locker.DeliveryReceived".Translate(), Color.white, 2f);
                    }
                    return true;
                }

                return false;
            }
            catch (System.Exception ex)
            {
                // Log.Error($"[Locker CRASH CAUGHT in TryAcceptThing] For {thing?.LabelShort ?? "null"} x{thing?.stackCount ?? 0}:\n{ex.Message}\nStackTrace:\n{ex.StackTrace}");
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
                    innerContainer.TryAdd(thing, true);
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

        // Add this method to your Building_RimazonLocker class

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

    /// <summary>
    /// Locker Contents Window
    /// </summary>
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
            if (locker?.InnerContainer != null)
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

            //Widgets.Label(new Rect(0f, 60f, inRect.width, 25f), $"Total items: {locker.InnerContainer.TotalStackCount}");
            Widgets.Label(new Rect(0f, 60f, inRect.width, 25f), 
                "RICS.Storage.TotalItems".Translate(locker.InnerContainer.TotalStackCount));

            Rect viewRect = new Rect(0f, 120f, inRect.width, inRect.height - 120f);
            Rect listRect = new Rect(0f, 0f, viewRect.width - 20f, cachedContents.Count * 35f);

            // Draw column headers - FOUR-COLUMN LAYOUT with Quantity
            if (cachedContents.Count > 0)
            {
                Rect headerRect = new Rect(viewRect.x, viewRect.y, viewRect.width, 25f);
                GUI.color = Color.gray;
                Widgets.DrawLineHorizontal(headerRect.x, headerRect.y + 24f, headerRect.width);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.MiddleLeft;
                // Item column
                // Widgets.Label(new Rect(headerRect.x + 30f, headerRect.y, 180f, 25f), "Item");
                Widgets.Label(new Rect(headerRect.x + 30f, headerRect.y, 180f, 25f), "RICS.Storage.Item".Translate());
                // Quantity column
                // Widgets.Label(new Rect(headerRect.x + 220f, headerRect.y, 70f, 25f), "Qty");
                Widgets.Label(new Rect(headerRect.x + 220f, headerRect.y, 70f, 25f), "RICS.Storage.Quantity".Translate());
                // Individual item value
                // Widgets.Label(new Rect(headerRect.x + 300f, headerRect.y, 90f, 25f), "Each Value");
                // Total value (item value × quantity)
                Widgets.Label(new Rect(headerRect.x + 300f, headerRect.y, 90f, 25f), "RICS.Storage.EachValue".Translate());

                // Widgets.Label(new Rect(headerRect.x + 400f, headerRect.y, 120f, 25f), "Total Value");
                Widgets.Label(new Rect(headerRect.x + 400f, headerRect.y, 120f, 25f), "RICS.Storage.TotalValue".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.BeginScrollView(viewRect, ref scrollPosition, listRect);
            float y = 25f; // Start below header

            for (int i = 0; i < cachedContents.Count; i++)
            {
                Thing thing = cachedContents[i];
                Rect rowRect = new Rect(0f, y, listRect.width, 32f);

                // Highlight alternate rows
                if (i % 2 == 0)
                {
                    Widgets.DrawLightHighlight(rowRect);
                }

                // Icon
                Widgets.ThingIcon(new Rect(0f, y + 2f, 28f, 28f), thing);

                // Name - Manually include stack count since we're not using Building_Storage
                Text.Anchor = TextAnchor.MiddleLeft;
                string itemName = thing.LabelCapNoCount ?? thing.def?.label ?? "RICS.Unknown".Translate();
                Widgets.Label(new Rect(30f, y, 190f, 32f), itemName);

                // Quantity
                string quantityText = thing.stackCount.ToString();
                Widgets.Label(new Rect(220f, y, 80f, 32f), quantityText);

                // Individual item value (per unit)
                string eachValue = thing.MarketValue.ToStringMoney();
                Widgets.Label(new Rect(300f, y, 100f, 32f), eachValue);

                // Total value (item value × quantity)
                float totalValue = thing.MarketValue * thing.stackCount;
                string totalValueText = totalValue.ToStringMoney();
                Widgets.Label(new Rect(400f, y, 130f, 32f), totalValueText);

                // Info button
                if (Widgets.ButtonImage(new Rect(listRect.width - 24f, y + 4f, 24f, 24f), TexButton.Info))
                {
                    if (thing?.def != null)
                    {
                        // Prefer this version — much less likely to crash
                        Find.WindowStack.Add(new Dialog_InfoCard(thing.def, thing.Stuff));
                    }
                    else
                    {
                        // Messages.Message("Cannot show info for this item", MessageTypeDefOf.RejectInput);
                        Messages.Message("RICS.CannotShowInfo".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }

                // Tooltip - shows detailed info including stack count
                string tooltip = thing.GetInspectString();
                if (!string.IsNullOrEmpty(tooltip))
                {
                    TooltipHandler.TipRegion(rowRect, tooltip);
                }

                y += 35f;
            }

            Widgets.EndScrollView();
            Text.Anchor = TextAnchor.UpperLeft;

            // Bottom buttons
            Rect buttonRect = new Rect(0f, inRect.height - 30f, inRect.width, 30f);
            // if (Widgets.ButtonText(new Rect(buttonRect.x, buttonRect.y, 150f, 30f), "Eject All"))
            if (Widgets.ButtonText(new Rect(buttonRect.x, buttonRect.y, 150f, 30f), "RICS.EjectAll".Translate()))
            {
                // With this:
                locker.SafeEjectAllContents();
                CacheContents(); // Refresh
            }
        }
    }

    // === Add this class to your RimazonLocker.cs file ===

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
                if (locker?.settings == null)
                {
                    // Widgets.Label(inRect, "Storage settings not available.");
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

                // Draw filter (below priority)
                Rect filterRect = new Rect(mainRect.x, mainRect.y + 35f, mainRect.width, mainRect.height - 35f);
                DrawFilter(filterRect, locker.settings.filter, locker.def.building?.defaultStorageSettings?.filter);
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

