using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BamStructure
{
    public class CompProperties_AreaCooler : CompProperties
    {
        public float heatPerSecond_Gentle;
        public float heatPerSecond_Light;
        public float heatPerSecond_Strong;
		public float energyByLevel_Gentle;
		public float energyByLevel_Light;
		public float energyByLevel_Strong;
		public float heatPushMinTemperature;
        public const int tickRare = 250;

		public float GetHeatPerSecond(int level)
		{
			switch (level)
			{
				default: return heatPerSecond_Gentle;
				case 1: return heatPerSecond_Light;
				case 2: return heatPerSecond_Strong;
			}
		}

		public float GetEnergyPerSecond(int level)
		{
			switch (level)
			{
				default: return energyByLevel_Gentle;
				case 1: return energyByLevel_Light;
				case 2: return energyByLevel_Strong;
			}
		}

		public CompProperties_AreaCooler()
        {
            compClass = typeof(CompAreaCooler);
        }
    }


    public class CompAreaCooler : ThingComp
    {
        public CompProperties_AreaCooler Props => (CompProperties_AreaCooler)props;
        CompPowerTrader _power;
		CompPowerTrader Power => _power = _power ?? parent.TryGetComp<CompPowerTrader>();
        CompFlickable _flick;
		CompFlickable Flick => _flick =_flick ?? parent.TryGetComp<CompFlickable>();
        IntVec3 cellMin, cellMax;

		static Color selectedButtonColor = new Color(0.6f, 1f, 0.6f);

		float heatPushPerRare;
        int level = 0;
        bool isInstalled = false;

        public bool IsOn => (Power != null && Power.PowerOn) && (Flick == null || Flick.SwitchIsOn);

		public static string GetIconPath(int level)
		{
			switch (level)
			{
				default: return "UI/Commands/Set_Gentle";
				case 1: return "UI/Commands/Set_Light";
				case 2: return "UI/Commands/Set_Strong";
			}
		}


		public static string GetLevelNamedArgument(int level)
		{
			switch (level)
			{
				default: return "BamStructure_Fan_Gentle";
				case 1: return "BamStructure_Fan_Light";
				case 2: return "BamStructure_Fan_Strong";
			}
		}

		public static string GetLevelString(int level) => GetLevelNamedArgument(level).Translate();

		public static string GetLevelDescriptionString(int level)
		{
			return "BamStructure_Fan_Level_Desc".Translate(GetLevelString(level).Translate().Named("CurrentLevel"));
		}

		public string GetLevelChangeButtonString(int nextLevel)
		{
			return "BamStructure_Fan_Button_Desc".Translate(GetLevelString(nextLevel).Translate().Named("NextLevel"), GetLevelString(level).Translate().Named("CurrentLevel"));
		}

		public Command_Action GetLevelChangeButtonAction(int wantLevel)
		{
			Command_Action result = new Command_Action()
			{
				defaultLabel = GetLevelString(wantLevel),
				defaultDesc = GetLevelChangeButtonString(wantLevel),
				icon = ContentFinder<Texture2D>.Get(GetIconPath(wantLevel), true),
				groupKey = 4000,
				action = () => SetLevelWithMote(wantLevel)
			};

			if(level == wantLevel){result.defaultIconColor = selectedButtonColor;}

			return result;
		}

		public int SetLevel(int newLevel)
		{
			level = newLevel;
			UpdatePowerOutput();
            heatPushPerRare = Props.GetHeatPerSecond(level) * (CompProperties_AreaCooler.tickRare / 60f);
			return level;
		}

		public int SetLevelWithMote(int newLevel)
		{
			int result = SetLevel(newLevel);
            MoteMaker.ThrowText(parent.Position.ToVector3() + (Vector3.one * 0.5f), parent.Map, GetLevelDescriptionString(level), 3);
			return result;
        }

        public void UpdatePowerOutput()
        {
            if (Power != null)
            {
                Power.PowerOutput = -Props.GetEnergyPerSecond(level);
            }
        }

		public override string CompInspectStringExtra()
		{
			string result = $"{base.CompInspectStringExtra()}{GetLevelDescriptionString(level)}";

			return result;
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref level, "FanLevel", 0, true);
			SetLevel(level);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			for(int i = 0; i < 3; i++)
			{
				yield return GetLevelChangeButtonAction(i);
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
			RefreshCoolingCell();
			SetLevel(level);
            isInstalled = true;
		}

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            cellMin = cellMax = IntVec3.Zero;
            isInstalled = false;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!(isInstalled && IsOn)) return;
            UpdatePowerOutput();

            Map currentMap = parent.Map;
            if (currentMap == null) return;

            float cellTemperature = parent.Position.GetTemperature(currentMap);
            if (cellTemperature > Props.heatPushMinTemperature) GenTemperature.PushHeat(parent.Position, currentMap, heatPushPerRare);

            for (int i = cellMin.x; i <= cellMax.x; i++)
            {
                for (int j = cellMin.z; j <= cellMax.z; j++)
                {
                    InCellAction(new IntVec3(i, 0, j));
                }
            }
        }

        public void RefreshCoolingCell()
        {
            BuildingProperties currentBuilding = parent.def.building;
            IntRange depth = currentBuilding.watchBuildingStandDistanceRange;
            int width = currentBuilding.watchBuildingStandRectWidth;
            int halfWidth = (width - 1) / 2;

            IntVec3 forward = parent.Rotation.FacingCell;
            IntVec3 right = parent.Rotation.Rotated(RotationDirection.Clockwise).FacingCell;
            IntVec3 position = parent.Position;

			IntVec3 leftForward = position + (forward * depth.TrueMax) - (right * (width - 1 - halfWidth));
			IntVec3 rightBack = position + (forward * (depth.TrueMin)) + (right * halfWidth);

			cellMin.x = Mathf.Min(leftForward.x, rightBack.x);
			cellMax.x = Mathf.Max(leftForward.x, rightBack.x);
			cellMin.z = Mathf.Min(leftForward.z, rightBack.z);
			cellMax.z = Mathf.Max(leftForward.z, rightBack.z);
		}

        public void InCellAction(IntVec3 location)
        {
            Map currentMap = parent.Map;
            if (!location.InBounds(currentMap)) return;
            Building currentBuilding = location.GetEdifice(currentMap);
            if (currentBuilding != null && currentBuilding.def.passability == Traversability.Impassable) return;

            foreach(Thing currentThing in location.GetThingList(currentMap))
            {
                if(currentThing is Pawn asPawn)
                {
                    Hediff fanBreeze = asPawn.health.GetOrAddHediff(BamStructureDefs.FanBreeze);
					if (fanBreeze != null)
					{
						if (fanBreeze.TryGetComp(out HediffComp_Disappears disappear))
						{
							disappear.ticksToDisappear = disappear.disappearsAfterTicks;
						}
						fanBreeze.Severity = Mathf.Lerp(fanBreeze.Severity, 0.1f + (level * 0.4f), 0.1f);
					}
                }
            }
        }
    }
}
