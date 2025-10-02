using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace TinyBuilder
{
	public static class Extension_TinyBuilder
	{
		public static Vector3 GetMouseOffset(this IntVec3 cell) => UI.MouseMapPosition() - cell.ToVector3Shifted();

        public static Vector3 ToWorldVector3(this Vector2 origin) => new Vector3(origin.x, 1.0f, origin.y);

        public static bool TryGetCompProperties<PropertiesType>(this BuildableDef originDef, out PropertiesType result) where PropertiesType : CompProperties
		{
			result = (originDef as ThingDef)?.GetCompProperties<PropertiesType>();
			return result != null;
		}

		public static bool TryGetTinyThingComp(this Thing target, out CompTinyThing result)
		{
			result = null;
			if (target == null) return false;
			ThingDef thingDef = target.def;
			if (thingDef == null) return false;
			if (thingDef.GetCompProperties<CompProperties_TinyThing>() == null) return false;

			if (!target.TryGetComp(out result)) return false;

			return true;
		}
		public static bool TryGetTinyBuilder(this Map map, out MapComponent_TinyBuilder result)
		{
			result = map.GetComponent<MapComponent_TinyBuilder>();
			return result != null;
		}
	}

    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanPlaceBlueprintAt))]
    static class Patch_CanPlaceBlueprintAt_Tiny
    {
        static void Postfix(BuildableDef entDef, IntVec3 center, Rot4 rot, Map map,ref AcceptanceReport __result)
        {
            if (__result.Accepted) return;

            if (!entDef.TryGetCompProperties(out CompProperties_TinyThing _)) return;

            __result = AcceptanceReport.WasAccepted;
        }
    }

	[HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
	static class Patch_ThingPrint_Tiny
	{
		static bool Prefix(Thing __instance, SectionLayer layer)
		{
			if (!(__instance is Blueprint_Build asBuildInstance)) return true;

			Map map = __instance.Map;
			if (map == null) return true;
			BuildableDef def = asBuildInstance.EntityToBuild();
			if (def == null) return true;
			if (!def.TryGetCompProperties(out CompProperties_TinyThing _)) return true;
			if (!map.TryGetTinyBuilder(out MapComponent_TinyBuilder asTinyBuilder)) return false;

			IntVec3 cell = __instance.Position;
			string key = MapComponent_TinyBuilder.GetBuildKey(def, cell, asBuildInstance.stuffToUse);
			if (!asTinyBuilder.GetDrawableTime(key, Time.time)) return false;

            Graphic finalGraphic = __instance.Graphic;
			if (finalGraphic == null) return false;
			Vector3 initialPosition = __instance.DrawPos;
			initialPosition.y = def.altitudeLayer.AltitudeFor();

			if(asTinyBuilder.TryGetOffsets(key, out List<Vector3> offsets))
			{
				int index = 0;
				foreach (Thing currentThing in cell.GetThingList(map))
				{
					if ((!(currentThing is Blueprint_Build asBuild)) || asBuild.EntityToBuild() != def) continue;
					Vector3 currentPosition = initialPosition + ((offsets.Count > index) ? offsets[index] : Vector3.zero);
					Printer_Plane.PrintPlane(layer, currentPosition, currentThing.DrawSize, finalGraphic.MatAt(currentThing.Rotation, currentThing));
					index++;
                }
			}
			return false;
		}
	}

    [HarmonyPatch(typeof(GhostDrawer), nameof(GhostDrawer.DrawGhostThing))]
    static class Patch_DrawGhostThing_Tiny
	{
        static bool Prefix(IntVec3 center, Rot4 rot, ThingDef thingDef,Graphic baseGraphic, Color ghostCol, AltitudeLayer drawAltitude)
        {
            if (thingDef == null || !thingDef.TryGetCompProperties(out CompProperties_TinyThing _)) return true;

			Graphic finalGraphic = thingDef.graphic;

            Vector3 drawPos = UI.MouseMapPosition() + finalGraphic.DrawOffset(rot);
            drawPos.y = drawAltitude.AltitudeFor();

			Vector3 size = finalGraphic.drawSize.ToWorldVector3();
            Graphic ghost = GhostUtility.GhostGraphicFor(finalGraphic, thingDef, ghostCol);
            Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, size);
            Graphics.DrawMesh(MeshPool.plane10, matrix, ghost.MatAt(rot), 0);

            return false;
        }
    }

	[HarmonyPatch(typeof(Frame), nameof(Frame.Destroy))]
	static class Patch_FrameDestroy_Tiny
	{
		static void Prefix(Frame __instance, DestroyMode mode)
		{
            if (mode == DestroyMode.Vanish)
            {
                return;
            }

            Map map = __instance.Map;
			if (map != null && map.TryGetTinyBuilder(out MapComponent_TinyBuilder asTinyBuilder))
			{
				BuildableDef def = __instance.def.entityDefToBuild;
				if (def != null && def.TryGetCompProperties(out CompProperties_TinyThing _))
				{
					asTinyBuilder.RemoveOffset(def, __instance.Position, __instance.EntityToBuildStuff());
                }
            }
		}
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
	static class Patch_ThingDestroy_Tiny
	{
		static void Prefix(Thing __instance, DestroyMode mode)
		{
			if (mode == DestroyMode.Vanish)
			{
				return;
			}

			MapComponent_TinyBuilder asTinyBuilder;

            if (__instance is Blueprint_Build asBlueprint)
            {
                Map map = __instance.Map;
                if (map != null && map.TryGetTinyBuilder(out asTinyBuilder))
                {
                    BuildableDef def = asBlueprint.EntityToBuild();
                    if (def != null && def.TryGetCompProperties(out CompProperties_TinyThing _))
					{
						asTinyBuilder.RemoveOffset(def, __instance.Position, asBlueprint.stuffToUse); 
                    }
                }
            }
			else if(__instance is Building asBuilding)
			{
				Map map = __instance.Map;
                if (map != null && map.TryGetTinyBuilder(out asTinyBuilder))
				{
                    if (!__instance.TryGetTinyThingComp(out CompTinyThing asTinyThing)) return;
                    if (!(__instance.Faction == Faction.OfPlayer && (Find.PlaySettings?.autoRebuild ?? false) && (map.areaManager?.Home?[__instance.Position] ?? false))) return;

                    asTinyBuilder.AddOffset(asBuilding.def, __instance.Position, asTinyThing.drawOffset, asBuilding.Stuff);
                }
			}
		}
	}

    public class Designator_ClearTinyOffsets : Designator
    {
        public string GetTinyBuilderClear_Lable() => "TinyBuilderClear_Lable".Translate();
        public string GetTinyBuilderClear_Description() => "TinyBuilderClear_Description".Translate();
        public string GetTinyBuilderClear_Warning() => "TinyBuilderClear_Warning".Translate();
        public string GetTinyBuilderClear_Complete() => "TinyBuilderClear_Complete".Translate();

        public Designator_ClearTinyOffsets()
        {
            defaultLabel = GetTinyBuilderClear_Lable();
            defaultDesc = GetTinyBuilderClear_Description();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/RemovePaint", true);
            useMouseIcon = false;
            soundSucceeded = SoundDefOf.Designate_Cancel;
        }

        public override bool DragDrawMeasurements => false;
        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => true;
        public override void DesignateSingleCell(IntVec3 c) { }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(GetTinyBuilderClear_Warning(), ClearOffset, destructive: true));
        }

        void ClearOffset()
        {
            Map map = Find.CurrentMap;
            if (map == null || !map.TryGetTinyBuilder(out MapComponent_TinyBuilder asTinyBuilder) || asTinyBuilder == null) return;
			asTinyBuilder.ClearOffsets();
            Messages.Message(GetTinyBuilderClear_Complete(), MessageTypeDefOf.TaskCompletion, false);
        }
    }

    public class MapComponent_TinyBuilder : MapComponent
	{

        public Dictionary<string, List<Vector3>> offsets = new Dictionary<string, List<Vector3>>();
        public Dictionary<string, float> lastDrawTimes = new Dictionary<string, float>();

		public MapComponent_TinyBuilder(Map map) : base(map) { }

        public static string GetBuildKey(BuildableDef def, IntVec3 cell, ThingDef stuff) => $"{def.defName}|{cell.x}|{cell.z}|{stuff?.defName}";
        public static IntVec3 GetCellFromKey(string key)
		{
			string[] splited = key.Split('|');
			if (splited.Length >= 3)
			{
				return new IntVec3(int.Parse(splited[1]), 0, int.Parse(splited[2]));
			}
			return default;
        }

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref offsets, "TinyOffsets", LookMode.Value, LookMode.Value);
			if (offsets == null) offsets = new Dictionary<string, List<Vector3>>();
			else ClearOffsets();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            offsets = new Dictionary<string, List<Vector3>>();
        }

        public bool GetDrawableTime(string key, float wantTime)
		{
			if(string.IsNullOrEmpty(key)) return false;
			if(lastDrawTimes.TryGetValue(key, out float drawTime)) 
			{
				lastDrawTimes[key] = wantTime;
				return drawTime < wantTime; 
			}
			else
			{
				lastDrawTimes.Add(key, wantTime);
				return true;
			}
		}

		bool ClearPredicate(Thing currentThing)
		{
			BuildableDef checkDef = null;
            if(currentThing is Blueprint_Build asBlueprint)
			{
				checkDef = asBlueprint.EntityToBuild();
            }
			else if(currentThing is Frame asFrame)
			{
				checkDef = asFrame.def.entityDefToBuild;
			}

			return checkDef?.TryGetCompProperties(out CompProperties_TinyThing _) ?? false;
        }

		public void ClearOffsets()
		{
			foreach (var currentPair in offsets)
			{
				IntVec3 currentCell = GetCellFromKey(currentPair.Key);
				int thingCount = currentCell.GetThingList(map).Count(ClearPredicate);
				int offsetCount = currentPair.Value.Count;

				if (thingCount == 0)
				{
					currentPair.Value.Clear();
                }
                else if (offsetCount > thingCount)
				{
					currentPair.Value.RemoveRange(thingCount, offsetCount - thingCount);
				}
            }
            offsets.RemoveAll(currentPair => currentPair.Value.Count == 0);
        }

        public void AddOffset(BuildableDef def, IntVec3 cell, Vector3 offset, ThingDef stuff)
		{
			string key = GetBuildKey(def, cell, stuff);
            if (!offsets.TryGetValue(key, out List<Vector3> list))
            {
                list = new List<Vector3>();
                offsets[key] = list;
            }
            list.Add(offset);
		}

		public void RemoveOffset(BuildableDef def, IntVec3 cell, ThingDef stuff)
		{
			string key = GetBuildKey(def, cell, stuff);
			if (offsets.TryGetValue(key, out List<Vector3> list) && list.Count> 0)  list.RemoveLast(); 
		}

        public bool TryPopOffset(BuildableDef def, IntVec3 cell, ThingDef stuff, out Vector3 result)
        {
            string key = GetBuildKey(def, cell, stuff);
            if (offsets.TryGetValue(key, out List<Vector3> list) && list.Count > 0)
            {
                result = list[0];
                list.RemoveAt(0);
                if (list.Count == 0) offsets.Remove(key);
                return true;
            }
            result = default;
            return false;
        }

        public bool TryGetOffsets(BuildableDef def, IntVec3 cell, ThingDef stuff, out List<Vector3> list)
        {
            string key = GetBuildKey(def, cell, stuff);
            if (offsets.TryGetValue(key, out list) && list != null && list.Count > 0) return true;
            list = null;
            return false;
        }
		public bool TryGetOffsets(string key, out List<Vector3> list)
		{
			if (offsets.TryGetValue(key, out list) && list != null && list.Count > 0) return true;
			list = null;
			return false;
		}

	}

    public class CompProperties_TinyThing : CompProperties
	{
		public CompProperties_TinyThing()
		{
			compClass = typeof(CompTinyThing);
		}
	}

	public class CompTinyThing : ThingComp
	{
		public Vector3 drawOffset = Vector3.zero;

		public CompProperties_TinyThing Props => (CompProperties_TinyThing)props;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref drawOffset, "DrawOffset", Vector3.zero);
			(parent as Building_Tiny)?.InitTiny(this);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (drawOffset == Vector3.zero && parent.Map.TryGetTinyBuilder(out MapComponent_TinyBuilder builder))
            {
				builder.TryPopOffset(parent.def, parent.Position, parent.Stuff, out drawOffset);
				(parent as Building_Tiny)?.InitTiny(this);
            }
        }

    }

	public class Building_Tiny : Building
	{
		CompTinyThing asTiny;
		public void InitTiny(CompTinyThing newTiny)
		{
			if (newTiny == null) return;
			asTiny = newTiny;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
			InitTiny(GetComp<CompTinyThing>());
        }

        public override void Print(SectionLayer layer)
        {
            if (asTiny == null || Graphic == null) { base.Print(layer); return; }

            Vector3 pos = base.DrawPos + asTiny.drawOffset + Graphic.DrawOffset(Rotation);
            pos.y = def.altitudeLayer.AltitudeFor();

            Vector2 size = Graphic.drawSize;
            Material mat = Graphic.MatAt(Rotation, this);

            Printer_Plane.PrintPlane(layer, pos, size, mat, 0f, false, null, null, 0.01f);
        }
    }


	public class Placeworker_TinyThing : PlaceWorker
	{
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
			return AcceptanceReport.WasAccepted;
        }

        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
			return true;
        }
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			if (!def.TryGetCompProperties(out CompProperties_TinyThing asTiny)) { base.DrawGhost(def, center, rot, ghostCol, thing); return; }
		}


		public override void PostPlace(Map map, BuildableDef def, IntVec3 loc, Rot4 rot)
		{
			base.PostPlace(map, def, loc, rot);

			if(map.TryGetTinyBuilder(out MapComponent_TinyBuilder builder))
			{
				ThingDef stuff = null;
				if(Find.DesignatorManager.SelectedDesignator is Designator_Build asBuild) stuff = asBuild.StuffDef;
                builder.AddOffset(def, loc, loc.GetMouseOffset(), stuff);
            }
        }

	}
}
