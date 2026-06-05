namespace OniAccess.Widgets {
	/// <summary>
	/// Stateless utility methods for widget speech, tooltip reading, validity
	/// checking, and programmatic interaction. Extracted from BaseWidgetHandler
	/// so that any handler (including NavTreeHandler-based ones like
	/// DetailsScreenHandler) can reuse them without inheritance.
	/// </summary>
	public static class WidgetOps {
		// ========================================
		// SPEECH
		// ========================================

		/// <summary>
		/// Build speech text for a widget by delegating to its virtual GetSpeechText().
		/// </summary>
		public static string GetSpeechText(Widget widget) {
			string text = CleanTooltipEntry(widget.GetSpeechText());
			if (!widget.IsInteractable)
				text += $", {(string)STRINGS.ONIACCESS.FABRICATOR.UNAVAILABLE}";
			return text;
		}

		// ========================================
		// TOOLTIP
		// ========================================

		/// <summary>
		/// Look up tooltip text for a widget via its GameObject's ToolTip component.
		/// Returns null if suppressed, missing, or empty.
		/// </summary>
		public static string GetTooltipText(Widget widget) {
			if (widget.SuppressTooltip) return null;

			// Radio-collapsed dropdowns: read the selected member's tooltip,
			// not the container's.
			if (widget is DropdownWidget
				&& widget.Tag is System.Collections.Generic.List<SideScreenWalker.RadioMember> members) {
				for (int i = 0; i < members.Count; i++) {
					if (members[i].Toggle != null
						&& SideScreenWalker.IsToggleActive(members[i].Toggle)) {
						var tt = members[i].Toggle.GetComponent<ToolTip>();
						if (tt != null) return ReadAllTooltipText(tt);
						break;
					}
				}
				return null;
			}

			if (widget.GameObject == null) return null;

			var tooltip = GetEnabledTooltip(widget.GameObject);
			if (tooltip == null) return null;

			return ReadAllTooltipText(tooltip);
		}

		/// <summary>
		/// Search self, children, then parents for the first enabled ToolTip.
		/// Disabled tooltips (e.g., prefab defaults the game turns off) often
		/// contain unresolved MISSING.STRINGS keys.
		/// </summary>
		private static ToolTip GetEnabledTooltip(UnityEngine.GameObject go) {
			var tt = go.GetComponent<ToolTip>();
			if (tt != null && tt.enabled) return tt;

			tt = go.GetComponentInChildren<ToolTip>();
			if (tt != null && tt.enabled) return tt;

			tt = go.GetComponentInParent<ToolTip>();
			if (tt != null && tt.enabled) return tt;

			return null;
		}

		/// <summary>
		/// Append tooltip text to speech text, dropping any tooltip sentence
		/// that duplicates an existing comma-separated segment of the speech.
		/// </summary>
		public static string AppendTooltip(string speech, string tooltip) {
			if (tooltip == null) return speech;
			if (string.IsNullOrEmpty(speech)) return tooltip;

			// Strip rich text from the tooltip before comparing. At this point
			// the tooltip has been through CleanTooltipEntry (newlines → periods)
			// but still contains raw HTML tags (sprites, links, colors).
			tooltip = Speech.TextFilter.FilterForSpeech(tooltip);
			if (string.IsNullOrEmpty(tooltip)) return speech;

			var speechSegments = new System.Collections.Generic.HashSet<string>(
				speech.Split(new[] { ", " }, System.StringSplitOptions.None));

			var tooltipSentences = tooltip.Split(new[] { ". " }, System.StringSplitOptions.None);
			var novel = new System.Collections.Generic.List<string>();
			foreach (var sentence in tooltipSentences) {
				string trimmed = sentence.TrimEnd('.', ' ');
				if (!string.IsNullOrWhiteSpace(trimmed) && !speechSegments.Contains(trimmed))
					novel.Add(trimmed);
			}

			if (novel.Count == 0) return speech;
			return $"{speech}, {string.Join(", ", novel)}";
		}

		/// <summary>
		/// Rebuild a ToolTip's dynamic content and return all multiString
		/// entries as sentences separated by periods.
		/// </summary>
		public static string ReadAllTooltipText(ToolTip tooltip) {
			tooltip.RebuildDynamicTooltip();

			if (tooltip.multiStringCount == 0) return null;

			if (tooltip.multiStringCount == 1) {
				string single = CleanTooltipEntry(tooltip.GetMultiString(0));
				return string.IsNullOrEmpty(single) ? null : single;
			}

			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < tooltip.multiStringCount; i++) {
				string entry = CleanTooltipEntry(tooltip.GetMultiString(i));
				if (string.IsNullOrEmpty(entry)) continue;
				if (sb.Length > 0) {
					char last = sb[sb.Length - 1];
					if (last != '.' && last != '!' && last != '?')
						sb.Append('.');
					sb.Append(' ');
				}
				sb.Append(entry);
			}
			return sb.Length == 0 ? null : sb.ToString();
		}

		/// <summary>
		/// Replace newlines and bullet characters with sentence boundaries
		/// so the screen reader pauses naturally between fields.
		/// </summary>
		internal static string CleanTooltipEntry(string text) {
			if (string.IsNullOrEmpty(text)) return text;

			// Strip Private Use Area characters (U+E000–U+F8FF) and the object
			// replacement character (U+FFFC). TMPro substitutes sprite tags with
			// PUA codepoints (e.g. U+E00F for germs); screen readers announce
			// these as stray symbols.
			var sb = new System.Text.StringBuilder(text.Length);
			foreach (char c in text) {
				if (c >= '\uE000' && c <= '\uF8FF') continue;
				if (c == '\uFFFC') continue;
				sb.Append(c);
			}
			text = sb.ToString();

			// Replace bullets (with surrounding whitespace variants)
			text = text.Replace(" \u2022 ", ". ");
			text = text.Replace("\u2022 ", ". ");
			text = text.Replace("\u2022", ".");

			// Replace newlines with period-space sentence boundaries.
			// The game uses \n as a field separator in tooltip text;
			// TextFilter would otherwise collapse these to plain spaces.
			text = text.Replace("\n", ". ");

			// Collapse runs of whitespace so indented lines (e.g. "\n    • X")
			// don't leave gaps that prevent ". . " deduplication below.
			while (text.Contains("  "))
				text = text.Replace("  ", " ");
			text = text.Replace("\t", " ");
			text = text.Replace(" ,", ",");
			text = text.TrimStart(' ', '.');
			while (text.Contains(". . "))
				text = text.Replace(". . ", ". ");
			text = text.Replace("..", ".");
			text = text.TrimEnd();

			return text;
		}

		// ========================================
		// VALIDITY
		// ========================================

		/// <summary>
		/// Check whether a widget is still valid (not destroyed, active in hierarchy,
		/// and interactable where applicable).
		/// </summary>
		public static bool IsValid(Widget widget) {
			if (widget == null) return false;
			return widget.IsValid();
		}

		// ========================================
		// MULTI-TOGGLE STATE
		// ========================================

		/// <summary>
		/// Map a MultiToggle's CurrentState to a speech string.
		/// 4-state toggles (ReceptacleSideScreen, mutation panel):
		///   0=Inactive, 1=Active(selected), 2=Disabled, 3=DisabledActive
		/// 2/3-state toggles: 0=off, last=on, middle=mixed.
		/// </summary>
		public static string GetMultiToggleState(MultiToggle mt) {
			int stateCount = mt.states != null ? mt.states.Length : 2;

			if (stateCount == 4) {
				bool selected = mt.CurrentState == 1 || mt.CurrentState == 3;
				bool disabled = mt.CurrentState == 2 || mt.CurrentState == 3;
				if (selected && disabled)
					return $"{(string)STRINGS.ONIACCESS.STATES.SELECTED}, {(string)STRINGS.ONIACCESS.STATES.DISABLED}";
				if (selected) return (string)STRINGS.ONIACCESS.STATES.SELECTED;
				if (disabled) return (string)STRINGS.ONIACCESS.STATES.DISABLED;
				return (string)STRINGS.ONIACCESS.STATES.OFF;
			}

			int last = stateCount - 1;
			if (mt.CurrentState <= 0)
				return (string)STRINGS.ONIACCESS.STATES.OFF;
			if (mt.CurrentState >= last)
				return (string)STRINGS.ONIACCESS.STATES.ON;
			return (string)STRINGS.ONIACCESS.STATES.MIXED;
		}

		// ========================================
		// SLIDER FORMATTING
		// ========================================

		public static string FormatSliderValue(KSlider slider) {
			if (slider.wholeNumbers) {
				return ((int)slider.value).ToString();
			}

			// Only treat as percentage when maxValue is well above 1 — a max of
			// 1.0 or 10.0 is ambiguous (e.g., gas/liquid valve flow in kg/s).
			if (slider.minValue >= 0f && slider.maxValue > 1f && slider.maxValue <= 100f) {
				return GameUtil.GetFormattedPercent(slider.value);
			}

			return slider.value.ToString("F1");
		}

		// ========================================
		// INTERACTION
		// ========================================

		public static void ClickButton(KButton button) {
			button.PlayPointerDownSound();
			button.SignalClick(KKeyCode.Mouse0);
		}

		public static void ClickMultiToggle(MultiToggle toggle) {
			var eventData = new UnityEngine.EventSystems.PointerEventData(
				UnityEngine.EventSystems.EventSystem.current) {
				button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
				clickCount = 1
			};
			toggle.OnPointerDown(eventData);
			toggle.OnPointerClick(eventData);
		}

		/// <summary>
		/// Extract a button's label from its child LocText, or return a fallback.
		/// </summary>
		public static string GetButtonLabel(KButton button, string fallback = null) {
			var locText = button.GetComponentInChildren<LocText>();
			if (locText != null) {
				string parsed = Speech.TextFilter.FilterForSpeech(locText.GetParsedText());
				if (!string.IsNullOrEmpty(parsed)) return parsed;
				string raw = Speech.TextFilter.FilterForSpeech(locText.text);
				if (!string.IsNullOrEmpty(raw)) return raw;
			}
			return fallback;
		}
	}
}
