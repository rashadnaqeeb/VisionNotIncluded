namespace OniAccess.Widgets {
	/// <summary>
	/// A toggle widget (KToggle, MultiToggle used as toggle).
	/// Speaks on/off state, validates interactability, clicks on activation.
	/// </summary>
	public class ToggleWidget: Widget {
		public override string RoleKey => "toggle";
		public override bool IsActivatable() => true;

		public override bool IsInteractable {
			get {
				if (_isInteractableOverride.HasValue) return _isInteractableOverride.Value;
				var toggle = Component as KToggle;
				if (toggle != null) return toggle.IsInteractable();
				return true;
			}
		}

		public override string GetSpeechText() {
			if (SpeechFunc != null) {
				string result = SpeechFunc()?.Trim();
				if (!string.IsNullOrEmpty(result)) return result;
			}

			var toggle = Component as KToggle;
			if (toggle != null) {
				string state = SideScreenWalker.IsToggleActive(toggle)
					? (string)STRINGS.ONIACCESS.STATES.ON
					: (string)STRINGS.ONIACCESS.STATES.OFF;
				return $"{Label}, {state}";
			}
			var mt = Component as MultiToggle;
			if (mt != null)
				return $"{Label}, {WidgetOps.GetMultiToggleState(mt)}";
			return Label;
		}

		public override bool IsValid() {
			if (GameObject != null && !GameObject.activeInHierarchy) return false;
			if (Component is MultiToggle) return true;
			return Component != null || GameObject != null;
		}

		/// <summary>
		/// Click the toggle. Returns true if handled.
		/// Post-interaction speech is the handler's responsibility.
		/// </summary>
		public override bool Activate() {
			var toggle = Component as KToggle;
			if (toggle != null) {
				toggle.Click();
				return true;
			}
			var mt = Component as MultiToggle;
			if (mt != null) {
				WidgetOps.ClickMultiToggle(mt);
				return true;
			}
			var btn = Component as KButton;
			if (btn != null) {
				WidgetOps.ClickButton(btn);
				return true;
			}
			return false;
		}
	}
}
