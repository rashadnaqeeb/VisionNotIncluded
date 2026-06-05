using System;
using System.Collections.Generic;

namespace OniAccess.Widgets {
	/// <summary>
	/// A general-purpose <see cref="NavItem"/> for menu trees built from game data
	/// rather than live UI controls. Its label, children, and activation are supplied
	/// as delegates, so a handler can describe a whole drill tree declaratively without
	/// a class per node type.
	///
	/// Like <see cref="LabelItem"/>, nothing is stored: the label is read on each
	/// <see cref="Announce"/> and children are computed on each <see cref="GetChildren"/>,
	/// so the tree always reflects live, filtered, or dynamic content. Children are the
	/// lazy <c>children()</c> the unified engine walks.
	/// </summary>
	public sealed class MenuNode: NavItem {
		private readonly Func<string> _announce;
		private readonly Func<IReadOnlyList<NavItem>> _children;
		private readonly Func<bool> _activate;
		private readonly bool _navigable;

		public MenuNode(
				Func<string> announce,
				Func<IReadOnlyList<NavItem>> children = null,
				Func<bool> activate = null,
				bool navigable = true,
				string roleKey = null) {
			_announce = announce;
			_children = children;
			_activate = activate;
			_navigable = navigable;
			RoleKey = roleKey;
		}

		public string RoleKey { get; }
		public bool IsNavigable() => _navigable;
		public bool IsActivatable() => _activate != null;
		public string Announce() => _announce();
		public bool Activate() => _activate != null && _activate();
		public bool Adjust(int direction, int stepLevel) => false;

		public IReadOnlyList<NavItem> GetChildren() =>
			_children?.Invoke() ?? Array.Empty<NavItem>();
	}
}
