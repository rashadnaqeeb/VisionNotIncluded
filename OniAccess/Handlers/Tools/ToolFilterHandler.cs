using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Tools {
	/// <summary>
	/// Modal menu for selecting a tool filter/mode. Two use cases:
	/// 1. Opened from ToolHandler (F key) to change filter for the active tool.
	/// 2. Opened from ToolPickerHandler for tools that require mode-pick before
	///    activation (e.g., Harvest: pick "when ready" vs "do not harvest" first).
	/// </summary>
	public class ToolFilterHandler: BaseMenuHandler {
		internal const string HarvestWhenReadyKey = "HARVEST_WHEN_READY";
		internal const string DoNotHarvestKey = "DO_NOT_HARVEST";

		private readonly ToolHandler _owner;
		private readonly ModToolInfo _pendingTool;
		private List<string> _filterKeys;
		private List<string> _filterNames;
		// Live ToggleData objects from the game's menu, parallel to _filterKeys.
		// Null when running on the harvest mode-pick fallback (menu not populated).
		private List<ToolParameterMenu.ToggleData> _toggles;

		public override string DisplayName => (string)STRINGS.ONIACCESS.TOOLS.FILTER_NAME;

		public override IReadOnlyList<HelpEntry> HelpEntries => ToolPickerHandler.ModalMenuHelp;

		/// <summary>
		/// Change filter for an active tool (F key in tool mode).
		/// </summary>
		public ToolFilterHandler(ToolHandler owner) {
			_owner = owner;
			_pendingTool = null;
		}

		/// <summary>
		/// Pick mode before activating a tool (e.g., Harvest from tool picker).
		/// </summary>
		public ToolFilterHandler(ModToolInfo pendingTool) {
			_owner = null;
			_pendingTool = pendingTool;
		}

		public override int ItemCount => _filterKeys != null ? _filterKeys.Count : 0;

		public override string GetItemLabel(int index) {
			if (_filterNames == null || index < 0 || index >= _filterNames.Count) return null;
			return _filterNames[index];
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (_filterNames != null && CurrentIndex >= 0 && CurrentIndex < _filterNames.Count)
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(ItemSpeech(CurrentIndex)));
		}

		/// <summary>
		/// Spoken form of an item: checkbox filters (dig tool) append their
		/// on/off state; radio filters are just the name.
		/// </summary>
		private string ItemSpeech(int index) {
			string name = _filterNames[index];
			if (_toggles == null) return name;
			var toggle = _toggles[index];
			if (!toggle.isToggleInclusive) return name;
			string state = toggle.IsOn
				? (string)STRINGS.ONIACCESS.STATES.ON
				: (string)STRINGS.ONIACCESS.STATES.OFF;
			return name + ", " + state;
		}

		private static ToolParameterMenu.ToggleData[] ReadMenuToggles() {
			var toggles = Traverse.Create(ToolMenu.Instance.toolParameterMenu)
				.Field<ToolParameterMenu.ToggleData[]>("currentTogglesData").Value;
			return toggles != null && toggles.Length > 0 ? toggles : null;
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			_filterKeys = new List<string>();
			_filterNames = new List<string>();
			_toggles = null;
			CurrentIndex = 0;
			_search.Clear();

			var toggles = ReadMenuToggles();

			if (toggles == null && _pendingTool != null
				&& _pendingTool.ToolType == typeof(HarvestTool)) {
				_filterKeys.Add(HarvestWhenReadyKey);
				_filterKeys.Add(DoNotHarvestKey);
				for (int i = 0; i < _filterKeys.Count; i++)
					_filterNames.Add(Strings.Get("STRINGS.UI.TOOLS.FILTERLAYERS." + _filterKeys[i] + ".NAME"));
			} else if (toggles != null) {
				_toggles = new List<ToolParameterMenu.ToggleData>();
				int onIndex = 0;
				int idx = 0;
				foreach (var toggle in toggles) {
					if (toggle.state == ToolParameterMenu.ToggleState.Disabled)
						continue;
					_toggles.Add(toggle);
					_filterKeys.Add(toggle.name);
					_filterNames.Add(Strings.Get("STRINGS.UI.TOOLS.FILTERLAYERS." + toggle.name + ".NAME"));
					if (toggle.state == ToolParameterMenu.ToggleState.On)
						onIndex = idx;
					idx++;
				}
				CurrentIndex = onIndex;
			}

			if (_filterNames.Count > 0) {
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(ItemSpeech(CurrentIndex)));
			} else {
				Util.Log.Warn("ToolFilterHandler.OnActivate: no filter parameters available");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLTIP.CLOSED);
				HandlerStack.Pop();
			}
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

		private static System.Reflection.MethodInfo _onChangeMethod;

		protected override void ActivateCurrentItem() {
			if (_filterKeys == null || CurrentIndex < 0 || CurrentIndex >= _filterKeys.Count)
				return;

			if (_pendingTool != null)
				ToolPickerHandler.ActivateTool(_pendingTool);

			// Activating a pending tool populates the parameter menu, so the
			// toggles may only exist now. Find the clicked one by key.
			var toggles = ReadMenuToggles();
			ToolParameterMenu.ToggleData clicked = null;
			if (toggles != null) {
				foreach (var toggle in toggles) {
					if (toggle.name == _filterKeys[CurrentIndex]
						&& toggle.state != ToolParameterMenu.ToggleState.Disabled) {
						clicked = toggle;
						break;
					}
				}
			}

			if (clicked != null) {
				// Mirrors the game's ToolParameterMenu.ChangeToSetting, which now
				// takes a private Widget and can't be invoked with a filter key.
				// The ToggleData objects are shared with the tool's currentFilters,
				// so mutating them and firing OnChange updates both the menu
				// visuals and the tool.
				if (clicked.isToggleInclusive) {
					clicked.state = clicked.IsOn
						? ToolParameterMenu.ToggleState.Off
						: ToolParameterMenu.ToggleState.On;
				} else {
					foreach (var toggle in toggles)
						if (toggle.state != ToolParameterMenu.ToggleState.Disabled)
							toggle.state = ToolParameterMenu.ToggleState.Off;
					clicked.state = ToolParameterMenu.ToggleState.On;
				}

				try {
					if (_onChangeMethod == null)
						_onChangeMethod = AccessTools.Method(typeof(ToolParameterMenu), "OnChange");
					_onChangeMethod.Invoke(ToolMenu.Instance.toolParameterMenu, null);
				} catch (System.Exception ex) {
					Util.Log.Error($"ToolFilterHandler.ActivateCurrentItem: filter apply failed: {ex}");
				}
			} else {
				Util.Log.Warn($"ToolFilterHandler.ActivateCurrentItem: filter '{_filterKeys[CurrentIndex]}' not found in menu");
			}

			bool hadSelection = _owner != null && _owner.HasSelection;
			if (_owner != null)
				_owner.ClearSelection();

			bool isInclusive = clicked != null && clicked.isToggleInclusive;
			string announcement = isInclusive ? ItemSpeech(CurrentIndex) : _filterNames[CurrentIndex];
			if (hadSelection)
				announcement += ", " + (string)STRINGS.ONIACCESS.TOOLS.SELECTION_CLEARED;

			if (isInclusive) {
				// Checkbox menu: stay open so more filters can be toggled
				SpeechPipeline.SpeakInterrupt(announcement);
			} else if (_owner != null) {
				SpeechPipeline.SpeakInterrupt(announcement);
				HandlerStack.Pop();
			} else {
				HandlerStack.Replace(new ToolHandler());
			}
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLTIP.CLOSED);
				HandlerStack.Pop();
				return true;
			}
			return false;
		}
	}
}
