using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;


namespace BamStructure
{
    [HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfDef))]
    public class Patch_JobGiver_UseStylingStationAutomatic_TryGiveJob_BamStructure
    {
        static bool Prefix(ListerBuildings __instance, ThingDef def, ref List<Building> __result)
        {
            if (def == ThingDefOf.StylingStation)
            {
                __result = __instance.AllBuildingsColonistOfClass<Building_StylingStation>().ToList<Building>();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ListerThings), nameof(ListerThings.ThingsMatching))]
    public class Patch_ListerThings_ThingsMatching_BamStructure
    {
        static void Postfix(ListerThings __instance, ThingRequest req, ref List<Thing> __result)
        { 
            if (req.singleDef == ThingDefOf.StylingStation)
            {
                if(__result == null || __result.Count == 0) __result = new List<Thing>();
                List<Thing> modStylingStations = __instance.AllThings.FindAll((currentThing) =>
                {
                    return currentThing?.def?.thingClass == typeof(Building_StylingStation);
                });

                __result.AddRange(modStylingStations);
                __result = __result.Distinct().ToList();
            }
        }
    }
}
