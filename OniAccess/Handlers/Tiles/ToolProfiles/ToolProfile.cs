using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.ToolProfiles {
	/// <summary>
	/// Pairs a tool's name with sections to prepend onto the active overlay
	/// composer, sections to append after it, or a full GlanceComposer that
	/// replaces it entirely.
	/// </summary>
	public sealed class ToolProfile {
		public string ToolName { get; }

		/// <summary>Sections prepended to the overlay composer. Null when IsOverride or appending.</summary>
		public IReadOnlyList<ICellSection> PrependSections { get; }

		/// <summary>Sections appended after the overlay composer's sections. Null unless created by Appending.</summary>
		public IReadOnlyList<ICellSection> AppendSections { get; }

		/// <summary>Full replacement composer. Null when not IsOverride.</summary>
		public GlanceComposer Composer { get; }

		public bool IsOverride => Composer != null;

		/// <summary>Prepend mode: these sections are inserted before the overlay composer's sections.</summary>
		public ToolProfile(string toolName, IReadOnlyList<ICellSection> prependSections) {
			ToolName = toolName;
			PrependSections = prependSections;
		}

		/// <summary>Override mode: this composer fully replaces the overlay composer.</summary>
		public ToolProfile(string toolName, GlanceComposer composer) {
			ToolName = toolName;
			Composer = composer;
		}

		private ToolProfile(string toolName, IReadOnlyList<ICellSection> appendSections, bool append) {
			ToolName = toolName;
			AppendSections = appendSections;
		}

		/// <summary>
		/// Append mode: these sections follow the overlay composer's sections,
		/// the slot the build extent uses so the player hears the cell first
		/// and can interrupt before the tool's extent plays.
		/// </summary>
		public static ToolProfile Appending(string toolName, IReadOnlyList<ICellSection> appendSections) {
			return new ToolProfile(toolName, appendSections, append: true);
		}
	}
}
