using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace CallToArms
{
    public static class Extension_Pawn
    {
        public static bool IsDraftable(this Pawn target, Map map) => target.Spawned && !target.DestroyedOrNull() && target.Faction == Faction.OfPlayer && target.drafter != null && !target.drafter.Drafted && !target.DeadOrDowned && !target.InMentalState && target.Map == map;
		public static bool IsValidArea(this Area area, Map map) => area.Map != null && area.Map == map && map.areaManager.AllAreas.Contains(area);

		public static bool IsChild(this Pawn target) => (target?.ageTracker?.CurLifeStage?.developmentalStage ?? DevelopmentalStage.Adult) <= DevelopmentalStage.Child;

		public static Pawn IsCarryingBaby(this Pawn target)
		{
			if (!ModsConfig.BiotechActive) return null;

			Pawn carriedPawn = target.carryTracker?.CarriedThing as Pawn;
			if(carriedPawn != null && carriedPawn.RaceProps.Humanlike)
			{
				DevelopmentalStage stage = carriedPawn.ageTracker?.CurLifeStage?.developmentalStage ?? DevelopmentalStage.Adult;

				return stage <= DevelopmentalStage.Baby ? carriedPawn : null;
			}
			return null;
		}
	}

    public class ITab_EquipmentSetting : ITab
	{
		static ThingFilter savedFilter = null;
		static bool savedWithWeapon = false;
		static bool savedWithUtility = false;

		const string tabNameKey = "CallToArms_Tab_EquipmentSelecter";

        static readonly Vector2 tabSize = new Vector2(400f, 600f);

        const float headerHeight = 28f;
        const float headerPadding = 16.0f;
        const float filterPadding = 10.0f;
		const float allowLineHeight = 24.0f;
        const float copyButtonSize = 20.0f;

		ThingFilterUI.UIState filterState = new ThingFilterUI.UIState();

        Rect windowRect, headerRect, copyRect, pasteRect, withWeaponLabelRect, withWeaponCheckRect, withUtilityLabelRect, withUtilityCheckRect, filterRect;

		public static string GetDraftAllowDraftWithoutWeaponLabelString() => "CallToArms_AllowDraftWithoutWeapon_Label".Translate();
		public static string GetDraftAllowDraftWithoutUtilityLabelString() => "CallToArms_AllowDraftWithoutUtility_Label".Translate();

		public ITab_EquipmentSetting()
		{
			size = tabSize;
			labelKey = tabNameKey;
            windowRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            headerRect = windowRect;
            headerRect.height = headerHeight;
            copyRect = headerRect;
            copyRect.width = copyRect.height = copyButtonSize;
            copyRect.x = size.x - copyRect.width - 30.0f;
            pasteRect = copyRect;
            pasteRect.width = copyButtonSize;
            pasteRect.x -= copyButtonSize;

            float currentHeight = headerHeight + headerPadding;
			float filterWidth = windowRect.width - (filterPadding * 2);

			withWeaponLabelRect = new Rect(filterPadding, currentHeight, filterWidth - allowLineHeight, allowLineHeight);
			withWeaponCheckRect = new Rect(withWeaponLabelRect.width + filterPadding, currentHeight, allowLineHeight, allowLineHeight);
			currentHeight += allowLineHeight;
			withUtilityLabelRect = new Rect(filterPadding, currentHeight, filterWidth - allowLineHeight, allowLineHeight);
			withUtilityCheckRect = new Rect(withUtilityLabelRect.width + filterPadding, currentHeight, allowLineHeight, allowLineHeight);
			currentHeight += allowLineHeight;

			filterRect = new Rect(filterPadding, currentHeight, filterWidth, windowRect.height - currentHeight - filterPadding);
        }

		protected override void FillTab()
		{
            Event currentEvent = Event.current;

            CompEmergencyDrafter drafter = SelThing?.TryGetComp<CompEmergencyDrafter>();
            Map currentMap = SelThing?.Map;
            if (drafter == null || currentMap == null) return;
            if (Widgets.ButtonImage(copyRect, CallToArmsTextures.CopyTexture))
            {
				if (savedFilter == null) savedFilter = new ThingFilter();
				savedFilter.CopyAllowancesFrom(drafter.EquipmentFilter);
                savedWithWeapon = drafter.draftWithoutWeapon;
                savedWithUtility = drafter.draftWithoutUtility;
            }

            if (savedFilter != null && Widgets.ButtonImage(pasteRect, CallToArmsTextures.PasteTexture))
            {
                drafter.EquipmentFilter = savedFilter;
				drafter.draftWithoutWeapon = savedWithWeapon;
				drafter.draftWithoutUtility = savedWithUtility;
			}

            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, tabNameKey.Translate());
            Text.Font = GameFont.Small;

			Widgets.Label(withWeaponLabelRect, GetDraftAllowDraftWithoutWeaponLabelString());
			Widgets.Checkbox(withWeaponCheckRect.position, ref drafter.draftWithoutWeapon);

			Widgets.Label(withUtilityLabelRect, GetDraftAllowDraftWithoutUtilityLabelString());
			Widgets.Checkbox(withUtilityCheckRect.position, ref drafter.draftWithoutUtility);

			ThingFilterUI.DoThingFilterConfigWindow(filterRect, filterState, drafter.EquipmentFilter, CompEmergencyDrafter.OriginFilter,1,null,CompEmergencyDrafter.OriginFilter.hiddenSpecialFilters,true,true);
        }
    }


	public class ITab_DraftSetting : ITab
    {
		static List<Pawn> savedList = null;
		Area savedArea = null;

		static readonly Vector2 tabSize = new Vector2(400f,600f);
		static readonly Vector2 portraitSize = new Vector2(rowHeight, rowHeight);

        const string tabNameKey = "CallToArms_Tab_DraftSelecter";
		const float headerHeight = 28f;
		const float headerPadding = 16.0f;
		const float copyButtonSize = 20.0f;
		const float listPadding = 5.0f;
		const float viewPadding = 16.0f;
		const float rowPadding = 0.0f;
        const float rowHeight = 30f;
		const float checkSize = 24f;

        Rect windowRect, headerRect, copyRect, rowRect, pawnInfoRect, pasteRect, countRect, listRect, viewRect,
			checkRect, draftAreaRect, draftAllRect, pawnPortraitRect, pawnDetailRect, pawnNameRect, pawnWeaponRect, pawnWeaponDetailRect;

        Vector2 scrollPosition;
		bool allSelected = false;
		bool? mouseSelected = null;


        public static string GetDraftAllString() => "CallToArms_Button_DraftAll".Translate();
		public static string GetDraftAreaEmptyString() => "CallToArms_Button_DraftAreaEmpty".Translate();
		public static string GetDrafterCountString(int count) => "CallToArms_DrafterCount_Label".Translate(count.Named("count"));


		public ITab_DraftSetting()
        {
            size = tabSize;
            labelKey = tabNameKey;

            windowRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            headerRect = windowRect;
            headerRect.height = headerHeight;
			copyRect = headerRect;
            copyRect.width = copyRect.height = copyButtonSize;
            copyRect.x = size.x - copyRect.width - 30.0f;
            pasteRect = copyRect;
            pasteRect.width = copyButtonSize;
            pasteRect.x -= copyButtonSize;
			float currentHeight = headerHeight + headerPadding;
			countRect = headerRect;
			countRect.y = currentHeight;
			currentHeight += countRect.height;
			listRect = new Rect(windowRect.x, currentHeight, windowRect.width, 0.0f);

            viewRect = new Rect(0f, 0f, listRect.width - viewPadding, 0f);
            rowRect = new Rect(rowPadding, 0f, viewRect.width, rowHeight);
            pawnInfoRect = new Rect(rowRect.x, rowRect.y, viewRect.width - checkSize - rowPadding, rowHeight);
            checkRect = new Rect(viewRect.width - checkSize, rowRect.y, checkSize, checkSize);

            draftAreaRect = pawnInfoRect;
            draftAreaRect.x = viewPadding;
            draftAreaRect.width = windowRect.width - 150.0f;
            draftAreaRect.y = currentHeight;

            draftAllRect = draftAreaRect;
            draftAllRect.x = draftAreaRect.x + draftAreaRect.width + viewPadding;
            draftAllRect.width = windowRect.width - draftAllRect.x - viewPadding;

            listRect.y = currentHeight += draftAllRect.height + viewPadding;
            listRect.height = windowRect.height - currentHeight - listPadding;

            pawnPortraitRect = pawnInfoRect;
            pawnPortraitRect.width = rowHeight;
            pawnDetailRect = pawnPortraitRect;
            pawnDetailRect.x = pawnPortraitRect.width;
            pawnNameRect = pawnInfoRect;
            pawnNameRect.x = pawnDetailRect.x + pawnDetailRect.width;
            pawnNameRect.width = pawnInfoRect.width - pawnNameRect.x - pawnPortraitRect.width;
            pawnWeaponRect = pawnPortraitRect;
            pawnWeaponRect.x = pawnNameRect.x + pawnNameRect.width;
            pawnWeaponDetailRect = pawnWeaponRect;
            pawnWeaponDetailRect.x = pawnWeaponRect.x - pawnWeaponRect.width;
        }

        protected override void FillTab()
        {
			Event currentEvent = Event.current;

			CompEmergencyDrafter drafter = SelThing?.TryGetComp<CompEmergencyDrafter>();
            Map currentMap = SelThing?.Map;
            if (drafter == null || currentMap == null) return;

			
			if (Widgets.ButtonImage(copyRect, CallToArmsTextures.CopyTexture))
			{
				savedList = drafter.GetSelected();
				savedArea = drafter.DraftArea;
			}

			if (savedList != null && Widgets.ButtonImage(pasteRect, CallToArmsTextures.PasteTexture))
			{
				drafter.SetSelected(savedList, true);
				drafter.DraftArea = savedArea;
			}

			Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, tabNameKey.Translate());
            Text.Font = GameFont.Small;

			Widgets.Label(countRect, GetDrafterCountString(drafter.GetSelectedCount()));

			List<Pawn> colonists = Find.ColonistBar.GetColonistsInOrder();
            viewRect.height = colonists.Count * rowHeight;

			drafter.CheckValidDraftArea();
			bool wasAllSelected = allSelected;
			float currentHeight = headerHeight + headerPadding;
			string draftAreaEmpty = GetDraftAreaEmptyString();
            string draftButtonText = drafter.DraftArea?.Label ?? draftAreaEmpty;
			if (Widgets.ButtonText(draftAreaRect, draftButtonText))
			{
				List<FloatMenuOption> areaOptions = new List<FloatMenuOption>();
				areaOptions.Add(new FloatMenuOption(draftAreaEmpty, () => drafter.DraftArea = null));

				foreach (Area currentArea in currentMap.areaManager.AllAreas.OfType<Area_Allowed>())
				{
					Area tempArea = currentArea;
					areaOptions.Add(new FloatMenuOption(tempArea.Label, () => drafter.DraftArea = tempArea));
				}
				Find.WindowStack.Add(new FloatMenu(areaOptions));
			}
			Widgets.Label(draftAllRect, GetDraftAllString());
			Widgets.Checkbox(draftAllRect.position + (Vector2.right * draftAllRect.width), ref allSelected);
			if (wasAllSelected != allSelected) drafter.SetSelected(colonists, allSelected);

			bool isLeftClick = currentEvent.button == 0;
			if (!isLeftClick && mouseSelected.HasValue) mouseSelected = null;
			
			Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
			allSelected = true;
			pawnPortraitRect.y = pawnDetailRect.y = pawnWeaponRect.y = pawnWeaponDetailRect.y = pawnNameRect.y = pawnInfoRect.y = rowRect.y = 0.0f;
			foreach (Pawn currentColonist in colonists)
            {
				RenderTexture currentPortrait = PortraitsCache.Get(currentColonist, portraitSize, Rot4.South);
				ThingWithComps weapon = currentColonist.equipment?.Primary;
                Widgets.DrawHighlightIfMouseover(rowRect);
                if(currentColonist.Drafted) Widgets.DrawLightHighlight(rowRect);

				bool wasSelected = drafter.IsSelected(currentColonist);
				bool isSelected = wasSelected;
				bool tempSelected = isSelected;
				checkRect.y = rowRect.y;
				GUI.DrawTexture(pawnPortraitRect, currentPortrait);
				TipSignal currentColonistTip = new TipSignal($"{currentColonist.LabelCap}\n{currentColonist.GetInspectString()}", currentColonist.thingIDNumber);
                TooltipHandler.TipRegion(pawnPortraitRect, currentColonistTip);
				Widgets.InfoCardButton(pawnDetailRect.x, pawnDetailRect.y, currentColonist);
				if(weapon != null)
				{
					Widgets.ThingIcon(pawnWeaponRect, weapon);
					TooltipHandler.TipRegion(pawnWeaponRect, new TipSignal(weapon.LabelCapNoCount, weapon.thingIDNumber));
                    Widgets.InfoCardButton(pawnWeaponDetailRect.x, pawnWeaponDetailRect.y, weapon);
                }
                Widgets.Label(pawnNameRect, currentColonist.LabelCap);
                TooltipHandler.TipRegion(pawnNameRect, currentColonistTip);
                Widgets.Checkbox(checkRect.position, ref tempSelected);
				if(currentEvent.isMouse && checkRect.Contains(currentEvent.mousePosition) && isLeftClick)
				{
					if(currentEvent.type == EventType.MouseDown) mouseSelected = isSelected = !wasSelected;
					else if(mouseSelected.HasValue)
					{
						if (currentEvent.type == EventType.MouseDrag)	isSelected = mouseSelected.Value;
						else											mouseSelected = null;
					}
				}

				if (isSelected != wasSelected)
				{
					drafter.SetSelected(currentColonist, isSelected);
				}

				if(Widgets.ButtonInvisible(pawnInfoRect))
				{
					switch (currentEvent.button)
					{
						case 0:
							CameraJumper.TryJump(currentColonist);
                            Find.Selector.ClearSelection();
                            Find.Selector.Select(currentColonist, playSound: true);
                            break;
					}
					currentEvent.Use();
				}

				pawnPortraitRect.y = pawnDetailRect.y = pawnWeaponRect.y = pawnWeaponDetailRect.y = pawnNameRect.y = pawnInfoRect.y = rowRect.y += rowHeight;
				allSelected &= isSelected;
            }
            Widgets.EndScrollView();

			currentHeight += colonists.Count * rowHeight;
        }
    }

	public class PlaceWorker_TownBellDraftArea : PlaceWorker
	{
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            base.DrawGhost(def, center, rot, ghostCol, thing);
			Map currentMap = Find.CurrentMap;
			if (currentMap == null) return;
			CompProperties_EmergencyDrafter props = def.GetCompProperties<CompProperties_EmergencyDrafter>();

            GenDraw.DrawRadiusRing(center, Mathf.Max(1, props?.draftRadius ?? 8));
        }
    }

    public class CompProperties_EmergencyDrafter : CompProperties
    {
		public int draftRadius = 25;
        public CompProperties_EmergencyDrafter()
        {
            compClass = typeof(CompEmergencyDrafter);
        }
    }

	public class Building_TownBell : Building
	{

	}

    public class JobDriver_DraftAsJob : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_General.DoAtomic(() =>
            {
                if (pawn.IsDraftable(pawn.Map))
                {
                    Queue<Job> origins = new Queue<Job>();
                    foreach (QueuedJob currentQueue in pawn.jobs.jobQueue.ToArray()) origins.Enqueue(currentQueue.job.Clone());
                    pawn.drafter.Drafted = true;
					pawn.jobs.ClearQueuedJobs();

                    Job firstJob = origins.Count > 0 ? origins.Dequeue() : null;
                    foreach (Job currentQueue in origins) pawn.jobs.jobQueue.EnqueueLast(currentQueue);
                    pawn.jobs.StartJob(firstJob, JobCondition.InterruptForced, pawn.thinker.MainThinkNodeRoot, false, true, null, JobTag.DraftedOrder, true);
                }
            });
        }
    }


    public class CompEmergencyDrafter : ThingComp
    {
        public static Texture2D GetCallToArms4Selected_MenuIcon() => ContentFinder<Texture2D>.Get("UI/Commands/CallToArms_Selected", true);
        public static string GetCallToArms4SelectedLabelString() => "CallToArms_Selected_Label".Translate();
        public static string GetCallToArms4SelectedDescriptionString() => "CallToArms_Selected_Description".Translate();
        public static string GetCallToArms4NotSelectedDescriptionString() => "CallToArms_Not_Selected_Description".Translate();
        public static string GetCallToArms4HasNotDraftableDescriptionString() => "CallToArms_HasNot_Draftable_Description".Translate();

        public static Texture2D GetCallToArms4All_MenuIcon() => ContentFinder<Texture2D>.Get("UI/Commands/CallToArms_All", true);
        public static string GetCallToArms4AllLabelString() => "CallToArms_All_Label".Translate();
        public static string GetCallToArms4AllDescriptionString() => "CallToArms_All_Description".Translate();

		public static Texture2D GetDraftAllowCarryingBaby_MenuIcon() => ContentFinder<Texture2D>.Get("UI/Commands/DraftWithBaby", true);
		public static string GetDraftAllowCarryingBabyLabelString() => "CallToArms_AllowCarryingBaby_Label".Translate();
		public static string GetDraftAllowCarryingBabyDescriptionString() => "CallToArms_AllowCarryingBaby_Description".Translate();
		public static Texture2D GetDraftAllowDraftChild_MenuIcon() => ContentFinder<Texture2D>.Get("UI/Commands/DraftChild", true);
		public static string GetDraftAllowDraftChildLabelString() => "CallToArms_AllowDraftChild_Label".Translate();
		public static string GetDraftAllowDraftChildDescriptionString() => "CallToArms_AllowDraftChild_Description".Translate();

		public static string GetDraftAreaNotEnoughString(int count) => "CallToArms_Message_DraftAreaNotEnough".Translate(count.Named("count"));
		public static string GetDraftCancelByCarryingBabyString(int count) => "CallToArms_Message_DraftCancelByCarryingBaby".Translate(count.Named("count"));
        public static string GetDrafterCountString(int count) => "CallToArms_DrafterCount_Label".Translate(count.Named("count"));

        List<Pawn> selectedColonist = new List<Pawn>();

        static ThingFilter _originFilter;
        public static ThingFilter OriginFilter => _originFilter;

        ThingFilter _equipmentFilter;
		public ThingFilter EquipmentFilter
		{
			get => _equipmentFilter;
			set
			{
				if (_equipmentFilter == null) _equipmentFilter = new ThingFilter(); 
				_equipmentFilter.CopyAllowancesFrom(value);
			}
		}
        public CompProperties_EmergencyDrafter Props => (CompProperties_EmergencyDrafter)props;

		public bool draftGlobal = false;

		public bool draftCarryingBaby = false;
		public bool draftChild = false;
		public bool draftWithoutWeapon = true;
		public bool draftWithoutUtility = true;

        Area _draftArea;
        public Area DraftArea
        {
            get => HasValidDraftArea() ? _draftArea : null;
            set => _draftArea = value;
        }

        public bool HasSelectedColonist => selectedColonist.Count > 0;
        public bool HasDraftableSelectedColonist => selectedColonist.Any((current) => current != null && current.IsDraftable(parent.Map));

		public bool HasValidDraftArea() => _draftArea != null && _draftArea.IsValidArea(parent.Map);
		public void CheckValidDraftArea() { if (_draftArea != null && !_draftArea.IsValidArea(parent.Map)) _draftArea = null; }
        public bool IsSelected(Pawn target) => selectedColonist.Contains(target);
		public void ToggleSelected(Pawn target) { if (IsSelected(target)) selectedColonist.Remove(target); else selectedColonist.Add(target); }
		public void SetSelected(Pawn target, bool value) { if (value) { if(!IsSelected(target))selectedColonist.Add(target); } else selectedColonist.Remove(target); }
		public void SetSelected(List<Pawn> newList, bool value) 
		{
			selectedColonist.Clear();
			if (value) selectedColonist.AddRange(newList.Where(current => current.Map == parent.Map)); 
		}
		public List<Pawn> GetSelected() => selectedColonist;
		public int GetSelectedCount() => selectedColonist?.Count ?? 0;
		public bool GetAllowCarryingBaby() => draftCarryingBaby;
		public void SetAllowCarryingBaby(bool value) => draftCarryingBaby = value;
		public void ToggleAllowCarryingBaby() => SetAllowCarryingBaby(!draftCarryingBaby);

        public bool GetAllowDraftChild() => draftChild;
        public void SetAllowDraftChild(bool value) => draftChild = value;
        public void ToggleAllowDraftChild() => SetAllowDraftChild(!draftChild);

        public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			SetFilterAllow();
		}

        public override string CompInspectStringExtra()
        {
            string result = $"{base.CompInspectStringExtra()}{GetDrafterCountString(GetSelectedCount())}";

            return result;
        }


        public virtual void SetFilterAllow()
		{
			if (_originFilter == null)
			{
				_originFilter = new ThingFilter();
				_originFilter.SetAllow(ThingCategoryDefOf.Weapons, true);
				if (!ModsConfig.OdysseyActive) _originFilter.SetAllow(ThingCategoryDefOf.WeaponsUnique, true);

				_originFilter.SetAllow(ThingCategoryDefOf.ApparelUtility, true);

				SpecialThingFilterDef smeltable = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowSmeltable");
				SpecialThingFilterDef nonSmeltableWeapons = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowNonSmeltableWeapons");
				SpecialThingFilterDef burnableWeapons = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowBurnableWeapons");
				SpecialThingFilterDef nonBurnableWeapons = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowNonBurnableWeapons");
				SpecialThingFilterDef biocodedWeapons = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowBiocodedWeapons");
				SpecialThingFilterDef nonBiocodedWeapons = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowNonBiocodedWeapons");


				SpecialThingFilterDef smeltableApparel = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowSmeltableApparel");
				SpecialThingFilterDef nonSmeltableApparel = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowNonSmeltableApparel");
				SpecialThingFilterDef burnableApparel = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowBurnableApparel");
				SpecialThingFilterDef nonBurnableApparel = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowNonBurnableApparel");
                SpecialThingFilterDef biocodedApparel = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowBiocodedApparel");
                SpecialThingFilterDef nonBiocodedApparel = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowNonBiocodedApparel");

				if(biocodedApparel != null)		_originFilter.SetAllow(biocodedApparel, false);
				if(biocodedWeapons != null)		_originFilter.SetAllow(biocodedWeapons, false);

				if(smeltable != null)			_originFilter.hiddenSpecialFilters.Add(smeltable);
				if(nonSmeltableWeapons != null)	_originFilter.hiddenSpecialFilters.Add(nonSmeltableWeapons);
                if(burnableWeapons != null)		_originFilter.hiddenSpecialFilters.Add(burnableWeapons);
                if(nonBurnableWeapons != null)	_originFilter.hiddenSpecialFilters.Add(nonBurnableWeapons);
                if(nonBiocodedWeapons != null)	_originFilter.hiddenSpecialFilters.Add(nonBiocodedWeapons);

				if(smeltableApparel != null)	_originFilter.hiddenSpecialFilters.Add(smeltableApparel);
				if(nonSmeltableApparel != null)	_originFilter.hiddenSpecialFilters.Add(nonSmeltableApparel);
				if(burnableApparel != null)		_originFilter.hiddenSpecialFilters.Add(burnableApparel);
                if(nonBurnableApparel != null)	_originFilter.hiddenSpecialFilters.Add(nonBurnableApparel);
				if(nonBiocodedApparel != null)	_originFilter.hiddenSpecialFilters.Add(nonBiocodedApparel);
				_originFilter.hiddenSpecialFilters.Add(SpecialThingFilterDefOf.AllowNonDeadmansApparel);
			}
			if (EquipmentFilter == null)
			{
				EquipmentFilter = _originFilter;
                EquipmentFilter.SetAllow(SpecialThingFilterDefOf.AllowDeadmansApparel, false);
            }
        }

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Collections.Look(ref selectedColonist, "SelectedPawn", LookMode.Reference);
			Scribe_References.Look(ref _draftArea, "DraftArea");
			Scribe_Deep.Look(ref _equipmentFilter, "EquipmentFilter");
			Scribe_Values.Look(ref draftCarryingBaby, "AllowCarryingBaby");
			Scribe_Values.Look(ref draftChild, "AllowDraftChild");
			Scribe_Values.Look(ref draftWithoutWeapon, "DraftWithoutWeapon", true);
			Scribe_Values.Look(ref draftWithoutUtility, "DraftWithoutUtility", true);
			if(Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				selectedColonist?.RemoveAll((currentPawn) => currentPawn.DestroyedOrNull());
				SetFilterAllow();
			}
		}

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
			yield return new Command_Action
			{
				Disabled = !HasDraftableSelectedColonist,
				disabledReason = !HasSelectedColonist ? GetCallToArms4NotSelectedDescriptionString()
								: !HasDraftableSelectedColonist ? GetCallToArms4HasNotDraftableDescriptionString() : "",
				defaultLabel = GetCallToArms4SelectedLabelString(),
				defaultDesc = GetCallToArms4SelectedDescriptionString(),
				icon = GetCallToArms4Selected_MenuIcon(),
				action = OnCallToArms4Selected,
				hotKey = KeyBindingDefOf.Command_ColonistDraft
			};

            yield return new Command_Action
            {
                defaultLabel = GetCallToArms4AllLabelString(),
                defaultDesc = GetCallToArms4AllDescriptionString(),
                icon = GetCallToArms4All_MenuIcon(),
                action = OnCallToArms4All,
				hotKey = KeyBindingDefOf.Command_ItemForbid
            };

			if(ModsConfig.BiotechActive)
			{
                yield return new Command_Toggle
                {
                    isActive = GetAllowDraftChild,
                    defaultLabel = GetDraftAllowDraftChildLabelString(),
                    defaultDesc = GetDraftAllowDraftChildDescriptionString(),
                    icon = GetDraftAllowDraftChild_MenuIcon(),
                    toggleAction = ToggleAllowDraftChild
                };
                yield return new Command_Toggle
				{
					isActive = GetAllowCarryingBaby,
					defaultLabel = GetDraftAllowCarryingBabyLabelString(),
					defaultDesc = GetDraftAllowCarryingBabyDescriptionString(),
					icon = GetDraftAllowCarryingBaby_MenuIcon(),
					toggleAction = ToggleAllowCarryingBaby
				};
            }
        }


		bool DraftPredicate(Pawn target) => 
			(!ModsConfig.BiotechActive ||
			(draftChild || 
			!target.IsChild()) && 
			(draftCarryingBaby || target.IsCarryingBaby() == null)) && 
			target.IsDraftable(parent.Map);

		public bool HasDisallowedUtility(Pawn target, out Apparel result)
		{
            IEnumerable<Apparel> utilityfinder = target.apparel.WornApparel.Where(current => current.HasThingCategory(ThingCategoryDefOf.ApparelUtility));

			if (utilityfinder.Count() == 0)
			{
				result = null;
				return !draftWithoutUtility;
			}
			result = utilityfinder.FirstOrDefault();

			return !EquipmentFilter.Allows(result);
		}
        public IEnumerable<Thing> GetAllowedUtility()
		{
			float draftRadius = Props.draftRadius;
            IntVec3 originPosition = parent.Position;
            return parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel)
                .Where(current => current.HasThingCategory(ThingCategoryDefOf.ApparelUtility) && current.Position.DistanceTo(originPosition) <= draftRadius &&EquipmentFilter.Allows(current))
                .OrderBy(current => current.Position.DistanceToSquared(originPosition));
        }

        public bool HasDisallowedWeapon(Pawn target, out ThingWithComps result)
		{
			float draftRadius = Props.draftRadius;
			result = target.equipment.Primary;
			if (result == null) return !draftWithoutWeapon;
            return !EquipmentFilter.Allows(result);
		}

		public IEnumerable<Thing> GetAllowedWeapon()
		{
			float draftRadius = Props.draftRadius;
			IntVec3 originPosition = parent.Position;
			return parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
				.Where(current => current.Position.DistanceTo(originPosition) <= draftRadius && EquipmentFilter.Allows(current))
				.OrderBy(current => current.Position.DistanceToSquared(originPosition));
        }

		IEnumerable<IntVec3> GetDraftableSpots(IntVec3 from, int radius, Area targetArea = null)
		{
			Map currentMap = parent.Map;
			Pawn onCellPawn;
			return GenRadial.RadialCellsAround(from, radius, true)
				.Where(current => current.InBounds(currentMap) && (targetArea == null || targetArea[current]) && current.Standable(currentMap) && ((onCellPawn = current.GetFirstPawn(currentMap)) == null || !onCellPawn.Drafted))
				.OrderBy(current => current.DistanceToSquared(from));
		}

		void DraftAndMove(Pawn target, IntVec3 location, List<Thing> allowedWeapons, List<Thing> allowedUtility)
		{
			if (target == null) return;
			Map map = target.Map;
			if (map != parent.Map) return;

			Queue<Job> jobs = new Queue<Job>();
			bool isShift = Event.current.shift;
			if (!isShift) target.jobs.ClearQueuedJobs();

            Pawn carryingBaby = target.IsCarryingBaby();
			if (draftCarryingBaby && carryingBaby != null)
			{
				Job safetyJob = JobMaker.MakeJob(JobDefOf.BringBabyToSafety, carryingBaby);
				safetyJob.count = 1;
                JobEnqueue(jobs, safetyJob);
			}

            JobEnqueue(jobs, JobMaker.MakeJob(CallToArmsDefs.DraftAsJob));
            JobEnqueue(jobs, JobMaker.MakeJob(JobDefOf.Goto, location));


            bool isDisallowedWeapon = HasDisallowedWeapon(target, out ThingWithComps disallowedWeapon);
			bool needChangeWeapon = isDisallowedWeapon && disallowedWeapon != null;
			bool isDisallowedUtility = HasDisallowedUtility(target, out Apparel disallowedUtility);
			bool needChangeUtility = isDisallowedUtility && disallowedUtility != null;

			Predicate<Thing> thingCheckPredicate = currentThing =>
                !currentThing.IsForbidden(target) &&
				target.CanReserveAndReach(currentThing, PathEndMode.Touch, Danger.Deadly);

            if (isDisallowedWeapon || isDisallowedUtility)
            {
                if (needChangeWeapon) JobEnqueue(jobs, JobMaker.MakeJob(JobDefOf.DropEquipment, disallowedWeapon));
                if (needChangeUtility) JobEnqueue(jobs, JobMaker.MakeJob(JobDefOf.RemoveApparel, disallowedUtility));

				bool isMoved = false;
                if (!draftWithoutWeapon && isDisallowedWeapon)
                {
					if(allowedWeapons.Count > 0)
					{
                        Thing focusWeapon = allowedWeapons.Find(thingCheckPredicate);
						if (focusWeapon != null)
						{
							JobEnqueue(jobs, JobMaker.MakeJob(JobDefOf.Equip, focusWeapon));
							allowedWeapons.Remove(focusWeapon);
							isMoved = true;
						}
					}
                }

                if (!draftWithoutUtility && isDisallowedUtility)
                {
                    if (allowedUtility.Count > 0)
                    {
                        Thing focusUtility = allowedUtility.Find(thingCheckPredicate);
                        if (focusUtility != null)
                        {
                            JobEnqueue(jobs, JobMaker.MakeJob(JobDefOf.Wear, focusUtility));
                            allowedUtility.Remove(focusUtility);
							isMoved = true;
                        }
                    }
                }
			
				if(isMoved) JobEnqueue(jobs, JobMaker.MakeJob(JobDefOf.Goto, location));
			}


            Building interactionBuilding = map.listerBuildings.allBuildingsColonist.Find(current => (current.def?.hasInteractionCell ?? false) && current.InteractionCell == location && (current.GetComp<CompMannable>() != null));
			if (interactionBuilding != null)
			{
				JobEnqueue(jobs, JobMaker.MakeJob(JobDefOf.ManTurret, interactionBuilding));
			}
			else
			{
				JobEnqueue(jobs, JobMaker.MakeJob(JobDefOf.Goto, location));
			}

			if (!isShift)
			{
				Job firstJob = jobs.Dequeue();
				target.jobs.StartJob(firstJob, JobCondition.InterruptForced, target.thinker.MainThinkNodeRoot, false, true, null, JobTag.DraftedOrder, true);
            }

            foreach (Job currentJob in jobs) target.jobs.jobQueue.EnqueueLast(currentJob);
        }

		public virtual void JobEnqueue(Queue<Job> result, Job wantJob)
		{
            wantJob.playerForced = true;
            result.Enqueue(wantJob);
        }

		public void CheckCarryingBabyAlert(List<Pawn> from)
		{
			if (draftCarryingBaby) return;
            IEnumerable<Pawn> carryingBabyTargets = from.Where(current => current.IsCarryingBaby() != null);
            int carryingBabyCount = carryingBabyTargets.Count();
            if (carryingBabyCount > 0) Messages.Message(GetDraftCancelByCarryingBabyString(carryingBabyCount), carryingBabyTargets.ToList(), MessageTypeDefOf.NegativeEvent, false);
        }

        public void OnCallToArms4Selected()
		{
			CheckValidDraftArea();
            if (selectedColonist.Any(current => current.DestroyedOrNull())) selectedColonist = selectedColonist.Where(current => !current.DestroyedOrNull()).ToList();

			CheckCarryingBabyAlert(selectedColonist);

            List<Pawn> draftTargets = selectedColonist
			.Where(DraftPredicate)
			.OrderBy(current => current.Position.DistanceToSquared(parent.Position))
			.ToList();
			CalltoArms(draftTargets);
		}

		public void OnCallToArms4All()
        {
			List<Pawn> colonist = Find.ColonistBar.GetColonistsInOrder();
            CheckCarryingBabyAlert(colonist);

            List<Pawn> draftTargets = colonist
                .Where(DraftPredicate)
                .ToList();

			CalltoArms(draftTargets);
		}

		void CalltoArms(List<Pawn> targetList)
		{
			if(targetList == null) return;

			List<IntVec3> draftLocations = GetDraftableSpots(parent.Position, Props.draftRadius, DraftArea).ToList();

			int originCount = targetList.Count();
			int maxCount = Mathf.Min(originCount, draftLocations.Count());
			List<Thing> allowedWeapons = GetAllowedWeapon().ToList();
			List<Thing> allowedUtilitys = GetAllowedUtility().ToList();

            if (Find.Selector.IsSelected(parent)) Find.Selector.ClearSelection();

            for (int i = 0; i < maxCount; i++)
			{
				Pawn currentTarget = targetList[i];
				DraftAndMove(currentTarget, draftLocations[i], allowedWeapons, allowedUtilitys);
				Find.Selector.Select(currentTarget, playSound: true);
			}

			if(originCount > maxCount)
			{
				List<Pawn> missingPawns = new List<Pawn>();
				for (int i = maxCount; i < originCount; i++){missingPawns.Add(targetList[i]);}

				Messages.Message(GetDraftAreaNotEnoughString(originCount - maxCount), missingPawns, MessageTypeDefOf.NegativeEvent, false);
			}
		}
	}
}
