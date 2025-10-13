using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace TinyBuilder
{
    public static class Extension_TinyBuilder
    {
        public static Vector3 GetMouseOffset(this IntVec3 cell)
        {
            Vector3 result = UI.MouseMapPosition() - cell.ToVector3Shifted();
            Event currentEvent = Event.current;
            if (currentEvent != null)
            {
                float snapSize = currentEvent.control ? 0.1f : currentEvent.shift ? 0.25f : 0.0f;
                if(snapSize > 0.0f)
                {
                    result.x = Mathf.Round(result.x / snapSize) * snapSize;
                    result.z = Mathf.Round(result.z / snapSize) * snapSize;
                }
            }
            return result;
        }
        public static Vector3 ToWorldVector3(this Vector2 origin, Rot4 rot) => rot.IsVertical ? new Vector3(origin.x, 1.0f, origin.y) : new Vector3(origin.y, 1.0f, origin.x);
        public static bool NeedFlip(this Graphic graphic, Rot4 rot) => (rot == Rot4.West && graphic.WestFlipped) || (rot == Rot4.East && graphic.EastFlipped);

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
        static void Postfix(BuildableDef entDef, IntVec3 center, Rot4 rot, Map map, ref AcceptanceReport __result)
        {
            if (__result.Accepted) return;

            if (!entDef.TryGetCompProperties(out CompProperties_TinyThing _)) return;

            __result = AcceptanceReport.WasAccepted;
        }

    }
    [HarmonyPatch(typeof(Designator_Install), nameof(Designator_Install.CanDesignateCell))]
    static class Patch_DesignatorInstall_CanDesignateCell_Tiny
    {
        static void Postfix(Designator_Install __instance, IntVec3 c, ref AcceptanceReport __result)
        {
            if (__result.Accepted || !__instance.PlacingDef.TryGetCompProperties(out CompProperties_TinyThing _)) return;
            __result = AcceptanceReport.WasAccepted;
        }
    }

    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.PlaceBlueprintForInstall))]
    static class Patch_PlaceInstall_Tiny
    {
        static void Prefix(MinifiedThing itemToInstall, IntVec3 center)
        {
            if (itemToInstall?.InnerThing is Thing innerThing && innerThing.TryGetComp(out CompTinyThing asTinyThing))
            {
                asTinyThing.installOffset = center.GetMouseOffset();
            }
        }
    }

    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.PlaceBlueprintForReinstall))]
    static class Patch_PlaceReinstall_Tiny
    {
        static void Prefix(Building buildingToReinstall, IntVec3 center)
        {
            if (buildingToReinstall.TryGetComp(out CompTinyThing asTinyThing))
            {
                asTinyThing.installOffset = center.GetMouseOffset();
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
    static class Patch_ThingPrint_Tiny
    {
        static bool Prefix(Thing __instance, SectionLayer layer)
        {
            if (__instance is Blueprint_Build asBuildInstance)
            {
                BuildableDef def = asBuildInstance.EntityToBuild();
                if (def == null) return true;
                if (!def.TryGetCompProperties(out CompProperties_TinyThing _)) return true;

                Map map = __instance.Map;
                if (map == null) return true;
                if (!map.TryGetTinyBuilder(out MapComponent_TinyBuilder asTinyBuilder)) return true;

                IntVec3 cell = __instance.Position;
                string key = MapComponent_TinyBuilder.GetBuildKey(def, cell, asBuildInstance.Rotation, asBuildInstance.stuffToUse);
                if (!asTinyBuilder.GetDrawableTime(key, Time.time)) return false;

                Graphic finalGraphic = __instance.Graphic;
                if (finalGraphic == null) return false;
                Vector3 initialPosition = __instance.DrawPos;
                initialPosition.y = def.altitudeLayer.AltitudeFor();

                if (asTinyBuilder.TryGetOffsets(key, out List<Vector3> offsets))
                {
                    int index = 0;
                    int maxIndex = offsets.Count;
                    foreach (Thing currentThing in cell.GetThingList(map))
                    {
                        if ((!(currentThing is Blueprint_Build asBuild)) || asBuild.EntityToBuild() != def || currentThing.Rotation != __instance.Rotation || !Equals(asBuild.stuffToUse, asBuildInstance.stuffToUse)) continue;
                        Rot4 currentRotation = currentThing.Rotation;
                        bool needFlip = finalGraphic.NeedFlip(currentRotation);
                        Vector3 currentPosition = initialPosition + ((maxIndex > index) ? offsets[index] : Vector3.zero) + currentThing.def.graphicData.DrawOffsetForRot(currentRotation);
                        Vector2 finalSize = currentThing.DrawSize;
                        Printer_Plane.PrintPlane(layer, currentPosition, finalSize, finalGraphic.MatAt(currentRotation), finalGraphic.ShouldDrawRotated ? currentRotation.AsAngle : 0, needFlip);
                        index++;
                    }
                }
                return false;
            }
            else if (__instance is Blueprint_Install asInstallInstance)
            {
                BuildableDef def = asInstallInstance.EntityToBuild();
                if (def == null) return true;
                if (!def.TryGetCompProperties(out CompProperties_TinyThing _)) return true;

                Map map = __instance.Map;
                if (map == null) return true;
                if (!map.TryGetTinyBuilder(out MapComponent_TinyBuilder asTinyBuilder)) return true;

                Thing originThing = asInstallInstance.MiniToInstallOrBuildingToReinstall;
                if (originThing is MinifiedThing minified) originThing = minified.InnerThing;
                if (originThing == null) return true;
                Graphic finalGraphic = __instance.Graphic;

                if (originThing.TryGetComp(out CompTinyThing asTiny))
                {
                    Rot4 currentRotation = __instance.Rotation;
                    Vector3 currentPosition = __instance.DrawPos + (asTiny.installOffset != null ? asTiny.installOffset.Value : Vector3.zero) + __instance.def.graphicData.DrawOffsetForRot(currentRotation); ;
                    bool needFlip = finalGraphic.NeedFlip(currentRotation);

                    Vector2 finalSize = finalGraphic.drawSize;
                    Printer_Plane.PrintPlane(layer, currentPosition, finalSize, finalGraphic.MatAt(currentRotation), finalGraphic.ShouldDrawRotated ? currentRotation.AsAngle : 0, needFlip);
                }
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GhostDrawer), nameof(GhostDrawer.DrawGhostThing))]
    static class Patch_DrawGhostThing_Tiny
    {
        static bool Prefix(IntVec3 center, Rot4 rot, ThingDef thingDef, Graphic baseGraphic, Color ghostCol, AltitudeLayer drawAltitude)
        {
            if (thingDef == null || !thingDef.TryGetCompProperties(out CompProperties_TinyThing _)) return true;

            Graphic finalGraphic = thingDef.graphic;

            Vector3 drawPos = center.ToVector3Shifted() + center.GetMouseOffset() + finalGraphic.DrawOffset(rot);
            drawPos.y = drawAltitude.AltitudeFor();


            Graphic ghost = GhostUtility.GhostGraphicFor(finalGraphic, thingDef, ghostCol);
            Graphics.DrawMesh(ghost.MeshAt(rot), drawPos, finalGraphic.ShouldDrawRotated ? rot.AsQuat : Quaternion.identity, ghost.MatAt(rot), 0);

            return false;
        }
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.Destroy))]
    static class Patch_FrameDestroy_Tiny
    {
        static void Prefix(Frame __instance, DestroyMode mode)
        {
            if (mode == DestroyMode.Vanish || mode == DestroyMode.FailConstruction)  return;
            Map map = __instance.Map;
            if (map != null && map.TryGetTinyBuilder(out MapComponent_TinyBuilder asTinyBuilder))
            {
                if (mode == DestroyMode.KillFinalize && !(__instance.Faction == Faction.OfPlayer && (Find.PlaySettings?.autoRebuild ?? false) && (map.areaManager?.Home?[__instance.Position] ?? false))) return;
                BuildableDef def = __instance.def.entityDefToBuild;
                if (def != null && def.TryGetCompProperties(out CompProperties_TinyThing _))
                {
                    asTinyBuilder.RemoveOffset(def, __instance.Position, __instance.Rotation, __instance.EntityToBuildStuff());
                }
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    static class Patch_ThingDestroy_Tiny
    {
        static void Prefix(Thing __instance, DestroyMode mode)
        {

            if (__instance is Blueprint_Build asBlueprint)
            {
                if (mode == DestroyMode.Vanish || mode == DestroyMode.FailConstruction) return;
                MapComponent_TinyBuilder asTinyBuilder;
                Map map = __instance.Map;
                if (map != null && map.TryGetTinyBuilder(out asTinyBuilder))
                {
                    BuildableDef def = asBlueprint.EntityToBuild();
                    if (def != null && def.TryGetCompProperties(out CompProperties_TinyThing _))
                    {
                        asTinyBuilder.RemoveOffset(def, __instance.Position, __instance.Rotation, asBlueprint.stuffToUse);
                    }
                }
            }
            else if (__instance is Building asBuilding)
            {
                if (mode != DestroyMode.KillFinalize) return;
                MapComponent_TinyBuilder asTinyBuilder;
                Map map = __instance.Map;
                if (map != null && map.TryGetTinyBuilder(out asTinyBuilder))
                {
                    if (!__instance.TryGetTinyThingComp(out CompTinyThing asTinyThing)) return;
                    if (!(__instance.Faction == Faction.OfPlayer && (Find.PlaySettings?.autoRebuild ?? false) && (map.areaManager?.Home?[__instance.Position] ?? false))) return;

                    asTinyBuilder.AddOffset(asBuilding.def, __instance.Position, asTinyThing.drawOffset, __instance.Rotation, asBuilding.Stuff);
                }
            }
        }
    }


    [HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionBracketFor))]
    static class Patch_SelectionDrawer_Tiny
    {
        static readonly float bracketScale = 0.1f;
        static readonly float bracketDistance = 0.75f;
        static readonly Vector3 bracketSizeVector = new Vector3(bracketScale, 1.0f, bracketScale);
        static readonly Quaternion bracketRotator = Quaternion.Euler(0f, 90.0f, 0f);


        static bool Prefix(object obj)
        {
            if (!(obj is Thing asThing && asThing.TryGetTinyThingComp(out _))) return true;

            Vector3 center = asThing.DrawPos;
            center.y = AltitudeLayer.MetaOverlays.AltitudeFor();

            Graphic currentGraphic = asThing.Graphic ?? asThing.def?.graphic;
            Vector2 drawSize = currentGraphic != null ? currentGraphic.drawSize : Vector2.one;

            bool isVertical = asThing.Rotation.IsVertical;

            float width = (isVertical ? drawSize.x : drawSize.y) * 0.5f;
            float height = (isVertical ? drawSize.y : drawSize.x) * 0.5f;

            Matrix4x4[] matrixs =
            {
                Matrix4x4.TRS(new Vector3(center.x + width, center.y, center.z + height), Quaternion.identity, bracketSizeVector),
                Matrix4x4.TRS(new Vector3(center.x + width, center.y, center.z - height), Quaternion.Euler(0f, 90f, 0f), bracketSizeVector),
                Matrix4x4.TRS(new Vector3(center.x - width, center.y, center.z - height), Quaternion.Euler(0f, 180f, 0f), bracketSizeVector),
                Matrix4x4.TRS(new Vector3(center.x - width, center.y, center.z + height), Quaternion.Euler(0f, 270f, 0f), bracketSizeVector),
            };

            foreach (Matrix4x4 currentMatrix in matrixs)
            {
                Graphics.DrawMesh(MeshPool.plane10, currentMatrix, TinyBuilderTextures.LFrameMat, 0);
            }

            return false;
        }
    }

    public class Designator_SelectNearestTiny : Designator
    {
        public static string GetTinyBuilderSelect_Label() => "TinyBuilderSelect_Label".Translate();
        public static string GetTinyBuilderSelect_Description() => "TinyBuilderSelect_Description".Translate();
        public static string GetTinyBuilderSelect_NoTinyThing() => "TinyBuilderSelect_NoTinyThing".Translate();

        public static string GetTinyBuilderCancel_Label() => "TinyBuilderCancel_Label".Translate();
        public static string GetTinyBuilderCancel_Description() => "TinyBuilderCancel_Description".Translate();

        Thing _hoverCandidate = null;

        public Designator_SelectNearestTiny()
        {
            defaultLabel = GetTinyBuilderSelect_Label();
            defaultDesc = GetTinyBuilderSelect_Description();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ForbidOff", true);
            soundSucceeded = SoundDefOf.Tick_Tiny;
            hotKey = KeyBindingDefOf.Command_TogglePower;
            useMouseIcon = true;
        }

        public override bool DragDrawMeasurements => false;
        public override AcceptanceReport CanDesignateThing(Thing t) => false;

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            Map map = Find.CurrentMap;
            if (map == null) return false;

            foreach (var currentThing in c.GetThingList(map))
                if (currentThing is Building && currentThing.TryGetTinyThingComp(out _))
                    return AcceptanceReport.WasAccepted;

            return GetTinyBuilderSelect_NoTinyThing();
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();

            Map map = Find.CurrentMap;
            if (map == null) return;

            IntVec3 c = UI.MouseCell();
            Vector3 mouse = UI.MouseMapPosition();

            _hoverCandidate = null;
            float nearestDistance = float.MaxValue;

            foreach (Thing currentThing in c.GetThingList(map))
            {
                if (!(currentThing is Building) || !currentThing.TryGetTinyThingComp(out _)) continue;

                Vector3 currentPosition = currentThing.DrawPos;
                float currentDistance = (currentPosition.x - mouse.x) * (currentPosition.x - mouse.x) + (currentPosition.z - mouse.z) * (currentPosition.z - mouse.z);
                if (currentDistance < nearestDistance)
                {
                    nearestDistance = currentDistance;
                    _hoverCandidate = currentThing;
                }
            }

            if (_hoverCandidate != null) SelectionDrawer.DrawSelectionBracketFor(_hoverCandidate);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (_hoverCandidate != null)
            {
                OnClickTarget(_hoverCandidate);
            }
            else
            {
                Messages.Message(GetTinyBuilderSelect_NoTinyThing(), MessageTypeDefOf.RejectInput, false);
            }
        }

        public virtual void OnClickTarget(Thing target)
        {
            Find.Selector.ClearSelection();
            Find.Selector.Select(_hoverCandidate);
            var root = Find.MainButtonsRoot;
            if (root != null && root.tabs.OpenTab == MainButtonDefOf.Architect) root.tabs.EscapeCurrentTab();
        }
    }

    public class Designator_DeconstructNearestTiny : Designator_SelectNearestTiny
    {
        public static string GetTinyBuilderDeconstruct_Label() => "TinyBuilderDeconstruct_Label".Translate();
        public static string GetTinyBuilderDeconstruct_Description() => "TinyBuilderDeconstruct_Description".Translate();

        public Designator_DeconstructNearestTiny()
        {
            defaultLabel = GetTinyBuilderDeconstruct_Label();
            defaultDesc = GetTinyBuilderDeconstruct_Description();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Deconstruct", true);
            soundSucceeded = SoundDefOf.Tick_Tiny;
            hotKey = KeyBindingDefOf.Designator_Deconstruct;
            useMouseIcon = true;
        }

        public override void OnClickTarget(Thing target)
        {
            if (target is Building asBuilding)
            {
                Designator_Deconstruct Deconstructor = new Designator_Deconstruct();
                if (Deconstructor.CanDesignateThing(asBuilding))
                {
                    Deconstructor.DesignateThing(asBuilding);
                }
            }
        }
    }

    public class Designator_ClearTinyOffsets : Designator
    {
        public string GetTinyBuilderClear_Label() => "TinyBuilderClear_Label".Translate();
        public string GetTinyBuilderClear_Description() => "TinyBuilderClear_Description".Translate();
        public string GetTinyBuilderClear_Warning() => "TinyBuilderClear_Warning".Translate();
        public string GetTinyBuilderClear_Complete() => "TinyBuilderClear_Complete".Translate();

        public Designator_ClearTinyOffsets()
        {
            defaultLabel = GetTinyBuilderClear_Label();
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
        public class TinyOffset : IExposable
        {
            public List<Vector3> offsets = new List<Vector3>();

            public static implicit operator List<Vector3>(TinyOffset instance) => instance.offsets;

            public void ExposeData()
            {
                Scribe_Collections.Look(ref offsets, "InsideOffsets", LookMode.Value);
            }
        }
        public Dictionary<string, TinyOffset> offsets = new Dictionary<string, TinyOffset>();
        public Dictionary<string, float> lastDrawTimes = new Dictionary<string, float>();

        public MapComponent_TinyBuilder(Map map) : base(map) { }

        public static string GetBuildKey(BuildableDef def, IntVec3 cell, Rot4 rot, ThingDef stuff) => $"{def.defName}|{cell.x}|{cell.z}|{rot.AsInt}|{stuff?.defName}";
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
            Scribe_Collections.Look(ref offsets, "TinyOffsets", LookMode.Value, LookMode.Deep);
            if (offsets == null) offsets = new Dictionary<string, TinyOffset>();
        }

        public bool GetDrawableTime(string key, float wantTime)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (lastDrawTimes.TryGetValue(key, out float drawTime))
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

        bool ClearPredicate(Thing currentThing, IntVec3 cell, string originKey)
        {
            BuildableDef checkDef = null;
            ThingDef materialThing = null;
            if (currentThing is Blueprint_Build asBlueprint)
            {
                checkDef = asBlueprint.EntityToBuild();
                materialThing = asBlueprint.EntityToBuildStuff();
            }
            else if (currentThing is Frame asFrame)
            {
                checkDef = asFrame.def.entityDefToBuild;
                materialThing = asFrame.Stuff;
            }

            if (checkDef != null)
            {
                string currentKey = GetBuildKey(checkDef, cell, currentThing.Rotation, materialThing);

                return currentKey == originKey;
            }
            else
            {
                return false;
            }
        }

        public void ClearOffsets()
        {
            foreach (var currentPair in offsets)
            {
                List<Vector3> value = ((List<Vector3>)currentPair.Value);

                string key = currentPair.Key;
                IntVec3 currentCell = GetCellFromKey(key);
                int thingCount = currentCell.GetThingList(map).Count(current => ClearPredicate(current, currentCell, key));
                int offsetCount = value.Count;
                if (thingCount == 0)
                {
                    value.Clear();
                }
                else if (offsetCount > thingCount)
                {
                    value.RemoveRange(thingCount, offsetCount - thingCount);
                }
            }
            offsets.RemoveAll(currentPair => ((List<Vector3>)currentPair.Value).Count == 0);
        }

        public void AddOffset(BuildableDef def, IntVec3 cell, Vector3 offset, Rot4 rot, ThingDef stuff)
        {
            string key = GetBuildKey(def, cell, rot, stuff);
            if (!offsets.TryGetValue(key, out TinyOffset list))
            {
                list = new TinyOffset();
                offsets[key] = list;
            }
            ((List<Vector3>)list).Add(offset);
        }

        public void RemoveOffset(BuildableDef def, IntVec3 cell, Rot4 rot, ThingDef stuff)
        {
            string key = GetBuildKey(def, cell, rot, stuff);
            if (offsets.TryGetValue(key, out TinyOffset list) && ((List<Vector3>)list).Count > 0) ((List<Vector3>)list).RemoveLast();
        }
        public bool RemoveOffsetAt(BuildableDef def, IntVec3 cell, Rot4 rot, ThingDef stuff, int index)
        {
            string key = GetBuildKey(def, cell, rot, stuff);
            if (offsets.TryGetValue(key, out TinyOffset list))
            {
                List<Vector3> currentList = (List<Vector3>)list;
                if (currentList != null && index >= 0 && index < currentList.Count)
                {
                    currentList.RemoveAt(index);
                    if (currentList.Count == 0) offsets.Remove(key);
                    return true;
                }
            }
            return false;
        }
        public bool TryPopOffset(BuildableDef def, IntVec3 cell, Rot4 rot, ThingDef stuff, out Vector3 result)
        {
            string key = GetBuildKey(def, cell, rot, stuff);
            if (offsets.TryGetValue(key, out TinyOffset list) && ((List<Vector3>)list).Count > 0)
            {
                List<Vector3> currentOffsets = (List<Vector3>)list;
                result = currentOffsets[0];
                currentOffsets.RemoveAt(0);
                if (currentOffsets.Count == 0) offsets.Remove(key);
                return true;
            }
            result = default;
            return false;
        }

        public bool TryGetOffsets(BuildableDef def, IntVec3 cell, Rot4 rot, ThingDef stuff, out List<Vector3> list)
        {
            string key = GetBuildKey(def, cell, rot, stuff);
            if (offsets.TryGetValue(key, out TinyOffset currentOffset) && (list = (List<Vector3>)currentOffset) != null && list.Count > 0) return true;
            list = null;
            return false;
        }
        public bool TryGetOffsets(string key, out List<Vector3> list)
        {
            if (offsets.TryGetValue(key, out TinyOffset currentOffset) && (list = (List<Vector3>)currentOffset) != null && list.Count > 0) return true;
            list = null;
            return false;
        }

    }

    public class CompProperties_GlowerWithNotify : CompProperties_Glower
    {
        public CompProperties_GlowerWithNotify() => compClass = typeof(CompGlowerWithNotify);
    }

    public class CompGlowerWithNotify : CompGlower
    {
        public event System.Action OnColorChanged;
        protected override void SetGlowColorInternal(ColorInt? color)
        {
            base.SetGlowColorInternal(color);
            OnColorChanged?.Invoke();
        }
    }

    public class CompProperties_TinyThing : CompProperties
    {
        public CompProperties_TinyThing() => compClass = typeof(CompTinyThing);
        public bool sortDescending = false;
    }

    public class CompTinyThing : ThingComp
    {
        public Vector3 drawOffset = Vector3.zero;
        public Vector3? installOffset = null;

        public CompProperties_TinyThing Props => (CompProperties_TinyThing)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            bool hasInstallOffset = installOffset.HasValue;
            Vector3 tempInstallOffset = hasInstallOffset ? installOffset.Value : Vector3.zero;
            Scribe_Values.Look(ref drawOffset, "DrawOffset", Vector3.zero);
            Scribe_Values.Look(ref hasInstallOffset, "HasInstallOffset", false);
            Scribe_Values.Look(ref tempInstallOffset, "InstallOffset", Vector3.zero);
            if (hasInstallOffset) installOffset = tempInstallOffset;
            else installOffset = null;
            (parent as Building_Tiny)?.InitTiny(this);
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if(!respawningAfterLoad)
            {
                if (installOffset != null)
                {
                    drawOffset = installOffset.Value;
                }
                else if (drawOffset == Vector3.zero && parent.Map.TryGetTinyBuilder(out MapComponent_TinyBuilder builder))
                {
                    builder.TryPopOffset(parent.def, parent.Position, parent.Rotation, parent.Stuff, out drawOffset);
                }
            }

            (parent as Building_Tiny)?.InitTiny(this);
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

        public override Vector3 DrawPos => base.DrawPos + (asTiny?.drawOffset ?? Vector3.zero);

        public override void Print(SectionLayer layer)
        {
            if (asTiny == null || Graphic == null) { base.Print(layer); return; }

            Rot4 currentRotation = Rotation;
            Vector3 position = DrawPos + def.graphicData.DrawOffsetForRot(currentRotation);
            position.y = def.altitudeLayer.AltitudeFor();
            if (asTiny.Props.sortDescending) position.y += position.z * 0.0001f;
            Material mat = Graphic.MatAt(Rotation, this);
            bool needFlip = Graphic.NeedFlip(currentRotation);
            Vector2 finalSize = Graphic.drawSize;
            float angle = Graphic.ShouldDrawRotated ? Rotation.AsAngle : 0;

            Printer_Plane.PrintPlane(layer, position, finalSize, mat, angle, needFlip);
        }
    }

    public class Building_TinyLamp : Building_Tiny
    {
        CompGlowerWithNotify asGlower;
        public override Color DrawColorTwo
        {
            get
            {
                if (asGlower == null) return base.DrawColorTwo;
                else return asGlower.GlowColor.ToColor + Color.black;
            }
        }

        public void InitGlow()
        {
            if(this.TryGetComp(out asGlower))
            {
                asGlower.OnColorChanged -= Notify_ColorChanged;
                asGlower.OnColorChanged += Notify_ColorChanged;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            InitGlow();
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

            if (map.TryGetTinyBuilder(out MapComponent_TinyBuilder builder))
            {
                ThingDef stuff = null;
                if (Find.DesignatorManager.SelectedDesignator is Designator_Build asBuild) stuff = asBuild.StuffDef;
                builder.AddOffset(def, loc, loc.GetMouseOffset(), rot, stuff);
            }
        }

    }
}
