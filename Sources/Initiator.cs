using RimWorld;
using HarmonyLib;
using Verse;
using UnityEngine;
using Microsoft.Win32;
using System.Linq;

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
    ////Harmony Patch for Single Mod
    //
    //[StaticConstructorOnStartup]
    //public static class CallToArmsHarmonyInit
    //{
    //    static CallToArmsHarmonyInit()
    //    {
    //        Harmony harmony = new Harmony("CallToArms.DraftFunctions");
    //        harmony.PatchAll();
    //    }
    //}

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
        public string GetUseCategoryIntegrationString() => "BamStructure_UseCategoryIntegration".Translate();
        public string GetTrackPawnMovementForRoofVisibilityString() => "RoofsOnRoofs_TrackPawnMovementForRoofVisibility".Translate();
        public string GetTrackThingSelectForRoofVisibilityString() => "RoofsOnRoofs_TrackThingSelectForRoofVisibility".Translate();

		public bool useCategoryIntegration = false;
		public static bool trackPawnMovementForRoofVisibility = true;
		public static bool trackThingSelectForRoofVisibility = false;

        public override void ExposeData()
        {
            base.ExposeData();

			Scribe_Values.Look(ref useCategoryIntegration, "BamStructureCategoryIntegration", false);
			Scribe_Values.Look(ref trackPawnMovementForRoofVisibility, "RoofsOnRoofsTrackPawnMovementForRoofVisibility", true);
			Scribe_Values.Look(ref trackThingSelectForRoofVisibility, "RoofsOnRoofsTrackThingSelectForRoofVisibility", false);
		}

		public void DoWindowContents(Rect inRect)
		{
			var listing = new Listing_Standard();
			listing.Begin(inRect);
			listing.CheckboxLabeled(GetUseCategoryIntegrationString(), ref useCategoryIntegration);
			listing.CheckboxLabeled(GetTrackPawnMovementForRoofVisibilityString(), ref trackPawnMovementForRoofVisibility);
			listing.CheckboxLabeled(GetTrackThingSelectForRoofVisibilityString(), ref trackThingSelectForRoofVisibility);
			listing.End();
		}
	}

    public class BamStructureMod : Mod
    {
        public string GetLevelDescriptionString(int level) => "MVS_Fan_Level_Desc".Translate();

        public static BamStructureSettings Settings;

        public BamStructureMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BamStructureSettings>();

			LongEventHandler.ExecuteWhenFinished(() =>
			{
				if(!Settings.useCategoryIntegration)
				{ 
					var removeMethod = typeof(DefDatabase<DesignationCategoryDef>).GetMethod("Remove", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
					if (removeMethod != null)
					{
						removeMethod?.Invoke(null, new object[] { BamStructureDefs.Designation_BamStructure });
					}
				}
			});
		}
		public override string SettingsCategory() => "BamStructure";
		public override void DoSettingsWindowContents(Rect inRect)
		{
			Settings.DoWindowContents(inRect);
		}
	}


	public class CategoryIntegrator : DefModExtension
	{
		public override void ResolveReferences(Def parentDef)
		{
			base.ResolveReferences(parentDef);
			if(BamStructureMod.Settings.useCategoryIntegration && parentDef is BuildableDef asBuildable)
			{
				asBuildable.designationCategory = BamStructureDefs.Designation_BamStructure;
			}
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

		public static DesignationCategoryDef Designation_BamStructure;

        public static ThingDef EndStylingStation;
    }

    public class Graphic_Multi_Clamped : Graphic_Multi
    {
        public override void Init(GraphicRequest req)
        {
            base.Init(req);
            MatWest.mainTexture.wrapMode = TextureWrapMode.Clamp;
            MatWest.mainTexture.filterMode = FilterMode.Point;
            MatSouth.mainTexture.wrapMode = TextureWrapMode.Clamp;
            MatSouth.mainTexture.filterMode = FilterMode.Point;
            MatEast.mainTexture.wrapMode = TextureWrapMode.Clamp;
            MatEast.mainTexture.filterMode = FilterMode.Point;
            MatNorth.mainTexture.wrapMode = TextureWrapMode.Clamp;
            MatNorth.mainTexture.filterMode = FilterMode.Point;
        }
    }
}
