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
		/// Convenience for the common case: a navigable item with no live UI
		/// control and no tooltip, whose announcement is an already-assembled
		/// string. Equivalent to composing a <see cref="LabelItem"/> with no
		/// context or tooltip. Item decoration (control role, list position) may
		/// attach here once a future feature wires real context through.
		/// </summary>
		public static string ComposeLabel(string text) {
			return Compose(new LabelItem(text), NavContext.None, null);
		}

		/// <summary>
		/// Compose an announcement that is NOT a navigable item: a screen, tab, or
		/// section heading, a status line (empty list, no selection), an action
		/// confirmation (queued, dismissed), or a blocked-action reason. Routing
		/// these through the composer keeps it the single point every spoken
		/// string passes through, so a future global text policy (pronunciation,
		/// punctuation) reaches them too. Unlike <see cref="ComposeLabel"/> a
		/// message is never given list context, so item decoration (role,
		/// position) never lands on a heading or status line.
		/// </summary>
		public static string ComposeMessage(string text) {
			return Compose(new LabelItem(text), NavContext.None, null);
		}
	}
}
