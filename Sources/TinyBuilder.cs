using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using UnityEngine;
using Verse;

namespace TinyBuilder
{
	public static class Extension_TinyBuilder
	{
		public static Vector3 GetMouseOffset(this IntVec3 cell) => UI.MouseMapPosition() - cell.ToVector3Shifted();

		public static Vector3 GetRotatedVector(this Vector2 origin, Rot4 rotation)
		{
			switch (rotation.AsInt)
			{
				case 0: return origin;                    
				case 1: return new Vector2(origin.y, -origin.x);
				case 2: return new Vector2(-origin.x, -origin.y);
				case 3: return new Vector2(-origin.y, origin.x);
				default: return origin;
			}
		}

		public static Vector3 GetTinySize(this CompProperties_TinyThing props, ThingDef def) => new Vector3(def.graphic.drawSize.x, 1.0f, def.graphic.drawSize.y);

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

	public class MapComponent_TinyBuilder : MapComponent
	{
		public Dictionary<int, List<Vector3>> offsets = new Dictionary<int, List<Vector3>>();

		public MapComponent_TinyBuilder(Map map) : base(map) { }

		public static int GetBuildHash(BuildableDef def, IntVec3 cell)  

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref offsets, "TinyBuilderOffsets", LookMode.Reference, LookMode.Value);
			if(offsets == null) offsets = new Dictionary<int, List<Vector3>>();
		}


		public void AddOffset(BuildableDef def, IntVec3 cell)

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
		}

	}

	public class Building_Tiny : Building
	{
		CompTinyThing asTiny;
		Vector3 drawScale;

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			asTiny = GetComp<CompTinyThing>();
			if (asTiny == null) Log.Warning("[TinyBuilder] Missing CompTinyThing");
			else
			{
				drawScale = asTiny.Props.GetTinySize(def);
			}
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			if(asTiny == null || Graphic == null)	base.DrawAt(drawLoc, flip);
			Material drawMaterial = def.graphic.MatAt(Rotation, this);
			Matrix4x4 drawMatrix = Matrix4x4.TRS(drawLoc + asTiny.drawOffset, Quaternion.identity, drawScale);
			Graphics.DrawMesh(MeshPool.plane10, drawMatrix, drawMaterial, 0);

			Comps_PostDraw();
		}
	}


	public class Placeworker_TinyThing : PlaceWorker
	{
		public static Vector3 currentOffset;

		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			return base.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);

			////테이블 위라던지 본인 레이어와 겹치는데 작은게 아닌 거라던지 다 빼버리기..
			//if (!checkingDef.TryGetCompProperties(out CompProperties_TinyThing asTiny)) return base.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);

			//Rect currentRect = GetMouseWorldOffset(loc.ToVector3()).GetClampedRect(asTiny.drawSize, rot);
			//IEnumerable<Thing> thingsInSameLoc = loc.GetThingList(map)?.Where(current => current.def.category == ThingCategory.Building);

			//if (thingsInSameLoc.Count() == 0) return AcceptanceReport.WasAccepted;

			//CompTinyThing otherTiny = null;

			//bool isFailed = thingsInSameLoc.Any(
			//	current =>
			//		current != thingToIgnore &&
			//		current.def.surfaceType != SurfaceType.Eat
			//		&& (current.def.altitudeLayer == checkingDef.altitudeLayer &&
			//		(!current.TryGetComp(out otherTiny) ||
			//		/otherTiny.DrawRect.IsColliding(currentRect)))
			//);

			//return isFailed ? AcceptanceReport.WasRejected : AcceptanceReport.WasAccepted;
		}

		public override bool ForceAllowPlaceOver(BuildableDef other)
		{
			return true;
			//테이블 위에만 둘까 생각하고 있었음..
			//if (!(other is ThingDef otherThing)) return base.ForceAllowPlaceOver(other);
			//return otherThing.surfaceType == SurfaceType.Eat;
		}

		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			if (!def.TryGetCompProperties(out CompProperties_TinyThing asTiny))
			{
				base.DrawGhost(def, center, rot, ghostCol, thing);
				return;
			}

			Vector3 size = Vector3.up;
			CompProperties_TinyThing props = def.GetCompProperties<CompProperties_TinyThing>();

			Vector2 drawSize = props != null ? def.graphic.drawSize : Vector2.one;
			size.x = drawSize.x;
			size.z = drawSize.y;

			Material mat = SolidColorMaterials.SimpleSolidColorMaterial(ghostCol, false);
			Vector3 drawPos = UI.MouseMapPosition();
			currentOffset = center.GetMouseOffset();
			Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, size);
			Material drawMaterial = def.graphic.MatAt(rot);
			Graphics.DrawMesh(MeshPool.plane10, matrix, drawMaterial, 0);
		}


		public override void PostPlace(Map map, BuildableDef def, IntVec3 loc, Rot4 rot)
		{
			base.PostPlace(map, def, loc, rot);

		}

	}
}
