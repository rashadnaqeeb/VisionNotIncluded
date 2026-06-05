using System.Collections.Generic;

namespace OniAccess.Widgets {
	/// <summary>
	/// Base class for all navigable UI widgets. Subclasses define
	/// type-specific behavior (speech, validity, activation, adjustment)
	/// so dispatch happens via virtual calls instead of switch statements.
	/// </summary>
	public class Widget: NavItem {
		public string Key { get; set; }
		public string Label { get; set; }
		public UnityEngine.Component Component { get; set; }
		public UnityEngine.GameObject GameObject { get; set; }
		public object Tag { get; set; }
		public System.Func<string> SpeechFunc { get; set; }
		public bool SuppressTooltip { get; set; }
		public List<Widget> Children { get; set; }

		protected bool? _isInteractableOverride;

		public virtual bool IsInteractable {
			get => _isInteractableOverride ?? true;
			set => _isInteractableOverride = value;
		}

		/// <summary>
		/// Build speech text for this widget. SpeechFunc short-circuits
		/// if set; otherwise subclasses provide type-specific formatting.
		/// </summary>
		public virtual string GetSpeechText() {
			if (SpeechFunc != null) {
				string result = SpeechFunc()?.Trim();
				if (!string.IsNullOrEmpty(result)) return result;
			}
			return Label;
		}

		// ========================================
		// NavItem
		// ========================================

		/// <summary>
		/// Control role key consumed by future decoration (verbose UI mode).
		/// Null on the base type and on read-only labels.
		/// </summary>
		public virtual string RoleKey => null;

		public bool IsNavigable() => IsValid();

		/// <summary>Whether Enter performs an action. False for read-only and adjust-only widgets.</summary>
		public virtual bool IsActivatable() => false;

		/// <summary>Own label and value, cleaned and live-read. Tooltip is appended by the composer.</summary>
		public string Announce() => WidgetOps.GetSpeechText(this);

		public IReadOnlyList<NavItem> GetChildren() {
			if (Children == null) return System.Array.Empty<NavItem>();
			return Children;
		}

		/// <summary>
		/// Whether the widget is still valid for navigation (active, visible).
		/// </summary>
		public virtual bool IsValid() {
			if (GameObject != null && !GameObject.activeInHierarchy) return false;
			return Component != null || GameObject != null;
		}

		/// <summary>
		/// Activate the widget (click, toggle, begin editing). Returns true if handled.
		/// </summary>
		public virtual bool Activate() => false;

		/// <summary>
		/// Adjust the widget's value (slider step, dropdown cycle).
		/// Returns true if the value changed.
		/// </summary>
		public virtual bool Adjust(int direction, int stepLevel) => false;

		/// <summary>
		/// Whether this widget supports Left/Right adjustment.
		/// </summary>
		public virtual bool IsAdjustable => false;

		/// <summary>
		/// Copy mutable fields from source into this widget, preserving
		/// identity and position in the list. Used by SectionMerger.
		/// </summary>
		public virtual void UpdateFrom(Widget source) {
			Label = source.Label;
			Component = source.Component;
			GameObject = source.GameObject;
			Tag = source.Tag;
			SpeechFunc = source.SpeechFunc;
			SuppressTooltip = source.SuppressTooltip;
			_isInteractableOverride = source._isInteractableOverride;
		}
	}
}
