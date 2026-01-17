// PawnAppearanceCommandHandler.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class PawnAppearanceCommandHandler
    {
        #region Head Type Command

        public static string HandleSetHeadCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            var pawn = CAPChatInteractiveMod.GetPawnAssignmentManager().GetAssignedPawn(messageWrapper);
            if (pawn == null)
                return "You don't have an assigned pawn. Use !pawn to get one.";

            if (args.Length == 0)
            {
                var head = pawn.story.headType;
                return $"Current head:{head.defName} | Usage: !sethead <headtype_name> OR !sethead list";
            }
                

            if (args[0].ToLower() == "list")
            {
                List<HeadTypeDef> headList = new List<HeadTypeDef>();
                return ListAvailableHeadTypes(pawn, ref headList);
            }

            string headTypeName = string.Join(" ", args);
            var headTypeDef = FindHeadType(headTypeName);

            if (headTypeDef == null)
                return $"Head type '{headTypeName}' not found. Use !sethead list to see available options.";

            if (!CanUseHeadType(pawn, headTypeDef) && (pawn?.def != null && !pawn.def.GetType().Name.Contains("AlienRace")))
                return $"'{headTypeDef.defName}' is not compatible with your pawn's gender or genes.";
            else if (ModsConfig.IsActive("erdelf.HumanoidAlienRaces")
                && pawn?.def != null
                && pawn.def.GetType().Name.Contains("AlienRace"))
            {
                var alienHeads = new List<HeadTypeDef>();
                ListAvailableHeadTypes(pawn, ref alienHeads);
                if (!alienHeads.Any(h => h.defName == headTypeDef.defName))
                    return $"Your pawns race doesnt allow {headTypeDef.defName}";

            }

            pawn.story.headType = headTypeDef;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            return $"✅ Changed head type to '{headTypeDef.defName}'.";
        }

        private static HeadTypeDef FindHeadType(string name)
        {
            return DefDatabase<HeadTypeDef>.AllDefs.FirstOrDefault(h =>
                h.defName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                (h.label?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        private static bool CanUseHeadType(Pawn pawn, HeadTypeDef headType)
        {

            // Gender filter (Gender.None is allowed)
            if (headType.gender != Gender.None && headType.gender != pawn.gender)
                return false;

            if (!ModsConfig.BiotechActive || pawn.genes == null)
                return true;

            // Prefer xenogenes if any forced head genes exist
            var activeXenoGenes = pawn.genes.Xenogenes
                .Where(g => g.Active && g.def.forcedHeadTypes?.Count > 0)
                .ToList();

            if (activeXenoGenes.Count > 0)
            {
                int count = activeXenoGenes.Count;
                foreach (var gene in activeXenoGenes)
                {
                    if (gene.def.forcedHeadTypes.Contains(headType))
                        count--;
                }
                if (count == 0)
                    return true;
                else return false;
            }

            // Otherwise fall back to germline genes
            var activeEndoGenes = pawn.genes.Endogenes
                .Where(g => g.Active && g.def.forcedHeadTypes?.Count > 0)
                .ToList();

            if (activeEndoGenes.Count > 0)
            {
                int count = activeEndoGenes.Count;
                foreach (var gene in activeEndoGenes)
                {
                    if (gene.def.forcedHeadTypes.Contains(headType))
                        count--;
                }
                if (count == 0)
                    return true;
            }

            return true;
        }

        private static string ListAvailableHeadTypes(Pawn pawn, ref List<HeadTypeDef> headList)
        {           
            List<HeadTypeDef> compatible = null;

            // HAR
            if (ModsConfig.IsActive("erdelf.HumanoidAlienRaces")
                && pawn?.def != null
                && pawn.def.GetType().Name.Contains("AlienRace"))
            {
                var alienRace = Get(pawn.def, "alienRace");
                var general = Get(alienRace, "generalSettings");
                var partGen = Get(general, "alienPartGenerator");

                compatible = Get(partGen, "headTypes") as List<HeadTypeDef>;
                if (compatible != null)
                    headList.AddRange(compatible);
                else
                {
                    return "No compatible AlienRace head types found.";
                }
            }
            else
            {
                compatible = DefDatabase<HeadTypeDef>.AllDefs
                .Where(h =>
                    (h.gender == Gender.None || h.gender == pawn.gender)
                    && CanUseHeadType(pawn, h))
                .ToList();
            }           

            if (compatible.Count == 0)
                return "No compatible head types found.";

            return "Available head types: "
                   + string.Join(", ", compatible.Select(h => h.defName));
        }

        #endregion

        #region Body Type Command

        public static string HandleSetBodyCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            var pawn = CAPChatInteractiveMod.GetPawnAssignmentManager().GetAssignedPawn(messageWrapper);
            if (pawn == null)
                return "You don't have an assigned pawn. Use !pawn to get one.";

            if (pawn.ageTracker.AgeBiologicalYears < 13)
                return "Your pawn is too young to change body type.";

            if (args.Length == 0)
            {
                var body = pawn.story.bodyType;
                return $"Current body:{body.defName} | Usage: !setbody <bodytype_name> OR !setbody list";
            }
                

            if (args[0].ToLower() == "list")
                return ListAvailableBodyTypes(pawn);

            string bodyTypeName = string.Join(" ", args);
            var bodyTypeDef = FindBodyType(bodyTypeName);

            if (bodyTypeDef == null)
                return $"Body type '{bodyTypeName}' not found. Use !setbody list to see available options.";

            if (!CanUseBodyType(pawn, bodyTypeDef))
                return $"'{bodyTypeDef.defName}' is not compatible with your pawn's genes or xenotype.";

            pawn.story.bodyType = bodyTypeDef;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            return $"✅ Changed body type to '{bodyTypeDef.defName}'.";
        }

        private static BodyTypeDef FindBodyType(string name)
        {
            return DefDatabase<BodyTypeDef>.AllDefs.FirstOrDefault(b =>
                b.defName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                (b.label?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        private static bool CanUseBodyType(Pawn pawn, BodyTypeDef bodyType)
        {
            // Never allow child or baby body types
            if (bodyType == BodyTypeDefOf.Child || bodyType == BodyTypeDefOf.Baby)
                return false;

            bool hasBody = false;
            // Check if body type is gene-locked (Biotech)
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                // Check xenogenes first (they override germline)
                
                var xenogenes = pawn.genes.Xenogenes;
                if (xenogenes != null && xenogenes.Any())
                {
                    foreach (var gene in xenogenes)
                    {
                        if (gene.def.endogeneCategory == EndogeneCategory.BodyType)
                        {
                            var requiredBodyType = GeneticBodyTypeToBodyTypeDef(gene.def.bodyType.Value, pawn.gender);
                            if (requiredBodyType != null && bodyType == requiredBodyType)
                            {
                                hasBody = true;
                                break;
                            }
                        }
                    }
                }

                // Then check germline genes (only if no xenogene override)
                var germlineGenes = pawn.genes.Endogenes;
                if (germlineGenes != null && !hasBody)
                {
                    foreach (var gene in germlineGenes)
                    {
                        if (gene.def.endogeneCategory == EndogeneCategory.BodyType) 
                        {
                            var requiredBodyType = GeneticBodyTypeToBodyTypeDef(gene.def.bodyType.Value, pawn.gender);
                            if (requiredBodyType != null && bodyType == requiredBodyType)
                            {
                                hasBody = true;
                                break;
                            }
                        }
                    }
                }
            }

            return hasBody;
        }

        private static BodyTypeDef GeneticBodyTypeToBodyTypeDef(GeneticBodyType geneticBodyType, Gender gender)
        {
            string bodyTypeName = geneticBodyType.ToString();

            if (geneticBodyType == GeneticBodyType.Standard)
            {
                bodyTypeName = gender == Gender.Female ? "Female" : "Male";
            }

            // Try to find the body type def by name
            var bodyTypeDef = DefDatabase<BodyTypeDef>.AllDefs.FirstOrDefault(b =>
                b.defName.Equals(bodyTypeName, StringComparison.OrdinalIgnoreCase));

            if (bodyTypeDef != null)
                return bodyTypeDef;

            // Fallback
            bodyTypeDef = DefDatabase<BodyTypeDef>.AllDefs.FirstOrDefault(b =>
                b.defName.IndexOf(bodyTypeName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                bodyTypeName.IndexOf(b.defName, StringComparison.OrdinalIgnoreCase) >= 0);

            return bodyTypeDef;
        }

        private static string ListAvailableBodyTypes(Pawn pawn)
        {
            List<BodyTypeDef> compatible = null;

            // HAR
            if (ModsConfig.IsActive("erdelf.HumanoidAlienRaces")
                && pawn?.def != null
                && pawn.def.GetType().Name.Contains("AlienRace"))
            {
                var alienRace = Get(pawn.def, "alienRace");
                var general = Get(alienRace, "generalSettings");
                var partGen = Get(general, "alienPartGenerator");

                compatible = Get(partGen, "bodyTypes") as List<BodyTypeDef>;
            }
            else
            {

                foreach(var body in DefDatabase<BodyTypeDef>.AllDefs.ToList())
                {
                    if (CanUseBodyType(pawn, body))
                        compatible.Add(body);                       
                }

            }

            if (compatible.Count == 0)
                return "No compatible body types found.";

            return "Available body types: "
                + string.Join(", ", compatible.Select(b => b.label ?? b.defName));
        }

        #endregion

        #region Hair Style Command

        public static string HandleSetHairCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            var pawn = CAPChatInteractiveMod.GetPawnAssignmentManager().GetAssignedPawn(messageWrapper);
            if (pawn == null)
                return "You don't have an assigned pawn. Use !pawn to get one.";

            if (args.Length == 0)
            {
                var hair = pawn.story.hairDef;
                return $"Current hair:{hair.defName} | Usage: !sethair <hair_name> OR !sethair list";
            }
                

            if (args[0].ToLower() == "list")
                return ListAvailableHairStyles(pawn);

            string hairName = string.Join(" ", args);
            var hairDef = FindHairStyle(hairName);

            if (hairDef == null)
                return $"Hair style '{hairName}' not found. Use !sethair list to see available options.";

            if (!CanUseHairStyle(pawn, hairDef))
                return $"'{hairDef.LabelCap}' is not compatible with your pawn's genes.";

            pawn.story.hairDef = hairDef;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            return $"✅ Changed hair style to '{hairDef.LabelCap}'.";
        }

        public static string HandleDyeHairCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            return "";
        }

        private static HairDef FindHairStyle(string name)
        {
            return DefDatabase<HairDef>.AllDefs.FirstOrDefault(h =>
                h.defName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                (h.label?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        private static bool CanUseHairStyle(Pawn pawn, HairDef hairDef)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null)
                return true;

            if (hairDef.requiredGene != null && !pawn.genes.HasActiveGene(hairDef.requiredGene))
                return false;

            // Check if genes allow this style item
            return pawn.genes.StyleItemAllowed(hairDef);
        }

        private static string ListAvailableHairStyles(Pawn pawn)
        {
            var compatible = DefDatabase<HairDef>.AllDefs
                .Where(h => CanUseHairStyle(pawn, h))
                .OrderBy(h => h.label ?? h.defName)
                .Take(15)
                .ToList();

            if (compatible.Count == 0)
                return "No compatible hair styles found.";

            var result = "Available hair styles (showing first 15): ";
            result += string.Join(", ", compatible.Select(h => h.label ?? h.defName));

            return result;
        }

        #endregion

        #region Beard Style Command

        public static string HandleSetBeardCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (!ModsConfig.IdeologyActive)
                return "The Ideology DLC is required for beard customization.";

            var pawn = CAPChatInteractiveMod.GetPawnAssignmentManager().GetAssignedPawn(messageWrapper);
            if (pawn == null)
                return "You don't have an assigned pawn. Use !pawn to get one.";

            if (args.Length == 0)
            {
                var beard = pawn.style.beardDef.defName ?? "None";
                return $"Current beard:{beard} | Usage: !setbeard <beard_name> OR !setbeard list";
            }
                

            if (args[0].ToLower() == "list")
                return ListAvailableBeardStyles(pawn);

            string beardName = string.Join(" ", args);
            var beardDef = FindBeardStyle(beardName);

            if (beardDef == null)
                return $"Beard style '{beardName}' not found. Use !setbeard list to see available options.";

            if (!CanUseBeardStyle(pawn, beardDef))
                return $"'{beardDef.LabelCap}' is not compatible with your pawn's genes.";

            pawn.style.beardDef = beardDef;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            return $"✅ Changed beard style to '{beardDef.LabelCap}'.";
        }

        private static BeardDef FindBeardStyle(string name)
        {
            return DefDatabase<BeardDef>.AllDefs.FirstOrDefault(b =>
                b.defName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                (b.label?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        private static bool CanUseBeardStyle(Pawn pawn, BeardDef beardDef)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null)
                return true;

            if (beardDef.requiredGene != null && (!pawn.genes.Xenogenes.Any(g=> g.def == beardDef.requiredGene) || !pawn.genes.Endogenes.Any(g => g.def == beardDef.requiredGene)))
                return false;

            return pawn.genes.StyleItemAllowed(beardDef);
        }

        private static string ListAvailableBeardStyles(Pawn pawn)
        {
            var compatible = DefDatabase<BeardDef>.AllDefs
                .Where(b => CanUseBeardStyle(pawn, b) == true)
                .OrderBy(b => b.label ?? b.defName)
                .Take(15)
                .ToList();

            if (compatible.Count == 0)
                return "No compatible beard styles found.";

            var result = "Available beard styles (showing first 15): ";
            result += string.Join(", ", compatible.Select(b => b.label ?? b.defName));

            return result;
        }

        #endregion

        internal static object Get(object obj, string name)
        {
            if (obj == null) return null;

            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var t = obj.GetType();

            return t.GetField(name, flags)?.GetValue(obj)
                ?? t.GetProperty(name, flags)?.GetValue(obj);
        }
    }
}