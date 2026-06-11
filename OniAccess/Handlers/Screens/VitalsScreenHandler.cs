using System;
using System.Collections.Generic;

using Klei.AI;

using OniAccess.Handlers.Tiles;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// 2D grid handler for the VitalsTableScreen (duplicant health stats).
	///
	/// Builds a virtual table from live game state on every navigation event.
	/// Column list is built once on activation; DLC3 Power Banks column is
	/// conditionally included.
	///
	/// Column headers include static descriptions (sourced from game Amount/Attribute
	/// data). Data cells assemble value + delta + modifier sources programmatically
	/// rather than using pre-formatted game tooltips.
	/// </summary>
	public class VitalsScreenHandler: BaseTableHandler {
		struct ColumnDef {
			public string Name;
			public string HeaderDescription;
			public Func<MinionIdentity, string> GetValue;
			public Func<MinionIdentity, float> GetSortValue;
			public bool Sortable;
		}

		List<ColumnDef> _columns;

		public override string DisplayName => STRINGS.ONIACCESS.VITALS_SCREEN.HANDLER_NAME;

		public VitalsScreenHandler(KScreen screen) : base(screen) { }

		// ========================================
		// HELP
		// ========================================

		static readonly List<HelpEntry> _helpEntries = new List<HelpEntry>(TableNavHelpEntries) {
			TableSortHelpEntry,
			new HelpEntry("Enter (data row)", STRINGS.ONIACCESS.VITALS_SCREEN.FOCUS_DUPLICANT),
		};

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// TABLE SETUP
		// ========================================

		protected override void OnTableActivate() {
			_columns = new List<ColumnDef> {
				new ColumnDef {
					Name = STRINGS.UI.VITALSSCREEN.STRESS,
					HeaderDescription = TextFilter.FilterForSpeech(
						Db.Get().Amounts.Stress.description),
					GetValue = GetStressValue,
					GetSortValue = mi => Db.Get().Amounts.Stress.Lookup(mi).value,
					Sortable = true
				},
				new ColumnDef {
					Name = STRINGS.UI.VITALSSCREEN.QUALITYOFLIFE_EXPECTATIONS,
					HeaderDescription = TextFilter.FilterForSpeech(
						Db.Get().Attributes.QualityOfLife.Description),
					GetValue = GetMoraleValue,
					GetSortValue = mi => Db.Get().Attributes.QualityOfLifeExpectation.Lookup(mi).GetTotalValue(),
					Sortable = true
				},
			};

			if (Game.IsDlcActiveForCurrentSave("DLC3_ID")) {
				_columns.Add(new ColumnDef {
					Name = STRINGS.UI.VITALSSCREEN_POWERBANKS,
					HeaderDescription = TextFilter.FilterForSpeech(
						Db.Get().Amounts.BionicInternalBattery.description),
					GetValue = GetPowerBanksValue,
					GetSortValue = mi => {
						if (mi.HasTag(GameTags.Minions.Models.Bionic))
							return mi.GetAmounts().Get(Db.Get().Amounts.BionicInternalBattery).value;
						return -1f;
					},
					Sortable = true
				});
			}

			_columns.Add(new ColumnDef {
				Name = STRINGS.UI.VITALSSCREEN_CALORIES,
				HeaderDescription = STRINGS.ONIACCESS.VITALS_SCREEN.FULLNESS_HEADER,
				GetValue = GetFullnessValue,
				GetSortValue = mi => {
					var amount = Db.Get().Amounts.Calories.Lookup(mi);
					return amount != null ? amount.value : -1f;
				},
				Sortable = true
			});

			_columns.Add(new ColumnDef {
				Name = STRINGS.UI.VITALSSCREEN_HEALTH,
				HeaderDescription = TextFilter.FilterForSpeech(
					Db.Get().Amounts.HitPoints.description),
				GetValue = GetHealthValue,
				GetSortValue = mi => Db.Get().Amounts.HitPoints.Lookup(mi).value,
				Sortable = true
			});

			_columns.Add(new ColumnDef {
				Name = STRINGS.UI.VITALSSCREEN_SICKNESS,
				HeaderDescription = STRINGS.ONIACCESS.VITALS_SCREEN.DISEASE_HEADER,
				GetValue = GetSicknessValue,
				GetSortValue = null,
				Sortable = false
			});
		}

		// ========================================
		// ROW LIST BUILDING
		// ========================================

		protected override void BuildRowList() {
			_rows.Clear();
			bool showDividers = DlcManager.FeatureClusterSpaceEnabled();

			_rows.Add(new RowEntry { Kind = TableRowKind.ColumnHeader });

			var worldIds = ClusterManager.Instance.GetWorldIDsSorted();
			foreach (int worldId in worldIds) {
				var world = ClusterManager.Instance.GetWorld(worldId);
				if (world == null || !world.IsDiscovered) continue;

				var minions = GetLiveMinionsForWorld(worldId);
				if (minions.Count == 0) continue;

				if (showDividers)
					_rows.Add(new RowEntry { Kind = TableRowKind.WorldDivider, WorldId = worldId });

				if (_sortColumn >= 0 && _sortColumn < _columns.Count && _columns[_sortColumn].Sortable) {
					var colDef = _columns[_sortColumn];
					minions.Sort((a, b) => {
						var miA = (MinionIdentity)a;
						var miB = (MinionIdentity)b;
						int cmp = colDef.GetSortValue(miA).CompareTo(colDef.GetSortValue(miB));
						if (_sortAscending) cmp = -cmp;
						if (cmp != 0) return cmp;
						return a.GetProperName().CompareTo(b.GetProperName());
					});
				}

				foreach (var minion in minions) {
					_rows.Add(new RowEntry { Kind = TableRowKind.Minion, Identity = minion, WorldId = worldId });
				}
			}

			var stored = GetStoredMinions();
			if (stored.Count > 0) {
				if (showDividers)
					_rows.Add(new RowEntry { Kind = TableRowKind.WorldDivider, WorldId = StoredMinionWorldId });
				foreach (var smi in stored) {
					_rows.Add(new RowEntry { Kind = TableRowKind.StoredMinion, Identity = smi, WorldId = StoredMinionWorldId });
				}
			}
		}

		// ========================================
		// TABLE SHAPE
		// ========================================

		protected override int GetColumnCount(TableRowKind kind) {
			return _columns.Count;
		}

		protected override string GetColumnName(int col) {
			if (_rows[_row].Kind == TableRowKind.ColumnHeader)
				return null;
			if (col >= 0 && col < _columns.Count)
				return _columns[col].Name;
			return null;
		}

		protected override string GetSearchableColumnName(int col) {
			if (col >= 0 && col < _columns.Count)
				return _columns[col].Name;
			return null;
		}

		protected override string GetRowLabel(RowEntry row) {
			switch (row.Kind) {
				case TableRowKind.ColumnHeader:
					return null;
				case TableRowKind.Minion:
				case TableRowKind.StoredMinion:
					return row.Identity.GetProperName();
				default:
					return null;
			}
		}

		protected override string GetCellValue(RowEntry row) {
			if (_col < 0 || _col >= _columns.Count)
				return "";

			switch (row.Kind) {
				case TableRowKind.ColumnHeader: {
						var col = _columns[_col];
						string desc = col.HeaderDescription;
						if (!string.IsNullOrEmpty(desc))
							return col.Name + ", " + desc;
						return col.Name;
					}

				case TableRowKind.Minion:
					return _columns[_col].GetValue((MinionIdentity)row.Identity);

				case TableRowKind.StoredMinion: {
						var smi = (StoredMinionIdentity)row.Identity;
						string reason = TextFilter.FilterForSpeech(string.Format(
							STRINGS.UI.TABLESCREENS.INFORMATION_NOT_AVAILABLE_TOOLTIP,
							smi.GetStorageReason(), smi.GetProperName()));
						return STRINGS.UI.TABLESCREENS.NA + ", " + reason;
					}

				default:
					return "";
			}
		}

		protected override bool IsColumnSortable(int col) {
			if (col >= 0 && col < _columns.Count)
				return _columns[col].Sortable;
			return false;
		}

		// ========================================
		// ENTER
		// ========================================

		protected override void OnEnterPressed(RowEntry row) {
			if (row.Kind != TableRowKind.Minion) return;
			var mi = (MinionIdentity)row.Identity;
			SelectTool.Instance.SelectAndFocus(
				mi.transform.GetPosition(),
				mi.GetComponent<KSelectable>(),
				new UnityEngine.Vector3(8f, 0f, 0f));
			TileCursor.Instance?.JumpTo(Grid.PosToCell(mi.transform.GetPosition()));
			SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.VITALS_SCREEN.FOCUSED);
		}

		// ========================================
		// AMOUNT BREAKDOWN HELPER
		// ========================================

		static string FormatAmountBreakdown(AmountInstance amount) {
			var formatter = amount.amount.displayer.Formatter;
			var parts = new List<string>();

			float delta = amount.deltaAttribute.GetTotalDisplayValue();
			parts.Add(string.Format(
				STRINGS.ONIACCESS.VITALS_SCREEN.CHANGE,
				formatter.GetFormattedValue(delta, formatter.DeltaTimeSlice)));

			for (int i = 0; i < amount.deltaAttribute.Modifiers.Count; i++) {
				var mod = amount.deltaAttribute.Modifiers[i];
				parts.Add(mod.GetDescription() + ": " + formatter.GetFormattedModifier(mod));
			}
			return string.Join(". ", parts);
		}

		// ========================================
		// COLUMN VALUE BUILDERS
		// ========================================

		static string GetStressValue(MinionIdentity mi) {
			var amount = Db.Get().Amounts.Stress.Lookup(mi);
			return amount.GetValueString() + ". " + FormatAmountBreakdown(amount);
		}

		static string GetMoraleValue(MinionIdentity mi) {
			var qolAttr = Db.Get().Attributes.QualityOfLife.Lookup(mi);
			var parts = new List<string>();
			parts.Add(qolAttr.GetFormattedValue());

			for (int i = 0; i < qolAttr.Modifiers.Count; i++) {
				var mod = qolAttr.Modifiers[i];
				string formatted = mod.GetFormattedString();
				if (formatted != null)
					parts.Add(mod.GetDescription() + ": " + formatted);
			}

			return string.Join(". ", parts);
		}

		static string GetPowerBanksValue(MinionIdentity mi) {
			if (!mi.HasTag(GameTags.Minions.Models.Bionic))
				return STRINGS.UI.TABLESCREENS.NA;

			var amount = mi.GetAmounts().Get(Db.Get().Amounts.BionicInternalBattery);
			var parts = new List<string>();
			parts.Add(amount.GetValueString());

			var batteryMonitor = mi.GetSMI<BionicBatteryMonitor.Instance>();
			if (batteryMonitor != null) {
				parts.Add(TextFilter.FilterForSpeech(string.Format(
					STRINGS.DUPLICANTS.MODIFIERS.BIONIC_WATTS.TOOLTIP.CURRENT_WATTAGE_LABEL,
					GameUtil.GetFormattedWattage(batteryMonitor.Wattage))));

				foreach (var mod in batteryMonitor.Modifiers) {
					if (mod.value != 0f)
						parts.Add(TextFilter.FilterForSpeech(mod.name));
				}
			}
			return string.Join(". ", parts);
		}

		static string GetFullnessValue(MinionIdentity mi) {
			var amount = Db.Get().Amounts.Calories.Lookup(mi);
			if (amount == null)
				return STRINGS.UI.TABLESCREENS.NA;

			string result = amount.GetValueString() + ". " + FormatAmountBreakdown(amount);

			var ration = mi.GetSMI<RationMonitor.Instance>();
			if (ration != null) {
				result += ". " + string.Format(
					STRINGS.ONIACCESS.VITALS_SCREEN.EATEN_TODAY,
					GameUtil.GetFormattedCalories(ration.GetRationsAteToday()));
			}
			return result;
		}

		static string GetHealthValue(MinionIdentity mi) {
			var amount = Db.Get().Amounts.HitPoints.Lookup(mi);
			return amount.GetValueString() + ". " + FormatAmountBreakdown(amount);
		}

		static string GetSicknessValue(MinionIdentity mi) {
			string label = GetSicknessLabel(mi);
			string detail = GetSicknessDetail(mi);
			if (detail != null)
				return label + ". " + TextFilter.FilterForSpeech(detail);
			return label;
		}

		// ========================================
		// SICKNESS LABEL
		// ========================================

		static string GetSicknessLabel(MinionIdentity mi) {
			var sicknessList = new List<KeyValuePair<string, float>>();

			foreach (var sickness in mi.GetComponent<MinionModifiers>().sicknesses.ModifierList) {
				sicknessList.Add(new KeyValuePair<string, float>(
					sickness.modifier.Name, sickness.GetInfectedTimeRemaining()));
			}

			if (DlcManager.FeatureRadiationEnabled()) {
				var radMonitor = mi.GetSMI<RadiationMonitor.Instance>();
				if (radMonitor != null && radMonitor.sm.isSick.Get(radMonitor)) {
					var effects = mi.GetComponent<Effects>();
					string radName;
					if (effects.HasEffect(RadiationMonitor.minorSicknessEffect)
						|| effects.HasEffect(RadiationMonitor.bionic_minorSicknessEffect))
						radName = Db.Get().effects.Get(RadiationMonitor.minorSicknessEffect).Name;
					else if (effects.HasEffect(RadiationMonitor.majorSicknessEffect)
						|| effects.HasEffect(RadiationMonitor.bionic_majorSicknessEffect))
						radName = Db.Get().effects.Get(RadiationMonitor.majorSicknessEffect).Name;
					else if (effects.HasEffect(RadiationMonitor.extremeSicknessEffect)
						|| effects.HasEffect(RadiationMonitor.bionic_extremeSicknessEffect))
						radName = Db.Get().effects.Get(RadiationMonitor.extremeSicknessEffect).Name;
					else
						radName = STRINGS.DUPLICANTS.MODIFIERS.RADIATIONEXPOSUREDEADLY.NAME;
					sicknessList.Add(new KeyValuePair<string, float>(
						radName, radMonitor.SicknessSecondsRemaining()));
				}
			}

			if (sicknessList.Count == 0)
				return STRINGS.UI.VITALSSCREEN.NO_SICKNESSES;

			if (sicknessList.Count > 1) {
				float minTime = float.MaxValue;
				foreach (var item in sicknessList)
					minTime = UnityEngine.Mathf.Min(minTime, item.Value);
				return string.Format(STRINGS.UI.VITALSSCREEN.MULTIPLE_SICKNESSES,
					GameUtil.GetFormattedCycles(minTime));
			}

			return string.Format(STRINGS.UI.VITALSSCREEN.SICKNESS_REMAINING,
				sicknessList[0].Key, GameUtil.GetFormattedCycles(sicknessList[0].Value));
		}

		static string GetSicknessDetail(MinionIdentity mi) {
			var parts = new List<string>();

			if (DlcManager.FeatureRadiationEnabled()) {
				var radMonitor = mi.GetSMI<RadiationMonitor.Instance>();
				if (radMonitor != null && radMonitor.sm.isSick.Get(radMonitor))
					parts.Add(radMonitor.GetEffectStatusTooltip());
			}

			var sicknesses = mi.GetComponent<MinionModifiers>().sicknesses;
			if (sicknesses.IsInfected()) {
				foreach (var item in sicknesses.ModifierList) {
					parts.Add(item.modifier.Name);
					var statusItem = item.GetStatusItem();
					parts.Add(statusItem.GetTooltip(item.ExposureInfo));
				}
			}

			if (parts.Count == 0)
				return null;

			return string.Join(", ", parts);
		}
	}
}
