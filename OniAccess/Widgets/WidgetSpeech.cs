namespace OniAccess.Widgets {
	/// <summary>
	/// The single composer for spoken item text. Every navigation model routes
	/// per-item speech through here, so a future cross-cutting feature (verbose
	/// UI, role announcement, position readout) becomes a one-place change.
	///
	/// Today it wraps the item's live <see cref="NavItem.Announce"/> text with
	/// its tooltip, matching the previous BuildWidgetText behavior byte for byte.
	/// The <see cref="NavContext"/> and <see cref="NavItem.RoleKey"/> are accepted
	/// but not yet spoken.
	/// </summary>
	public static class WidgetSpeech {
		public static string Compose(NavItem item, NavContext ctx, string tooltip) {
			string text = item.Announce();
			return WidgetOps.AppendTooltip(text, tooltip);
		}

		/// <summary>
		/// Convenience for the common case: an item with no live UI control and no
		/// tooltip, whose announcement is an already-assembled string. Equivalent
		/// to composing a <see cref="LabelItem"/> with no context or tooltip.
		/// </summary>
		public static string ComposeLabel(string text) {
			return Compose(new LabelItem(text), NavContext.None, null);
		}
	}
}
