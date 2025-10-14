using RimWorld;
using HarmonyLib;
using Verse;
using UnityEngine;

namespace RoofsOnRoofs
{
    ////Harmony Patch for Single Mod
    //
    //[StaticConstructorOnStartup]
    //public static class RoofsOnRoofsHarmonyInit
    //{
    //    static RoofsOnRoofsHarmonyInit()
    //    {
    //        Harmony harmony = new Harmony("RoofsOnRoofs.RoofBuilder");
    //        harmony.PatchAll();
    //    }
    //}

    [DefOf]
    public static class RoofsOnRoofsDefs
    {
        public static DesignationCategoryDef DesignationCategory_Roofs;
    }

    [StaticConstructorOnStartup]
    public static class RoofsOnRoofsTextures
    {
        static Texture2D _roofIcon;
        public static Texture2D RoofIcon
        {
            get
            {
                if (_roofIcon == null)
                {
                    _roofIcon = ContentFinder<Texture2D>.Get("UI/Buttons/ShowRoofGraphicOverlay") ?? TexButton.ShowRoofOverlay;
                }
                return _roofIcon;
            }
        }
    }
    public class RoofsOnRoofsGameComponent : GameComponent
    {
        public static event System.Action<bool> OnVisibleChanged;

        static bool _lastShow = false;
        public static bool IsShowing => _lastShow;

        static bool _roofTab = false;
        public static bool RoofTab
        {
            get { return _roofTab; }
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
            get { return _showRoof; }
            set
            {
                if (_showRoof == value) return;
                _showRoof = value;
                UpdateShower();
            }
        }

        static void UpdateShower()
        {
            bool currentShow = _showRoof || _roofTab;
            if (_lastShow != currentShow)
            {
                _lastShow = currentShow;
                OnVisibleChanged?.Invoke(currentShow);
            }
        }

        public RoofsOnRoofsGameComponent(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref _showRoof, "ShowRoofGraphic", false);
            UpdateShower();
        }
    }
}

namespace TinyBuilder
{

	[StaticConstructorOnStartup]
	public static class TinyBuilderHarmonyInit
	{
		static TinyBuilderHarmonyInit()
		{
			Harmony harmony = new Harmony("TinyBuilder.TinyPlace");
			harmony.PatchAll();
		}
	}

	[StaticConstructorOnStartup]
    public static class TinyBuilderTextures
    {
        static Material _LFrameMat;
        public static Material LFrameMat
        {
            get
            {
                if (_LFrameMat == null) _LFrameMat = MaterialPool.MatFrom("UI/Frames/LFrame", ShaderDatabase.Transparent);
                return _LFrameMat;
            }
        }
    }
}

namespace CallToArms
{
    [DefOf]
    public static class CallToArmsDefs
    {
        public static JobDef DraftAsJob;
    }

	[StaticConstructorOnStartup]
    public static class CallToArmsTextures
    {
        static Texture2D _copyTexture;
        public static Texture2D CopyTexture
        {
            get 
            {
                if(_copyTexture == null) _copyTexture = ContentFinder<Texture2D>.Get("UI/Commands/CopySettings");
                return _copyTexture;
            }
        }
        static Texture2D _pasteTexture;
        public static Texture2D PasteTexture
        {
            get
            {
                if (_pasteTexture == null) _pasteTexture = ContentFinder<Texture2D>.Get("UI/Commands/PasteSettings");
                return _pasteTexture;
            }
        }
    }
}

namespace BamStructure
{
    public class BamStructureSettings : ModSettings
    {
        public override void ExposeData()
        {
            base.ExposeData();
        }
    }

    public class BamStructureMod : Mod
    {
        public string GetLevelDescriptionString(int level) => "MVS_Fan_Level_Desc".Translate();

        public static BamStructureSettings Settings;

        public BamStructureMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BamStructureSettings>();
        }
    }

    [DefOf]
    public static class BamStructureDefs
    {
        public static JobDef Play_Darts;

        public static ThoughtDef HitBetweenTheEyes;

        public static HediffDef FanBreeze;

        public static FleckDef Fleck_Dart_Red;
        public static FleckDef Fleck_Dart_Yellow;
        public static FleckDef Fleck_Dart_Blue;
        public static FleckDef Fleck_Dart_Green;
    }
}
