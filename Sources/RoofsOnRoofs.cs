using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
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
                drawLoc.y = buildDef.altitudeLayer.AltitudeFor() + 0.1f;
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
                    texture2D_m.wrapMode = TextureWrapMode.Clamp;
                    texture2D_m.anisoLevel = 0;
                    texture2D_m.minimumMipmapLevel = 0;
                    texture2D_m.mipMapBias = -10.0f;
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
        public static int brightnessCount = 9;

        string[] _brightnessTypeNames;
        public string[] BrightnessTypeNames => _brightnessTypeNames;

        Texture2D[] _brightnessTextures;
        public Texture2D[] BrightnessTextures => _brightnessTextures;

        List<FloatMenuOption> _brightFloatMenu = new List<FloatMenuOption>();

        public List<FloatMenuOption> BrightFloatMenu => _brightFloatMenu;

        protected string attachmentString = string.Empty;
        public int currentBrightnessType = 1;
        public virtual string TargetReplaceTag => "Roof";
        public virtual string GetLabel() => "RoofsOnRoofs_ChangeBrightnessRoof_Label".Translate();

        public override AcceptanceReport CanDesignateThing(Thing t) => OriginDesignateThing(t) && (t.def.replaceTags?.Contains(TargetReplaceTag) ?? false);

        AcceptanceReport OriginDesignateThing(Thing t) => base.CanDesignateThing(t);


        public Designator_ChangeBrightnessRoof()
        {
            defaultLabel = GetLabel();
            defaultDesc = "";
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ChangeBrightness_Roof", true);
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Paint;

            _brightnessTextures = new Texture2D[brightnessCount];
            _brightnessTextures[0] = ContentFinder<Texture2D>.Get("UI/Designators/Color_BB", true);
            _brightnessTextures[1] = ContentFinder<Texture2D>.Get("UI/Designators/Color_NN", true);
            _brightnessTextures[2] = ContentFinder<Texture2D>.Get("UI/Designators/Color_DD", true);
            _brightnessTextures[3] = ContentFinder<Texture2D>.Get("UI/Designators/Color_BN", true);
            _brightnessTextures[4] = ContentFinder<Texture2D>.Get("UI/Designators/Color_BD", true);
            _brightnessTextures[5] = ContentFinder<Texture2D>.Get("UI/Designators/Color_NB", true);
            _brightnessTextures[6] = ContentFinder<Texture2D>.Get("UI/Designators/Color_ND", true);
            _brightnessTextures[7] = ContentFinder<Texture2D>.Get("UI/Designators/Color_DB", true);
            _brightnessTextures[8] = ContentFinder<Texture2D>.Get("UI/Designators/Color_DN", true);

            if (_brightnessTypeNames == null)
            {
                _brightnessTypeNames = new string[brightnessCount];
                for (int i = 0; i < _brightnessTypeNames.Length; i++) _brightnessTypeNames[i] = $"RoofsOnRoofs_BrightType{i}".Translate();
            }

            for (int i = 0; i < _brightnessTypeNames.Length; i++)
            {
                int buffer = i;
                string label = BrightnessTypeNames[i];
                _brightFloatMenu.Add(new FloatMenuOption(label, () =>
                {
                    currentBrightnessType = buffer;
                    attachmentString = label;
                }, BrightnessTextures[i], Color.white));
            }

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
        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                Find.DesignatorManager.Select(this);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                return BrightFloatMenu;
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
            Find.WindowStack.Add(new FloatMenu(BrightFloatMenu));
        }
    }

    public class Designator_ShowRoofOverlayToggle : Designator
    {
        private static Texture2D _iconOn;
        private static Texture2D IconOn
        {
            get
            {
                if (_iconOn == null) { _iconOn = ContentFinder<Texture2D>.Get("UI/Designators/RoofOverlayOn"); }
                return _iconOn;
            }
        }
        private static Texture2D _iconOff;
        private static Texture2D IconOff
        {
            get
            {
                if (_iconOff == null) { _iconOff = ContentFinder<Texture2D>.Get("UI/Designators/RoofOverlayOff"); }
                return _iconOff;
            }
        }

        public string GetLabel() => "RoofsOnRoofs_ShowRoofOverlayToggle_Description".Translate();
        public string GetDescription() => "ShowRoofOverlayToggleButton".Translate();
        public Designator_ShowRoofOverlayToggle()
        {
            defaultLabel = GetLabel();
            defaultDesc = GetDescription();
        }
        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => true;

        public override void ProcessInput(Event ev)
        {
            PlaySettings setting = Find.PlaySettings;
            if (setting == null) return;
            ((setting.showRoofOverlay = !setting.showRoofOverlay) ? SoundDefOf.Checkbox_TurnedOn: SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
        }

        public void UpdateIcon()
        {
            icon = (Find.PlaySettings?.showRoofOverlay ?? false) ? IconOn : IconOff;
        }
        public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
        {
            UpdateIcon();
            base.DrawIcon(rect, buttonMat, parms);
        }
    }

    public class Designator_ChangeBrightnessWallSide : Designator_ChangeBrightnessRoof
    {
        public override string GetLabel() => "RoofsOnRoofs_ChangeBrightnessWallSide_Label".Translate();
        public override string TargetReplaceTag => "WallSide";
        public Designator_ChangeBrightnessWallSide()
        {
            defaultLabel = GetLabel();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ChangeBrightness_WallSide", true);
        }
    }

    public class Designator_ChangeBrightnessPatch : Designator_ChangeBrightnessRoof
    {
        public override string GetLabel() => "RoofsOnRoofs_ChangeBrightnessPatch_Label".Translate();
        public override string TargetReplaceTag => "RoofPatch";
        public Designator_ChangeBrightnessPatch()
        {
            defaultLabel = GetLabel();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ChangeBrightness_Patch", true);
        }
    }



    public class Designator_DeconstructRoof : Designator_Deconstruct
    {

        public virtual string GetLabel() => "RoofsOnRoofs_DeconstructRoof_Label".Translate();

        public override AcceptanceReport CanDesignateThing(Thing t) => OriginDesignateThing(t) && (t.def.replaceTags?.Contains(TargetReplaceTag) ?? false);

        AcceptanceReport OriginDesignateThing(Thing t) => base.CanDesignateThing(t);

        public virtual string TargetReplaceTag => "Roof";

        public Designator_DeconstructRoof()
        {
            defaultLabel = GetLabel();
            defaultDesc = "";
            icon = ContentFinder<Texture2D>.Get("UI/Designators/DeconstructRoof", true);
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Cancel;
        }
    }

    public class Designator_DeconstructWallSide : Designator_DeconstructRoof
    {
        public override string GetLabel() => "RoofsOnRoofs_DeconstructWallSide_Label".Translate();
        public override string TargetReplaceTag => "WallSide";
        public Designator_DeconstructWallSide()
        {
            defaultLabel = GetLabel();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/DeconstructWallSide", true);
        }
    }

    public class Designator_DeconstructPatch : Designator_DeconstructRoof
    {
        public override string GetLabel() => "RoofsOnRoofs_DeconstructPatch_Label".Translate();
        public override string TargetReplaceTag => "RoofPatch";
        public Designator_DeconstructPatch()
        {
            defaultLabel = GetLabel();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/DeconstructPatch", true);
        }
    }

}
