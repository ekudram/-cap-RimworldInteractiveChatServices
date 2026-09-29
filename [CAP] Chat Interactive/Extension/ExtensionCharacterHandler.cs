// File: ExtensionCharacterHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Structured character-sheet JSON for the Viewer Hub. Not the chat command processor.

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Extension
{
    public static class ExtensionCharacterHandler
    {
        public static string HandleHeader(ExtensionJob job)
        {
            if (!TryPawn(job, out Pawn pawn, out string err, allowMissing: true))
                return err;

            if (pawn == null)
                return ExtensionEnvelope.Fail("NoPawnAssigned", "You do not have a pawn assigned.");

            Viewer viewer = null;
            try
            {
                string name = ExtensionViewerContext.ResolveViewerName(job);
                if (!string.IsNullOrEmpty(name))
                    viewer = Viewers.GetViewerNoAdd(name);
            }
            catch (Exception ex)
            {
                Logger.Debug($"[RICS Extension] Header viewer lookup: {ex.Message}");
            }

            return ExtensionEnvelope.Ok(new
            {
                pawn = PawnHeaderDto(pawn),
                viewer = new
                {
                    coins = viewer?.Coins ?? 0,
                    karma = viewer != null ? Mathf.RoundToInt(viewer.Karma) : 0
                }
            });
        }

        public static string HandleBodyHealth(ExtensionJob job)
        {
            if (!TryPawn(job, out Pawn pawn, out string err))
                return err;

            try
            {
                int healthPct = HealthPercent(pawn);
                return ExtensionEnvelope.Ok(new
                {
                    healthPercent = healthPct,
                    capacities = Capacities(pawn),
                    bodyParts = BodyParts(pawn),
                    conditions = Conditions(pawn)
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Body/health: {ex}");
                return ExtensionEnvelope.Fail("HandlerError", "Could not read body / health.");
            }
        }

        public static string HandleGear(ExtensionJob job)
        {
            if (!TryPawn(job, out Pawn pawn, out string err))
                return err;

            try
            {
                return ExtensionEnvelope.Ok(new
                {
                    weapons = Weapons(pawn),
                    apparel = Apparel(pawn)
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Gear: {ex}");
                return ExtensionEnvelope.Fail("HandlerError", "Could not read gear.");
            }
        }

        public static string HandleImplants(ExtensionJob job)
        {
            if (!TryPawn(job, out Pawn pawn, out string err))
                return err;

            try
            {
                var groups = (pawn.health?.hediffSet?.hediffs ?? Enumerable.Empty<Hediff>())
                    .Where(h => h != null && h.Visible && IsImplantOrAddedPart(h))
                    .GroupBy(h => h.Part != null ? h.Part.LabelCap.ToString() : "Whole body")
                    .OrderBy(g => g.Key)
                    .Select(g => (object)new
                    {
                        bodyPart = g.Key,
                        items = g.Select(h => (object)new
                        {
                            label = Plain(h.LabelCap),
                            description = Plain(h.def?.description)
                        }).ToList()
                    })
                    .ToList();

                return ExtensionEnvelope.Ok(new { groups });
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Implants: {ex}");
                return ExtensionEnvelope.Fail("HandlerError", "Could not read implants.");
            }
        }

        public static string HandleNeeds(ExtensionJob job)
        {
            if (!TryPawn(job, out Pawn pawn, out string err))
                return err;

            try
            {
                var list = new List<object>();
                var needs = pawn.needs?.AllNeeds;
                if (needs != null)
                {
                    foreach (Need need in needs)
                    {
                        if (need?.def == null || !need.ShowOnNeedList)
                            continue;
                        float pct = Mathf.Clamp01(need.MaxLevel > 0f ? need.CurLevel / need.MaxLevel : 0f);
                        int percent = Mathf.RoundToInt(pct * 100f);
                        list.Add(new
                        {
                            label = Plain(need.LabelCap),
                            percent,
                            status = NeedStatus(pct),
                            emoji = NeedEmoji(need.def.defName, pct)
                        });
                    }
                }

                return ExtensionEnvelope.Ok(new { needs = list });
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Needs: {ex}");
                return ExtensionEnvelope.Fail("HandlerError", "Could not read needs.");
            }
        }

        public static string HandleBackstoriesTraits(ExtensionJob job)
        {
            if (!TryPawn(job, out Pawn pawn, out string err))
                return err;

            try
            {
                string race = pawn.def?.LabelCap.ToString();
                string xenotype = null;
                if (ModsConfig.BiotechActive && pawn.genes != null)
                {
                    xenotype = pawn.genes.XenotypeLabelCap;
                    if (string.IsNullOrEmpty(xenotype))
                        xenotype = pawn.genes.xenotypeName;
                }

                object childhood = null;
                if (pawn.story?.Childhood != null)
                {
                    childhood = new
                    {
                        title = Plain(pawn.story.Childhood.title),
                        description = Plain(pawn.story.Childhood.FullDescriptionFor(pawn))
                    };
                }

                object adulthood = null;
                if (pawn.story?.Adulthood != null)
                {
                    adulthood = new
                    {
                        title = Plain(pawn.story.Adulthood.title),
                        description = Plain(pawn.story.Adulthood.FullDescriptionFor(pawn))
                    };
                }

                string viewerName = ViewerDisplayName(job);
                var traits = new List<object>();
                if (pawn.story?.traits?.allTraits != null)
                {
                    foreach (Trait trait in pawn.story.traits.allTraits)
                    {
                        if (trait?.def == null)
                            continue;
                        bool geneLocked = ModsConfig.BiotechActive && trait.sourceGene != null;
                        string rawDesc = trait.CurrentData?.description ?? trait.def.description;
                        traits.Add(new
                        {
                            label = Plain(trait.LabelCap),
                            geneLocked,
                            description = ResolvePawnTokens(Plain(rawDesc), viewerName, pawn)
                        });
                    }
                }

                return ExtensionEnvelope.Ok(new
                {
                    race,
                    xenotype,
                    childhood,
                    adulthood,
                    traits
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Story: {ex}");
                return ExtensionEnvelope.Fail("HandlerError", "Could not read backstories / traits.");
            }
        }

        public static string HandleStats(ExtensionJob job)
        {
            if (!TryPawn(job, out Pawn pawn, out string err))
                return err;

            try
            {
                var stats = new List<object>
                {
                    StatRow("Kills (humanlike)", RecordInt(pawn, RecordDefOf.KillsHumanlikes).ToString()),
                    StatRow("Kills (animals)", RecordInt(pawn, RecordDefOf.KillsAnimals).ToString()),
                    StatRow("Move speed", pawn.GetStatValue(StatDefOf.MoveSpeed).ToString("0.00") + " c/s"),
                    StatRow("Incoming damage multiplier", Pct(pawn.GetStatValue(StatDefOf.IncomingDamageFactor))),
                    StatRow("Toxic resistance", Pct(pawn.GetStatValue(StatDefOf.ToxicResistance))),
                    StatRow("Vacuum resistance", StatDefOf.VacuumResistance != null
                        ? Pct(pawn.GetStatValue(StatDefOf.VacuumResistance))
                        : "—"),
                    StatRow("Pain shock threshold", Pct(NamedStat(pawn, "PainShockThreshold"))),
                    StatRow("Mental break threshold", Pct(pawn.GetStatValue(StatDefOf.MentalBreakThreshold)))
                };

                return ExtensionEnvelope.Ok(new { stats });
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Stats: {ex}");
                return ExtensionEnvelope.Fail("HandlerError", "Could not read stats.");
            }
        }

        public static string HandleRelations(ExtensionJob job)
        {
            if (!TryPawn(job, out Pawn pawn, out string err))
                return err;

            try
            {
                var familyRows = new List<RelRow>();
                var friendRows = new List<RelRow>();
                var rivalRows = new List<RelRow>();

                if (pawn.relations != null)
                {
                    var familySet = new HashSet<Pawn>();
                    foreach (Pawn other in pawn.relations.RelatedPawns)
                    {
                        if (other == null || other == pawn)
                            continue;
                        var rel = pawn.GetMostImportantRelation(other);
                        if (rel == null)
                            continue;
                        bool isFamily = rel.familyByBloodRelation
                            || rel == PawnRelationDefOf.Spouse
                            || rel == PawnRelationDefOf.Lover
                            || rel == PawnRelationDefOf.Fiance
                            || rel == PawnRelationDefOf.ExSpouse
                            || rel == PawnRelationDefOf.ExLover;
                        if (!isFamily)
                            continue;
                        familySet.Add(other);
                        familyRows.Add(MakeRel(pawn, other, rel.GetGenderSpecificLabelCap(other)));
                    }

                    var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                    IEnumerable<Pawn> pool = mgr?.GetAllViewerPawns()
                        ?? pawn.Map?.mapPawns?.FreeColonists
                        ?? Enumerable.Empty<Pawn>();

                    foreach (Pawn other in pool)
                    {
                        if (other == null || other == pawn || familySet.Contains(other))
                            continue;
                        int op = pawn.relations.OpinionOf(other);
                        if (op > 10)
                            friendRows.Add(MakeRel(pawn, other, "Friend"));
                        else if (op < -10)
                            rivalRows.Add(MakeRel(pawn, other, "Rival"));
                    }
                }

                return ExtensionEnvelope.Ok(new
                {
                    family = familyRows,
                    friends = friendRows.OrderByDescending(r => r.opinion).Take(10).ToList(),
                    rivals = rivalRows.OrderBy(r => r.opinion).Take(10).ToList()
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Relations: {ex}");
                return ExtensionEnvelope.Fail("HandlerError", "Could not read relations.");
            }
        }

        private sealed class RelRow
        {
            public string name;
            public string relation;
            public int opinion;
        }

        private static RelRow MakeRel(Pawn me, Pawn other, string relation)
        {
            int opinion = 0;
            try { opinion = me.relations.OpinionOf(other); } catch { /* ignore */ }
            return new RelRow
            {
                name = other.Name?.ToStringShort ?? other.LabelShortCap,
                relation = Plain(relation),
                opinion = opinion
            };
        }

        private static bool TryPawn(ExtensionJob job, out Pawn pawn, out string errorJson, bool allowMissing = false)
        {
            pawn = ExtensionViewerContext.TryGetAssignedPawn(job, out errorJson);
            if (errorJson != null)
                return false;
            if (pawn == null || pawn.Destroyed)
            {
                if (allowMissing)
                {
                    pawn = null;
                    return true;
                }
                errorJson = ExtensionEnvelope.Fail("NoPawnAssigned", "You do not have a pawn assigned.");
                return false;
            }
            return true;
        }

        private static object PawnHeaderDto(Pawn pawn)
        {
            string gender;
            string emoji;
            switch (pawn.gender)
            {
                case Gender.Male:
                    gender = "Male";
                    emoji = "♂";
                    break;
                case Gender.Female:
                    gender = "Female";
                    emoji = "♀";
                    break;
                default:
                    gender = "None";
                    emoji = "⚧";
                    break;
            }

            int age = 0;
            try { age = pawn.ageTracker?.AgeBiologicalYears ?? 0; } catch { /* ignore */ }

            return new
            {
                fullName = pawn.Name?.ToStringFull ?? pawn.LabelCap.ToString(),
                shortName = pawn.LabelShortCap,
                gender,
                genderEmoji = emoji,
                ageBiological = age,
                healthPercent = HealthPercent(pawn),
                isDead = pawn.Dead,
                isDestroyed = pawn.Destroyed,
                race = pawn.def?.LabelCap.ToString(),
                xenotype = ModsConfig.BiotechActive && pawn.genes != null
                    ? (pawn.genes.XenotypeLabelCap ?? pawn.genes.xenotypeName)
                    : null
            };
        }

        private static int HealthPercent(Pawn pawn)
        {
            try
            {
                if (pawn.health?.summaryHealth == null)
                    return 100;
                return Mathf.RoundToInt(pawn.health.summaryHealth.SummaryHealthPercent * 100f);
            }
            catch
            {
                return 100;
            }
        }

        private static List<object> Capacities(Pawn pawn)
        {
            var defs = new List<PawnCapacityDef>
            {
                PawnCapacityDefOf.Consciousness,
                PawnCapacityDefOf.Moving,
                PawnCapacityDefOf.Manipulation,
                PawnCapacityDefOf.Talking,
                DefDatabase<PawnCapacityDef>.GetNamedSilentFail("Eating"),
                PawnCapacityDefOf.Sight,
                PawnCapacityDefOf.Hearing,
                PawnCapacityDefOf.Breathing,
                PawnCapacityDefOf.BloodFiltration,
                PawnCapacityDefOf.BloodPumping,
                DefDatabase<PawnCapacityDef>.GetNamedSilentFail("Metabolism")
            };

            var list = new List<object>();
            if (pawn.health?.capacities == null)
                return list;

            foreach (PawnCapacityDef def in defs)
            {
                if (def == null)
                    continue;
                float level = pawn.health.capacities.GetLevel(def);
                list.Add(new
                {
                    label = Plain(def.LabelCap),
                    value = Math.Round(level, 2),
                    percent = Mathf.RoundToInt(level * 100f)
                });
            }
            return list;
        }

        private static List<object> BodyParts(Pawn pawn)
        {
            var list = new List<object>();
            var parts = pawn.RaceProps?.body?.AllParts;
            if (parts == null || pawn.health?.hediffSet == null)
                return list;

            foreach (BodyPartRecord part in parts
                .Where(p => p?.def != null)
                .OrderByDescending(p => p.height)
                .ThenByDescending(p => p.coverageAbsWithChildren))
            {
                bool missing = pawn.health.hediffSet.PartIsMissing(part);
                float max = part.def.GetMaxHealth(pawn);
                float cur = missing ? 0f : pawn.health.hediffSet.GetPartHealth(part);
                int integrity = max > 0f ? Mathf.RoundToInt(cur / max * 100f) : (missing ? 0 : 100);
                list.Add(new
                {
                    part = part.LabelCap.ToString(),
                    integrity,
                    status = PartStatus(pawn, part, missing)
                });
            }
            return list;
        }

        private static string PartStatus(Pawn pawn, BodyPartRecord part, bool missing)
        {
            if (missing)
                return "Missing";

            var onPart = pawn.health.hediffSet.hediffs
                .Where(h => h != null && h.Part == part && h.Visible)
                .ToList();

            if (onPart.Any(IsImplantOrAddedPart))
                return "Bionic";
            if (onPart.Any(h => h is Hediff_Injury && h.IsPermanent()))
                return "Scar";
            if (onPart.Any(h => h.def != null && h.def.isBad))
                return "Injured";
            return "Healthy";
        }

        private static List<object> Conditions(Pawn pawn)
        {
            var list = new List<object>();
            if (pawn.health?.hediffSet?.hediffs == null)
                return list;

            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h == null || !h.Visible || h.def == null)
                    continue;
                if (h is Hediff_MissingPart)
                    continue;
                if (IsImplantOrAddedPart(h) && !h.def.isBad)
                    continue;
                if (!h.def.isBad && !(h is Hediff_Injury))
                    continue;

                string severity;
                if (!h.def.isBad)
                    severity = "Positive";
                else if (h.Severity < 0.25f)
                    severity = "Minor";
                else if (h.def.lethalSeverity > 0f && h.Severity >= h.def.lethalSeverity * 0.7f)
                    severity = "Severe";
                else
                    severity = "Moderate";

                list.Add(new
                {
                    label = Plain(h.LabelCap),
                    severity,
                    description = Plain(h.def.description)
                });
            }
            return list;
        }

        private static List<object> Weapons(Pawn pawn)
        {
            var list = new List<object>();
            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary != null)
                list.Add(WeaponDto(primary));
            return list;
        }

        private static object WeaponDto(Thing weapon)
        {
            string damage = null;
            string range = null;
            try
            {
                VerbProperties vp = weapon.def?.Verbs?.FirstOrDefault(v => v != null && !v.IsMeleeAttack);
                if (vp != null)
                {
                    range = vp.range.ToString("0.#");
                    if (vp.defaultProjectile?.projectile != null)
                    {
                        int dmg = vp.defaultProjectile.projectile.GetDamageAmount(weapon);
                        damage = dmg + " dmg";
                    }
                }
                else
                {
                    float dps = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                    damage = dps.ToString("0.0") + " DPS";
                }
            }
            catch { /* optional stats */ }

            string quality = null;
            if (weapon.TryGetQuality(out QualityCategory q))
                quality = q.GetLabel().CapitalizeFirst();

            return new
            {
                label = ItemLabel(weapon, quality),
                quality,
                wornPercent = (int?)null,
                damage,
                range,
                traits = Array.Empty<object>(),
                bodyPartsCovered = (string)null
            };
        }

        private static List<object> Apparel(Pawn pawn)
        {
            var list = new List<object>();
            var worn = pawn.apparel?.WornApparel;
            if (worn == null)
                return list;

            foreach (Apparel item in worn)
            {
                if (item == null)
                    continue;
                string quality = null;
                if (item.TryGetQuality(out QualityCategory q))
                    quality = q.GetLabel().CapitalizeFirst();

                int? wornPct = null;
                if (item.MaxHitPoints > 0)
                    wornPct = Mathf.RoundToInt(item.HitPoints / (float)item.MaxHitPoints * 100f);

                string covered = null;
                if (item.def?.apparel?.bodyPartGroups != null && item.def.apparel.bodyPartGroups.Count > 0)
                {
                    covered = string.Join(", ",
                        item.def.apparel.bodyPartGroups.Select(g => g.LabelCap.ToString()));
                }

                list.Add(new
                {
                    label = ItemLabel(item, quality),
                    quality,
                    wornPercent = wornPct,
                    bodyPartsCovered = Plain(covered),
                    traits = Array.Empty<object>()
                });
            }
            return list;
        }

        private static bool IsImplantOrAddedPart(Hediff hediff)
        {
            if (hediff?.def == null)
                return false;
            Type cls = hediff.def.hediffClass;
            if (cls != null)
            {
                if (typeof(Hediff_AddedPart).IsAssignableFrom(cls))
                    return true;
                if (typeof(Hediff_Implant).IsAssignableFrom(cls))
                    return true;
            }
            return hediff.def.spawnThingOnRemoved != null && (hediff is Hediff_AddedPart || hediff is Hediff_Implant);
        }

        private static string NeedStatus(float pct)
        {
            if (pct >= 0.9f) return "Excellent";
            if (pct >= 0.7f) return "Good";
            if (pct >= 0.5f) return "OK";
            if (pct >= 0.3f) return "Low";
            if (pct >= 0.1f) return "Very low";
            return "Critical";
        }

        private static string NeedEmoji(string defName, float pct)
        {
            string key = (defName ?? "").ToLowerInvariant();
            if (key.Contains("food") || key.Contains("hunger")) return pct >= 0.3f ? "🍽" : "🍴";
            if (key.Contains("rest") || key.Contains("sleep")) return "💤";
            if (key.Contains("joy") || key.Contains("recreation")) return "🎮";
            if (key.Contains("beauty")) return "✨";
            if (key.Contains("comfort")) return "🛋";
            if (key.Contains("outdoors")) return "🌤";
            if (key.Contains("mood")) return "🙂";
            return "";
        }

        private static object StatRow(string label, string value) => new { label, value };

        private static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private static float NamedStat(Pawn pawn, string defName)
        {
            var def = DefDatabase<StatDef>.GetNamedSilentFail(defName);
            return def == null ? 0f : pawn.GetStatValue(def);
        }

        private static int RecordInt(Pawn pawn, RecordDef def)
        {
            if (pawn.records == null || def == null)
                return 0;
            return pawn.records.GetAsInt(def);
        }

        private static string ViewerDisplayName(ExtensionJob job)
        {
            string name = ExtensionViewerContext.ResolveViewerName(job);
            if (string.IsNullOrEmpty(name))
                return name;
            try
            {
                Viewer v = Viewers.GetViewerNoAdd(name);
                if (!string.IsNullOrWhiteSpace(v?.DisplayName))
                    return v.DisplayName;
            }
            catch { /* username is enough */ }
            return name;
        }

        /// <summary>
        /// Grammar tokens in trait XML. Name uses the Twitch viewer (fun), pronouns follow pawn gender.
        /// Same token set as the store / traits editor.
        /// </summary>
        private static string ResolvePawnTokens(string text, string viewerName, Pawn pawn)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string name = string.IsNullOrWhiteSpace(viewerName)
                ? (pawn?.LabelShort ?? "they")
                : viewerName.Trim();

            bool female = pawn != null && pawn.gender == Gender.Female;
            bool male = pawn != null && pawn.gender == Gender.Male;
            string pronoun = female ? "she" : male ? "he" : "they";
            string possessive = female ? "her" : male ? "his" : "their";
            string objective = female ? "her" : male ? "him" : "them";

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

        private static string ItemLabel(Thing thing, string quality)
        {
            if (thing == null)
                return "";
            string label = Plain(thing.LabelCap);
            if (string.IsNullOrEmpty(label))
                label = Plain(thing.LabelNoCount);
            if (!string.IsNullOrEmpty(quality) && !string.IsNullOrEmpty(label))
            {
                label = Regex.Replace(
                    label,
                    @"\s*\(" + Regex.Escape(quality) + @"\)\s*$",
                    "",
                    RegexOptions.IgnoreCase);
                label = Regex.Replace(
                    label,
                    @"\s+" + Regex.Escape(quality) + @"\s*$",
                    "",
                    RegexOptions.IgnoreCase);
            }
            return (label ?? "").Trim();
        }

        private static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            string s = text;
            try { s = s.StripTags(); } catch { /* keep */ }
            // Quality-color mods and chat-style tags: <color=#FFFFFF>Normal</color>
            s = Regex.Replace(s, @"<[^>]+>", string.Empty);
            return s.Trim();
        }
    }
}
