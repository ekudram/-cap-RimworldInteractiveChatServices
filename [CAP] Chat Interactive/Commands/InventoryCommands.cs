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
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Commands.CommandHandlers;
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Utilities;
using RimWorld;
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
                return "RICS.CC.Buy.Usage".Translate();
            }

            // Global store kill-switch (respects the setting you already have in CAPGlobalChatSettings)
            var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (globalSettings != null && !globalSettings.StoreCommandsEnabled)
            {
                return "RICS.CC.Buy.StoreDisabled".Translate(); // "Store commands are currently disabled."
            }

            string subCommand = args[0].ToLowerInvariant();

            // Check if this is a pawn purchase
            if (subCommand == "pawn")
            {
                if (!SubCommandEnabled("pawn"))
                {
                    return "RICS.CC.Buy.SubCommandDisabled".Translate("pawn");
                }
                var pawnArgs = args.Skip(1).ToArray();
                var pawnCommand = new Pawn();
                return pawnCommand.Execute(messageWrapper, pawnArgs);
            }

            // Check if this is an event purchase
            if (subCommand == "event")
            {
                if (!SubCommandEnabled("event"))
                {
                    return "RICS.CC.Buy.SubCommandDisabled".Translate("event");
                }
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Event();
                return newCommand.Execute(messageWrapper, newArgs);
            }

            // check if this is a weather purchase
            if (subCommand == "weather")
            {
                if (!SubCommandEnabled("weather"))
                {
                    return "RICS.CC.Buy.SubCommandDisabled".Translate("weather");
                }
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Weather();
                return newCommand.Execute(messageWrapper, newArgs);
            }

            if (subCommand == "use")
            {
                if (!SubCommandEnabled("use"))
                {
                    return "RICS.CC.Buy.SubCommandDisabled".Translate("use");
                }
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Use();
                return newCommand.Execute(messageWrapper, newArgs);
            }

            if (subCommand == "equip")
            {
                if (!SubCommandEnabled("equip"))
                {
                    return "RICS.CC.Buy.SubCommandDisabled".Translate("equip");
                }
                var newArgs = args.Skip(1).ToArray();
                var newCommand = new Equip();
                return newCommand.Execute(messageWrapper, newArgs);
            }

            if (subCommand == "wear")
            {
                if (!SubCommandEnabled("wear"))
                {
                    return "RICS.CC.Buy.SubCommandDisabled".Translate("wear");
                }
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

                if (!cooldownManager.CanPurchaseItem())
                {
                    return "RICS.CC.Buy.PurchaseLimit".Translate(globalSettings.MaxItemPurchases, globalSettings.EventCooldownDays);
                }

                return BuyItemCommandHandler.HandleBuyItem(messageWrapper, args, false, false, false);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in buy command: {ex}");
                return $"Error purchasing item: {ex.Message}";
            }
        }
        private bool SubCommandEnabled(string commandName)
        {
            if (string.IsNullOrEmpty(commandName)) return false;

            try
            {
                var cmdSettings = CommandSettingsManager.GetSettings(commandName.ToLowerInvariant());
                return cmdSettings?.Enabled ?? true; // fail-open if no settings entry (prevents accidental lockout)
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to check enabled status for subcommand '{commandName}': {ex.Message}");
                return true; // graceful degradation
            }
        }
    }

    public class Use : ChatCommand
    {
        public override string Name => "use";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args.Length == 0)
            {
                // return "Usage: !use [item] ";
                return "RICS.CC.use.usage".Translate();
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
                // return $"Store purchase limit reached ({globalSettings.MaxItemPurchases} per {globalSettings.EventCooldownDays} days)";
                return "RICS.CC.common.PurchaseLimit".Translate(globalSettings.MaxItemPurchases, globalSettings.EventCooldownDays);
            }
            return UseItemCommandHandler.HandleUseItem(messageWrapper, args);
        }
    }

    public class Equip : ChatCommand
    {
        public override string Name => "equip";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args.Length == 0)
            {
                // return "Usage: !equip [item] [quality] [material]";
                return "RICS.CC.equip.usage".Translate() ;
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
                return "RICS.CC.common.PurchaseLimit".Translate(globalSettings.MaxItemPurchases, globalSettings.EventCooldownDays);
            }

            return BuyItemCommandHandler.HandleBuyItem(messageWrapper, args, true, false);
        }
    }

    public class Wear : ChatCommand
    {
        public override string Name => "wear";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args.Length == 0)
            {
                return "RICS.CC.wear.usage".Translate();
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
                return "RICS.CC.common.PurchaseLimit".Translate(globalSettings.MaxItemPurchases, globalSettings.EventCooldownDays);
            }
            return BuyItemCommandHandler.HandleBuyItem(messageWrapper, args, false, true);
        }
    }

    public class Backpack : ChatCommand
    {
        public override string Name => "backpack";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args.Length == 0)
            {
                return "RICS.CC.backpack.usage".Translate();
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
                return "RICS.CC.common.PurchaseLimit".Translate(globalSettings.MaxItemPurchases, globalSettings.EventCooldownDays);
            }
            return BuyItemCommandHandler.HandleBuyItem(messageWrapper, args, false, false, true);
        }
    }

    public class PurchaseList : ChatCommand
    {
        public override string Name => "purchaselist";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            // return $"Check out the item prices and purchase list here: {settings.priceListUrl}";
            return "RICS.CC.purchaselist.message".Translate(settings.priceListUrl);
        }
    }

    public class PriceCheck : ChatCommand
    {
        public override string Name => "pricecheck";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            // Delegated to handler (maintainability + new armor/weapon stats feature)
            return CommandHandlerPriceCheck.HandlePriceCheck(messageWrapper, args);
        }
    }

    public class Surgery : ChatCommand
    {
        public override string Name => "surgery";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args.Length == 0)
            {
                //return "Usage: !surgery [implant/BiotechSurgery] [left/right] [quantity] https://tinyurl.com/SurgeryCmdWiki";
                return "RICS.CC.surgery.usage".Translate();
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
                return "RICS.CC.common.PurchaseLimit".Translate(globalSettings.MaxItemPurchases, globalSettings.EventCooldownDays);
            }
            return SurgeryItemCommandHandler.HandleSurgery(messageWrapper, args);
        }
    }
}