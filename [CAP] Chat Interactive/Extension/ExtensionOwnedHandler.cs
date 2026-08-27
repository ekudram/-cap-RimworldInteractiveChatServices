// ExtensionOwnedHandler.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge
// Structured owned-gear list + unclaim for the Viewer Hub. Not the chat processor.

using CAP_ChatInteractive.Ownership;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Extension
{
    public static class ExtensionOwnedHandler
    {
        public static string HandleGet(ExtensionJob job)
        {
            Pawn pawn = ExtensionViewerContext.TryGetAssignedPawn(job, out string err);
            if (err != null)
                return err;

            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
            {
                return ExtensionEnvelope.Ok(new
                {
                    ownershipActive = false,
                    hasPawn = pawn != null,
                    pawn = PawnDto(pawn),
                    items = Array.Empty<object>(),
                    message = "RICS.MPCH.Owned.Disabled".Translate().ToString()
                });
            }

            if (pawn == null || pawn.Destroyed)
            {
                return ExtensionEnvelope.Ok(new
                {
                    ownershipActive = true,
                    hasPawn = false,
                    pawn = (object)null,
                    items = Array.Empty<object>(),
                    message = "RICS.Pawn.NoPawn".Translate().ToString()
                });
            }

            var all = RICS_OwnedItemsCollector.CollectForPawn(pawn);
            var ordered = RICS_OwnedItemsCollector.WeaponsSorted(all)
                .Concat(RICS_OwnedItemsCollector.ApparelSorted(all))
                .ToList();

            var items = new List<object>(ordered.Count);
            foreach (var item in ordered)
            {
                if (item?.Thing == null || item.Thing.Destroyed)
                    continue;
                items.Add(ToDto(item));
            }

            return ExtensionEnvelope.Ok(new
            {
                ownershipActive = true,
                hasPawn = true,
                pawn = PawnDto(pawn),
                items,
                count = items.Count,
                message = items.Count == 0
                    ? "RICS.MPCH.Disown.NoneOwned".Translate().ToString()
                    : "RICS.MPCH.Owned.Header".Translate(pawn.LabelShortCap).ToString()
            });
        }

        public static string HandleDisown(ExtensionJob job)
        {
            Pawn pawn = ExtensionViewerContext.TryGetAssignedPawn(job, out string err);
            if (err != null)
                return err;

            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return ExtensionEnvelope.Fail("OwnershipOff", "RICS.MPCH.Owned.Disabled".Translate());

            if (pawn == null || pawn.Destroyed)
                return ExtensionEnvelope.Fail("NoPawn", "RICS.Pawn.NoPawn".Translate());

            int thingId = ParseThingId(job?.Body);
            if (thingId <= 0)
                return ExtensionEnvelope.Fail("BadRequest", "Missing item id.");

            var all = RICS_OwnedItemsCollector.CollectForPawn(pawn);
            var match = all.FirstOrDefault(i => i.Thing != null && i.Thing.thingIDNumber == thingId);
            if (match?.Thing == null || match.Thing.Destroyed)
                return ExtensionEnvelope.Fail("Gone", "RICS.MPCH.Disown.Gone".Translate());

            var owner = RICS_OwnershipUtility.GetOwner(match.Thing);
            if (owner != pawn)
                return ExtensionEnvelope.Fail("NotYours", "RICS.MPCH.Disown.NotFound".Translate(match.Thing.LabelNoCount));

            string label = match.Thing.LabelNoCount;
            if (!RICS_OwnershipUtility.ClearOwner(match.Thing, "extension unclaim"))
                return ExtensionEnvelope.Fail("Failed", "RICS.MPCH.Disown.Failed".Translate(label));

            return ExtensionEnvelope.Ok(new
            {
                id = thingId,
                panelOnly = true,
                message = "RICS.MPCH.Disown.Ok".Translate(label).ToString()
            });
        }

        private static int ParseThingId(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return 0;
            try
            {
                var jo = JObject.Parse(body);
                int? id = jo.Value<int?>("id") ?? jo.Value<int?>("thingId");
                return id ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private static object PawnDto(Pawn pawn)
        {
            if (pawn == null)
                return null;
            return new
            {
                fullName = pawn.Name?.ToStringFull ?? pawn.LabelCap.ToString(),
                shortName = pawn.LabelShortCap,
                isDead = pawn.Dead,
                isDestroyed = pawn.Destroyed
            };
        }

        private static object ToDto(RICS_OwnedItem item)
        {
            Thing t = item.Thing;
            return new
            {
                id = t.thingIDNumber,
                label = t.LabelCap.ToString(),
                defName = t.def?.defName,
                kind = item.IsWeapon ? "weapon" : (item.IsApparel ? "apparel" : "item"),
                where = item.Where ?? "Unknown",
                quality = item.QualityLabel,
                marketValue = item.MarketValue,
                armor = item.IsApparel
                    ? (object)new
                    {
                        sharp = item.ArmorSharp,
                        blunt = item.ArmorBlunt,
                        heat = item.ArmorHeat,
                        summary = RICS_OwnedItemsCollector.ArmorSummary(item)
                    }
                    : null
            };
        }
    }
}
