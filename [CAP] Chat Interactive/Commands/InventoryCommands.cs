// InventoryCommands.cs
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
// Commands for purchasing and using items from the in-game store.
using CAP_ChatInteractive.Commands.CommandHandlers;
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Utilities;
using System;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    public class Buy : ChatCommand
    {
        public override string Name => "buy";
        
        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !buy <item> [quality] [material] [quantity], only used for items.  Also !use !equip !wear";
            }

            // Check if this is a pawn purchase
            if (args[0].ToLower() == "pawn")
            {
                var pawnArgs = args.Skip(1).ToArray();
                var pawnCommand = new Pawn();
                return pawnCommand.Execute(messageWrapper, pawnArgs);
            }

            // Check if this is a event purchase
            if (args[0].ToLower() == "event")
            {
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Event();
                return newCommand.Execute(messageWrapper, newArgs);
            }
            // check if this is a weather purchase
            if (args[0].ToLower() == "weather")
            {
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Weather();
                return newCommand.Execute(messageWrapper, newArgs);
            }
            
            if (args[0].ToLower() == "use")
            {
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Use();
                return newCommand.Execute(messageWrapper, newArgs);
            }

            if (args[0].ToLower() == "equip")
            {
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Equip();
                return newCommand.Execute(messageWrapper, newArgs);
            }

            if (args[0].ToLower() == "wear")
            {
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Wear();
                return newCommand.Execute(messageWrapper, newArgs);
            }


            try
            {
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager == null)
                {
                    cooldownManager = new GlobalCooldownManager(Current.Game);
                    Current.Game.components.Add(cooldownManager);
                }
                var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

                if (!cooldownManager.CanPurchaseItem())
                {
                  return $"Store purchase limit reached ({globalSettings.MaxItemPurchases} per {globalSettings.EventCooldownDays} days)";
                }

                return BuyItemCommandHandler.HandleBuyItem(messageWrapper, args, false, false,false);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in buy command: {ex}");
                return $"Error purchasing item: {ex.Message}";
            }
        }
    }

    public class Use : ChatCommand
    {
        public override string Name => "use";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !use [item] ";
            }
            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (cooldownManager == null)
            {
                cooldownManager = new GlobalCooldownManager(Current.Game);
                Current.Game.components.Add(cooldownManager);
            }
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            if (!cooldownManager.CanPurchaseItem())
            {
               return $"Store purchase limit reached ({globalSettings.MaxItemPurchases} per {globalSettings.EventCooldownDays} days)";
            }
            return UseItemCommandHandler.HandleUseItem(user, args);
        }
    }

    public class Equip : ChatCommand
    {
        public override string Name => "equip";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !equip [item] [quality] [material]";
            }

            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (cooldownManager == null)
            {
                cooldownManager = new GlobalCooldownManager(Current.Game);
                Current.Game.components.Add(cooldownManager);
            }
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            if (!cooldownManager.CanPurchaseItem())
            {
                return $"Store purchase limit reached ({globalSettings.MaxItemPurchases} per {globalSettings.EventCooldownDays} days)";
            }

            return BuyItemCommandHandler.HandleBuyItem(user, args, true, false);
        }
    }

    public class Wear : ChatCommand
    {
        public override string Name => "wear";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !wear [item] [quality] [material]";
            }

            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (cooldownManager == null)
            {
                cooldownManager = new GlobalCooldownManager(Current.Game);
                Current.Game.components.Add(cooldownManager);
            }
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            if (!cooldownManager.CanPurchaseItem())
            {
                return $"Store purchase limit reached ({globalSettings.MaxItemPurchases} per {globalSettings.EventCooldownDays} days)";
            }
            return BuyItemCommandHandler.HandleBuyItem(user, args, false, true);
        }
    }

    public class Backpack : ChatCommand
    {
        public override string Name => "backpack";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !backpack [item] [quality] [material]";
            }
            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (cooldownManager == null)
            {
                cooldownManager = new GlobalCooldownManager(Current.Game);
                Current.Game.components.Add(cooldownManager);
            }
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            if (!cooldownManager.CanPurchaseItem())
            {
                return $"Store purchase limit reached ({globalSettings.MaxItemPurchases} per {globalSettings.EventCooldownDays} days)";
            }
            return BuyItemCommandHandler.HandleBuyItem(user, args, false, false, true);
        }
    }

    public class PurchaseList : ChatCommand
    {
        public override string Name => "purchaselist";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            return $"Check out the item prices and purchase list here: {settings.priceListUrl}";
        }
    }

    public class PriceCheck : ChatCommand
    {
        public override string Name => "pricecheck";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !pricecheck [item] [quality] [material] [quantity] - Example: !pricecheck assault rifle masterwork 1";
            }
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
            try
            {
                // Use the CommandParserUtility for consistent argument parsing
                var parsed = CommandParserUtility.ParseCommandArguments(
                    args,
                    allowQuality: true,
                    allowMaterial: true,
                    allowSide: false,  // Side doesn't affect price for non-surgery items
                    allowQuantity: true
                );

                if (parsed.HasError)
                {
                    return $"❌ {parsed.Error}";
                }

                Logger.Debug($"PriceCheck parsing - Item: '{parsed.ItemName}', Quality: '{parsed.Quality}', Material: '{parsed.Material}', Quantity: {parsed.Quantity}");

                var storeItem = StoreCommandHelper.GetStoreItemByName(parsed.ItemName);
                if (storeItem == null)
                {
                    // Try to find similar items for helpful suggestions
                    //var suggestions = CAP_ChatInteractive.Store.StoreItemsDatabase.FindSimilarItems(parsed.ItemName, 3);
                    //if (suggestions.Any())
                    //{
                    //    var suggestionList = string.Join(", ", suggestions.Select(s => s.DisplayName));
                    //    return $"❌ Item '{parsed.ItemName}' not found. Did you mean: {suggestionList}?";
                    //}
                    return $"❌ Item '{parsed.ItemName}' not found in the store.";
                }

                // Get the ThingDef for price calculation
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    return $"❌ Could not find item definition for '{parsed.ItemName}'";
                }

                // Parse quality using ItemConfigHelper
                var quality = ItemConfigHelper.ParseQuality(parsed.Quality);

                // Parse material if specified
                ThingDef material = null;
                if (parsed.Material != null && !parsed.Material.Equals("random", StringComparison.OrdinalIgnoreCase))
                {
                    material = ItemConfigHelper.ParseMaterial(parsed.Material, thingDef);
                    if (material == null)
                    {
                        return $"❌ Material '{parsed.Material}' not found or cannot be used for {parsed.ItemName}";
                    }
                }

                // Check if quality is allowed based on settings
                if (quality.HasValue && !ItemConfigHelper.IsQualityAllowed(quality))
                {
                    if (settings != null)
                    {
                        return $"❌ {quality.Value} quality is disabled in settings";
                    }
                }

                // Calculate the price using the same logic as the buy command
                int price = ItemConfigHelper.CalculateFinalPrice(
                    storeItem,
                    parsed.Quantity,
                    quality,
                    material
                );

                // Build a clear response message
                string qualityStr = quality.HasValue ? quality.Value.ToString().ToLower() : "normal";
                string materialStr = material != null ? material.label : "default";
                string quantityStr = parsed.Quantity > 1 ? $"{parsed.Quantity}x " : "";

                return $"💰 Price Check: {quantityStr}{storeItem.CustomName} ({qualityStr}, {materialStr}) = {price} {currencySymbol}";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in pricecheck command: {ex}");
                return $"❌ Error calculating price: {ex.Message}";
            }
        }
    }

    public class Surgery : ChatCommand
    {
        public override string Name => "surgery";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !surgery [implant] [left/right] [quantity] - Example: !surgery bionic arm left 1";
            }
            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                           if (cooldownManager == null)
                {
                    cooldownManager = new GlobalCooldownManager(Current.Game);
                    Current.Game.components.Add(cooldownManager);
                }
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            if (!cooldownManager.CanPurchaseItem())
            {
                return $"Store purchase limit reached ({globalSettings.MaxItemPurchases} per {globalSettings.EventCooldownDays} days)";
            }
            return SurgeryItemCommandHandler.HandleSurgery(user, args);
        }
    }
}