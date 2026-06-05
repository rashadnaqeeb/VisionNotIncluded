using System.Collections.Generic;

using HarmonyLib;
using OniAccess.Input;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Modal handler for the DisinfectThresholdDiagram panel (germ overlay sidebar).
	/// Three items: auto-disinfect toggle, germ threshold slider, threshold text input.
	/// Opened by Shift+G from TileCursorHandler when the germ overlay is active.
	/// </summary>
	public class DisinfectSettingsHandler: BaseMenuHandler {
		private enum Item { Toggle = 0, Slider = 1, Input = 2 }
		private const int ItemTotal = 3;
		private const int SliderConversion = 1000;

		private KToggle _toggle;
		private KSlider _slider;
		private KNumberInputField _inputField;
		private readonly TextEditHelper _textEdit = new TextEditHelper();

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.HANDLERS.DISINFECT_SETTINGS;
		public override int ItemCount => ItemTotal;
		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public DisinfectSettingsHandler() : base(null) {
			HelpEntries = BuildHelpEntries();
		}

		public override void OnActivate() {
			if (!ResolveComponents()) {
				Util.Log.Warn("DisinfectSettingsHandler: failed to resolve diagram components");
				HandlerStack.Pop();
				return;
			}
			PlaySound("HUD_Click_Open");
			base.OnActivate();
			SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(BuildItemSpeech(0)));
		}

		public override void OnDeactivate() {
			base.OnDeactivate();
			_toggle = null;
			_slider = null;
			_inputField = null;
		}

		public override string GetItemLabel(int index) {
			switch ((Item)index) {
				case Item.Toggle: return (string)STRINGS.ONIACCESS.DISINFECT_SETTINGS.AUTO_DISINFECT;
				case Item.Slider: return (string)STRINGS.UI.OVERLAYS.DISEASE.DISINFECT_THRESHOLD_DIAGRAM.THRESHOLD_PREFIX;
				case Item.Input: return (string)STRINGS.ONIACCESS.DISINFECT_SETTINGS.THRESHOLD_INPUT;
				default: return null;
			}
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (CurrentIndex < 0 || CurrentIndex >= ItemTotal) return;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(BuildItemSpeech(CurrentIndex)));
		}

		protected override bool IsItemValid(int index) {
			if (index == (int)Item.Toggle) return true;
			return SaveGame.Instance.enableAutoDisinfect;
		}

		protected override void ActivateCurrentItem() {
			if (!EnsureComponents()) return;
			switch ((Item)CurrentIndex) {
				case Item.Toggle:
					_toggle.Click();
					SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(BuildItemSpeech(CurrentIndex)));
					return;
				case Item.Slider:
					return;
				case Item.Input:
					if (!_textEdit.IsEditing)
						_textEdit.Begin(_inputField.field, onEnd: SpeakCurrentItemQueued);
					else
						_textEdit.Confirm();
					return;
			}
		}

		protected override void AdjustCurrentItem(int direction, int stepLevel) {
			if ((Item)CurrentIndex != Item.Slider) return;
			if (!EnsureComponents()) return;

			float step = InputUtil.StepForLevel(stepLevel);
			float oldValue = _slider.value;
			_slider.value = UnityEngine.Mathf.Clamp(
				_slider.value + step * direction,
				_slider.minValue, _slider.maxValue);

			if (_slider.value == oldValue) {
				PlaySound(direction < 0 ? "Slider_Boundary_Low" : "Slider_Boundary_High");
				return;
			}

			int germs = (int)_slider.value * SliderConversion;
			SaveGame.Instance.minGermCountForDisinfect = germs;
			_inputField.SetDisplayValue(germs.ToString());

			PlaySound("Slider_Move");
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(BuildItemSpeech(CurrentIndex)));
		}

		public override bool Tick() {
			if (_textEdit.HandleTick()) return false;
			return base.Tick();
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (_textEdit.HandleKeyDown(e)) return true;
			if (base.HandleKeyDown(e)) return true;
			if (e.TryConsume(Action.Escape)) {
				Close();
				return true;
			}
			return false;
		}

		private void Close() {
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
		}

		private void SpeakCurrentItemQueued() {
			if (CurrentIndex >= 0 && CurrentIndex < ItemTotal)
				SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(BuildItemSpeech(CurrentIndex)));
		}

		private string BuildItemSpeech(int index) {
			string units = (string)STRINGS.UI.OVERLAYS.DISEASE.DISINFECT_THRESHOLD_DIAGRAM.UNITS;
			switch ((Item)index) {
				case Item.Toggle:
					string state = SaveGame.Instance.enableAutoDisinfect
						? (string)STRINGS.ONIACCESS.STATES.ON
						: (string)STRINGS.ONIACCESS.STATES.OFF;
					return $"{STRINGS.ONIACCESS.DISINFECT_SETTINGS.AUTO_DISINFECT}, {state}";
				case Item.Slider:
				case Item.Input:
					int germs = SaveGame.Instance.minGermCountForDisinfect;
					string label = index == (int)Item.Slider
						? (string)STRINGS.UI.OVERLAYS.DISEASE.DISINFECT_THRESHOLD_DIAGRAM.THRESHOLD_PREFIX
						: (string)STRINGS.ONIACCESS.DISINFECT_SETTINGS.THRESHOLD_INPUT;
					return $"{label}, {germs} {units}";
				default:
					return null;
			}
		}

		/// <summary>
		/// The timelapse screenshot system toggles the overlay off and back on,
		/// which destroys and recreates the DisinfectThresholdDiagram. If that
		/// happens while this handler is active, our component references die.
		/// Re-resolve from the new diagram instance when that's detected.
		/// </summary>
		private bool EnsureComponents() {
			if (_toggle && _slider && _inputField)
				return true;
			if (!ResolveComponents()) {
				Util.Log.Warn("DisinfectSettingsHandler: diagram destroyed, closing");
				Close();
				return false;
			}
			return true;
		}

		private bool ResolveComponents() {
			if (OverlayLegend.Instance == null) return false;
			var diagram = OverlayLegend.Instance.GetComponentInChildren<DisinfectThresholdDiagram>();
			if (diagram == null) return false;

			var traverse = Traverse.Create(diagram);
			_toggle = traverse.Field("toggle").GetValue<KToggle>();
			_slider = traverse.Field("slider").GetValue<KSlider>();
			_inputField = traverse.Field("inputField").GetValue<KNumberInputField>();

			return _toggle != null && _slider != null && _inputField != null;
		}
	}
}
