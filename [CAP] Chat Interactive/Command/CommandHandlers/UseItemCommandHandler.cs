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
// Command handler for buying items from Rimazon store
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    internal static class UseItemCommandHandler
    {

        public static string HandleUseItem(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                Logger.Debug($"HandleUseItem called for user: {messageWrapper.Username}, args: {string.Join(", ", args)}");

                if (args.Length == 0)
                {
                    return "Usage: !use <item> [quantity]";
                }

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(messageWrapper.Username);

                // REPLACE the parsing code (about 30 lines) with:
                var parsed = CommandParserUtility.ParseCommandArguments(args, allowQuality: false, allowMaterial: false, allowSide: false, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                string itemName = parsed.ItemName;
                string quantityStr = parsed.Quantity.ToString();

                // Get store item
                var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
                if (storeItem == null)
                {
                    return $"Item '{itemName}' not found in Rimazon.";
                }

                // if (!storeItem.Enabled)
                //{
                //    return $"Item '{itemName}' is not available for purchase.";
                //}

                if (!storeItem.IsUsable)
                {
                    return $"{itemName} is not a usable item.";
                }

                // Check research requirements
                if (!StoreCommandHelper.HasRequiredResearch(storeItem))
                {
                    return $"{itemName} requires research that hasn't been completed yet.";
                }

                // Parse quantity
                if (!int.TryParse(quantityStr, out int quantity) || quantity < 1)
                {
                    quantity = 1;
                }

                // Check quantity limits and clamp to maximum allowed
                if (storeItem.HasQuantityLimit && quantity > storeItem.QuantityLimit)
                {
                    Logger.Debug($"Quantity {quantity} exceeds limit of {storeItem.QuantityLimit} for {itemName}, clamping to maximum");
                    quantity = storeItem.QuantityLimit;
                }

                // Get viewer's pawn
                var viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                Verse.Pawn rimworldPawn = viewerPawn; // This is already a Verse.Pawn
                Logger.Debug($"Viewer pawn for {messageWrapper.Username}: {(viewerPawn != null ? viewerPawn.Name.ToString() : "null")}");
                Logger.Debug($"Viewer pawn dead status: {(viewerPawn != null ? viewerPawn.Dead.ToString() : "N/A")}");
                Logger.Debug($"Rimworld pawn for {messageWrapper.Username}: {(rimworldPawn != null ? rimworldPawn.Name.ToString() : "null")}");
                Logger.Debug($"Rimworld pawn dead status: {(rimworldPawn != null ? rimworldPawn.Dead.ToString() : "N/A")}");

                if (viewerPawn == null)
                {
                    return "You need to have a pawn in the colony to use items. Use !buy pawn first.";
                }

                // SPECIAL RESURRECTION LOGIC: Allow Resurrector Mech Serum on dead pawns
                bool isResurrectorSerum = storeItem.DefName == "MechSerumResurrector";

                if (viewerPawn.Dead && !isResurrectorSerum)
                {
                    return "Your pawn is dead. You cannot use items.";
                }

                // For resurrector serum on dead pawns, force quantity to 1
                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    quantity = 1;
                    Logger.Debug($"Using Resurrector Mech Serum on dead pawn, quantity forced to 1");
                }

                // Calculate final price (no quality/material multipliers for usable items)
                int finalPrice = storeItem.BasePrice * quantity;

                string validationError = ValidateMechanitorImplant(storeItem, rimworldPawn, itemName, quantity);
                if (validationError != null)
                {
                    return validationError;
                }

                // Check if user can afford
                if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
                {
                    return $"You need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} to use {quantity}x {itemName}! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }

                // Get thing def
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    Logger.Error($"ThingDef not found: {storeItem.DefName}");
                    return $"Error: Item definition not found.";
                }

                // Deduct coins
                viewer.TakeCoins(finalPrice);

                int karmaEarned = finalPrice / 100;
                if (karmaEarned > 0)
                {
                    viewer.GiveKarma(karmaEarned);
                    Logger.Debug($"Awarded {karmaEarned} karma for {finalPrice} coin purchase");
                }

                // SPECIAL RESURRECTION: Handle resurrector serum differently
                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                    cooldownManager.RecordItemPurchase(storeItem.DefName); // or "apparel", "item", etc.
                    ResurrectPawn(viewerPawn);
                }
                else
                {
                    // Normal item usage
                    var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                    cooldownManager.RecordItemPurchase(storeItem.DefName); // or "apparel", "item", etc.
                    UseItemImmediately(thingDef, quantity, rimworldPawn);
                }

                // Send appropriate letter notification
                string itemLabel = thingDef?.LabelCap ?? itemName;
                string invoiceLabel;
                string invoiceMessage;

                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    // Pink letter for resurrection
                    invoiceLabel = $"💖 Rimazon Resurrection - {messageWrapper.Username}";
                    invoiceMessage = CreateRimazonResurrectionInvoice(messageWrapper.Username, itemLabel, finalPrice, currencySymbol);
                    LookTargets resurrectionLookTargets = new LookTargets(viewerPawn);
                    Logger.Debug($"Sending resurrection letter to {messageWrapper.Username} for pawn {viewerPawn.Name}");
                    Logger.Debug($"Resurrection look targets: {resurrectionLookTargets}");
                    MessageHandler.SendPinkLetter(invoiceLabel, invoiceMessage, resurrectionLookTargets);
                }
                else if (IsMajorPurchase(finalPrice, null)) // Don't check quality for use commands
                {
                    invoiceLabel = $"🔵 Rimazon Instant - {messageWrapper.Username}";
                    invoiceMessage = CreateRimazonInstantInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol);
                    LookTargets useLookTargets = new LookTargets(rimworldPawn);
                    Logger.Debug($"Sending major use letter to {messageWrapper.Username} for pawn {rimworldPawn.Name}");
                    Logger.Debug($"Use look targets: {useLookTargets}");
                    MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, useLookTargets);
                }
                else
                {
                    invoiceLabel = $"🔵 Rimazon Instant - {messageWrapper.Username}";
                    invoiceMessage = CreateRimazonInstantInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol);
                    LookTargets useLookTargets = new LookTargets(rimworldPawn);
                    Logger.Debug($"Sending regular use letter to {messageWrapper.Username} for pawn {rimworldPawn.Name}");
                    Logger.Debug($"Use look targets: {useLookTargets}");
                    MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, useLookTargets);
                }

                Logger.Debug($"Use item successful: {messageWrapper.Username} used {quantity}x {itemName} for {finalPrice}{currencySymbol}");

                // Return appropriate success message
                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    return $"💖 RESURRECTION! Used {itemName} to bring your pawn back to life for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }
                else
                {
                    return $"Used {quantity}x {itemName} for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleUseItem: {ex}");
                return "Error using item. Please try again.";
            }
        }

        public static string CreateRimazonResurrectionInvoice(string username, string itemName, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON RESURRECTION SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Service: Pawn Resurrection\n";
            invoice += $"Item: {itemName}\n";
            invoice += $"====================\n";
            invoice += $"Total: {price:N0}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Resurrection!\n";
            invoice += $"Your pawn has been restored to life!\n";
            invoice += $"Life is precious - cherish every moment! 💖";

            return invoice;
        }

        public static bool IsPawnCompletelyDestroyed(Verse.Pawn pawn)
        {
            try
            {
                // Check if the pawn exists as a corpse in any map
                foreach (var map in Find.Maps)
                {
                    foreach (var thing in map.listerThings.AllThings)
                    {
                        if (thing is Corpse corpse && corpse.InnerPawn == pawn)
                        {
                            return false; // Corpse exists, not completely destroyed
                        }
                    }
                }

                // Check if pawn exists in world pawns (dead)
                if (Find.WorldPawns.AllPawnsDead.Contains(pawn))
                {
                    return false; // Pawn exists in world pawns
                }

                // If we get here, the pawn is completely gone
                Logger.Debug($"Pawn {pawn.Name} is completely destroyed - no corpse found in any map or world pawns");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking if pawn is destroyed: {ex}");
                return true; // Assume destroyed if we can't check
            }
        }

        // ===== RESURRECTION METHODS =====
        public static void ResurrectPawn(Verse.Pawn pawn)
        {
            try
            {
                Logger.Debug($"Attempting to resurrect pawn: {pawn?.Name}");

                // Safety check - ensure pawn exists and is actually dead
                if (pawn == null)
                {
                    Logger.Error("Cannot resurrect - pawn is null");
                    return;
                }

                if (!pawn.Dead)
                {
                    Logger.Warning($"Pawn {pawn.Name} is not dead, cannot resurrect");
                    return;
                }

                // Check if pawn is completely destroyed (no corpse exists)
                if (IsPawnCompletelyDestroyed(pawn))
                {
                    Logger.Error($"Cannot resurrect {pawn.Name} - pawn is completely destroyed (no corpse exists)");
                    return;
                }

                Logger.Debug($"Resurrecting pawn: {pawn.Name}");

                // Use RimWorld's built-in resurrection method with side effects
                try
                {
                    ResurrectionUtility.TryResurrectWithSideEffects(pawn);
                }
                catch (NullReferenceException)
                {
                    Logger.Warning("Failed to revive with side effects -- falling back to regular revive");
                    ResurrectionUtility.TryResurrect(pawn);
                }

                Logger.Debug($"Successfully resurrected pawn: {pawn.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error resurrecting pawn: {ex}");
                throw;
            }
        }

        private static string CreateRimazonInstantInvoice(string username, string itemName, int quantity, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON INSTANT\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Item: {itemName} x{quantity}\n";
            invoice += $"Service: Immediate Use\n";
            invoice += $"====================\n";
            invoice += $"Total: {price:N0}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Instant!\n";
            invoice += $"No delivery required - instant satisfaction!";

            return invoice;
        }

        private static SkillDef GetSkillDefFromNeurotrainer(string defName)
        {
            return defName.ToLower() switch
            {
                string s when s.Contains("melee") => SkillDefOf.Melee,
                string s when s.Contains("shooting") => SkillDefOf.Shooting,
                string s when s.Contains("construction") => SkillDefOf.Construction,
                string s when s.Contains("mining") => SkillDefOf.Mining,
                string s when s.Contains("cooking") => SkillDefOf.Cooking,
                string s when s.Contains("plants") => SkillDefOf.Plants,
                string s when s.Contains("animals") => SkillDefOf.Animals,
                string s when s.Contains("crafting") => SkillDefOf.Crafting,
                string s when s.Contains("artistic") => SkillDefOf.Artistic,
                string s when s.Contains("medical") => SkillDefOf.Medicine,
                string s when s.Contains("social") => SkillDefOf.Social,
                string s when s.Contains("intellectual") => SkillDefOf.Intellectual,
                _ => null
            };
        }

        private static bool HasPsylink(Verse.Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                return false;

            // Check for any psylink hediff
            return pawn.health.hediffSet.hediffs.Any(hediff =>
                hediff.def?.defName?.Contains("Psylink") == true ||
                hediff.def?.defName?.Contains("Psychic") == true);
        }

        // ===== UTILITY METHODS =====
        public static bool IsMajorPurchase(int price, QualityCategory? quality)
        {
            // Legendary quality items
            if (quality.HasValue && quality.Value == QualityCategory.Legendary)
                return true;

            // Very expensive items (adjust threshold as needed)
            if (price >= 5000)
                return true;

            return false;
        }

        private static bool IsSustainerSound(string soundDefName)
        {
            if (string.IsNullOrEmpty(soundDefName)) return false;

            // Common sustainer sound names that shouldn't be played as one-shot
            string[] sustainerKeywords = {
        "Sustain", "Loop", "Ambient", "Meal_Eat", "Ingest_", "Burning",
        "Wind", "Engine", "Working", "Charging", "Ritual"
    };

            foreach (string keyword in sustainerKeywords)
            {
                if (soundDefName.Contains(keyword))
                    return true;
            }

            return false;
        }

        private static void PlayFallbackIngestSound(ThingDef thingDef, Verse.Pawn pawn)
        {
            try
            {
                if (thingDef.IsDrug)
                {
                    // Use specific drug sounds based on drug type
                    if (thingDef.ingestible.drugCategory == DrugCategory.Social || thingDef.defName.Contains("Smoke"))
                    {
                        SoundDefOf.Interact_Ignite.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                    else if (thingDef.ingestible.drugCategory == DrugCategory.Hard)
                    {
                        SoundDefOf.Crunch.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                    else
                    {
                        SoundDefOf.Click.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                }
                else if (thingDef.ingestible.IsMeal)
                {
                    // Use crunch sound for meals (eating sound)
                    SoundDefOf.Crunch.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.IsCorpse || thingDef.defName.Contains("Meat"))
                {
                    SoundDefOf.RawMeat_Eat.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.IsIngestible && thingDef.ingestible != null &&
                         (thingDef.ingestible.foodType & FoodTypeFlags.Liquor) != 0)
                {
                    // For beer and other liquor, use a liquid sound
                    SoundDefOf.HissSmall.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.defName.Contains("Berry") || thingDef.defName.Contains("Fruit"))
                {
                    // For fruits/berries, use raw vegetable eat sound
                    SoundDefOf.RawMeat_Eat.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else
                {
                    // Default for vegetables and other foods
                    SoundDefOf.Crunch.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error in PlayFallbackIngestSound: {ex.Message}");
                // Final fallback - use a very basic sound
                SoundDefOf.Click.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
        }

        // ===== SOUND METHODS =====
        private static void PlayIngestSoundSafely(ThingDef thingDef, Verse.Pawn pawn)
        {
            try
            {
                // Try to use the ingest sound from the thing definition first
                if (thingDef.ingestible.ingestSound != null)
                {
                    // Check if this is a sustainer sound that shouldn't be played as one-shot
                    string soundName = thingDef.ingestible.ingestSound.defName;
                    if (IsSustainerSound(soundName))
                    {
                        Logger.Debug($"Skipping sustainer sound: {soundName}, using fallback");
                        PlayFallbackIngestSound(thingDef, pawn);
                    }
                    else
                    {
                        // It's safe to play as one-shot
                        thingDef.ingestible.ingestSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                }
                else
                {

                    // No specific ingest sound defined, use fallback
                    PlayFallbackIngestSound(thingDef, pawn);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error playing ingest sound for {thingDef.defName}: {ex.Message}");
                PlayFallbackIngestSound(thingDef, pawn);
            }
        }

        // ===== ITEM USAGE  =====

        private static void UseItemImmediately(ThingDef thingDef, int quantity, Verse.Pawn pawn)
        {
            for (int i = 0; i < quantity; i++)
            {
                Thing thing = ThingMaker.MakeThing(thingDef);

                // Handle different types of usable items with appropriate sounds
                if (thingDef.IsIngestible && thingDef.ingestible != null)
                {
                    // DEBUG: Log nutrition before ingestion
                    float nutritionBefore = pawn.needs.food?.CurLevel ?? 0f;
                    Logger.Debug($"Nutrition before ingestion: {nutritionBefore}");

                    // SPAWN THE ITEM FIRST so ingestion works properly
                    GenSpawn.Spawn(thing, pawn.Position, pawn.Map);

                    // Now ingest the spawned item and APPLY the nutrition
                    float nutritionWanted = pawn.needs.food?.NutritionWanted ?? 0f;
                    Logger.Debug($"Nutrition wanted: {nutritionWanted}");

                    // Ingest returns the nutrition gained - we need to apply it to the pawn
                    float nutritionGained = thing.Ingested(pawn, nutritionWanted);
                    Logger.Debug($"Nutrition gained from ingestion: {nutritionGained}");

                    // Apply the nutrition to the pawn's food need
                    if (pawn.needs.food != null)
                    {
                        pawn.needs.food.CurLevel += nutritionGained;
                        Logger.Debug($"Nutrition after manual application: {pawn.needs.food.CurLevel}");
                    }

                    // Play appropriate sound - use safe sound playing method
                    PlayIngestSoundSafely(thingDef, pawn);

                    // Clean up - the item should be consumed/destroyed by Ingested(), but ensure it's gone
                    if (thing.Spawned)
                    {
                        thing.Destroy();
                    }
                }
                else if (thingDef.IsMedicine)
                {
                    // Medicine - add to inventory since immediate use is complex
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    SoundDefOf.Interact_Tend.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.HasComp(typeof(CompUsable)) || thingDef.HasComp(typeof(CompUsableImplant)))
                {
                    // NEW: Generalized handling for all CompUsable items (includes implants, trainers, etc.)
                    UseCompUseEffectItem(thing, pawn);  // Updated method name for clarity
                    Logger.Debug($"Applying CompUsable/Implant effect for {thingDef.defName} on {pawn.Name}");
                    SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.defName.Contains("Psytrainer") || thingDef.defName.Contains("Neurotrainer") || thingDef.defName == "PsychicAmplifier")
                {

                    // FIX: Actually use psy trainers and neurotrainers instead of just adding to inventory
                    UseCompUseEffectItem(thing, pawn);
                    Logger.Debug($"Used psy/neuro trainer {thingDef.defName} on {pawn.Name}");
                }
                else if (thingDef.defName.Contains("Neuroformer"))
                {
                    // Neuroformers - add to inventory (these are typically used via right-click)
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.defName.Contains("MechSerum"))
                {
                    // Mech serums - add to inventory
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    SoundDefOf.MechSerumUsed.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else
                {
                    // Fallback for other usable items - add to inventory
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    SoundDefOf.Standard_Pickup.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                cooldownManager.RecordItemPurchase(thingDef.defName); // or "apparel", "item", etc.

                Logger.Debug($"Used item {thingDef.defName}, played sound effect");
            }
        }

        private static void UseCompUseEffectItem(Thing thing, Verse.Pawn pawn)
        {
            try
            {
                Logger.Debug($"Attempting to use item: {thing.def.defName} on pawn {pawn.Name}");

                // Spawn the item temporarily so comps can initialize
                GenSpawn.Spawn(thing, pawn.Position, pawn.Map);

                List<CompUseEffect> compUseEffects = new List<CompUseEffect>();

                // Get ALL CompUseEffect components, not just the first one
                if (thing is ThingWithComps thingWithComps)
                {
                    foreach (var comp in thingWithComps.AllComps)
                    {
                        if (comp is CompUseEffect compUseEffect)
                        {
                            compUseEffects.Add(compUseEffect);
                        }
                    }
                }

                Logger.Debug($"Found {compUseEffects.Count} CompUseEffect components");

                bool anyEffectApplied = false;

                if (thing.def.defName.Contains("Psytrainer") && !HasPsylink(pawn))
                {
                    Logger.Debug($"Pawn {pawn.Name} does not have psylink, cannot use psy trainer");
                    // Add to inventory instead of using
                    if (thing.Spawned)
                    {
                        thing.DeSpawn();
                    }
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    return;
                }

                foreach (var compUseEffect in compUseEffects)
                {
                    Logger.Debug($"Processing CompUseEffect: {compUseEffect.GetType().FullName}");

                    AcceptanceReport acceptance = compUseEffect.CanBeUsedBy(pawn);
                    Logger.Debug($"CanBeUsedBy result for {compUseEffect.GetType().Name}: Accepted={acceptance.Accepted}, Reason={acceptance.Reason}");

                    if (acceptance.Accepted)
                    {
                        Logger.Debug($"Calling DoEffect on {compUseEffect.GetType().Name}...");
                        compUseEffect.DoEffect(pawn);
                        Logger.Debug($"DoEffect completed on {compUseEffect.GetType().Name}");
                        anyEffectApplied = true;

                        // Try SelectedUseOption as well
                        try
                        {
                            Logger.Debug($"Calling SelectedUseOption on {compUseEffect.GetType().Name}...");
                            bool selectedResult = compUseEffect.SelectedUseOption(pawn);
                            Logger.Debug($"SelectedUseOption result on {compUseEffect.GetType().Name}: {selectedResult}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"SelectedUseOption failed on {compUseEffect.GetType().Name} (may be normal): {ex.Message}");
                        }
                    }
                    else
                    {
                        Logger.Warning($"Cannot use {compUseEffect.GetType().Name} on pawn {pawn.Name}: {acceptance.Reason}");
                    }
                }

                if (!anyEffectApplied)
                {
                    Logger.Warning("No CompUseEffect components could be applied to pawn");
                }

                // Despawn the item after use (it's consumed)
                if (thing.Spawned)
                {
                    thing.DeSpawn();
                }

                SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));

                // Log skill levels for debugging
                if (thing.def.defName.Contains("Neurotrainer"))
                {
                    var skillDef = GetSkillDefFromNeurotrainer(thing.def.defName);
                    if (skillDef != null)
                    {
                        int skillLevel = pawn.skills.GetSkill(skillDef).Level;
                        Logger.Debug($"Pawn {pawn.Name} {skillDef.defName} skill level after use: {skillLevel}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error using item {thing.def.defName}: {ex}");
                // Fallback: add to inventory if usage fails
                if (thing.Spawned)
                {
                    thing.DeSpawn();
                }
                if (!pawn.inventory.innerContainer.TryAdd(thing))
                {
                    GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
            }
        }

        // ===== Biotech Implant Handling (example of a specific CompUsable item) =====

        private static string ValidateMechanitorImplant(StoreItem storeItem, Verse.Pawn pawn, string itemName, int quantity)
        {
            // Skip validation if Biotech DLC is not active
            if (!ModLister.BiotechInstalled)
                return null;

            // Check if this is a Mechanitor implant
            bool isControlSublink = storeItem.DefName == "ControlSublink";
            bool isControlSublinkHigh = storeItem.DefName == "ControlSublinkHigh";
            bool isMechFormfeeder = storeItem.DefName == "MechFormfeeder";
            bool isRemoteRepairer = storeItem.DefName == "RemoteRepairer";
            bool isRemoteShielder = storeItem.DefName == "RemoteShielder";
            bool isRepairProbe = storeItem.DefName == "RepairProbe";

            if (!isControlSublink && !isControlSublinkHigh && !isMechFormfeeder &&
                !isRemoteRepairer && !isRemoteShielder && !isRepairProbe)
                return null;

            // Get all hediffs on the pawn
            var hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs == null)
                return null;

            // For ControlSublink implants - they use a single Hediff_Level with severity as count
            if (isControlSublink || isControlSublinkHigh)
            {
                var sublinkHediff = hediffs.FirstOrDefault(h => h.def.defName == "ControlSublinkImplant") as Hediff_Level;
                int currentLevel = sublinkHediff?.level ?? 0;

                if (isControlSublink)
                {
                    // Standard sublink: max 3
                    if (currentLevel >= 3)
                    {
                        return $"Cannot install {itemName}. Maximum of 3 already installed. Current: {currentLevel}/3";
                    }

                    // Check if quantity would exceed max limit
                    if (quantity > 1 && (currentLevel + quantity) > 3)
                    {
                        int availableSlots = 3 - currentLevel;
                        return $"Cannot install {quantity}x {itemName}. You only have {availableSlots} slot(s) available. Current: {currentLevel}/3";
                    }
                }
                else if (isControlSublinkHigh)
                {
                    // High sublink: max 6, requires at least 3 standard first
                    if (currentLevel < 3)
                    {
                        return $"Cannot install {itemName}. You need 3 standard Control Sublinks first. Current: {currentLevel}/3";
                    }

                    if (currentLevel >= 6)
                    {
                        return $"Cannot install {itemName}. Maximum of 6 already installed. Current: {currentLevel}/6";
                    }

                    // Check if quantity would exceed max limit
                    if (quantity > 1 && (currentLevel + quantity) > 6)
                    {
                        int availableSlots = 6 - currentLevel;
                        return $"Cannot install {quantity}x {itemName}. You only have {availableSlots} slot(s) available. Current: {currentLevel}/6";
                    }
                }
            }

            // For other Mechanitor implants (all use Hediff_Level with levelIsQuantity=true)
            else if (isMechFormfeeder)
            {
                var hediff = hediffs.FirstOrDefault(h => h.def.defName == "MechFormfeederImplant") as Hediff_Level;
                int currentLevel = hediff?.level ?? 0;
                int maxAllowed = 6;

                if (currentLevel >= maxAllowed)
                {
                    return $"Cannot install {itemName}. Maximum of {maxAllowed} already installed. Current: {currentLevel}/{maxAllowed}";
                }

                if (quantity > 1 && (currentLevel + quantity) > maxAllowed)
                {
                    int availableSlots = maxAllowed - currentLevel;
                    return $"Cannot install {quantity}x {itemName}. You only have {availableSlots} slot(s) available. Current: {currentLevel}/{maxAllowed}";
                }
            }
            else if (isRemoteRepairer)
            {
                var hediff = hediffs.FirstOrDefault(h => h.def.defName == "RemoteRepairerImplant") as Hediff_Level;
                int currentLevel = hediff?.level ?? 0;
                int maxAllowed = 3;

                if (currentLevel >= maxAllowed)
                {
                    return $"Cannot install {itemName}. Maximum of {maxAllowed} already installed. Current: {currentLevel}/{maxAllowed}";
                }

                if (quantity > 1 && (currentLevel + quantity) > maxAllowed)
                {
                    int availableSlots = maxAllowed - currentLevel;
                    return $"Cannot install {quantity}x {itemName}. You only have {availableSlots} slot(s) available. Current: {currentLevel}/{maxAllowed}";
                }
            }
            else if (isRemoteShielder)
            {
                var hediff = hediffs.FirstOrDefault(h => h.def.defName == "RemoteShielderImplant") as Hediff_Level;
                int currentLevel = hediff?.level ?? 0;
                int maxAllowed = 3;

                if (currentLevel >= maxAllowed)
                {
                    return $"Cannot install {itemName}. Maximum of {maxAllowed} already installed. Current: {currentLevel}/{maxAllowed}";
                }

                if (quantity > 1 && (currentLevel + quantity) > maxAllowed)
                {
                    int availableSlots = maxAllowed - currentLevel;
                    return $"Cannot install {quantity}x {itemName}. You only have {availableSlots} slot(s) available. Current: {currentLevel}/{maxAllowed}";
                }
            }
            else if (isRepairProbe)
            {
                var hediff = hediffs.FirstOrDefault(h => h.def.defName == "RepairProbeImplant") as Hediff_Level;
                int currentLevel = hediff?.level ?? 0;
                int maxAllowed = 6;

                if (currentLevel >= maxAllowed)
                {
                    return $"Cannot install {itemName}. Maximum of {maxAllowed} already installed. Current: {currentLevel}/{maxAllowed}";
                }

                if (quantity > 1 && (currentLevel + quantity) > maxAllowed)
                {
                    int availableSlots = maxAllowed - currentLevel;
                    return $"Cannot install {quantity}x {itemName}. You only have {availableSlots} slot(s) available. Current: {currentLevel}/{maxAllowed}";
                }
            }

            return null;
        }

    }
}