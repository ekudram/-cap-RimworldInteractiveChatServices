// File: BuyPawnCommandHandler.cs 
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
// 
// Command handler for buying items from Rimazon store
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class BuyItemCommandHandler
    {
        private const string ReturnDivider = " | ";

        public static string HandleBuyItem(ChatMessageWrapper messageWrapper, string[] args, bool requireEquippable = false, bool requireWearable = false, bool addToInventory = false)
        {
            try
            {
                var parsed = CommandParserUtility.ParseCommandArguments(args, allowQuality: true, allowMaterial: true, allowSide: false, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                string itemName = parsed.ItemName;
                string qualityStr = parsed.Quality;
                string materialStr = parsed.Material;
                int quantity = Math.Max(1, parsed.Quantity);

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(messageWrapper);
                bool needsPawn = requireEquippable || requireWearable || addToInventory;

                var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
                if (storeItem == null)
                    return "RICS.BICH.Return.ItemNotFound".Translate(itemName);

                if (!storeItem.Enabled && !requireEquippable && !requireWearable)
                    return "RICS.BICH.Return.ItemDisabled".Translate(itemName);

                if (requireEquippable && !storeItem.IsEquippable)
                    return "RICS.BICH.Return.ItemNotEquippable".Translate(itemName);

                if (requireWearable && !storeItem.IsWearable)
                    return "RICS.BICH.Return.ItemNotWearable".Translate(itemName);

                if (!StoreCommandHelper.IsItemTypeValid(storeItem, requireEquippable, requireWearable, false))
                {
                    string itemType = StoreCommandHelper.GetItemTypeDescription(storeItem);
                    string expectedType = requireEquippable ? "equippable" : requireWearable ? "wearable" : "purchasable";
                    return "RICS.BICH.Return.WrongItemType".Translate(itemName, itemType, expectedType);
                }

                var researchResult = StoreCommandHelper.HasRequiredResearch(storeItem);
                if (!researchResult.Allowed)
                {
                    string msg = "RICS.BICH.Return.ResearchRequired".Translate(itemName);
                    if (!string.IsNullOrEmpty(researchResult.BlockingResearchLabel))
                        msg += ReturnDivider + researchResult.BlockingResearchLabel;
                    return msg;
                }

                var quality = ItemConfigHelper.ParseQuality(qualityStr);
                if (!ItemConfigHelper.IsQualityAllowed(quality))
                    return "RICS.BICH.Return.QualityNotAllowed".Translate(qualityStr);

                // Content items (TextBook/Novel/Schematic/Tome) seed internal data from quality.
                // Force Normal only when buyer omits quality — blank spawn is worse UX than settings deny.
                if (!quality.HasValue &&
                    (storeItem.DefName.Equals("TextBook", StringComparison.OrdinalIgnoreCase) ||
                     storeItem.DefName.Equals("Novel", StringComparison.OrdinalIgnoreCase) ||
                     storeItem.DefName.Equals("Schematic", StringComparison.OrdinalIgnoreCase) ||
                     storeItem.DefName.Equals("Tome", StringComparison.OrdinalIgnoreCase)))
                {
                    quality = QualityCategory.Normal;
                    Logger.Debug($"[BuyItem] Forced Normal quality for {storeItem.DefName} (no quality arg) to avoid blank content");
                }

                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    Logger.Error($"[BuyItem] ThingDef not found: {storeItem.DefName}");
                    return "RICS.BICH.Return.DefNotFound".Translate();
                }

                if (StoreCommandHelper.IsRaceBanned(thingDef))
                    return "RICS.BICH.Return.RaceBanned".Translate(itemName);

                ThingDef material = null;
                if (thingDef.MadeFromStuff)
                {
                    material = ItemConfigHelper.ParseMaterial(materialStr, thingDef);
                    if (materialStr != "random" && material == null)
                        return "RICS.BICH.Return.InvalidMaterial".Translate(materialStr, itemName);
                }

                if (storeItem.HasQuantityLimit && quantity > storeItem.QuantityLimit)
                    quantity = storeItem.QuantityLimit;

                // === PRICE + ITEM CREATION ===
                Thing finalItem = null;
                int finalPrice;
                bool isUniqueWeapon = StoreItem.IsUniqueWeapon(thingDef);

                if (isUniqueWeapon)
                {
                    // Create once so traits randomize for accurate pricing + same instance can deliver
                    finalItem = CreateTemporaryValuationItem(thingDef, quality, material);
                    if (finalItem != null)
                    {
                        finalPrice = (int)(finalItem.MarketValue * quantity);
                        Logger.Debug($"[BuyItem] Unique weapon '{itemName}' market value total {finalPrice}");
                    }
                    else
                    {
                        finalPrice = ItemConfigHelper.CalculateFinalPrice(storeItem, quantity, quality, material);
                    }
                }
                else
                {
                    finalPrice = ItemConfigHelper.CalculateFinalPrice(storeItem, quantity, quality, material);
                }

                if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
                {
                    if (finalItem != null) finalItem.Destroy();
                    return "RICS.BICH.Return.CannotAfford".Translate(
                        StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                        quantity,
                        itemName,
                        StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
                }

                Verse.Pawn viewerPawn = null;
                if (needsPawn)
                {
                    viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                    if (viewerPawn == null)
                        return "RICS.Pawn.NoPawn".Translate();

                    if (viewerPawn.Dead)
                    {
                        var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                        return "RICS.Pawn.Dead".Translate()
                               + ReturnDivider
                               + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
                    }

                    // Body part check for apparel — before coins/spawn
                    if (requireWearable && !ApparelUtility.HasPartsToWear(viewerPawn, thingDef))
                        return "RICS.BICH.Return.MissingBodyPartForWear".Translate(itemName);

                    // HAR race restriction — before coins/spawn
                    if (requireEquippable || requireWearable)
                    {
                        var provider = CAPChatInteractiveMod.Instance?.AlienProvider;
                        if (provider != null)
                        {
                            bool canUseItem = requireWearable
                                ? provider.CanWear(thingDef, viewerPawn.def)
                                : provider.CanEquip(thingDef, viewerPawn.def);

                            if (!canUseItem)
                            {
                                return "RICS.BICH.Return.HARRaceRestricted".Translate(
                                    itemName, requireWearable ? "worn" : "equipped");
                            }
                        }
                    }
                }
                else
                {
                    // Optional pawn for delivery positioning; null → colony-wide delivery
                    viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                }

                // Deliver FIRST, then charge only for what landed (no overcharge when no space).
                // equip/wear/backpack → on-pawn first; loose !buy → locker first; animals/mechs → drop pod.
                DeliveryResult deliveryResult;
                if (needsPawn)
                {
                    deliveryResult = ItemDeliveryHelper.SpawnItemForPawn(thingDef, quantity, quality, material,
                        viewerPawn, addToInventory, requireEquippable, requireWearable, preCreatedItem: finalItem);
                }
                else
                {
                    deliveryResult = ItemDeliveryHelper.SpawnItemForPawn(thingDef, quantity, quality, material,
                        viewerPawn, addToInventory: false, equipItem: false, wearItem: false, preCreatedItem: finalItem);
                }

                var allSpawnedItems = new List<Thing>();
                allSpawnedItems.AddRange(deliveryResult.LockerDeliveredItems);
                allSpawnedItems.AddRange(deliveryResult.DropPodDeliveredItems);
                allSpawnedItems.AddRange(deliveryResult.DirectlyDeliveredItems);

                int deliveredUnits = deliveryResult.TotalUnitsDelivered;
                if (deliveredUnits <= 0 && allSpawnedItems.Count > 0)
                    deliveredUnits = allSpawnedItems.Sum(t => t?.stackCount > 0 ? t.stackCount : 1);

                // Safety net only when things actually exist in the spawn lists
                if (deliveredUnits <= 0 &&
                    allSpawnedItems.Count > 0 &&
                    deliveryResult.PrimaryMethod == DeliveryMethod.PawnDelivery &&
                    deliveryResult.DeliveryPosition.IsValid)
                {
                    deliveredUnits = Math.Max(1, allSpawnedItems.Sum(t => t?.stackCount > 0 ? t.stackCount : 1));
                    Logger.Warning(
                        $"[BuyItem] PawnDelivery counts were 0 but {allSpawnedItems.Count} thing(s) spawned — " +
                        $"using stack total {deliveredUnits} for {itemName}");
                }

                int undeliveredUnits = deliveryResult.UndeliveredCount;
                if (undeliveredUnits <= 0 && deliveredUnits < quantity)
                    undeliveredUnits = quantity - deliveredUnits;

                Map deliveryLookMap = ItemDeliveryHelper.ResolveDeliveryMap(
                    viewerPawn, allowUndergroundRedirect: false)
                    ?? viewerPawn?.Map
                    ?? Find.CurrentMap
                    ?? Find.Maps?.FirstOrDefault(m => m != null);

                if (deliveredUnits <= 0)
                {
                    Logger.Warning(
                        $"[BuyItem] No space — cancelled {itemName} x{quantity} for {messageWrapper.Username} (no charge).");
                    ItemDeliveryHelper.LogMapSnapshot("[BuyItem no-space maps]");
                    return "RICS.BICH.Return.NoSpace".Translate(itemName, quantity);
                }

                int chargeQty = deliveredUnits;
                int chargePrice;
                if (chargeQty >= quantity)
                    chargePrice = finalPrice;
                else if (isUniqueWeapon && finalItem != null)
                    chargePrice = Math.Max(1, (int)(finalItem.MarketValue * chargeQty));
                else
                {
                    chargePrice = ItemConfigHelper.CalculateFinalPrice(storeItem, chargeQty, quality, material);
                    if (chargePrice <= 0 && finalPrice > 0 && quantity > 0)
                        chargePrice = Math.Max(1, (int)((long)finalPrice * chargeQty / quantity));
                }

                viewer.TakeCoins(chargePrice);
                finalPrice = chargePrice;
                quantity = chargeQty;

                float karmaEarned = chargePrice * (settings.KarmaPerStoreItem / 100f);
                if (karmaEarned > 0f)
                    viewer.GiveKarma(karmaEarned);

                if (needsPawn && viewerPawn != null)
                {
                    foreach (Thing spawnedItem in allSpawnedItems)
                        TrySetItemOwnership(spawnedItem, viewerPawn);

                    // Wear/equip safety: ensure anything now on the pawn for this delivery is owned
                    // (covers cases where the delivered list missed a worn instance).
                    TrySetOwnershipOnPawnGear(viewerPawn, allSpawnedItems);
                }

                LookTargets lookTargets = null;
                if (thingDef.thingClass == typeof(Verse.Pawn))
                {
                    if (deliveryResult.DeliveryPosition.IsValid && deliveryLookMap != null)
                        lookTargets = new LookTargets(deliveryResult.DeliveryPosition, deliveryLookMap);
                }
                else if (needsPawn)
                {
                    lookTargets = viewerPawn != null ? new LookTargets(viewerPawn) : null;
                }
                else if (deliveryResult.DeliveryPosition.IsValid && deliveryLookMap != null)
                {
                    lookTargets = new LookTargets(deliveryResult.DeliveryPosition, deliveryLookMap);
                }

                string itemLabel = thingDef?.LabelCap ?? itemName;
                string invoiceLabel;
                string invoiceMessage;
                string tClass = thingDef.thingClass.ToString();

                if (thingDef.thingClass == typeof(Verse.Pawn) || tClass == "Verse.Pawn")
                {
                    invoiceLabel = "RICS.BICH.Letter.Label.Pet".Translate(messageWrapper.Username);
                    invoiceMessage = CreateRimazonPetInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol);
                }
                else if (needsPawn)
                {
                    string serviceType = requireEquippable ? "Equip" : requireWearable ? "Wear" : "Backpack";
                    string emoji = requireEquippable ? "RICS.BICH.Letter.Emoji.Equip".Translate() :
                                   requireWearable ? "RICS.BICH.Letter.Emoji.Wear".Translate() :
                                   "RICS.BICH.Letter.Emoji.Backpack".Translate();

                    invoiceLabel = "RICS.BICH.Letter.Label.Direct".Translate(emoji, serviceType, messageWrapper.Username);
                    invoiceMessage = CreateRimazonDirectInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol, serviceType);
                }
                else
                {
                    invoiceLabel = "RICS.BICH.Letter.Label.Standard".Translate(messageWrapper.Username);
                    if (deliveryResult.LockerDeliveredItems.Count > 0 && deliveryResult.DropPodDeliveredItems.Count > 0)
                    {
                        invoiceMessage = CreateSplitInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice,
                            currencySymbol, quality, material, deliveryResult);
                    }
                    else
                    {
                        invoiceMessage = CreateRimazonInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice,
                            currencySymbol, quality, material, deliveryResult);
                    }
                }

                if (UseItemCommandHandler.IsMajorPurchase(finalPrice, quality))
                    MessageHandler.SendGoldLetter(invoiceLabel, invoiceMessage, lookTargets);
                else
                    MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage, lookTargets);

                string action = requireEquippable ? "RICS.BICH.Return.Action.Equipped".Translate() :
                                requireWearable ? "RICS.BICH.Return.Action.Worn".Translate() :
                                addToInventory ? "RICS.BICH.Return.Action.AddedToInventory".Translate() :
                                "RICS.BICH.Return.Action.Delivered".Translate();

                string success = "RICS.BICH.Return.Success".Translate(
                    quantity,
                    itemName,
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    action,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));

                if (undeliveredUnits > 0)
                    success += ReturnDivider + "RICS.BICH.Return.PartialNoSpace".Translate(undeliveredUnits, itemName);

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"[BuyItem] Error in HandleBuyItem: {ex}");
                return "RICS.BICH.Return.GenericError".Translate();
            }
        }

        // Add near the bottom of BuyItemCommandHandler.cs
        private static Thing CreateTemporaryValuationItem(ThingDef thingDef, QualityCategory? quality, ThingDef material)
        {
            try
            {
                Thing thing = ThingMaker.MakeThing(thingDef, material);

                if (quality.HasValue && thing.TryGetComp<CompQuality>() is CompQuality cq)
                {
                    cq.SetQuality(quality.Value, ArtGenerationContext.Outsider);
                }

                // Let CompUniqueWeapon run its full setup (this is what we want for accurate pricing)
                thing.PostPostMake();

                return thing;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create valuation item for {thingDef.defName}: {ex.Message}");
                return null;
            }
        }

        private static string CreateRimazonInvoice(string username, string itemName, int quantity, int price,
            string currencySymbol, QualityCategory? quality, ThingDef material, DeliveryResult deliveryResult)
        {
            int lockerCount = deliveryResult.LockerDeliveredCount;
            int dropPodCount = deliveryResult.DropPodDeliveredCount;

            // ───────────────────────────────────────────────
            // Delivery description WITHOUT the "Delivery: " prefix
            // (prevents duplication with the template's "Delivery: {3}")
            // ───────────────────────────────────────────────
            string deliveryDesc;
            if (lockerCount > 0 && dropPodCount > 0)
            {
                // fallback path (split invoice is normally used, but kept for safety)
                deliveryDesc = $"Mixed Delivery\n• Locker Delivery: {lockerCount}\n• Drop Pod Delivery: {dropPodCount}";
            }
            else if (lockerCount > 0)
            {
                deliveryDesc = $"Locker Delivery (x{lockerCount})";
            }
            else
            {
                deliveryDesc = "Standard Drop Pod";
            }

            // Quality & material lines (optional)
            string extraSpecs = "";
            if (quality.HasValue)
            {
                extraSpecs += "\n" + "RICS.BICH.Letter.Quality".Translate(quality.Value.ToString());
            }
            if (material != null)
            {
                extraSpecs += "\n" + "RICS.BICH.Letter.Material".Translate(material.LabelCap);
            }

            string deliverySection = deliveryDesc + extraSpecs;

            // Pricing breakdown only for mixed (rarely reached here)
            string pricingNote = "";
            if (lockerCount > 0 && dropPodCount > 0)
            {
                float pricePer = (float)price / quantity;
                int dropPodPrice = (int)(pricePer * dropPodCount);

                pricingNote = "\n" +
                    "RICS.BICH.Letter.Pricing.Locker".Translate(lockerCount, currencySymbol) +
                    "RICS.BICH.Letter.Pricing.DropPod".Translate(dropPodCount, dropPodPrice.ToString("N0"), currencySymbol) +
                    "\n" +
                    "RICS.BICH.Letter.Pricing.Total".Translate(price.ToString("N0"), currencySymbol);
            }

            // ───────────────────────────────────────────────
            // Exact 6 arguments matching the .xml template:
            // {0}=customer, {1}=itemName, {2}=quantity, {3}=deliverySection, {4}=price, {5}=currency
            // ───────────────────────────────────────────────
            string body = "RICS.BICH.Letter.Body.Standard".Translate(
                username,                    // {0}
                itemName,                    // {1}
                quantity.ToString(),         // {2}  → produces correct "Item: Cowboy hat 2"
                deliverySection,             // {3}  → "Standard Drop Pod\nQuality: Masterwork\n..."
                price.ToString("N0"),        // {4}
                currencySymbol               // {5}
            );

            if (!string.IsNullOrEmpty(pricingNote))
                body += pricingNote;

            // Contextual notes
            if (lockerCount > 0 && dropPodCount == 0)
                body += "\n" + "RICS.BICH.Letter.Locker.Note".Translate();
            else if (lockerCount > 0 && dropPodCount > 0)
                body += "\n" + "RICS.BICH.Letter.Mixed.Note".Translate();

            return body;
        }

        private static string CreateSplitInvoice(string username, string itemName, int quantity, int price,
            string currencySymbol, QualityCategory? quality, ThingDef material, DeliveryResult deliveryResult)
        {
            int lockerCount = deliveryResult.LockerDeliveredCount;
            int dropPodCount = deliveryResult.DropPodDeliveredCount;

            StringBuilder invoice = new StringBuilder();

            // Locker delivery section
            if (lockerCount > 0)
            {
                invoice.AppendLine("RICS.BICH.Letter.Split.LockerHeader".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.CustomerLine".Translate(username));
                invoice.AppendLine("RICS.BICH.Letter.Split.ItemLine".Translate(itemName, lockerCount.ToString()));
                invoice.AppendLine("RICS.BICH.Letter.Split.Separator".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.DeliveryLocker".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.StatusDelivered".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.TotalFree".Translate(currencySymbol));
                invoice.AppendLine("RICS.BICH.Letter.Split.Separator".Translate());
                invoice.AppendLine();
            }

            // Drop pod delivery section
            if (dropPodCount > 0)
            {
                invoice.AppendLine("RICS.BICH.Letter.Split.DropPodHeader".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.CustomerLine".Translate(username));
                invoice.AppendLine("RICS.BICH.Letter.Split.ItemLine".Translate(itemName, dropPodCount.ToString()));
                invoice.AppendLine("RICS.BICH.Letter.Split.Separator".Translate());

                // Add quality if specified
                if (quality.HasValue)
                {
                    invoice.AppendLine("RICS.BICH.Letter.Split.QualityLine".Translate(quality.Value.ToString()));
                }

                // Add material if specified
                if (material != null)
                {
                    invoice.AppendLine("RICS.BICH.Letter.Split.MaterialLine".Translate(material.LabelCap));
                }

                invoice.AppendLine("RICS.BICH.Letter.Split.DeliveryDropPod".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.StatusDelivered".Translate());

                // Calculate price only for drop pod items
                float pricePerItem = (float)price / quantity;
                int dropPodPrice = (int)(pricePerItem * dropPodCount);

                invoice.AppendLine("RICS.BICH.Letter.Split.TotalLine".Translate(dropPodPrice.ToString("N0"), currencySymbol));
                invoice.AppendLine("RICS.BICH.Letter.Split.Separator".Translate());
            }

            invoice.AppendLine("RICS.BICH.Letter.Split.Footer".Translate());

            return invoice.ToString();
        }

        private static string CreateRimazonDirectInvoice(string username, string itemName, int quantity, int price, string currencySymbol, string serviceType)
        {
            string invoice = "RICS.BICH.Letter.Direct.Body".Translate(
                serviceType.ToUpper(),
                username,
                itemName,
                quantity.ToString(),
                price.ToString("N0"),
                currencySymbol,
                GetDirectServiceMessage(serviceType)
            );

            return invoice;
        }

        private static string GetDirectServiceMessage(string serviceType)
        {
            return serviceType switch
            {
                "Equip" => "RICS.BICH.Letter.Direct.EquipMessage".Translate(),
                "Wear" => "RICS.BICH.Letter.Direct.WearMessage".Translate(),
                "Backpack" => "RICS.BICH.Letter.Direct.BackpackMessage".Translate(),
                _ => ""
            };
        }
        private static string CreateRimazonPetInvoice(string username, string itemName, int quantity, int price, string currencySymbol)
        {
            string petMessage = quantity == 1
                ? "RICS.BICH.Letter.Pet.Singular".Translate()
                : "RICS.BICH.Letter.Pet.Plural".Translate();

            string invoice = "RICS.BICH.Letter.Body.Pet".Translate(
                username,
                itemName,
                quantity.ToString(),
                price.ToString("N0"),
                currencySymbol,
                petMessage
            );

            return invoice;
        }
        /// <summary>
        /// Assign ownership after Equip/Wear/Backpack.
        /// Possessions Plus active → reflection into PP comps.
        /// Else if RICS ownership enabled → Comp_RICS_OwnedByPawn.
        /// </summary>
        private static void TrySetItemOwnership(Thing item, Verse.Pawn ownerPawn)
        {
            try
            {
                if (item == null || ownerPawn == null || item.Destroyed)
                    return;

                Ownership.RICS_OwnershipUtility.SetOwner(
                    item,
                    ownerPawn,
                    "RICS.BICH.Ownership.HistoryEntry".Translate());
            }
            catch (Exception ex)
            {
                Logger.Error($"[BuyItem] Error setting item ownership: {ex}");
            }
        }

        /// <summary>
        /// After wear/equip, re-apply ownership to delivered things found on the pawn
        /// (apparel tracker / equipment / inventory).
        /// </summary>
        private static void TrySetOwnershipOnPawnGear(Verse.Pawn pawn, List<Thing> delivered)
        {
            if (pawn == null || delivered == null || delivered.Count == 0)
                return;

            try
            {
                var ids = new HashSet<int>();
                foreach (var t in delivered)
                {
                    if (t != null && !t.Destroyed)
                        ids.Add(t.thingIDNumber);
                }

                void Consider(Thing t)
                {
                    if (t == null || t.Destroyed)
                        return;
                    if (!ids.Contains(t.thingIDNumber))
                        return;
                    TrySetItemOwnership(t, pawn);
                }

                if (pawn.apparel?.WornApparel != null)
                {
                    foreach (var a in pawn.apparel.WornApparel)
                        Consider(a);
                }
                if (pawn.equipment?.AllEquipmentListForReading != null)
                {
                    foreach (var e in pawn.equipment.AllEquipmentListForReading)
                        Consider(e);
                }
                if (pawn.inventory?.innerContainer != null)
                {
                    foreach (var inv in pawn.inventory.innerContainer)
                        Consider(inv);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[BuyItem] TrySetOwnershipOnPawnGear: {ex.Message}");
            }
        }
    }
}
