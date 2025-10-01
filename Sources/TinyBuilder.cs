using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using Verse.Noise;

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
    static class Patch_CanPlaceBlueprintAt_TinyOverride
    {
        static void Postfix(BuildableDef entDef, IntVec3 center, Rot4 rot, Map map,ref AcceptanceReport __result)
        {
            if (__result.Accepted) return;

            if (!entDef.TryGetCompProperties(out CompProperties_TinyThing _)) return;

            __result = AcceptanceReport.WasAccepted;
        }
    }

    [HarmonyPatch(typeof(GhostDrawer), nameof(GhostDrawer.DrawGhostThing))]
    static class Patch_GhostDrawer_Tiny
    {
        static bool Prefix(IntVec3 center, Rot4 rot, ThingDef thingDef,Graphic baseGraphic, Color ghostCol, AltitudeLayer drawAltitude)
        {
            if (thingDef == null || !thingDef.TryGetCompProperties(out CompProperties_TinyThing _)) return true;

			Graphic finalGraphic = baseGraphic ?? thingDef.graphic;

            Vector3 drawPos = UI.MouseMapPosition() + finalGraphic.DrawOffset(rot);
            drawPos.y = drawAltitude.AltitudeFor();

			Vector3 size = finalGraphic.drawSize.ToWorldVector3();
            Graphic ghost = GhostUtility.GhostGraphicFor(finalGraphic, thingDef, ghostCol);
            Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, size);
            Graphics.DrawMesh(MeshPool.plane10, matrix, ghost.MatAt(rot), 0);

            return false;
        }
    }

    public class MapComponent_TinyBuilder : MapComponent
	{

        public Dictionary<string, List<Vector3>> offsets = new Dictionary<string, List<Vector3>>();

		public MapComponent_TinyBuilder(Map map) : base(map) { }

        public static string GetBuildKey(BuildableDef def, IntVec3 cell) => $"{def.defName}|{cell.x}|{cell.z}";
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
			Scribe_Collections.Look(ref offsets, "TinyBuilderOffsets", LookMode.Value, LookMode.Value);
			if(offsets == null) offsets = new Dictionary<string, List<Vector3>>();
			else
			{
				foreach(var currentPair in offsets)
				{
					foreach(var currentThing in GetCellFromKey(currentPair.Key).GetThingList(map))
					{
                        if (!(currentThing is Building currentBuilding)) continue;
                    }
				}
			}
		}


		public void AddTransform(BuildableDef def, IntVec3 cell, Vector3 offset)
		{
			string key = GetBuildKey(def, cell);
            if (!offsets.TryGetValue(key, out List<Vector3> list))
            {
                list = new List<Vector3>();
                offsets[key] = list;
            }
            list.Add(offset);
		}

        public bool TryPopTransform(BuildableDef def, IntVec3 cell, out Vector3 result)
        {
            string key = GetBuildKey(def, cell);
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

        public bool TryGetOffsets(BuildableDef def, IntVec3 cell, out List<Vector3> list)
        {
            string key = GetBuildKey(def, cell);
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
                if (builder.TryPopTransform(parent.def, parent.Position, out drawOffset))
                {
                    Log.Warning($"Placed At {drawOffset}");
                }
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

            Vector3 pos = base.DrawPos + asTiny.drawOffset + def.graphic.DrawOffset(Rotation);
            pos.y = def.altitudeLayer.AltitudeFor();

            Vector2 size = def.graphic.drawSize;
            Material mat = def.graphic.MatAt(Rotation, this);

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
				builder.AddTransform(def, loc, loc.GetMouseOffset());
			}
		}

	}
}
