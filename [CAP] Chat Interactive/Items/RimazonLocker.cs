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

    // Main Class
    // public class Building_RimazonLocker : v, IThingHolder, IHaulDestination, IStoreSettingsParent

    /// <summary>
    /// Main Rimazon Locker building.
    /// Implements IThingHolder + IHaulDestination + IStoreSettingsParent with ultra-robust initialization
    /// to survive save/load, FSF Tweaks, and other storage mods.
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
            // Force early creation so no mod can interfere
            _ = InnerContainer;
            _ = GetStoreSettings();
        }

        // ====================== IThingHolder ======================
        public ThingOwner GetDirectlyHeldThings() => InnerContainer;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public new IThingHolder ParentHolder => null;   // Hides Thing.ParentHolder - required

        public IntVec3 GetPositionForHeldItems() => Position;

        // ====================== IHaulDestination ======================
        public new IntVec3 Position => base.Position;
        public new Map Map => base.Map;
        public bool HaulDestinationEnabled => true;     // Fixed: was missing

        // ====================== IStoreSettingsParent ======================
        public bool StorageTabVisible => Spawned && Map != null;

        public StorageSettings GetStoreSettings()
        {
            if (settings != null) return settings;

            settings = new StorageSettings(this);

            bool copied = false;
            var parentSettings = GetParentStoreSettings();
            if (parentSettings != null)
            {
                try
                {
                    settings.CopyFrom(parentSettings);
                    copied = true;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[RICS Locker] CopyFrom failed: {ex.Message}");
                }
            }

            if (!copied)
            {
                try
                {
                    settings.filter = new ThingFilter();
                    settings.filter.SetAllowAll(null);
                    settings.filter.ResolveReferences();
                }
                catch { }
            }

            // CRITICAL: Force Unstored so pawns haul OUT (delivery box behavior)
            if (settings != null)
                settings.Priority = StoragePriority.Unstored;

            return settings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def?.building?.defaultStorageSettings;
        }

        public void Notify_SettingsChanged() { }

        // ====================== Core Overrides ======================
        public override void PostMake()
        {
            base.PostMake();
            _ = GetStoreSettings();
            if (settings != null)
                settings.Priority = StoragePriority.Unstored;
        }

        public override void SpawnSetup(Map map, bool respawningAfterReload)
        {
            base.SpawnSetup(map, respawningAfterReload);
            _ = InnerContainer;
            _ = GetStoreSettings();
            if (settings != null)
                settings.Priority = StoragePriority.Unstored;

            Logger.Debug($"[RICS] Locker spawned at {Position} — stacks: {InnerContainer.Count}/{MaxStacks}");
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref customName, "customName");

            // Pass "this" so RimWorld correctly wires the owner on load
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref settings, "settings", this);

            // Post-load safety
            if (innerContainer == null)
            {
                innerContainer = new LockerThingOwner(this, false, LookMode.Deep);
                Logger.Warning($"Locker {instanceId}: innerContainer was null after load — recreated");
            }

            if (settings != null)
                settings.Priority = StoragePriority.Unstored;

            // Fix holdingOwner on loaded items
            if (innerContainer != null)
            {
                foreach (Thing t in innerContainer)
                {
                    if (t != null && t.holdingOwner == null)
                        t.holdingOwner = innerContainer;
                }
            }

            Logger.Debug($"Locker {instanceId} loaded — stacks: {innerContainer?.Count ?? 0}/{MaxStacks}");
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
        }

        // ====================== Accepts / TryAcceptThing ======================
        public virtual bool Accepts(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing is Pawn) return false;
            if (settings == null || !settings.AllowedToAccept(thing)) return false;

            try
            {
                int existingStacksOfSameType = 0;
                int totalSpaceForThisType = 0;
                var snapshot = InnerContainer.ToList();

                foreach (var existing in snapshot)
                {
                    if (existing == null || existing.Destroyed) continue;
                    if (existing.def == thing.def && existing.CanStackWith(thing))
                    {
                        existingStacksOfSameType++;
                        int spaceLeft = existing.def.stackLimit - existing.stackCount;
                        totalSpaceForThisType += Mathf.Max(0, spaceLeft);
                    }
                }

                if (existingStacksOfSameType > 0)
                {
                    if (totalSpaceForThisType >= thing.stackCount) return true;

                    int itemsRemaining = thing.stackCount - totalSpaceForThisType;
                    int stacksNeeded = Mathf.CeilToInt((float)itemsRemaining / thing.def.stackLimit);
                    int emptySlots = MaxStacks - InnerContainer.Count;
                    return emptySlots >= stacksNeeded;
                }

                int stacksRequired = Mathf.CeilToInt((float)thing.stackCount / thing.def.stackLimit);
                return (MaxStacks - InnerContainer.Count) >= stacksRequired;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[RimazonLocker] Accepts crash prevented: {ex.Message}", "LockerAccepts".GetHashCode());
                return false;
            }
        }

        public virtual bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (!Spawned || Map == null || thing == null || thing.Destroyed || !Accepts(thing))
                return false;

            try
            {
                bool merged = false;
                var snapshot = InnerContainer.ToList();
                foreach (var existing in snapshot)
                {
                    if (existing == null || existing.Destroyed) continue;
                    if (existing.def == thing.def && existing.CanStackWith(thing))
                    {
                        int spaceLeft = existing.def.stackLimit - existing.stackCount;
                        if (spaceLeft > 0)
                        {
                            int toMerge = Mathf.Min(spaceLeft, thing.stackCount);
                            existing.stackCount += toMerge;
                            thing.stackCount -= toMerge;
                            if (thing.stackCount <= 0)
                            {
                                thing.Destroy();
                                if (allowSpecialEffects)
                                    MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.25f), Map, "RICS.Locker.DeliveryReceived".Translate(), Color.white, 2f);
                                return true;
                            }
                            merged = true;
                        }
                    }
                }

                if (thing.stackCount > 0)
                {
                    if (thing.ParentHolder != null && thing.ParentHolder != this)
                        thing.ParentHolder.GetDirectlyHeldThings()?.Remove(thing);

                    bool added = InnerContainer.TryAdd(thing, allowSpecialEffects);
                    if (added)
                    {
                        if (allowSpecialEffects)
                            MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.25f), Map, "RICS.Locker.DeliveryReceived".Translate(), Color.white, 2f);
                        return true;
                    }
                }

                if (merged && allowSpecialEffects)
                    MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.25f), Map, "RICS.Locker.DeliveryReceived".Translate(), Color.white, 2f);

                return merged;
            }
            catch (Exception ex)
            {
                Log.Error($"[Locker] TryAcceptThing crash caught: {ex.Message}");
                return false;
            }
        }

        // ====================== Drop Pod Support ======================
        public void AcceptDropPod(DropPodIncoming dropPod, Thing[] contents)
        {
            foreach (Thing t in contents)
            {
                if (Accepts(t))
                    InnerContainer.TryAdd(t, true);
                else
                    GenPlace.TryPlaceThing(t, Position, Map, ThingPlaceMode.Near);
            }
            MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.25f), Map, "RICS.Locker.DeliveryReceived".Translate(), Color.white, 2f);
        }

        // ====================== Gizmos ======================
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

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
                action = () => OpenLocker()
            };

            yield return new Command_Action
            {
                defaultLabel = "Storage settings",
                defaultDesc = "Configure what can be stored in this locker.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Settings", true),
                action = () => OpenStorageSettings()
            };

            if (InnerContainer.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Eject all contents",
                    defaultDesc = "Drop all items from the locker to the ground nearby.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Eject", true),
                    action = () => SafeEjectAllContents()
                };
            }
        }

        // ====================== Eject Methods (moved here so they are accessible) ======================
        public void SafeEjectAllContents()
        {
            if (InnerContainer.Count == 0 || Map == null) return;

            try
            {
                IntVec3 dropCell = FindValidDropCell(Position, Map);

                if (dropCell.IsValid)
                {
                    bool success = InnerContainer.TryDropAll(dropCell, Map, ThingPlaceMode.Near);
                    if (!success)
                        SafeDropItemsIndividually();
                }
                else
                {
                    SafeDropItemsIndividually();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"SafeEjectAllContents ERROR: {ex.Message}");
                EmergencyEject();
            }

            if (Spawned && Map != null)
            {
                try
                {
                    MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.25f), Map, "RICS.Locker.ItemsEjected".Translate(), Color.white, 2f);
                }
                catch { }
            }
        }

        private IntVec3 FindValidDropCell(IntVec3 center, Map map, int radius = 3)
        {
            for (int r = 1; r <= radius; r++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, r, true))
                {
                    if (!cell.InBounds(map) || cell.Fogged(map) || !cell.Walkable(map))
                        continue;

                    Building building = cell.GetEdifice(map);
                    if (building != null && building.def.passability == Traversability.Impassable)
                        continue;

                    return cell;
                }
            }
            return center;
        }

        private void SafeDropItemsIndividually()
        {
            if (InnerContainer.Count == 0 || Map == null) return;

            List<Thing> itemsToDrop = new List<Thing>(InnerContainer);

            foreach (Thing thing in itemsToDrop)
            {
                if (thing == null || thing.Destroyed || !InnerContainer.Contains(thing)) continue;

                try
                {
                    IntVec3 dropCell = FindValidDropCell(Position, Map, 5);
                    if (dropCell.IsValid)
                    {
                        InnerContainer.TryDrop(thing, dropCell, Map, ThingPlaceMode.Direct, out _);
                    }
                    else
                    {
                        InnerContainer.TryDrop(thing, Position, Map, ThingPlaceMode.Near, out _);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error dropping {thing?.LabelCap ?? "unknown"}: {ex.Message}");
                }
            }
        }

        private void EmergencyEject()
        {
            Logger.Error("EmergencyEject: Destroying items to prevent crash");
            while (InnerContainer.Count > 0)
            {
                try
                {
                    Thing thing = InnerContainer[0];
                    if (thing != null)
                        thing.Destroy();
                    InnerContainer.Remove(thing);
                }
                catch
                {
                    InnerContainer.Clear();
                    break;
                }
            }
        }

        // ====================== UI Methods ======================
        public bool CanOpen => true;

        public void OpenLocker()
        {
            Find.WindowStack.Add(new Dialog_LockerContents(this));
        }

        public void OpenStorageSettings()
        {
            Find.WindowStack.Add(new Dialog_StorageSettings(this));
        }

        // ====================== Inspect / Label ======================
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (!customName.NullOrEmpty())
                text = text + "\n" + "RICS_Named".Translate(customName);

            text += "\n";
            if (InnerContainer.Count == 0)
                text += "RICS_LockerEmpty".Translate();
            else
            {
                text += "RICS_Contains".Translate() + InnerContainer.ContentsString.CapitalizeFirst();
                text += "\n" + "RICS_StackSlots".Translate(InnerContainer.Count, MaxStacks);
                text += "\n" + "RICS_TotalItems".Translate(InnerContainer.TotalStackCount);
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

        public override void PostMapInit()
        {
            base.PostMapInit();
            if (innerContainer != null)
            {
                foreach (Thing t in innerContainer)
                {
                    if (t != null)
                        t.holdingOwner = innerContainer;
                }
            }
        }

        // Rename helper (used by dialog)
        public void RenameLocker(string newName)
        {
            customName = newName.NullOrEmpty() ? null : newName.Trim();
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
