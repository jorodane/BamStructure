using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace TinyBuilder
{
	public static class Extension_TinyBuilder
	{
		public static Vector3 GetMouseOffset(this IntVec3 cell) => UI.MouseMapPosition() - cell.ToVector3Shifted();

		public static Vector2 GetRotatedVector(this Vector2 origin, Rot4 rotation)
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

        public static Vector3 ToInMapVector3(this Vector2 origin) => new Vector3(origin.x, 1.0f, origin.y);

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

        public Dictionary<string, Vector3> offsets = new Dictionary<string, Vector3>();

		public MapComponent_TinyBuilder(Map map) : base(map) { }

        public static string GetBuildHash(BuildableDef def, IntVec3 cell) => $"{def.defName}|{cell.x},{cell.z}";
        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref offsets, "TinyBuilderOffsets", LookMode.Value, LookMode.Value);
			if(offsets == null) offsets = new Dictionary<string, Vector3>();
		}


		public void AddTransform(BuildableDef def, IntVec3 cell, Vector3 offset)
		{
			string hash = GetBuildHash(def, cell);
            offsets.SetOrAdd(hash, offset);
		}

		public void RemoveTransform(BuildableDef def, IntVec3 cell)
		{
            string hash = GetBuildHash(def, cell);
			offsets.Remove(hash);
        }


        public bool TryGetTransform(BuildableDef def, IntVec3 cell, out Vector3 result)
        {
            string hash = GetBuildHash(def, cell);
            return offsets.TryGetValue(hash, out result);
        }
        public bool TryPopTransform(BuildableDef def, IntVec3 cell, out Vector3 result)
        {
            string hash = GetBuildHash(def, cell);
            if(offsets.TryGetValue(hash, out result))
			{
				offsets.Remove(hash);
				return true;
            }
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
		Matrix4x4 finalMatrix;
        Material drawMaterial;
		public void InitTiny(CompTinyThing newTiny)
		{
			asTiny = newTiny;
            Vector3 finalPos = DrawPos + asTiny.drawOffset;
            Vector3 finalSize = def.graphic.drawSize.ToInMapVector3();
			finalMatrix = Matrix4x4.TRS(finalPos, Quaternion.identity, finalSize);
            drawMaterial = def.graphic.MatAt(Rotation, this);
            Log.Warning($"Initiated To {asTiny.drawOffset}");
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			if (asTiny == null || Graphic == null)
			{
				base.DrawAt(drawLoc, flip);
				return;
			}
			Graphics.DrawMesh(MeshPool.plane10, finalMatrix, drawMaterial, 0);
            Comps_PostDraw();
		}
    }


	public class Placeworker_TinyThing : PlaceWorker
	{

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

		//public override bool ForceAllowPlaceOver(BuildableDef other)
		//{
			//return true;
			//테이블 위에만 둘까 생각하고 있었음..
			//if (!(other is ThingDef otherThing)) return base.ForceAllowPlaceOver(other);
			//return otherThing.surfaceType == SurfaceType.Eat;
		//}

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

			Vector3 drawPos = UI.MouseMapPosition() + def.graphic.DrawOffset(rot);
			drawPos.y = def.altitudeLayer.AltitudeFor();
			Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, size);
			Graphic ghost = GhostUtility.GhostGraphicFor(def.graphic, def, ghostCol);
			Graphics.DrawMesh(MeshPool.plane10, matrix, ghost.MatAt(rot), 0);

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
