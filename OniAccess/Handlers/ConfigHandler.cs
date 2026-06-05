using System.Collections.Generic;
using OniAccess.Config;
using OniAccess.Handlers.Tiles;
using OniAccess.Handlers.Tiles.Scanner;
using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers {
	public class ConfigHandler: NestedMenuHandler {
		private readonly ConfigSection[] _sections;
		private int _flatCount;

		public override string DisplayName => STRINGS.ONIACCESS.HANDLERS.CONFIG;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= new List<HelpEntry> {
				new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
				new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
				new HelpEntry("Ctrl+Up/Down", STRINGS.ONIACCESS.HELP.JUMP_GROUP),
				new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
				new HelpEntry("Enter/Right", STRINGS.ONIACCESS.HELP.OPEN_GROUP),
				new HelpEntry("Left", STRINGS.ONIACCESS.HELP.GO_BACK),
				new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.TOGGLE_OPTION),
			}.AsReadOnly();

		public ConfigHandler() {
			_sections = BuildSections();
			_flatCount = 0;
			for (int i = 0; i < _sections.Length; i++)
				_flatCount += _sections[i].Items.Count;
		}

		protected override int MaxLevel => 1;
		protected override int SearchLevel => 1;

		protected override int GetItemCount(int level, int[] indices) {
			if (level == 0) return _sections.Length;
			return _sections[indices[0]].Items.Count;
		}

		protected override string GetItemLabel(int level, int[] indices) {
			if (level == 0) return _sections[indices[0]].Title;
			var item = _sections[indices[0]].Items[indices[1]];
			string value = item.GetDisplayValue();
			return string.IsNullOrEmpty(value) ? item.Label : item.Label + ", " + value;
		}

		protected override string GetParentLabel(int level, int[] indices) {
			return _sections[indices[0]].Title;
		}

		protected override void ActivateLeafItem(int[] indices) {
			var item = _sections[indices[0]].Items[indices[1]];
			if (item is ActionConfigItem) {
				// The action opens its own handler, which owns its audio; a
				// post-activation speak here would clobber the new screen's title.
				item.Cycle(1);
				return;
			}
			item.Cycle(1);
			PlaySound("HUD_Click");
			SpeakCurrentItem();
		}

		protected override void HandleLeftRight(int direction, int stepLevel) {
			if (Level < MaxLevel) {
				base.HandleLeftRight(direction, stepLevel);
				return;
			}
			var item = _sections[GetIndex(0)].Items[GetIndex(1)];
			if (item is FloatConfigItem floatItem) {
				floatItem.Adjust(direction, InputUtil.FractionForLevel(stepLevel));
				PlaySound("HUD_Click");
				SpeakCurrentItem();
			} else {
				base.HandleLeftRight(direction, stepLevel);
			}
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			base.OnActivate();
			if (_sections.Length > 0)
				SpeechPipeline.SpeakQueued(GetItemLabel(0, new[] { 0 }));
		}

		// Search: flat across all items in all sections

		protected override int GetSearchItemCount(int[] indices) => _flatCount;

		protected override string GetSearchItemLabel(int flatIndex) {
			int remaining = flatIndex;
			for (int s = 0; s < _sections.Length; s++) {
				int count = _sections[s].Items.Count;
				if (remaining < count)
					return _sections[s].Items[remaining].Label;
				remaining -= count;
			}
			return null;
		}

		protected override void MapSearchIndex(int flatIndex, int[] outIndices) {
			int remaining = flatIndex;
			for (int s = 0; s < _sections.Length; s++) {
				int count = _sections[s].Items.Count;
				if (remaining < count) {
					outIndices[0] = s;
					outIndices[1] = remaining;
					return;
				}
				remaining -= count;
			}
		}

		public override bool Tick() {
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F12)
				&& !InputUtil.ShiftHeld() && !InputUtil.CtrlHeld() && !InputUtil.AltHeld()) {
				Close();
				return true;
			}
			return base.Tick();
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				if (Level > 0) {
					base.HandleLeftRight(-1, 0);
					return true;
				}
				Close();
				return true;
			}
			return false;
		}

		private void Close() {
			SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.TOOLTIP.CLOSED);
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
		}

		private class ConfigSection {
			public string Title;
			public List<ConfigItem> Items;
		}

		private static ConfigSection[] BuildSections() {
			return new[] {
				// --- Tile Cursor Settings ---
				new ConfigSection {
					Title = (string)STRINGS.ONIACCESS.CONFIG.SECTION_TILE_CURSOR,
					Items = new List<ConfigItem> {
						new EnumConfigItem<CoordinateMode>(
							(string)STRINGS.ONIACCESS.CONFIG.COORDINATE_MODE,
							() => ConfigManager.Config.CoordinateMode,
							value => {
								ConfigManager.Config.CoordinateMode = value;
								if (TileCursor.Instance != null)
									TileCursor.Instance.Mode = value;
							},
							new[] { CoordinateMode.Off, CoordinateMode.Append, CoordinateMode.Prepend },
							mode => {
								switch (mode) {
									case CoordinateMode.Off: return (string)STRINGS.ONIACCESS.TILE_CURSOR.COORD_OFF;
									case CoordinateMode.Append: return (string)STRINGS.ONIACCESS.TILE_CURSOR.COORD_APPEND;
									case CoordinateMode.Prepend: return (string)STRINGS.ONIACCESS.TILE_CURSOR.COORD_PREPEND;
									default: return mode.ToString();
								}
							}
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.LOCK_ZOOM,
							() => ConfigManager.Config.LockZoom,
							value => ConfigManager.Config.LockZoom = value
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.ANNOUNCE_BIOME_CHANGES,
							() => ConfigManager.Config.AnnounceBiomeChanges,
							value => ConfigManager.Config.AnnounceBiomeChanges = value
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.PASSABILITY_EARCONS,
							() => ConfigManager.Config.PassabilityEarcons,
							value => ConfigManager.Config.PassabilityEarcons = value
						),
						new FloatConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.PASSABILITY_VOLUME,
							() => ConfigManager.Config.PassabilityVolume,
							value => ConfigManager.Config.PassabilityVolume = value,
							0f, 2f
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.FOOTSTEP_EARCONS,
							() => ConfigManager.Config.FootstepEarcons,
							value => ConfigManager.Config.FootstepEarcons = value
						),
						new FloatConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.FOOTSTEP_VOLUME,
							() => ConfigManager.Config.FootstepVolume,
							value => ConfigManager.Config.FootstepVolume = value,
							0f, 2f
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.TEMPERATURE_BAND_EARCONS,
							() => ConfigManager.Config.TemperatureBandEarcons,
							value => ConfigManager.Config.TemperatureBandEarcons = value
						),
						new FloatConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.TEMPERATURE_BAND_VOLUME,
							() => ConfigManager.Config.TemperatureBandVolume,
							value => ConfigManager.Config.TemperatureBandVolume = value,
							0f, 2f
						),
					}
				},

				// --- Scanner Settings ---
				new ConfigSection {
					Title = (string)STRINGS.ONIACCESS.CONFIG.SECTION_SCANNER,
					Items = new List<ConfigItem> {
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.AUTO_MOVE_CURSOR,
							() => ConfigManager.Config.AutoMoveCursor,
							value => {
								ConfigManager.Config.AutoMoveCursor = value;
								if (ScannerNavigator.Instance != null)
									ScannerNavigator.Instance.SetAutoMove(value);
							}
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.SCANNER_MASS_READOUT,
							() => ConfigManager.Config.ScannerMassReadout,
							value => ConfigManager.Config.ScannerMassReadout = value
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.SCANNER_DIRECTION_EARCONS,
							() => ConfigManager.Config.ScannerDirectionEarcons,
							value => ConfigManager.Config.ScannerDirectionEarcons = value
						),
						new FloatConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.SCANNER_DIRECTION_VOLUME,
							() => ConfigManager.Config.ScannerDirectionVolume,
							value => ConfigManager.Config.ScannerDirectionVolume = value,
							0f, 2f
						),
						new ActionConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.CUSTOM_SCANNER_CATEGORIES,
							() => HandlerStack.Push(new CustomCategoryManagerHandler())
						),
					}
				},

				// --- Utility Readouts ---
				new ConfigSection {
					Title = (string)STRINGS.ONIACCESS.CONFIG.SECTION_UTILITY,
					Items = new List<ConfigItem> {
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.UTILITY_PRESENCE_EARCONS,
							() => ConfigManager.Config.UtilityPresenceEarcons,
							value => ConfigManager.Config.UtilityPresenceEarcons = value
						),
						new FloatConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.UTILITY_PRESENCE_VOLUME,
							() => ConfigManager.Config.UtilityPresenceVolume,
							value => ConfigManager.Config.UtilityPresenceVolume = value,
							0f, 2f
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.PIPE_SHAPE_EARCONS,
							() => ConfigManager.Config.PipeShapeEarcons,
							value => ConfigManager.Config.PipeShapeEarcons = value
						),
						new FloatConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.PIPE_SHAPE_VOLUME,
							() => ConfigManager.Config.PipeShapeVolume,
							value => ConfigManager.Config.PipeShapeVolume = value,
							0f, 2f
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.FLOW_SONIFICATION,
							() => ConfigManager.Config.FlowSonification,
							value => ConfigManager.Config.FlowSonification = value
						),
						new FloatConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.FLOW_SONIFICATION_VOLUME,
							() => ConfigManager.Config.FlowSonificationVolume,
							value => ConfigManager.Config.FlowSonificationVolume = value,
							0f, 2f
						),
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.FLOW_DIRECTION_READOUT,
							() => ConfigManager.Config.FlowDirectionReadout,
							value => ConfigManager.Config.FlowDirectionReadout = value
						),
					}
				},

				// --- Miscellaneous ---
				new ConfigSection {
					Title = (string)STRINGS.ONIACCESS.CONFIG.SECTION_MISC,
					Items = new List<ConfigItem> {
						new BoolConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.FOLLOW_MOVEMENT_EARCONS,
							() => ConfigManager.Config.FollowMovementEarcons,
							value => ConfigManager.Config.FollowMovementEarcons = value
						),
						new FloatConfigItem(
							(string)STRINGS.ONIACCESS.CONFIG.FOLLOW_MOVEMENT_VOLUME,
							() => ConfigManager.Config.FollowMovementVolume,
							value => ConfigManager.Config.FollowMovementVolume = value,
							0f, 2f
						),
					}
				},
			};
		}
	}
}
