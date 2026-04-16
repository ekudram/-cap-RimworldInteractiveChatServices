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

            if (thing.holdingOwner != this)
                thing.holdingOwner = this;

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

        private ThingOwner<Thing> innerContainer;
        public ThingOwner InnerContainer
        {
            get
            {
                if (innerContainer == null)
                {
                    innerContainer = new LockerThingOwner(this, false, LookMode.Deep);
                    Logger.Debug($"Locker {instanceId}: Lazy-created LockerThingOwner");
                }
                return innerContainer;
            }
        }

        public int MaxStacks => def.GetModExtension<LockerExtension>()?.maxStacks ?? 24;

        public StorageSettings settings;

        public int instanceId = 0;
        private static int instanceCounter = 0;

        public Building_RimazonLocker()
        {
            instanceId = ++instanceCounter;
            _ = InnerContainer;           // force early creation
            _ = GetStoreSettings();
        }

        // IThingHolder
        public ThingOwner GetDirectlyHeldThings() => InnerContainer;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            outChildren.Clear();                                      // CRITICAL for region stability
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public new IThingHolder ParentHolder => null;
        public IntVec3 GetPositionForHeldItems() => Position;

        // IHaulDestination
        public new IntVec3 Position => base.Position;
        public new Map Map => base.Map;
        public bool HaulDestinationEnabled => true;

        // IStoreSettingsParent
        public bool StorageTabVisible => Spawned && Map != null;

        public StorageSettings GetStoreSettings()
        {
            if (settings != null) return settings;
            settings = new StorageSettings(this);
            var parent = GetParentStoreSettings();
            if (parent != null) settings.CopyFrom(parent);
            else
            {
                settings.filter = new ThingFilter();
                settings.filter.SetAllowAll(null);
            }
            settings.Priority = StoragePriority.Unstored;   // delivery box behavior
            return settings;
        }

        public StorageSettings GetParentStoreSettings() => def?.building?.defaultStorageSettings;
        public void Notify_SettingsChanged() { }

        public override void PostMake()
        {
            base.PostMake();
            _ = GetStoreSettings();
        }

        public override void SpawnSetup(Map map, bool respawningAfterReload)
        {
            base.SpawnSetup(map, respawningAfterReload);

            // Synchronous initialization - no LongEventHandler (removes the timing race that causes CTD)
            _ = InnerContainer;
            _ = GetStoreSettings();
            if (settings != null)
                settings.Priority = StoragePriority.Unstored;

            Logger.Debug($"[RICS] Locker fully initialized at {Position} — stacks: {InnerContainer.Count}/{MaxStacks}");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref customName, "customName");
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref settings, "settings", this);

            if (innerContainer == null)
                innerContainer = new LockerThingOwner(this, false, LookMode.Deep);

            if (settings != null) settings.Priority = StoragePriority.Unstored;

            if (innerContainer != null)
            {
                foreach (Thing t in innerContainer)
                    if (t != null && t.holdingOwner == null)
                        t.holdingOwner = innerContainer;
            }
        }

        // ====================== Get Delivery with limits ======================
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

                    if (thing.stackCount > 0)
                    {
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

        // ====================== Rename & Inspect ======================
        public void RenameLocker(string newName)
        {
            customName = newName.NullOrEmpty() ? null : newName.Trim();
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();

            if (!customName.NullOrEmpty())
            {
                if (!text.NullOrEmpty()) text += "\n";
                text += "RICS_Named".Translate(customName);
            }

            if (!text.NullOrEmpty()) text += "\n";

            if (innerContainer.Count == 0)
                text += "RICS_LockerEmpty".Translate();
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
                    return customName + " (" + def.label + ")";
                return base.Label;
            }
        }

        // ====================== Gizmos ======================
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;

            yield return new Command_Action
            {
                defaultLabel = "Rename locker",
                defaultDesc = "Give this Rimazon locker a unique name for chat deliveries.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Rename", true),
                action = () => Find.WindowStack.Add(new Dialog_RenameLocker(this))
            };

            yield return new Command_Action
            {
                defaultLabel = "Open locker",
                defaultDesc = "View and access items in the locker.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_OpenLocker", true),
                action = OpenLocker
            };

            yield return new Command_Action
            {
                defaultLabel = "Storage settings",
                defaultDesc = "Configure what can be stored in this locker.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Settings", true),
                action = OpenStorageSettings
            };

            if (innerContainer.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Eject all contents",
                    defaultDesc = "Drop all items from the locker to the ground nearby.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Eject", true),
                    action = SafeEjectAllContents
                };
            }
        }

        // ====================== Eject ======================
        public void SafeEjectAllContents()
        {
            if (innerContainer.Count == 0 || Map == null) return;
            try
            {
                IntVec3 dropCell = FindValidDropCell(Position, Map);
                bool success = innerContainer.TryDropAll(dropCell, Map, ThingPlaceMode.Near);
                if (!success) SafeDropItemsIndividually();
            }
            catch (Exception ex)
            {
                Logger.Error($"SafeEjectAllContents ERROR: {ex.Message}");
            }
        }

        private IntVec3 FindValidDropCell(IntVec3 center, Map map, int radius = 3)
        {
            for (int r = 1; r <= radius; r++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, r, true))
                {
                    if (!cell.InBounds(map) || cell.Fogged(map) || !cell.Walkable(map)) continue;
                    Building building = cell.GetEdifice(map);
                    if (building != null && building.def.passability == Traversability.Impassable) continue;
                    return cell;
                }
            }
            return center;
        }

        private void SafeDropItemsIndividually()
        {
            if (innerContainer.Count == 0 || Map == null) return;
            List<Thing> itemsToDrop = new List<Thing>(innerContainer);
            foreach (Thing thing in itemsToDrop)
            {
                if (thing == null || thing.Destroyed) continue;
                try
                {
                    IntVec3 dropCell = FindValidDropCell(Position, Map, 5);
                    innerContainer.TryDrop(thing, dropCell.IsValid ? dropCell : Position, Map, ThingPlaceMode.Near, out _);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error dropping item: {ex.Message}");
                }
            }
        }

        // ====================== UI ======================
        public void OpenLocker() => Find.WindowStack.Add(new Dialog_LockerContents(this));

        // === Open Storage Settings Method ===
        public void OpenStorageSettings()
        {
            // Create a simple window for storage settings
            Find.WindowStack.Add(new Dialog_StorageSettings(this));
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
