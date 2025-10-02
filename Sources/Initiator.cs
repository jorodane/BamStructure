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
}

namespace CallToArms
{
    [DefOf]
    public static class CallToArmsDefs
    {
        public static JobDef DraftAsJob;
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
