using RimWorld;
using HarmonyLib;
using Verse;
using UnityEngine;

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
