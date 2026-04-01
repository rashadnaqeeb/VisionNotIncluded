using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.ToolProfiles {
	/// <summary>
	/// Pairs a tool's name with either sections to prepend onto the active
	/// overlay composer, or a full GlanceComposer that replaces it entirely.
	/// </summary>
	public sealed class ToolProfile {
		public string ToolName { get; }

		/// <summary>Sections prepended to the overlay composer. Null when IsOverride.</summary>
		public IReadOnlyList<ICellSection> PrependSections { get; }

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
	}
}
