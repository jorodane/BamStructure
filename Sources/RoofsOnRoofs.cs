using BamStructure;
using RoofsOnRoofs;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RoofsOnRoofs
{
    public static class Extension_RoofsOnRoofs
    {
        public static void GapPatch(this Texture2D targetTexture)
        {
            if (targetTexture != null)
            {
                targetTexture.filterMode = FilterMode.Point;
                targetTexture.wrapMode = TextureWrapMode.Clamp;
                targetTexture.anisoLevel = 0;
                targetTexture.minimumMipmapLevel = 0;
                targetTexture.mipMapBias = -10.0f;
            }
        }

		public static void GapPatchNotPoint(this Texture2D targetTexture)
		{
			if (targetTexture != null)
			{
				targetTexture.wrapMode = TextureWrapMode.Clamp;
				targetTexture.anisoLevel = 0;
				targetTexture.minimumMipmapLevel = 0;
				targetTexture.mipMapBias = -10.0f;
			}
		}

        public static bool IsDevQuickTest()
        {
            return Find.GameInitData == null
                && Find.Scenario == null
                && Find.World == null;
        }
    }


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

    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    public static class Patch_Window_PostClose_RoofsOnRoofs
    {
        static void Postfix()
        {
            if (Current.ProgramState != ProgramState.Playing) { RoofsOnRoofsGameComponent.RoofTab = false; return; }
            if (!(Find.MainTabsRoot?.OpenTab?.TabWindow is MainTabWindow_Architect)) RoofsOnRoofsGameComponent.RoofTab = false;
        }
    }

    //[HarmonyPatch(typeof(Selector), nameof(Selector.IsSelected))]
    //public static class Patch_Selector_Select_RoofsOnRoofs
    //{
    //    static bool Prefix(ref object obj, ref bool __result)
    //    {
    //        if (obj is Building_Roof && !RoofsOnRoofsGameComponent.ShowRoof) return !(__result = true);
    //        return true;
    //    }
    //}

    //[HarmonyPatch(typeof(Selector), nameof(Selector.Deselect))]
    //static class Patch_Selector_Deselect_RoofsOnRoofs
    //{
    //    static void Postfix(object obj)
    //    {
    //        if (obj is Building_Roof) RoofsOnRoofsGameComponent.Selected--;
    //    }
    //}
    //[HarmonyPatch(typeof(Selector), nameof(Selector.ClearSelection))]
    //static class Patch_Selector_ClearSelection_RoofsOnRoofs
    //{
    //    static void Postfix()
    //    {
    //        RoofsOnRoofsGameComponent.Selected = 0;
    //    }
    //}



    [HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.SetRoof))]
    static class Patch_RoofGrid_SetRoof_RoofsOnRoofs
    {
        static readonly FieldInfo MapField = AccessTools.Field(typeof(RoofGrid), "map");

        static void Postfix(RoofGrid __instance, IntVec3 c)
        {
            bool newRoof = __instance.Roofed(c);

            Map map = (Map)MapField.GetValue(__instance);
            if (map == null || !c.InBounds(map)) return;

            MapComponent_RoofVisibility visibilityComp = map.GetComponent<MapComponent_RoofVisibility>();
            visibilityComp?.OnRoofChanged(c, newRoof);

            if (newRoof) return;

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

    [HarmonyPatch(typeof(GravshipCapturer), nameof(GravshipCapturer.BeginGravshipRender))]
    static class Patch_GravshipCapturer_BeginGravshipRender_RoofsOnRoofs
    {
        static void Postfix()
        {
            RoofsOnRoofsGameComponent.Capturing = true;
        }
    }

    [HarmonyPatch(typeof(WorldComponent_GravshipController), nameof(WorldComponent_GravshipController.InitiateTakeoff))]
    static class Patch_WorldComponent_GravshipController_InitiateTakeoff_RoofsOnRoofs
    {
        static void Prefix()
        {
            RoofsOnRoofsGameComponent.Capturing = true;
        }
    }


    [HarmonyPatch(typeof(WorldComponent_GravshipController), "BeginLandingCutscene")]
    static class Patch_WorldComponent_GravshipController_InitiateLanding_RoofsOnRoofs
    {
        static void Postfix()
        {
            RoofsOnRoofsGameComponent.Capturing = true;
        }
    }


    [HarmonyPatch(typeof(WorldComponent_GravshipController), "ResetCutscene")]
    static class Patch_WorldComponent_GravshipController_ResetCutscene_RoofsOnRoofs
    {
        static void Prefix()
        {
            RoofsOnRoofsGameComponent.Capturing = false;
        }
    }

    [HarmonyPatch(typeof(GravshipCapturer), nameof(GravshipCapturer.BeginTerrainRender))]
    static class Patch_GravshipCapturer_BeginTerrainRender_RoofsOnRoofs
    {
        static void Prefix(ref Action<Capture> onComplete)
        {
            RoofsOnRoofsGameComponent.Capturing = true;
            RequestRedrawAllMaps();
            onComplete += CaptureComplete;
        }

        static void CaptureComplete(Capture capture)
        {
            RoofsOnRoofsGameComponent.Capturing = false;
        }
        public static void RequestRedrawAllMaps()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            foreach (var map in Find.Maps)
                map.mapDrawer.WholeMapChanged(MapMeshFlagDefOf.Things);
        }
    }

    [HarmonyPatch(typeof(Selector), nameof(Selector.ClearSelection))]
    static class Patch_Selector_ClearSelection
    {
        static void Postfix() => Find.CurrentMap?.GetComponent<MapComponent_RoofVisibility>()?.ClearAll();
    }

    [HarmonyPatch(typeof(Selector), nameof(Selector.Select))]
    static class Patch_Selector_Select
    {
        static void Postfix()
        {
            if (Extension_RoofsOnRoofs.IsDevQuickTest()) return;

            Map map = Find.CurrentMap;
            if (map == null) return;

            MapComponent_RoofVisibility comp = map.GetComponent<MapComponent_RoofVisibility>();
            if (comp == null) return;

            comp.OnObjectSelected(Find.Selector.SelectedObjects);
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class Patch_Pawn_PathFollower_TryEnterNextPathCell
    {
        static readonly FieldInfo field_Pawn = AccessTools.Field(typeof(Pawn_PathFollower), "pawn");

        static void Prefix(Pawn_PathFollower __instance, out IntVec3 __state)
        {
            if (BamStructureSettings.trackPawnMovementForRoofVisibility && field_Pawn.GetValue(__instance) is Pawn asPawn)
            {
                __state = asPawn.Position;
            }
            else __state = IntVec3.Zero;
        }

        static void Postfix(Pawn_PathFollower __instance, IntVec3 __state)
        {
            if (Extension_RoofsOnRoofs.IsDevQuickTest()) return;
            if (!BamStructureSettings.trackPawnMovementForRoofVisibility) return;
            Pawn pawn = field_Pawn.GetValue(__instance) as Pawn;
            if (!pawn.pather.MovingNow) return;
            if (!Find.Selector.IsSelected(pawn)) return;

            Map map = pawn.Map;
            if (map == null) return;

            IntVec3 next = pawn.Position;
            if (next != __state)
            {
                MapComponent_RoofVisibility comp = map.GetComponent<MapComponent_RoofVisibility>();
                if (comp != null)
                {
                    comp.OnPawnMoved(pawn, __state, next);
                }
            }
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
                texture2D_m?.GapPatch();
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

    public class Graphic_Multi_MultiColored : Graphic_Multi
    {
        private Material[] mats = new Material[4];
        private float drawRotatedExtraAngleOffset;
        private bool westFlipped;
        private bool eastFlipped;
        public override Material MatWest => mats[3];

        public override Material MatSouth => mats[2];

        public override Material MatEast => mats[1];

        public override Material MatNorth => mats[0];

        public override bool WestFlipped => westFlipped;
        public override bool EastFlipped => eastFlipped;
        public override float DrawRotatedExtraAngleOffset => drawRotatedExtraAngleOffset;


        public override void TryInsertIntoAtlas(TextureAtlasGroup groupKey){ return; }

        public override void Init(GraphicRequest req)
        {
            data = req.graphicData;
            path = req.path;
            maskPath = req.maskPath;
            color = req.color;
            colorTwo = req.colorTwo;
            drawSize = req.drawSize;
            Texture2D[] array = new Texture2D[mats.Length];
            array[0] = ContentFinder<Texture2D>.Get(req.path + "_north", reportFailure: false);
            array[1] = ContentFinder<Texture2D>.Get(req.path + "_east", reportFailure: false);
            array[2] = ContentFinder<Texture2D>.Get(req.path + "_south", reportFailure: false);
            array[3] = ContentFinder<Texture2D>.Get(req.path + "_west", reportFailure: false);
            if (array[0] == null)
            {
                if (array[2] != null)
                {
                    array[0] = array[2];
                    drawRotatedExtraAngleOffset = 180f;
                }
                else if (array[1] != null)
                {
                    array[0] = array[1];
                    drawRotatedExtraAngleOffset = -90f;
                }
                else if (array[3] != null)
                {
                    array[0] = array[3];
                    drawRotatedExtraAngleOffset = 90f;
                }
                else
                {
                    array[0] = ContentFinder<Texture2D>.Get(req.path, reportFailure: false);
                }
            }

            if (array[0] == null)
            {
                Log.Error("Failed to find any textures at " + req.path + " while constructing " + this.ToStringSafe());
                mats[0] = (mats[1] = (mats[2] = (mats[3] = BaseContent.BadMat)));
                return;
            }

            if (array[2] == null)
            {
                array[2] = array[0];
            }

            if (array[1] == null)
            {
                if (array[3] != null)
                {
                    array[1] = array[3];
                    eastFlipped = base.DataAllowsFlip;
                }
                else
                {
                    array[1] = array[0];
                }
            }

            if (array[3] == null)
            {
                if (array[1] != null)
                {
                    array[3] = array[1];
                    westFlipped = base.DataAllowsFlip;
                }
                else
                {
                    array[3] = array[0];
                }
            }

            Texture2D[] array2 = new Texture2D[mats.Length];
            if (req.shader.SupportsMaskTex())
            {
                string text = (maskPath.NullOrEmpty() ? path : maskPath);
                string text2 = (maskPath.NullOrEmpty() ? "m" : string.Empty);
                array2[0] = ContentFinder<Texture2D>.Get(text + "_north" + text2, reportFailure: false);
                array2[1] = ContentFinder<Texture2D>.Get(text + "_east" + text2, reportFailure: false);
                array2[2] = ContentFinder<Texture2D>.Get(text + "_south" + text2, reportFailure: false);
                array2[3] = ContentFinder<Texture2D>.Get(text + "_west" + text2, reportFailure: false);
                if (array2[0] == null)
                {
                    if (array2[2] != null)
                    {
                        array2[0] = array2[2];
                    }
                    else if (array2[1] != null)
                    {
                        array2[0] = array2[1];
                    }
                    else if (array2[3] != null)
                    {
                        array2[0] = array2[3];
                    }
                }

                if (array2[2] == null)
                {
                    array2[2] = array2[0];
                }

                if (array2[1] == null)
                {
                    if (array2[3] != null)
                    {
                        array2[1] = array2[3];
                    }
                    else
                    {
                        array2[1] = array2[0];
                    }
                }

                if (array2[3] == null)
                {
                    if (array2[1] != null)
                    {
                        array2[3] = array2[1];
                    }
                    else
                    {
                        array2[3] = array2[0];
                    }
                }
            }

            for (int i = 0; i < mats.Length; i++)
            {
                MaterialRequest req2 = default(MaterialRequest);
                array[i].GapPatch();
                req2.mainTex = array[i];
                req2.shader = req.shader;
                req2.color = color;
                req2.colorTwo = colorTwo;
                array2[i].GapPatch();
                req2.maskTex = array2[i];
                req2.shaderParameters = req.shaderParameters;
                req2.renderQueue = req.renderQueue;
                mats[i] = MaterialPool.MatFrom(req2);
            }
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_Multi_MultiColored>(path, newShader, drawSize, newColor, newColorTwo, data, maskPath);
        }

    }







    public class RoofsOnRoofsGameComponent : GameComponent
    {
        public static event Action OnVisibleChanged;

        public enum RoofRenderLevel { Not, Need, Must }

        static RoofRenderLevel _renderLevel;
        public static RoofRenderLevel RenderLevel => _renderLevel;

        //static float _lastUpdateTime = 0.0f;

        //public static float LastUpdateTime => _lastUpdateTime;

        //static int _selected = 0;
        //public static int Selected
        //{
        //    get => _selected;
        //    set
        //    {
        //        value = Mathf.Max(value, 0);
        //        if (_selected == value) return;
        //        if(value == 0)
        //        {
        //            _selected = 0;
        //        }
        //        else if(_selected != 0)
        //        {
        //            _selected = value;
        //            return;
        //        }
        //        else
        //        {
        //            _selected = value;
        //        }
        //        UpdateShower();
        //    }
        //}

        static bool _capturing = false;

        public static bool Capturing
        {
            get => _capturing;
            set
            {
                if (_capturing == value) return;
                _capturing = value;
                UpdateShower();
            }
        }

        static bool _roofTab = false;

        public static bool RoofTab
        {
            get => _roofTab;
            set
            {
                if (_roofTab == value) return;
                _roofTab = value;
                UpdateShower();
            }
        }

        static bool _showRoof = false;

        public static bool ShowRoof
        {
            get => _showRoof;
            set
            {
                if (_showRoof == value) return;
                _showRoof = value;
                UpdateShower();
            }
        }


        public static void UpdateShower()
        {
            if (GravshipCapturer.IsGravshipRenderInProgress || Capturing || RoofTab)// || Selected > 0)
            {
                _renderLevel = RoofRenderLevel.Must;
            }
            else if (ShowRoof)
            {
                _renderLevel = RoofRenderLevel.Need;
            }
            else
            {
                _renderLevel = RoofRenderLevel.Not;
            }
            //_lastUpdateTime = Time.time;
            UpdateVisible();
        }

        public static void UpdateVisible()
        {
            OnVisibleChanged?.Invoke();
        }

        public RoofsOnRoofsGameComponent(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref _showRoof, "ShowRoofGraphic", false);
            OnVisibleChanged = null;
            UpdateShower();
        }

        public override void StartedNewGame()
        {
            OnVisibleChanged = null;
            base.StartedNewGame();
        }
    }


    public class MapComponent_RoofVisibility : MapComponent
    {
        public int[] roofVisibleGrid;
        FloodFiller flooder;
        bool isDirty = false;

        public MapComponent_RoofVisibility(Map map) : base(map) { }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (isDirty)
            {
                RoofsOnRoofsGameComponent.UpdateShower();
                isDirty = false;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            int cellCount = map.cellIndices.NumGridCells;
            if(roofVisibleGrid == null || roofVisibleGrid.Length != cellCount) roofVisibleGrid = new int[cellCount];
            else Array.Clear(roofVisibleGrid, 0, cellCount);
            flooder = map.floodFiller;
        }

        public void ClearAll()
        {
            if (roofVisibleGrid != null)
            {
                Array.Clear(roofVisibleGrid, 0, roofVisibleGrid.Length);
                isDirty = true;
            }
        }

        public int this[IntVec3 c] => (roofVisibleGrid != null && c.InBounds(map)) ? roofVisibleGrid[map.cellIndices.CellToIndex(c)] : 0;

        bool GetRoofed(IntVec3 targetCell, Map from) => targetCell.IsValid && from.roofGrid.Roofed(targetCell);
        bool GetRoofed(IntVec3 targetCell) => targetCell.IsValid && targetCell.InBounds(map) && map.roofGrid.Roofed(targetCell);

        public void OnObjectSelected(List<object> objects)
        {
            if (BamStructureSettings.trackThingSelectForRoofVisibility)
            {
                ClearAll();
                foreach(object currentObject in objects)
                {
                    if(currentObject is Thing currentThing)
                    {
                        if (currentThing == null || currentThing.Map != map) continue;
                        IntVec3 currentPosition = currentThing.Position;
                        if (GetRoofed(currentPosition))
                        {
                            if(!(currentThing is Pawn))
                            {
                                int i = map.cellIndices.CellToIndex(currentThing.Position);
                                if ((uint)i < (uint)roofVisibleGrid.Length && roofVisibleGrid[i] > 0) continue;
                            }
                            OnCellChanged(currentPosition, +1);
                        }
                    }
                }
            }
            else if(BamStructureSettings.trackPawnMovementForRoofVisibility)
            {
                ClearAll();
                foreach (object currentObject in objects)
                {
                    if (currentObject is Pawn currentPawn)
                    {
                        if (currentPawn == null || currentPawn.Map != map) continue;
                        IntVec3 currentPosition = currentPawn.Position;
                        if (GetRoofed(currentPosition)) OnCellChanged(currentPosition, +1);
                    }
                }
            }
        }

        public void OnPawnMoved(Pawn from, IntVec3 oldPoisition, IntVec3 newPoisition)
        {
            if (from?.Map != map) return;
            bool oldRoofed = GetRoofed(oldPoisition);

            if (oldPoisition == newPoisition || !oldPoisition.IsValid || !newPoisition.IsValid) return;

            if(oldRoofed != GetRoofed(newPoisition))
            {
                if(oldRoofed)   OnCellChanged(oldPoisition, -1);
                else            OnCellChanged(newPoisition, +1);
            }
        }

        public void OnCellChanged(IntVec3 position, int delta)
        {
            isDirty = true;
            flooder.FloodFill(
                position,
                GetRoofed,
                targetCell =>
                {
                    if (!targetCell.InBounds(map)) return;
                    int i = map.cellIndices.CellToIndex(targetCell);
                    if ((uint)i < (uint)roofVisibleGrid.Length) roofVisibleGrid[i] += delta;
                },
                int.MaxValue
            );
        }

        public void OnRoofChanged(IntVec3 position, bool roofed)
        {
            if (roofVisibleGrid == null || !position.InBounds(map)) return;
            int cellIndex = map.cellIndices.CellToIndex(position);
            if ((uint)cellIndex < (uint)roofVisibleGrid.Length)
            {
                if(!roofed) roofVisibleGrid[cellIndex] = 0;
                else
                {
                    IntVec3[] nearPositions = { position + IntVec3.East, position + IntVec3.South, position + IntVec3.West, position + IntVec3.North };
                    int maxCount = 0;
                    foreach (IntVec3 checkingPosition in nearPositions)
                    {
                        if (!checkingPosition.InBounds(map)) continue;
                        int checkingIndex = map.cellIndices.CellToIndex(checkingPosition);
                        if ((uint)checkingIndex < (uint)roofVisibleGrid.Length)
                        {
                            maxCount = Math.Max(roofVisibleGrid[checkingIndex], maxCount);
                        }
                    }
                    roofVisibleGrid[cellIndex] = maxCount;
                }
            }
        }
    }

    

    public class Building_Roof : Building
    {
        public readonly static Color Color_Bright = Color.white;
        public readonly static Color Color_Normal = new Color(0.9f, 0.9f, 0.9f);
        public readonly static Color Color_Dark = new Color(0.7f, 0.7f, 0.7f);

        public readonly static Color[] Color_MainList = { Color_Bright, Color_Normal, Color_Dark, Color_Bright, Color_Bright, Color_Normal, Color_Normal, Color_Dark, Color_Dark };
        public readonly static Color[] Color_SubList = { Color_Bright, Color_Normal, Color_Dark, Color_Normal, Color_Dark, Color_Bright, Color_Dark, Color_Bright, Color_Dark };
        public override Color DrawColor => base.DrawColor * Color_MainList[brightness];
        public override Color DrawColorTwo => base.DrawColor * Color_SubList[brightness];

        protected int brightness = 1;
        protected bool showing = false;
        MapComponent_RoofVisibility visibilityComponent = null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref brightness, "Brightness", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RoofsOnRoofsGameComponent.OnVisibleChanged -= OnVisibleChanged;
                RoofsOnRoofsGameComponent.OnVisibleChanged += OnVisibleChanged;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RoofsOnRoofsGameComponent.OnVisibleChanged -= OnVisibleChanged;
            RoofsOnRoofsGameComponent.OnVisibleChanged += OnVisibleChanged;
            visibilityComponent = map.GetComponent<MapComponent_RoofVisibility>();
            showing = GetRoofVisible();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
            RoofsOnRoofsGameComponent.OnVisibleChanged -= OnVisibleChanged;
        }

        //public override void Notify_ThingSelected()
        //{
        //    base.Notify_ThingSelected();
        //    RoofsOnRoofsGameComponent.Selected++;
        //}

        public override void Discard(bool silentlyRemoveReferences = false)
        {
            base.Discard(silentlyRemoveReferences);
            RoofsOnRoofsGameComponent.OnVisibleChanged -= OnVisibleChanged;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            RoofsOnRoofsGameComponent.OnVisibleChanged -= OnVisibleChanged;
        }
        public override void Print(SectionLayer layer)
        {
            if(showing) base.Print(layer);
        }

        public virtual bool GetRoofVisible()
        {
            if (Map == null) return false;
            switch (RoofsOnRoofsGameComponent.RenderLevel)
            {
                case RoofsOnRoofsGameComponent.RoofRenderLevel.Must: return true;
                case RoofsOnRoofsGameComponent.RoofRenderLevel.Need: return GetRoofVisibleByPosition();
                default: return false;
            }
        }

        public virtual bool GetRoofVisibleByPosition() => (visibilityComponent != null && visibilityComponent[Position] <= 0);

        public virtual void OnVisibleChanged()
        {
            bool currentShowing = GetRoofVisible();
            if(currentShowing != showing)
            {
                Notify_ColorChanged();
                showing = currentShowing;
            }
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
            icon = (Find.PlaySettings?.showRoofOverlay ?? false) ? RoofsOnRoofsTextures.IconRoofOverlayOn : RoofsOnRoofsTextures.IconRoofOverlayOff;
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
