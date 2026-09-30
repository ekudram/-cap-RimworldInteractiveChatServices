// File: ExtensionStoreHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Filtered store lists + panel-only buy. Not the full JSON, not public chat.

using _CAP__Chat_Interactive.Command.CommandHelpers;
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Extension
{
    public static class ExtensionStoreHandler
    {
        public static string HandleItems(ExtensionJob job) => ListOrFail(() =>
        {
            var items = StoreInventory.AllStoreItems?.Values
                .Where(i => i != null && i.Enabled && i.modactive && i.BasePrice > 0)
                .Where(i => StoreCommandHelper.HasRequiredResearch(i).Allowed)
                .OrderBy(i => i.CustomName ?? i.DefName, StringComparer.OrdinalIgnoreCase)
                .Select(i => (object)new
                {
                    id = i.DefName,
                    name = Plain(i.CustomName) ?? i.DefName,
                    price = i.BasePrice,
                    max = i.HasQuantityLimit ? i.QuantityLimit : (int?)null,
                    category = i.Category
                })
                .ToList() ?? new List<object>();
            return EnvelopeList("items", items);
        });

        public static string HandleTraits(ExtensionJob job) => ListOrFail(() =>
        {
            string viewerName = ExtensionViewerContext.ResolveViewerName(job);
            Pawn pawn = null;
            if (!string.IsNullOrEmpty(viewerName))
                pawn = CAPChatInteractiveMod.GetPawnAssignmentManager()?.GetAssignedPawn(viewerName);
            if (pawn != null && (pawn.Destroyed || pawn.Dead))
                pawn = null;

            string pawnName = pawn != null
                ? (pawn.LabelShortCap ?? pawn.LabelShort)
                : "Timmy";
            if (string.IsNullOrWhiteSpace(pawnName))
                pawnName = "Timmy";

            int maxTraits = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.MaxTraits ?? 4;
            int traitCount = EffectiveTraitCount(pawn);
            bool hasPawn = pawn != null;

            var items = TraitsManager.AllBuyableTraits?.Values
                .Where(t => t != null && t.modactive && (t.CanAdd || t.CanRemove))
                .OrderBy(t => t.Name ?? t.DefName, StringComparer.OrdinalIgnoreCase)
                .Select(t => TraitRow(t, pawn, pawnName, hasPawn, traitCount, maxTraits))
                .ToList() ?? new List<object>();

            return ExtensionEnvelope.Ok(new
            {
                category = "traits",
                storeCommandsEnabled = CommandUtility.AreStoreCommandsEnabled(),
                pawnName,
                hasPawn,
                traitCount,
                maxTraits,
                items
            });
        });

        public static string HandleEvents(ExtensionJob job) => ListOrFail(() =>
        {
            var items = IncidentsManager.AllBuyableIncidents?.Values
                .Where(e => e != null && e.Enabled && e.modactive && e.ShouldBeInStore && e.IsAvailableForCommands && e.BaseCost > 0)
                .OrderBy(e => e.Label ?? e.DefName, StringComparer.OrdinalIgnoreCase)
                .Select(e => (object)new
                {
                    id = e.DefName,
                    name = Plain(e.Label) ?? e.DefName,
                    price = e.BaseCost,
                    max = 1,
                    karma = e.KarmaType,
                    description = Plain(e.Description)
                })
                .ToList() ?? new List<object>();
            return EnvelopeList("events", items);
        });

        public static string HandleWeather(ExtensionJob job) => ListOrFail(() =>
        {
            var items = BuyableWeatherManager.AllBuyableWeather?.Values
                .Where(w => w != null && w.Enabled && w.modactive && w.BaseCost > 0)
                .OrderBy(w => w.Label ?? w.DefName, StringComparer.OrdinalIgnoreCase)
                .Select(w => (object)new
                {
                    id = w.DefName,
                    name = Plain(w.Label) ?? w.DefName,
                    price = w.BaseCost,
                    max = 1,
                    karma = w.KarmaType,
                    description = Plain(w.Description)
                })
                .ToList() ?? new List<object>();
            return EnvelopeList("weather", items);
        });

        public static string HandleRaces(ExtensionJob job) => ListOrFail(() =>
        {
            bool pawnOn = CommandSettingsManager.GetSettings("pawn")?.Enabled ?? true;
            var races = new List<object>();
            var map = RaceSettingsManager.RaceSettings;
            if (map != null)
            {
                foreach (var kv in map.OrderBy(x => x.Value?.DisplayName ?? x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    RaceSettings rs = kv.Value;
                    if (rs == null || !rs.Enabled || !rs.ModActive)
                        continue;
                    var xenos = new List<object>();
                    if (rs.EnabledXenotypes != null)
                    {
                        foreach (var x in rs.EnabledXenotypes)
                        {
                            if (!x.Value)
                                continue;
                            float price = rs.BasePrice;
                            if (rs.XenotypePrices != null && rs.XenotypePrices.TryGetValue(x.Key, out float xp))
                                price = xp;
                            if (price <= 0)
                                continue;
                            xenos.Add(new { name = x.Key, price = (int)Math.Round(price) });
                        }
                    }
                    if (xenos.Count == 0)
                        continue;
                    races.Add(new
                    {
                        defName = kv.Key,
                        name = Plain(rs.DisplayName) ?? kv.Key,
                        basePrice = rs.BasePrice,
                        xenotypes = xenos
                    });
                }
            }
            return ExtensionEnvelope.Ok(new
            {
                storeCommandsEnabled = CommandUtility.AreStoreCommandsEnabled(),
                pawnCommandEnabled = pawnOn,
                races
            });
        });

        public static string HandleBuy(ExtensionJob job)
        {
            if (!ExtensionViewerContext.TryRequireGame(out string err))
                return err;

            string viewer = ExtensionViewerContext.ResolveViewerName(job);
            if (string.IsNullOrEmpty(viewer))
            {
                return ExtensionEnvelope.Fail(
                    "Unauthorized",
                    "No viewer identity. For LocalHttp send X-RICS-Dev-Viewer or ?viewer=");
            }

            ParseBuy(job?.Body, out string category, out string name, out string id, out int qty, out string xenotype, out string action);
            category = (category ?? "").Trim().ToLowerInvariant();
            string target = string.IsNullOrWhiteSpace(id) ? name : id;
            if (string.IsNullOrWhiteSpace(target))
                return ExtensionEnvelope.Fail("BadRequest", "Missing item name.");
            if (qty < 1)
                qty = 1;
            action = (action ?? "").Trim().ToLowerInvariant();

            bool storeOn = CommandUtility.AreStoreCommandsEnabled();
            if (!storeOn && category != "items")
                return ExtensionEnvelope.Fail("StoreClosed", "Store and interaction commands are currently disabled.");

            string commandId;
            string args;
            switch (category)
            {
                case "items":
                    commandId = "buy";
                    args = qty > 1 ? target + " " + qty : target;
                    break;
                case "traits":
                    commandId = action == "remove" || action == "removetrait" ? "removetrait" : "addtrait";
                    args = string.IsNullOrWhiteSpace(name) ? target : name;
                    break;
                case "events":
                    commandId = "event";
                    args = target;
                    break;
                case "weather":
                    commandId = "weather";
                    args = target;
                    break;
                case "races":
                    commandId = "pawn";
                    string race = string.IsNullOrWhiteSpace(id) ? name : id;
                    args = string.IsNullOrWhiteSpace(xenotype) ? race : race + " " + xenotype;
                    break;
                default:
                    return ExtensionEnvelope.Fail("BadRequest", "Unknown store category.");
            }

            var outcome = ChatCommandProcessor.ProcessExtensionCommand(viewer, commandId, args);
            if (!outcome.Success)
                return ExtensionEnvelope.Fail(outcome.ErrorCode, outcome.Message);

            return ExtensionEnvelope.Ok(new
            {
                panelOnly = true,
                category,
                command = commandId,
                message = outcome.Message
            });
        }

        private static string ListOrFail(Func<string> build)
        {
            if (Current.Game == null)
                return ExtensionEnvelope.Fail("NoGame", "Load a colony first.");
            try
            {
                return build();
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Store: {ex}");
                return ExtensionEnvelope.Fail("StoreUnavailable", "Store data is currently unavailable.");
            }
        }

        private static string EnvelopeList(string category, List<object> items, string pawnName = null)
        {
            return ExtensionEnvelope.Ok(new
            {
                category,
                storeCommandsEnabled = CommandUtility.AreStoreCommandsEnabled(),
                pawnName,
                items
            });
        }

        private static void ParseBuy(string body, out string category, out string name, out string id, out int qty, out string xenotype, out string action)
        {
            category = "";
            name = "";
            id = "";
            qty = 1;
            xenotype = "";
            action = "";
            if (string.IsNullOrWhiteSpace(body))
                return;
            try
            {
                var jo = JObject.Parse(body);
                category = jo.Value<string>("category") ?? "";
                name = jo.Value<string>("name") ?? "";
                id = jo.Value<string>("id") ?? jo.Value<string>("defName") ?? "";
                xenotype = jo.Value<string>("xenotype") ?? "";
                action = jo.Value<string>("action") ?? "";
                qty = jo.Value<int?>("qty") ?? jo.Value<int?>("quantity") ?? 1;
            }
            catch
            {
                /* keep defaults */
            }
        }

        private static object TraitRow(BuyableTrait t, Pawn pawn, string pawnName, bool hasPawn, int traitCount, int maxTraits)
        {
            bool owned = PawnHasTrait(pawn, t);
            string addWhy = null;
            bool addEnabled = false;
            if (!t.CanAdd)
                addWhy = "This trait cannot be added.";
            else if (!hasPawn)
                addWhy = "Need an assigned pawn.";
            else if (owned)
                addWhy = "Pawn already has this trait.";
            else if (traitCount >= maxTraits && !t.BypassLimit)
                addWhy = "At max traits (" + maxTraits + ").";
            else
                addEnabled = true;

            string removeWhy = null;
            bool removeEnabled = false;
            if (!t.CanRemove)
                removeWhy = "This trait cannot be removed.";
            else if (!hasPawn)
                removeWhy = "Need an assigned pawn.";
            else if (!owned)
                removeWhy = "Pawn does not have this trait.";
            else
                removeEnabled = true;

            return new
            {
                id = t.DefName,
                name = Plain(t.Name) ?? t.DefName,
                price = t.AddPrice,
                addPrice = t.AddPrice,
                removePrice = t.RemovePrice,
                canAdd = t.CanAdd,
                canRemove = t.CanRemove,
                bypassLimit = t.BypassLimit,
                owned,
                addEnabled,
                removeEnabled,
                addDisabledReason = addWhy,
                removeDisabledReason = removeWhy,
                max = 1,
                description = ResolveTraitTokens(Plain(t.Description), pawnName, pawn)
            };
        }

        private static bool PawnHasTrait(Pawn pawn, BuyableTrait t)
        {
            if (pawn?.story?.traits?.allTraits == null || t == null)
                return false;
            return pawn.story.traits.allTraits.Any(x =>
                x != null && x.def != null
                && x.def.defName == t.DefName
                && x.Degree == t.Degree);
        }

        private static int EffectiveTraitCount(Pawn pawn)
        {
            if (pawn?.story?.traits?.allTraits == null)
                return 0;
            int counted = 0;
            foreach (var trait in pawn.story.traits.allTraits)
            {
                if (trait?.def == null)
                    continue;
                var buyable = TraitsManager.AllBuyableTraits.Values
                    .FirstOrDefault(bt => bt.DefName == trait.def.defName && bt.Degree == trait.Degree);
                if (buyable == null || !buyable.BypassLimit)
                    counted++;
            }
            return counted;
        }

        private static string ResolveTraitTokens(string text, string pawnName, Pawn pawn)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string name = string.IsNullOrWhiteSpace(pawnName) ? "Timmy" : pawnName.Trim();
            bool female = pawn != null && pawn.gender == Gender.Female;
            bool male = pawn != null && pawn.gender == Gender.Male;
            string pronoun = pawn == null ? "he" : (female ? "she" : male ? "he" : "they");
            string possessive = pawn == null ? "his" : (female ? "her" : male ? "his" : "their");
            string objective = pawn == null ? "him" : (female ? "her" : male ? "him" : "them");

            string[] keys =
            {
                "PAWN_nameDef", "PAWN_name", "PAWN_label", "PAWN_def",
                "PAWN_pronoun", "PAWN_possessive", "PAWN_objective",
                "PANN_nameDef", "PANN_pronoun", "PANN_possessive", "PANN_objective"
            };
            string[] vals =
            {
                name, name, name, name,
                pronoun, possessive, objective,
                name, pronoun, possessive, objective
            };

            for (int i = 0; i < keys.Length; i++)
            {
                text = text.Replace("{" + keys[i] + "}", vals[i]);
                text = text.Replace("[" + keys[i] + "]", vals[i]);
            }
            return text;
        }

        private static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            string s = text;
            try { s = s.StripTags(); } catch { /* keep */ }
            s = Regex.Replace(s, @"<[^>]+>", string.Empty);
            return s.Trim();
        }
    }
}
