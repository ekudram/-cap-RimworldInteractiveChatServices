// File: UseItemCommandHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// !use — immediately consume/apply store items (food, serums, trainers, implants)
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    internal static class UseItemCommandHandler
    {
        private const string ReturnDivider = " | ";

        /// <summary>
        /// Chat <c>!use</c>: consume/apply a store item on the viewer pawn.
        /// Usable-comp items (Psytrainer, skill Neurotrainer, implants) must pass vanilla
        /// <see cref="CompUseEffect.CanBeUsedBy"/>-equivalent checks <b>before</b> apply or coin charge.
        /// </summary>
        public static string HandleUseItem(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();
                if (args.Length == 0)
                    return "RICS.UICH.Usage".Translate();

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null)
                    return "RICS.UICH.GenericError".Translate();

                string currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.UICH.GenericError".Translate();

                var parsed = CommandParserUtility.ParseCommandArguments(
                    args, allowQuality: false, allowMaterial: false, allowSide: false, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                string itemName = parsed.ItemName;
                string quantityStr = parsed.Quantity.ToString();

                var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
                if (storeItem == null)
                    return "RICS.UICH.ItemNotFound".Translate(itemName);

                if (!storeItem.IsUsable)
                    return "RICS.UICH.NotUsable".Translate(itemName);

                var anomalyDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (anomalyDef != null && !AnomalyStaging.CanPurchaseItem(anomalyDef, itemName, out string anomalyReason))
                    return anomalyReason;

                var researchResult = StoreCommandHelper.HasRequiredResearch(storeItem);
                if (!researchResult.Allowed)
                {
                    string researchInfo = string.IsNullOrEmpty(researchResult.BlockingResearchLabel)
                        ? string.Empty
                        : ReturnDivider + researchResult.BlockingResearchLabel;
                    return "RICS.UICH.ResearchRequired".Translate(itemName) + researchInfo;
                }

                if (!int.TryParse(quantityStr, out int quantity) || quantity < 1)
                    quantity = 1;

                if (storeItem.HasQuantityLimit && quantity > storeItem.QuantityLimit)
                    quantity = storeItem.QuantityLimit;

                Verse.Pawn viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                if (viewerPawn == null)
                    return "RICS.Pawn.NoPawn".Translate();

                bool isResurrectorSerum = storeItem.DefName == "MechSerumResurrector";

                if ((viewerPawn.Destroyed || viewerPawn.Dead) && !isResurrectorSerum)
                {
                    var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                    return "RICS.Pawn.Dead".Translate()
                           + ReturnDivider
                           + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
                }

                if (isResurrectorSerum && viewerPawn.Dead)
                    quantity = 1;

                int finalPrice = storeItem.BasePrice * quantity;
                if (viewer.Coins < finalPrice)
                {
                    return "RICS.UICH.NotEnoughCoins".Translate(
                        StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                        quantity,
                        itemName,
                        StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
                }

                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                    return "RICS.UICH.ItemDefMissing".Translate(itemName);

                if (isResurrectorSerum && viewerPawn.Dead && CannotResurrectPawn(viewerPawn))
                    return "RICS.UICH.BodyDestroyed".Translate();

                string validationError = ValidateMechanitorImplant(storeItem, viewerPawn, itemName, quantity);
                if (validationError != null)
                    return validationError;

                // Vanilla CompUseEffect_GainAbility.CanBeUsedBy requires a psycaster. Fail before apply/charge.
                if (IsPsytrainer(thingDef) && !IsPsycaster(viewerPawn))
                    return "RICS.UICH.PsytrainerRequiresPsycaster".Translate(itemName);

                // Vanilla CompUseEffect_LearnSkill.CanBeUsedBy rejects TotallyDisabled / no skills.
                // Learn(..., direct: true) would still add XP if we skipped this and called DoEffect.
                SkillDef trainerSkill = GetSkillTrainerSkill(thingDef);
                if (trainerSkill != null && !CanPawnUseSkill(viewerPawn, trainerSkill))
                    return "RICS.UICH.SkillTrainerSkillDisabled".Translate(itemName, trainerSkill.LabelCap);

                // Apply first, then charge
                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    ResurrectPawn(viewerPawn);
                    if (viewerPawn.Dead)
                        return "RICS.UICH.BodyDestroyed".Translate();
                }
                else
                {
                    if (!UseItemImmediately(thingDef, quantity, viewerPawn))
                        return "RICS.UICH.UseEffectRejected".Translate(itemName);
                }

                viewer.TakeCoins(finalPrice);
                AwardUseKarma(viewer, finalPrice, settings.KarmaPerStoreItem);
                Current.Game?.GetComponent<GlobalCooldownManager>()?.RecordItemPurchase(storeItem.DefName);

                string itemLabel = thingDef.label ?? itemName;
                LookTargets lookTargets = new LookTargets(viewerPawn);

                if (isResurrectorSerum)
                {
                    string invoiceLabel = "RICS.UICH.InvoiceResurrectLabel".Translate(messageWrapper.Username);
                    string invoiceMessage = CreateRimazonResurrectionInvoice(
                        messageWrapper.Username, itemLabel, finalPrice, currencySymbol);
                    MessageHandler.SendPinkLetter(invoiceLabel, invoiceMessage, lookTargets);

                    return "RICS.UICH.SuccessResurrect".Translate(
                        itemName,
                        StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                        StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
                }

                string instantLabel = "RICS.UICH.InvoiceInstantLabel".Translate(messageWrapper.Username);
                string instantMessage = CreateRimazonInstantInvoice(
                    messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol);
                MessageHandler.SendBlueLetter(instantLabel, instantMessage, lookTargets);

                return "RICS.UICH.SuccessNormal".Translate(
                    quantity,
                    itemName,
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }
            catch (Exception ex)
            {
                Logger.Error($"[UseItem] Error in HandleUseItem: {ex}");
                return "RICS.UICH.GenericError".Translate();
            }
        }

        public static string CreateRimazonResurrectionInvoice(string username, string itemName, int price, string currencySymbol)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RICS.UICH.Invoice.Resurrect.Header".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Separator".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Customer".Translate(username));
            sb.AppendLine("RICS.UICH.Invoice.Service".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Item".Translate(itemName));
            sb.AppendLine("RICS.UICH.Invoice.Separator".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Total".Translate(price.ToString("N0"), currencySymbol));
            sb.AppendLine("RICS.UICH.Invoice.Separator".Translate());
            sb.AppendLine("RICS.UICH.Invoice.ThankYou".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Restored".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Closing".Translate());
            return sb.ToString();
        }

        public static bool CannotResurrectPawn(Verse.Pawn pawn)
        {
            if (pawn == null || !pawn.Dead)
                return true;

            if (pawn.Discarded)
                return true;

            Corpse corpse = pawn.Corpse;
            if (corpse == null)
                return true;

            if (corpse.Destroyed || corpse.Map == null)
                return true;

            // Multi-map colony: corpse must be on the current map for simple revive path
            if (Find.CurrentMap != null && corpse.Map != Find.CurrentMap)
                return true;

            return false;
        }

        public static bool IsPawnCompletelyDestroyed(Verse.Pawn pawn)
        {
            try
            {
                if (pawn == null)
                    return true;

                foreach (var map in Find.Maps)
                {
                    foreach (var thing in map.listerThings.AllThings)
                    {
                        if (thing is Corpse corpse && corpse.InnerPawn == pawn)
                            return false;
                    }
                }

                if (Find.WorldPawns.AllPawnsDead.Contains(pawn))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[UseItem] Error checking if pawn is destroyed: {ex}");
                return true;
            }
        }

        public static void ResurrectPawn(Verse.Pawn pawn)
        {
            try
            {
                if (pawn == null)
                {
                    Logger.Error("[UseItem] Cannot resurrect — pawn is null");
                    return;
                }

                if (!pawn.Dead)
                    return;

                if (CannotResurrectPawn(pawn))
                    return;

                try
                {
                    ResurrectionUtility.TryResurrectWithSideEffects(pawn);
                }
                catch (NullReferenceException)
                {
                    Logger.Warning("[UseItem] Revive with side effects failed — falling back to TryResurrect");
                    ResurrectionUtility.TryResurrect(pawn);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[UseItem] Error resurrecting pawn: {ex}");
                throw;
            }
        }

        public static bool IsMajorPurchase(int price, QualityCategory? quality)
        {
            if (quality.HasValue && quality.Value == QualityCategory.Legendary)
                return true;
            return price >= 5000;
        }

        private static string CreateRimazonInstantInvoice(
            string username,
            string itemName,
            int quantity,
            int price,
            string currencySymbol)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RICS.UICH.Invoice.Instant.Header".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Separator".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Customer".Translate(username));
            sb.AppendLine("RICS.UICH.Invoice.Iteminstant".Translate(itemName, quantity));
            sb.AppendLine("RICS.UICH.Invoice.Service.Immediate".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Separator".Translate());
            sb.AppendLine("RICS.UICH.Invoice.Total".Translate(price.ToString("N0"), currencySymbol));
            sb.AppendLine("RICS.UICH.Invoice.Separator".Translate());
            sb.AppendLine("RICS.UICH.Invoice.ThankYouInstant".Translate());
            sb.AppendLine("RICS.UICH.Invoice.NoDelivery".Translate());
            return sb.ToString();
        }

        /// <summary>
        /// Royalty Psytrainer_* (or any GainAbility comp whose ability is a psycast).
        /// Not skill Neurotrainers and not PsychicAmplifier (the implant that creates a psycaster).
        /// </summary>
        private static bool IsPsytrainer(ThingDef def)
        {
            if (def == null)
                return false;

            if (def.defName.StartsWith(ThingDefGenerator_Neurotrainer.PsytrainerDefPrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            if (def.comps == null)
                return false;

            foreach (var comp in def.comps)
            {
                if (comp is CompProperties_UseEffect_GainAbility gain
                    && gain.ability != null
                    && gain.ability.IsPsycast)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True if the pawn can use a Psytrainer: vanilla psylink/PsychicAmplifier, or VPE implant level &gt; 0.
        /// VPE pawns often have no vanilla PsychicAmplifier; GetPsylinkLevel() is then 0.
        /// </summary>
        private static bool IsPsycaster(Verse.Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return false;

            try
            {
                if (pawn.GetPsylinkLevel() > 0)
                    return true;
            }
            catch
            {
                // Royalty tracker missing
            }

            if (HediffDefOf.PsychicAmplifier != null
                && pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier))
                return true;

            if (pawn.health.hediffSet.hediffs != null
                && pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Psylink))
                return true;

            try
            {
                var vpe = CAPChatInteractiveMod.Instance?.VPEProvider;
                if (vpe != null && vpe.GetBasicPsycastInfo(pawn)?.Level > 0)
                    return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[UseItem] VPE psycaster check failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Skill taught by a Neurotrainer via <see cref="CompProperties_UseEffect_LearnSkill"/>.
        /// Null if not a skill trainer. Psytrainers and PsychicAmplifier are not skill trainers.
        /// </summary>
        private static SkillDef GetSkillTrainerSkill(ThingDef def)
        {
            if (def?.comps == null)
                return null;

            foreach (var comp in def.comps)
            {
                if (comp is CompProperties_UseEffect_LearnSkill learn && learn.skill != null)
                    return learn.skill;
            }

            return null;
        }

        /// <summary>
        /// Same gate as vanilla <see cref="CompUseEffect_LearnSkill.CanBeUsedBy"/>:
        /// pawn must have a skill record that is not <see cref="SkillRecord.TotallyDisabled"/>.
        /// </summary>
        private static bool CanPawnUseSkill(Verse.Pawn pawn, SkillDef skill)
        {
            if (pawn?.skills == null || skill == null)
                return false;

            SkillRecord record = pawn.skills.GetSkill(skill);
            if (record == null)
                return false;

            return !record.TotallyDisabled;
        }

        private static bool HasPsylink(Verse.Pawn pawn) => IsPsycaster(pawn);

        private static bool IsSustainerSound(string soundDefName)
        {
            if (string.IsNullOrEmpty(soundDefName))
                return false;

            string[] sustainerKeywords =
            {
                "Sustain", "Loop", "Ambient", "Meal_Eat", "Ingest_", "Burning",
                "Wind", "Engine", "Working", "Charging", "Ritual"
            };

            foreach (string keyword in sustainerKeywords)
            {
                if (soundDefName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static void PlayFallbackIngestSound(ThingDef thingDef, Verse.Pawn pawn)
        {
            try
            {
                if (pawn?.Map == null)
                    return;

                TargetInfo target = new TargetInfo(pawn.Position, pawn.Map);

                if (thingDef.IsDrug)
                {
                    if (thingDef.ingestible?.drugCategory == DrugCategory.Social ||
                        thingDef.defName.IndexOf("Smoke", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        SoundDefOf.Interact_Ignite.PlayOneShot(target);
                    }
                    else if (thingDef.ingestible?.drugCategory == DrugCategory.Hard)
                    {
                        SoundDefOf.Crunch.PlayOneShot(target);
                    }
                    else
                    {
                        SoundDefOf.Click.PlayOneShot(target);
                    }
                }
                else if (thingDef.ingestible?.IsMeal == true)
                {
                    SoundDefOf.Crunch.PlayOneShot(target);
                }
                else if (thingDef.IsCorpse ||
                         thingDef.defName.IndexOf("Meat", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SoundDefOf.RawMeat_Eat.PlayOneShot(target);
                }
                else if (thingDef.IsIngestible && thingDef.ingestible != null &&
                         (thingDef.ingestible.foodType & FoodTypeFlags.Liquor) != 0)
                {
                    SoundDefOf.HissSmall.PlayOneShot(target);
                }
                else if (thingDef.defName.IndexOf("Berry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         thingDef.defName.IndexOf("Fruit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SoundDefOf.RawMeat_Eat.PlayOneShot(target);
                }
                else
                {
                    SoundDefOf.Crunch.PlayOneShot(target);
                }
            }
            catch
            {
                if (pawn?.Map != null)
                    SoundDefOf.Click.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
        }

        private static void PlayIngestSoundSafely(ThingDef thingDef, Verse.Pawn pawn)
        {
            try
            {
                if (pawn?.Map == null)
                    return;

                if (thingDef.ingestible?.ingestSound != null)
                {
                    string soundName = thingDef.ingestible.ingestSound.defName;
                    if (IsSustainerSound(soundName))
                        PlayFallbackIngestSound(thingDef, pawn);
                    else
                        thingDef.ingestible.ingestSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else
                {
                    PlayFallbackIngestSound(thingDef, pawn);
                }
            }
            catch
            {
                PlayFallbackIngestSound(thingDef, pawn);
            }
        }

        /// <summary>
        /// Apply one or more copies of the item. For CompUsable items, false means no effect ran — do not charge.
        /// Inventory-only paths (medicine, leftover defs) still count as applied.
        /// </summary>
        /// <returns>False if a usable-comp item applied no effect (do not charge coins).</returns>
        private static bool UseItemImmediately(ThingDef thingDef, int quantity, Verse.Pawn pawn)
        {
            if (thingDef == null || pawn == null || pawn.Map == null)
                return false;

            bool anyApplied = false;
            for (int i = 0; i < quantity; i++)
            {
                Thing thing = ThingMaker.MakeThing(thingDef);

                if (thingDef.IsIngestible && thingDef.ingestible != null)
                {
                    ApplyIngestibleImmediately(thing, thingDef, pawn);
                    anyApplied = true;
                }
                else if (thingDef.IsMedicine)
                {
                    if (pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.TryAdd(thing))
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    SoundDefOf.Interact_Tend.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    anyApplied = true;
                }
                else if (thingDef.HasComp(typeof(CompUsable)) || thingDef.HasComp(typeof(CompUsableImplant)))
                {
                    if (!UseCompUseEffectItem(thing, pawn))
                    {
                        if (!anyApplied)
                            return false;
                        break;
                    }
                    anyApplied = true;
                    SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.defName.IndexOf("Psytrainer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         thingDef.defName.IndexOf("Neurotrainer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         thingDef.defName == "PsychicAmplifier")
                {
                    if (!UseCompUseEffectItem(thing, pawn))
                    {
                        if (!anyApplied)
                            return false;
                        break;
                    }
                    anyApplied = true;
                }
                else if (thingDef.defName.IndexOf("Neuroformer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.TryAdd(thing))
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    anyApplied = true;
                }
                else if (thingDef.defName.IndexOf("MechSerum", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.TryAdd(thing))
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    SoundDefOf.MechSerumUsed.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    anyApplied = true;
                }
                else
                {
                    if (pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.TryAdd(thing))
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    SoundDefOf.Standard_Pickup.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    anyApplied = true;
                }
            }

            return anyApplied;
        }

        /// <summary>
        /// Instantly apply food/drugs. Prefer full-item nutrition so meals always fill when hungry.
        /// Fallback sets food CurLevel if Ingested applied drug effects but no nutrition
        /// (seen with spawn+Ingested and some meal mods; UI can look "late" or unchanged).
        /// </summary>
        private static void ApplyIngestibleImmediately(Thing thing, ThingDef thingDef, Verse.Pawn pawn)
        {
            if (thing == null || thingDef?.ingestible == null || pawn == null)
                return;

            if (thing.stackCount < 1)
                thing.stackCount = 1;

            float foodBefore = pawn.needs?.food != null ? pawn.needs.food.CurLevel : -1f;

            float itemNutrition = 0f;
            try
            {
                itemNutrition = FoodUtility.GetNutrition(pawn, thing, thingDef);
            }
            catch
            {
                // ignore
            }
            if (itemNutrition <= 0f)
            {
                try
                {
                    itemNutrition = thingDef.ingestible.CachedNutrition;
                }
                catch
                {
                    itemNutrition = 0f;
                }
            }

            // At least one full item of nutrition (not only the tiny remainder of NutritionWanted).
            float nutritionWanted = itemNutrition > 0f ? itemNutrition : 0.01f;
            if (pawn.needs?.food != null)
                nutritionWanted = Mathf.Max(nutritionWanted, pawn.needs.food.NutritionWanted);

            // Do not spawn on the map: free food can be hauled/interfered with by mods mid-ingest.
            try
            {
                if (!thing.Destroyed)
                    thing.Ingested(pawn, nutritionWanted);
            }
            catch (Exception ex)
            {
                Logger.Error($"[UseItem] Ingested failed for {thingDef.defName}: {ex.Message}");
            }

            // Guarantee food need moves for real food when the pawn still has room.
            if (pawn.needs?.food != null && foodBefore >= 0f && IsPrimarilyFood(thingDef))
            {
                float foodAfter = pawn.needs.food.CurLevel;
                float room = pawn.needs.food.MaxLevel - foodAfter;
                if (room > 0.01f && foodAfter <= foodBefore + 0.001f && itemNutrition > 0f)
                {
                    float add = Mathf.Min(itemNutrition, room);
                    pawn.needs.food.CurLevel = Mathf.Min(pawn.needs.food.MaxLevel, foodBefore + add);
                }
            }

            if (!thing.Destroyed)
                thing.Destroy(DestroyMode.Vanish);

            PlayIngestSoundSafely(thingDef, pawn);
        }

        /// <summary>True for meals / raw food — not social/hard drugs (those rely on Ingested effects).</summary>
        private static bool IsPrimarilyFood(ThingDef def)
        {
            if (def?.ingestible == null)
                return false;

            try
            {
                if (def.ingestible.IsMeal)
                    return true;

                // Drugs: coffee/beer/etc. — leave nutrition to Ingested only
                if (def.ingestible.drugCategory != DrugCategory.None)
                    return false;

                if (def.IsNutritionGivingIngestible)
                    return true;

                var ft = def.ingestible.foodType;
                if ((ft & (FoodTypeFlags.Meal | FoodTypeFlags.Meat | FoodTypeFlags.VegetableOrFruit
                           | FoodTypeFlags.AnimalProduct | FoodTypeFlags.Processed | FoodTypeFlags.Kibble)) != 0)
                    return true;

                return def.ingestible.CachedNutrition > 0.05f;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Spawn a temp thing, run <see cref="CompUseEffect.CanBeUsedBy"/> then <see cref="CompUseEffect.DoEffect"/>.
        /// Skipping CanBeUsedBy used to skip DoEffect but still charge coins. If nothing is accepted, vanish and return false.
        /// </summary>
        /// <returns>True if at least one CompUseEffect ran. False: vanish temp thing, caller must not charge.</returns>
        private static bool UseCompUseEffectItem(Thing thing, Verse.Pawn pawn)
        {
            try
            {
                if (thing == null || pawn?.Map == null)
                    return false;

                GenSpawn.Spawn(thing, pawn.Position, pawn.Map);

                var compUseEffects = new List<CompUseEffect>();
                if (thing is ThingWithComps thingWithComps)
                {
                    foreach (var comp in thingWithComps.AllComps)
                    {
                        if (comp is CompUseEffect compUseEffect)
                            compUseEffects.Add(compUseEffect);
                    }
                }

                // Safety net: never apply or gift a Psytrainer if the pawn is not a psycaster.
                // HandleUseItem should already have failed the command before charge.
                if (IsPsytrainer(thing.def) && !IsPsycaster(pawn))
                {
                    Logger.Warning($"[UseItem] Blocked Psytrainer {thing.def.defName} — pawn is not a psycaster");
                    VanishTempThing(thing);
                    return false;
                }

                // HandleUseItem should already have failed before charge; vanish if a later path still reaches here.
                SkillDef trainerSkill = GetSkillTrainerSkill(thing.def);
                if (trainerSkill != null && !CanPawnUseSkill(pawn, trainerSkill))
                {
                    Logger.Warning($"[UseItem] Blocked Neurotrainer {thing.def.defName} — skill {trainerSkill.defName} is disabled");
                    VanishTempThing(thing);
                    return false;
                }

                bool appliedAny = false;
                foreach (var compUseEffect in compUseEffects)
                {
                    AcceptanceReport acceptance = compUseEffect.CanBeUsedBy(pawn);
                    if (!acceptance.Accepted)
                        continue;

                    compUseEffect.DoEffect(pawn);
                    appliedAny = true;
                    try
                    {
                        compUseEffect.SelectedUseOption(pawn);
                    }
                    catch
                    {
                        // SelectedUseOption may throw for non-interactive uses
                    }
                }

                if (!appliedAny)
                {
                    Logger.Warning($"[UseItem] No usable effect applied for {thing.def?.defName}");
                    VanishTempThing(thing);
                    return false;
                }

                if (thing.Spawned)
                    thing.DeSpawn();
                if (thing != null && !thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);

                SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[UseItem] Error using item {thing?.def?.defName}: {ex}");
                VanishTempThing(thing);
                return false;
            }
        }

        private static void VanishTempThing(Thing thing)
        {
            if (thing == null)
                return;

            try
            {
                if (thing.Spawned)
                    thing.DeSpawn();
                if (!thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);
            }
            catch
            {
                // already gone
            }
        }

        private static string ValidateMechanitorImplant(StoreItem storeItem, Verse.Pawn pawn, string itemName, int quantity)
        {
            if (!ModLister.BiotechInstalled || storeItem == null || pawn == null)
                return null;

            string def = storeItem.DefName;
            bool isMechlink = string.Equals(def, "Mechlink", StringComparison.OrdinalIgnoreCase);
            bool isControlSublink = def == "ControlSublink";
            bool isControlSublinkHigh = def == "ControlSublinkHigh";
            bool isMechFormfeeder = def == "MechFormfeeder";
            bool isRemoteRepairer = def == "RemoteRepairer";
            bool isRemoteShielder = def == "RemoteShielder";
            bool isRepairProbe = def == "RepairProbe";

            if (!isMechlink && !isControlSublink && !isControlSublinkHigh && !isMechFormfeeder &&
                !isRemoteRepairer && !isRemoteShielder && !isRepairProbe)
                return null;

            TraitDef psychicallyDeafDef = DefDatabase<TraitDef>.GetNamedSilentFail("PsychicallyDeaf");
            if (psychicallyDeafDef != null && (pawn.story?.traits?.HasTrait(psychicallyDeafDef) ?? false))
                return "RICS.UICH.Mechanitor.PsychicallyDeaf".Translate();

            var hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs == null)
                return null;

            if (isMechlink)
            {
                bool alreadyHas = hediffs.Any(h =>
                    h?.def != null &&
                    (h.def.defName == "MechlinkImplant" ||
                     string.Equals(h.def.defName, "Mechlink", StringComparison.OrdinalIgnoreCase) ||
                     h is Hediff_Mechlink));

                if (!alreadyHas)
                {
                    try
                    {
                        alreadyHas = MechanitorUtility.IsMechanitor(pawn);
                    }
                    catch
                    {
                        // ignore if utility unavailable
                    }
                }

                if (alreadyHas)
                    return "RICS.UICH.Mechanitor.AlreadyHasMechlink".Translate();

                if (quantity > 1)
                    return "RICS.UICH.Mechanitor.MechlinkOneOnly".Translate();

                return null;
            }

            if (isControlSublink || isControlSublinkHigh)
            {
                var sublinkHediff = hediffs.FirstOrDefault(h => h.def?.defName == "ControlSublinkImplant") as Hediff_Level;
                int currentLevel = sublinkHediff?.level ?? 0;

                if (isControlSublink)
                    return CheckLevelLimit(itemName, quantity, currentLevel, maxAllowed: 3);

                if (currentLevel < 3)
                    return "RICS.UICH.Mechanitor.StandardRequired".Translate(itemName, currentLevel);

                return CheckLevelLimit(itemName, quantity, currentLevel, maxAllowed: 6);
            }

            if (isMechFormfeeder)
            {
                int currentLevel = (hediffs.FirstOrDefault(h => h.def?.defName == "MechFormfeederImplant") as Hediff_Level)?.level ?? 0;
                return CheckLevelLimit(itemName, quantity, currentLevel, maxAllowed: 6);
            }

            if (isRemoteRepairer)
            {
                int currentLevel = (hediffs.FirstOrDefault(h => h.def?.defName == "RemoteRepairerImplant") as Hediff_Level)?.level ?? 0;
                return CheckLevelLimit(itemName, quantity, currentLevel, maxAllowed: 3);
            }

            if (isRemoteShielder)
            {
                int currentLevel = (hediffs.FirstOrDefault(h => h.def?.defName == "RemoteShielderImplant") as Hediff_Level)?.level ?? 0;
                return CheckLevelLimit(itemName, quantity, currentLevel, maxAllowed: 3);
            }

            if (isRepairProbe)
            {
                int currentLevel = (hediffs.FirstOrDefault(h => h.def?.defName == "RepairProbeImplant") as Hediff_Level)?.level ?? 0;
                return CheckLevelLimit(itemName, quantity, currentLevel, maxAllowed: 6);
            }

            return null;
        }

        private static string CheckLevelLimit(string itemName, int quantity, int currentLevel, int maxAllowed)
        {
            if (currentLevel >= maxAllowed)
            {
                return "RICS.UICH.Mechanitor.MaxReached".Translate(
                    itemName,
                    maxAllowed,
                    currentLevel);
            }

            if (quantity > 1 && (currentLevel + quantity) > maxAllowed)
            {
                int availableSlots = maxAllowed - currentLevel;
                return "RICS.UICH.Mechanitor.ExceedsLimit".Translate(
                    quantity,
                    itemName,
                    availableSlots,
                    currentLevel,
                    maxAllowed);
            }

            return null;
        }

        private static void AwardUseKarma(Viewer viewer, int totalCost, float karmaPerStoreItem)
        {
            if (viewer == null || totalCost <= 0)
                return;

            float karmaEarned = totalCost * karmaPerStoreItem / 100f;
            if (karmaEarned > 0f)
                viewer.GiveKarma(karmaEarned);
        }
    }
}
