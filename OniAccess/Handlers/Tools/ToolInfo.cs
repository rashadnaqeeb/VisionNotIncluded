using System;

namespace OniAccess.Handlers.Tools {
	/// <summary>
	/// Describes a standard toolbar tool's properties and behavior flags.
	/// </summary>
	public sealed class ModToolInfo {
		public string ToolName { get; }
		public string Label { get; }
		public Type ToolType { get; }
		public bool HasFilterMenu { get; }
		public bool RequiresModeFirst { get; }
		public bool SupportsPriority { get; }
		public bool IsLineMode { get; }
		public string DragSound { get; }
		public string ConfirmSound { get; }
		public string ConfirmFormat { get; }
		public Func<int, int> CountTargets { get; }
		/// <summary>
		/// Counts targets across all selected cells at once, for tools whose
		/// targets span several cells and must be counted once each. Used in
		/// place of CountTargets when set.
		/// </summary>
		public Func<System.Collections.Generic.HashSet<int>, int> CountTargetsInCells { get; set; }

		public ModToolInfo(
			string toolName,
			string label,
			Type toolType,
			bool hasFilterMenu,
			bool requiresModeFirst,
			bool supportsPriority,
			bool isLineMode,
			string dragSound,
			string confirmSound,
			string confirmFormat,
			Func<int, int> countTargets
		) {
			ToolName = toolName;
			Label = label;
			ToolType = toolType;
			HasFilterMenu = hasFilterMenu;
			RequiresModeFirst = requiresModeFirst;
			SupportsPriority = supportsPriority;
			IsLineMode = isLineMode;
			DragSound = dragSound;
			ConfirmSound = confirmSound;
			ConfirmFormat = confirmFormat;
			CountTargets = countTargets;
		}
	}
}
