using System.Collections.Generic;
using System.Text.RegularExpressions;
using OniAccess.Util;

namespace OniAccess.Speech {
	/// <summary>
	/// Rich text filtering pipeline for speech output.
	/// Strips Unity Rich Text, TextMeshPro, and ONI-specific markup,
	/// converting meaningful tags to spoken text and silently removing decorative ones.
	///
	/// All text must pass through FilterForSpeech before reaching SpeechEngine.
	/// Filter order is critical -- sprite conversion must happen before tag stripping.
	/// </summary>
	public static class TextFilter {
		// Sprite tag: <sprite name=warning> or <sprite name="warning"/> or <sprite name="warning" />
		private static readonly Regex SpriteTagRegex =
			new Regex(@"<sprite\s+name=""?([^"">]+)""?\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		// Link tag: <link="LINK_ID">display text</link>
		private static readonly Regex LinkTagRegex =
			new Regex(@"<link=""[^""]*"">(.*?)</link>", RegexOptions.Compiled);

		// Hotkey placeholder: strip from {Hotkey} onward
		private static readonly Regex HotkeyPlaceholderRegex =
			new Regex(@"\s*\{Hotkey\}.*", RegexOptions.Compiled);

		// Catch-all for remaining rich text tags (bold, color, size, style, etc.)
		private static readonly Regex RichTextTagsRegex =
			new Regex("<[^>]+>", RegexOptions.Compiled);

		// Numeric bracket content like [45%] from status items
		private static readonly Regex NumericBracketRegex =
			new Regex(@"\[(\d[^\]]*)\]", RegexOptions.Compiled);

		// TextMeshPro shorthand sprite tags like [icon_name]
		private static readonly Regex TmpSpriteTagRegex =
			new Regex(@"\[[^\]]+\]\s*", RegexOptions.Compiled);

		// Temperature unit suffixes (°C, °F). Kelvin (" K") skipped — too
		// likely to false-match. Players pick a unit once; repeating it on
		// every temperature readout is noise.
		// Strip just the unit letter after the degree sign, keeping ° so
		// screen readers still say "degrees".
		private static readonly Regex TempUnitRegex =
			new Regex(@"(?<=°)[CF]\b", RegexOptions.Compiled);

		// Normalize whitespace (collapse multiple spaces/newlines/tabs)
		private static readonly Regex WhitespaceRegex =
			new Regex(@"\s+", RegexOptions.Compiled);

		// Sprite name -> spoken text mapping
		private static readonly Dictionary<string, string> _spriteTextMap =
			new Dictionary<string, string>();

		// Sprites already warned about (suppress repeated log spam)
		private static readonly HashSet<string> _warnedSprites = new HashSet<string>();

		// " (Original)" suffix the game appends to every non-mutated plant once
		// any mutation is discovered for that species.
		private static string _originalMutationSuffix;

		/// <summary>
		/// Register a sprite name to spoken text mapping.
		/// Meaningful sprites (e.g., "warning") are converted to words;
		/// unregistered sprites are silently stripped (but logged).
		/// </summary>
		/// <param name="spriteName">The sprite name as it appears in markup (case-insensitive)</param>
		/// <param name="spokenText">The text to speak instead of the sprite</param>
		public static void RegisterSprite(string spriteName, string spokenText) {
			if (!string.IsNullOrEmpty(spriteName))
				_spriteTextMap[spriteName.ToLowerInvariant()] = spokenText ?? "";
		}

		/// <summary>
		/// Initialize default ONI sprite mappings.
		/// Called during mod startup to register known meaningful sprites.
		/// </summary>
		public static void InitializeDefaults() {
			RegisterSprite("warning", (string)STRINGS.ONIACCESS.SPRITES.WARNING);
			RegisterSprite("logic_signal_green", (string)STRINGS.ONIACCESS.SPRITES.LOGIC_GREEN);
			RegisterSprite("logic_signal_red", (string)STRINGS.ONIACCESS.SPRITES.LOGIC_RED);
			SetOriginalMutationLabel((string)STRINGS.CREATURES.PLANT_MUTATIONS.NONE.NAME);
		}

		/// <summary>
		/// Set the "original plant" mutation label that should be stripped from
		/// plant names. Called by InitializeDefaults with the game's localized
		/// string; tests can call directly with a literal.
		/// </summary>
		public static void SetOriginalMutationLabel(string label) {
			_originalMutationSuffix = string.IsNullOrEmpty(label) ? null : " (" + label + ")";
		}

		/// <summary>
		/// Main filtering pipeline. Strips all rich text markup and converts
		/// meaningful sprites to spoken words.
		///
		/// Filter order (critical):
		/// 1. Convert known sprite tags to spoken text (before stripping tags)
		/// 2. Extract link display text (before stripping tags)
		/// 3. Strip hotkey placeholders
		/// 4. Strip all remaining rich text tags
		/// 5. Extract numeric bracket content (e.g., [45%] -> 45%)
		/// 6. Strip TMP bracket sprites
		/// 7. Clean up empty brackets/parens
		/// 8. Normalize whitespace
		/// 9. Trim
		/// </summary>
		public static string FilterForSpeech(string text) {
			if (string.IsNullOrEmpty(text)) return "";

			// Strip control characters (null bytes, etc.) that can truncate
			// speech output. TMP's GetParsedText() emits \x00 for empty fields.
			text = StripControlChars(text);
			if (text.Length == 0) return "";

			// Replace masculine ordinal indicator (º, U+00BA) with degree sign (°, U+00B0).
			// Screen readers mispronounce º; ONI uses it in temperature strings.
			text = text.Replace('\u00BA', '\u00B0');

			// Strip temperature unit suffixes now that º is normalized to °.
			text = TempUnitRegex.Replace(text, "");

			// Strip bullet (•, U+2022). ONI uses it as a list prefix in diagnostic
			// messages and tooltips. Screen readers announce it as "bullet" which
			// breaks speech flow.
			text = text.Replace("\u2022", "");

			// Strip "(Original)" mutation label the game appends to non-mutated
			// plants once any mutation is discovered for that species.
			if (_originalMutationSuffix != null)
				text = text.Replace(_originalMutationSuffix, "");

			// Resolve game template placeholders: {Hotkey/ActionName} → key name,
			// (ClickType/click) → "click" or "press" depending on controller.
			// LocText normally does this at render time; we read raw data.
			LocText.ParseText(text, out text);

			// Fast path: skip regex pipeline for plain text (no markup)
			if (text.IndexOf('<') < 0 && text.IndexOf('[') < 0 && text.IndexOf('{') < 0)
				return WhitespaceRegex.Replace(text, " ").Trim();

			// 1. Convert known sprite tags to spoken text, log unrecognized ones
			text = SpriteTagRegex.Replace(text, match => {
				string spriteName = match.Groups[1].Value.Trim().ToLowerInvariant();
				if (_spriteTextMap.TryGetValue(spriteName, out string spoken)) {
					// Append space after spoken text so it separates from following content;
					// whitespace normalization in step 7 will collapse any extra spaces
					return string.IsNullOrEmpty(spoken) ? "" : spoken + " ";
				}
				if (_warnedSprites.Add(spriteName))
					Log.Debug($"Unrecognized sprite tag: {spriteName}");
				return "";
			});

			// 2. Extract link display text (keep inner text, remove link wrapper)
			text = LinkTagRegex.Replace(text, "$1");

			// 3. Strip hotkey placeholders (from {Hotkey} onward)
			text = HotkeyPlaceholderRegex.Replace(text, "");

			// 4. Strip all remaining rich text tags
			text = RichTextTagsRegex.Replace(text, "");

			// 4b. A colon followed by a period (e.g. "<b>Effects:</b>. Morale")
			// leaves ":." after tag stripping. The colon is already punctuation.
			text = text.Replace(":.", ":");

			// 5. Extract numeric bracket content (e.g., [45%] -> 45%)
			text = NumericBracketRegex.Replace(text, "$1");

			// 6. Strip TMP bracket sprites
			text = TmpSpriteTagRegex.Replace(text, "");

			// 7. Clean up empty brackets/parens left behind
			text = text.Replace("[]", "");
			text = text.Replace("()", "");

			// 8. Normalize whitespace
			text = WhitespaceRegex.Replace(text, " ");

			// 9. Trim
			return text.Trim();
		}

		private static string StripControlChars(string text) {
			int i = 0;
			for (; i < text.Length; i++) {
				char c = text[i];
				if (c < 0x20 && c != '\n' && c != '\r' && c != '\t')
					break;
			}
			if (i == text.Length) return text;

			var sb = new System.Text.StringBuilder(text.Length);
			if (i > 0) sb.Append(text, 0, i);
			for (; i < text.Length; i++) {
				char c = text[i];
				if (c >= 0x20 || c == '\n' || c == '\r' || c == '\t')
					sb.Append(c);
			}
			return sb.ToString();
		}
	}
}
