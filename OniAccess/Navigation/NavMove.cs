using System;
using System.Collections.Generic;

using OniAccess.Widgets;

namespace OniAccess.Navigation {
	/// <summary>
	/// The outcome of a <see cref="NavTree"/> cursor operation, for the adapter to
	/// render into sound and speech. The engine itself is silent: it computes where
	/// the cursor lands and what changed, and hands that back as data so the same
	/// walking logic can be tested offline without the speech pipeline.
	/// </summary>
	public struct NavMove {
		/// <summary>
		/// Whether the cursor actually moved (or was re-asserted, for Home/End/Drill).
		/// False means a no-op: an empty list, a single navigable item, or a drill
		/// with no children. The adapter plays no sound and speaks nothing.
		/// </summary>
		public bool Moved;

		/// <summary>
		/// Whether the move wrapped past a boundary (end to start, or the jump group
		/// landed at or before the current group). The adapter plays the wrap sound
		/// ("HUD_Click") instead of the step sound ("HUD_Mouseover").
		/// </summary>
		public bool Wrapped;

		/// <summary>The live node the cursor landed on. Null only on a no-op.</summary>
		public NavItem Item;

		/// <summary>
		/// Ancestors whose index changed between the old and new cursor, shallowest
		/// first, for the spoken context prefix. Empty when the move stayed within the
		/// same parent. For a building crossing a category boundary this is
		/// [category, subcategory]; for one crossing only a subcategory it is
		/// [subcategory]; the adapter joins their <see cref="NavItem.Announce"/> text.
		/// </summary>
		public IReadOnlyList<NavItem> ChangedAncestors;

		public static readonly NavMove None = new NavMove {
			Moved = false,
			Wrapped = false,
			Item = null,
			ChangedAncestors = Array.Empty<NavItem>(),
		};
	}
}
