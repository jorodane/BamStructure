using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

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

    public class Graphic_Appearances_MultiColored : Graphic_Appearances
    {
        public override void Init(GraphicRequest req)
        {
            data = req.graphicData;
            path = req.path;
            color = req.color;
            colorTwo = req.colorTwo;
            drawSize = req.drawSize;
            List<StuffAppearanceDef> allDefsListForReading = DefDatabase<StuffAppearanceDef>.AllDefsListForReading;
            subGraphics = new Graphic[allDefsListForReading.Count];
            for (int i = 0; i < subGraphics.Length; i++)
            {
                StuffAppearanceDef stuffAppearance = allDefsListForReading[i];
                string text = req.path;
                if (!stuffAppearance.pathPrefix.NullOrEmpty())
                {
                    text = stuffAppearance.pathPrefix + "/" + text.Split('/').Last();
                }

                Texture2D texture2D = (from x in ContentFinder<Texture2D>.GetAllInFolder(text)
                                       where x.name.EndsWith(stuffAppearance.defName)
                                       select x).FirstOrDefault();
                if (texture2D != null)
                {
                    subGraphics[i] = GraphicDatabase.Get<Graphic_Single>(text + "/" + texture2D.name, req.shader, drawSize, color, colorTwo);
                }

                Texture2D texture2D_m = (from x in ContentFinder<Texture2D>.GetAllInFolder(text)
                                       where x.name.EndsWith(stuffAppearance.defName + "_m")
                                       select x).FirstOrDefault();
                if (texture2D_m != null)
                {
                    texture2D_m.filterMode = FilterMode.Point;
                }
            }

            for (int j = 0; j < subGraphics.Length; j++)
            {
                if (subGraphics[j] == null)
                {
                    subGraphics[j] = subGraphics[StuffAppearanceDefOf.Smooth.index];
                }
            }
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_Appearances_MultiColored>(path, newShader, drawSize, newColor, newColorTwo, data);
        }
    }

    public class Building_Roof : Building
    {
        public static Color Color_Bright = Color.white;
        public static Color Color_Normal = new Color(0.9f, 0.9f, 0.9f);
        public static Color Color_Dark = new Color(0.7f, 0.7f, 0.7f);

        public override Color DrawColor
        {
            get
            {
                Color result = base.DrawColor;
                switch(brightness)
                {
                    default: result *= Color_Bright; break;
                    case 1: result *= Color_Normal; break;
                    case 2: result *= Color_Dark; break;
                    case 3: result *= Color_Bright; break;
                    case 4: result *= Color_Bright; break;
                    case 5: result *= Color_Normal; break;
                    case 6: result *= Color_Normal; break;
                    case 7: result *= Color_Dark; break;
                    case 8: result *= Color_Dark; break;
                }
                return result;
            }
        }
        public override Color DrawColorTwo
        {
            get
            {
                Color result = base.DrawColor;
                switch (brightness)
                {
                    default: result *= Color_Bright; break;
                    case 1: result *= Color_Normal; break;
                    case 2: result *= Color_Dark; break;
                    case 3: result *= Color_Normal; break;
                    case 4: result *= Color_Dark; break;
                    case 5: result *= Color_Bright; break;
                    case 6: result *= Color_Dark; break;
                    case 7: result *= Color_Bright; break;
                    case 8: result *= Color_Normal; break;
                }
                return result;
            }
        }

        protected int brightness = 0;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref brightness, "Brightness", 0);
        }

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
        public override void Print(SectionLayer layer)
        {
            if (RoofsOnRoofsGameComponent.IsShowing) base.Print(layer);
        }

        public virtual void OnVisibleChanged(bool value)
        {
            Notify_ColorChanged();
        }

        public virtual void SetBrightness(int wantBrightness)
        {
            brightness = wantBrightness;
            Notify_ColorChanged();
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

    public class Designator_ChangeBrightnessRoof : Designator_Deconstruct
    {
        string[] _brightnessTypeNames;
        string[] BrightnessTypeNames
        { 
            get
            {
                if (_brightnessTypeNames == null)
                {
                    _brightnessTypeNames = new string[9];
                    for(int i = 0; i < _brightnessTypeNames.Length; i++) _brightnessTypeNames[i] = $"RoofsOnRoofs_BrightType{i}".Translate();
                }

                return _brightnessTypeNames;
            }
        }

        protected string attachmentString = string.Empty;
        public int currentBrightnessType = 0;
        public virtual string TargetReplaceTag => "Roof";

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (t.def == null || !(t.def.replaceTags?.Contains(TargetReplaceTag) ?? false)) return false; 
            return OriginCanDesignateThing(t);
        }

        protected AcceptanceReport OriginCanDesignateThing(Thing t) => true;

        public Designator_ChangeBrightnessRoof()
        {
            defaultLabel = "Change Roof Brightness";
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ChangeBrightness_Roof", true);
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Paint;
            attachmentString = BrightnessTypeNames[currentBrightnessType];
        }

        public override void DrawMouseAttachments()
        {
            if (useMouseIcon)
            {
                Texture iconTex = icon;
                float angle = iconAngle;
                Vector2 offset = iconOffset;
                GenUI.DrawMouseAttachment(iconTex, attachmentString, angle, offset);
            }
        }

        public override void DesignateThing(Thing t)
        {
            if (t is Building_Roof asRoof) asRoof.SetBrightness(currentBrightnessType);
        }

        public override void ProcessInput(Event ev)
        {
            Find.DesignatorManager.Select(this);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            ShowFloatMenu();
        }

        private void ShowFloatMenu()
        {
            var list = new List<FloatMenuOption>();

            for (int i = 0; i <= 7; i++)
            {
                int buffer = i;
                string label = BrightnessTypeNames[i];
                list.Add(new FloatMenuOption(buffer == currentBrightnessType ? $"✔ {label}" : label, () =>
                {
                    currentBrightnessType = buffer;
                    attachmentString = label;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }
    }

    public class Designator_ChangeBrightnessWallSide : Designator_ChangeBrightnessRoof
    {
        public override string TargetReplaceTag => "WallSide";
        public Designator_ChangeBrightnessWallSide()
        {
            defaultLabel = "Change WallSide Brightness";
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ChangeBrightness_WallSide", true);
        }
    }

    public class Designator_ChangeBrightnessPatch : Designator_ChangeBrightnessRoof
    {
        public override string TargetReplaceTag => "RoofPatch";
        public Designator_ChangeBrightnessPatch()
        {
            defaultLabel = "Change Patch Brightness";
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ChangeBrightness_Patch", true);
        }
    }



    public class Designator_DeconstructRoof : Designator_Deconstruct
    {
        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (t.def == null || t.def.thingClass != typeof(Building_Roof)) return false;
            return base.CanDesignateThing(t);
        }

        public static string GetDeconstructRoof_Label() => "RoofsOnRoofs_DeconstructRoof_Label".Translate();
        public static string GetDeconstructRoof_Description() => "RoofsOnRoofs_DeconstructRoof_Description".Translate();

        public Designator_DeconstructRoof()
        {
            defaultLabel = GetDeconstructRoof_Label();
            defaultDesc = GetDeconstructRoof_Description();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/DeconstructRoof", true);
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Cancel;
        }
    }

}
