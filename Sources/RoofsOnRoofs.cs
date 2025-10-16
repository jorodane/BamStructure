using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using Verse;

namespace RoofsOnRoofs
{
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings_DoPlaySettingsGlobalControls_RoofsOnRoofs
    {
        public static string GetToggleTooltipDescription() => "RoofsOnRoofs_ShowRoofGraphicToggle_Description".Translate();
        static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView) { return; }

            bool isShowRoof;
            bool wasShowRoof = isShowRoof = RoofsOnRoofsGameComponent.ShowRoof;
            row.ToggleableIcon(ref isShowRoof, RoofsOnRoofsTextures.RoofIcon, GetToggleTooltipDescription());

            if (isShowRoof != wasShowRoof)
            {
                RoofsOnRoofsGameComponent.ShowRoof = isShowRoof;
            }

        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Architect), "OpenTab")]
    public static class Patch_Architect_OpenTab_RoofsOnRoofs
    {
        static void Postfix(ref ArchitectCategoryTab __result)
        {
            if (__result == null) RoofsOnRoofsGameComponent.RoofTab = false;
            else RoofsOnRoofsGameComponent.RoofTab = __result.def == RoofsOnRoofsDefs.DesignationCategory_Roofs;
        }
    }

    [HarmonyPatch(typeof(Blueprint), "DrawAt")]
    public static class Patch_Blueprint_Build_DrawAt_RoofsOnRoofs
    {
        static void Prefix(Blueprint_Build __instance, ref Vector3 drawLoc)
        {
            if (__instance.def?.entityDefToBuild is ThingDef buildDef && buildDef.thingClass == typeof(Building_Roof))
            {
                drawLoc.y = buildDef.altitudeLayer.AltitudeFor() + 0.00001f;
            }
        }
    }



    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    public static class Patch_Window_PostClose_RoofsOnRoofs
    {
        static void Postfix()
        {
            if (Current.ProgramState != ProgramState.Playing) { RoofsOnRoofsGameComponent.RoofTab = false; return; }
            if (!(Find.MainTabsRoot?.OpenTab?.TabWindow is MainTabWindow_Architect)) RoofsOnRoofsGameComponent.RoofTab = false;
        }
    }

    [HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.SetRoof))]
    static class Patch_RoofGrid_SetRoof_RoofsOnRoofs
    {
        static readonly FieldInfo MapField = AccessTools.Field(typeof(RoofGrid), "map");

        static void Postfix(RoofGrid __instance, IntVec3 c, bool __state)
        {
            bool wasRoofed = __state;
            if (__state) return;

            Map map = (Map)MapField.GetValue(__instance);
            if (map == null || !c.InBounds(map)) return;

            List<Thing> everyThings = map.thingGrid.ThingsListAtFast(c);
            if (everyThings == null || everyThings.Count == 0) return;

            List<Thing> toDestroy = new List<Thing>();
            List<Thing> toCancel = new List<Thing>();

            foreach (Thing currentThing in everyThings)
            {
                if (currentThing == null) continue;
                else if (currentThing is Building_Roof)
                {
                    toDestroy.Add(currentThing);
                }
                else if (currentThing is Blueprint_Build asBlueprint)
                {
                    ThingDef buildDef = asBlueprint.def?.entityDefToBuild as ThingDef;
                    if (buildDef?.thingClass == typeof(Building_Roof)) toCancel.Add(currentThing);
                }
                else if (currentThing is Frame asFrame)
                {
                    ThingDef buildDef = asFrame.def?.entityDefToBuild as ThingDef;
                    if (buildDef?.thingClass == typeof(Building_Roof)) toCancel.Add(currentThing);
                }
            }

            foreach (Thing currentThing in toDestroy) currentThing.Destroy(DestroyMode.Deconstruct);
            foreach (Thing currentThing in toCancel) currentThing.Destroy(DestroyMode.Cancel);
        }
    }


    public class Building_Roof : Building
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RoofsOnRoofsGameComponent.OnVisibleChanged -= OnVisibleChanged;
            RoofsOnRoofsGameComponent.OnVisibleChanged += OnVisibleChanged;
        }


        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
            RoofsOnRoofsGameComponent.OnVisibleChanged -= OnVisibleChanged;
        }

        public virtual void OnVisibleChanged(bool value)
        {
            Notify_ColorChanged();
        }

        public override void Print(SectionLayer layer)
        {
            if(RoofsOnRoofsGameComponent.IsShowing) base.Print(layer);
        }
    }

    public class Placeworker_Roof : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            return map.roofGrid.Roofed(loc);
        }

        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            return true;
        }
    }

    public class Designator_DeconstructRoof : Designator_Deconstruct
    {
        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (t.def == null || t.def.thingClass != typeof(Building_Roof))
            {
                return false;
            }

            return base.CanDesignateThing(t);
        }

        public static string GetDeconstructRoof_Label() => "RoofsOnRoofs_DeconstructRoof_Label".Translate();
        public static string GetDeconstructRoof_Description() => "RoofsOnRoofs_DeconstructRoof_Description".Translate();

        public Designator_DeconstructRoof()
        {
            defaultLabel = GetDeconstructRoof_Label();
            defaultDesc = GetDeconstructRoof_Description();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/DeconstructRoof", true);
            useMouseIcon = false;
            soundSucceeded = SoundDefOf.Designate_Cancel;
        }
    }

}
