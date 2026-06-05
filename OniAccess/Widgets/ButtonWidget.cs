namespace OniAccess.Widgets {
	/// <summary>
	/// A clickable button widget (KButton or MultiToggle used as button).
	/// </summary>
	public class ButtonWidget: Widget {
		public override string RoleKey => NavRoles.Button;
		public override bool IsActivatable() => true;

		public override bool IsInteractable {
			get {
				if (_isInteractableOverride.HasValue) return _isInteractableOverride.Value;
				var btn = Component as KButton;
				if (btn != null) return btn.isInteractable;
				var toggle = Component as KToggle;
				if (toggle != null) return toggle.IsInteractable();
				return true;
			}
		}

		public override bool IsValid() {
			if (GameObject != null && !GameObject.activeInHierarchy) return false;
			return Component != null || GameObject != null;
		}

		public override bool Activate() {
			var kbutton = Component as KButton;
			if (kbutton != null) {
				WidgetOps.ClickButton(kbutton);
				return true;
			}
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
			return false;
		}
	}
}
