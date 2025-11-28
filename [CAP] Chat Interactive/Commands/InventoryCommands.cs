// InventoryCommands.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Commands for purchasing and using items from the in-game store.
using CAP_ChatInteractive.Commands.CommandHandlers;
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Store;
using RimWorld;
using RimWorld.BaseGen;
using RuntimeAudioClipLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    public class Buy : ChatCommand
    {
        public override string Name => "buy";
        
        public override string Execute(ChatMessageWrapper user, string[] args)
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
                return pawnCommand.Execute(user, pawnArgs);
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

                return BuyItemCommandHandler.HandleBuyItem(user, args, false, false,false);
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
                return "Usage: !use <item> ";
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
            return BuyItemCommandHandler.HandleUseItem(user, args);
        }
    }

    public class Equip : ChatCommand
    {
        public override string Name => "equip";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !equip <item> [quality] [material]";
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
                return "Usage: !wear <item> [quality] [material]";
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
                return "Usage: !backpack <item> [quality] [material]";
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

    public class Surgery : ChatCommand
    {
        public override string Name => "surgery";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !surgery <item>";
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
            return BuyItemCommandHandler.HandleSurgery(user, args);
        }
    }
}