using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using OniAccess.Util;
using UnityEngine;

[assembly: InternalsVisibleTo("OniAccess.Tests")]

namespace OniAccess.Handlers {
	/// <summary>
	/// Reusable type-ahead search helper for keyboard navigation.
	/// Builds a filtered results list (tiered matching) that can be navigated with Up/Down.
	/// Match priority: start-of-string whole word, start-of-string prefix,
	/// mid-string whole word, mid-string prefix, substring anywhere.
	/// Use HandleKey() with an ISearchable for centralized search behavior,
	/// or the lower-level API (AddChar/Search/NavigateResults) for custom handling.
	/// </summary>
	public class TypeAheadSearch {
		private StringBuilder _buffer = new StringBuilder(32);

		// Filtered results state
		private bool _isSearchActive;
		private List<int> _resultIndices = new List<int>();
		private List<string> _resultNames = new List<string>();
		private int _resultCursor;

		// Working lists for search, one pair per match tier (avoids allocation)
		private const int TierCount = 5;
		private List<int>[] _tierIndices;
		private List<string>[] _tierNames;
		private List<int>[] _tierPositions;
		private List<int> _workIndices = new List<int>();
		private List<string> _workNames = new List<string>();

		// Optional callback for full announcements (called with original index)
		private System.Action<int> _announceResult;

		/// <summary>
		/// Optional grouping function: maps an original item index to a group number.
		/// During merge, all group-0 items (sorted by tier+position) appear before
		/// group-1 items, etc. When null, all items are in a single group.
		/// </summary>
		public System.Func<int, int> GroupOf { get; set; }

		// Cached delegates for RunSearch (avoids allocation per call)
		private readonly System.Func<int, string> _getLabelCached;
		private readonly System.Action<int> _moveToIndexCached;

		// Stored reference to the current searchable context, set each HandleKey call
		private ISearchable _searchable;

		public TypeAheadSearch() {
			_getLabelCached = i => _searchable.GetSearchLabel(i);
			_moveToIndexCached = i => _searchable.SearchMoveTo(i);
			_tierIndices = new List<int>[TierCount];
			_tierNames = new List<string>[TierCount];
			_tierPositions = new List<int>[TierCount];
			for (int t = 0; t < TierCount; t++) {
				_tierIndices[t] = new List<int>();
				_tierNames[t] = new List<string>();
				_tierPositions[t] = new List<int>();
			}
		}

		/// <summary>
		/// Current search buffer contents.
		/// </summary>
		public string Buffer => _buffer.ToString();

		/// <summary>
		/// Whether there is an active search buffer.
		/// </summary>
		public bool HasBuffer => _buffer.Length > 0;

		/// <summary>
		/// Whether filtered results are currently being navigated.
		/// True after Search() is called, false after Clear().
		/// </summary>
		public bool IsSearchActive => _isSearchActive;

		/// <summary>
		/// Number of filtered results.
		/// </summary>
		public int ResultCount => _resultIndices.Count;

		/// <summary>
		/// The original-list index of the currently selected result, or -1 if no results.
		/// </summary>
		public int SelectedOriginalIndex =>
			_isSearchActive && _resultCursor >= 0 && _resultCursor < _resultIndices.Count
				? _resultIndices[_resultCursor]
				: -1;

		/// <summary>
		/// Add a character to the search buffer.
		/// Resets the buffer if timeout has elapsed since last input.
		/// </summary>
		public string AddChar(char c) {
			_buffer.Append(c);
			return _buffer.ToString();
		}

		/// <summary>
		/// Remove the last character from the search buffer (backspace).
		/// </summary>
		public bool RemoveChar() {
			if (_buffer.Length == 0)
				return false;

			_buffer.Length--;
			return true;
		}

		/// <summary>
		/// Clear the search buffer and all results state.
		/// </summary>
		public void Clear() {
			_buffer.Clear();
			_isSearchActive = false;
			_resultIndices.Clear();
			_resultNames.Clear();
			_resultCursor = 0;
			_announceResult = null;
		}

		// ========================================
		// HANDLEKEY - CENTRALIZED SEARCH BEHAVIOR
		// ========================================

		/// <summary>
		/// Handle all search-related keyboard behavior.
		/// Call this from HandleKeyDown after any modifier-key shortcuts (Ctrl+T, Alt+I, etc.).
		/// Returns true if the key was consumed by search.
		/// </summary>
		/// <param name="keyCode">The key that was pressed.</param>
		/// <param name="ctrlHeld">Whether Ctrl is held.</param>
		/// <param name="altHeld">Whether Alt is held.</param>
		/// <param name="searchable">The searchable context to search within.</param>
		/// <summary>
		/// Handle a typed character for search. Accepts any letter in any script.
		/// Returns true if the character was consumed by search.
		/// </summary>
		public bool HandleChar(char c, ISearchable searchable) {
			_searchable = searchable;

			if (!_isSearchActive) {
				if (searchable.SearchItemCount == 0)
					return false;
			}

			AddChar(c);
			RunSearch();
			return true;
		}

		public bool HandleKey(KeyCode keyCode, bool ctrlHeld, bool altHeld, ISearchable searchable) {
			_searchable = searchable;

			if (_isSearchActive) {
				switch (keyCode) {
					case KeyCode.UpArrow:
						NavigateResults(-1);
						return true;
					case KeyCode.DownArrow:
						NavigateResults(1);
						return true;
					case KeyCode.Home:
						JumpToFirstResult();
						return true;
					case KeyCode.End:
						JumpToLastResult();
						return true;
					case KeyCode.Backspace:
						if (!RemoveChar())
							return true;
						if (!HasBuffer) {
							Clear();
							Speech.SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.SEARCH.CLEARED);
							return true;
						}
						RunSearch();
						return true;
					case KeyCode.Space:
						if (!ctrlHeld && !altHeld) {
							AddChar(' ');
							RunSearch();
							return true;
						}
						return false;
				}
			}

			// Search inactive but has leftover buffer: handle Backspace
			if (keyCode == KeyCode.Backspace && HasBuffer) {
				if (!RemoveChar()) return true;
				if (!HasBuffer) {
					Clear();
					Speech.SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.SEARCH.CLEARED);
					return true;
				}
				RunSearch();
				return true;
			}

			return false;
		}

		private void RunSearch() {
			if (_searchable == null) return;
			Search(_searchable.SearchItemCount, _getLabelCached, _moveToIndexCached);
		}

		/// <summary>
		/// Perform a tiered search and announce results.
		/// </summary>
		/// <param name="itemCount">Number of items to search.</param>
		/// <param name="nameByIndex">Function returning the searchable name for an index, or null to skip.</param>
		/// <param name="announceResult">Optional callback for full announcements. Called with the original
		/// index of the matched item. When null, falls back to announcing the search name.</param>
		public void Search(int itemCount, System.Func<int, string> nameByIndex, System.Action<int> announceResult = null) {
			// Repeat single-letter: typing the same letter again cycles through results
			// e.g., b -> Beaver, b -> Bat, b -> Brewery
			string bufferStr = _buffer.ToString();
			if (_isSearchActive && _resultIndices.Count > 0 && _buffer.Length > 1 && IsAllSameChar(bufferStr)) {
				_buffer.Length = 1;
				if (announceResult != null)
					_announceResult = announceResult;
				CycleStartsWithResults();
				return;
			}

			if (announceResult != null)
				_announceResult = announceResult;

			if (!HasBuffer || itemCount == 0) {
				_resultIndices.Clear();
				_resultNames.Clear();
				_resultCursor = 0;
				_isSearchActive = true;
				Speech.SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.SEARCH.NO_MATCH, bufferStr));
				return;
			}

			// Classify each item into a match tier
			for (int t = 0; t < TierCount; t++) {
				_tierIndices[t].Clear();
				_tierNames[t].Clear();
				_tierPositions[t].Clear();
			}
			string trimmed = bufferStr.TrimEnd();
			if (trimmed.Length == 0) {
				_resultIndices.Clear();
				_resultNames.Clear();
				_resultCursor = 0;
				_isSearchActive = true;
				Speech.SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.SEARCH.NO_MATCH, bufferStr));
				return;
			}
			string lowerBuffer = trimmed.ToLowerInvariant();

			for (int i = 0; i < itemCount; i++) {
				string name = nameByIndex(i);
				if (string.IsNullOrEmpty(name)) continue;
				int tier = MatchTier(name.ToLowerInvariant(), lowerBuffer, out int pos);
				if (tier >= 0) {
					_tierIndices[tier].Add(i);
					_tierNames[tier].Add(name);
					_tierPositions[tier].Add(pos);
				}
			}

			// Sort each tier by name length, position as tiebreaker
			for (int t = 0; t < TierCount; t++) {
				if (_tierIndices[t].Count > 1)
					SortByLength(_tierIndices[t], _tierNames[t], _tierPositions[t]);
			}

			// Merge tiers: if GroupOf is set, output group 0 first, then group 1, etc.
			_workIndices.Clear();
			_workNames.Clear();
			if (GroupOf == null) {
				for (int t = 0; t < TierCount; t++) {
					_workIndices.AddRange(_tierIndices[t]);
					_workNames.AddRange(_tierNames[t]);
				}
			} else {
				int maxGroup = 0;
				for (int t = 0; t < TierCount; t++)
					for (int i = 0; i < _tierIndices[t].Count; i++) {
						int g = GroupOf(_tierIndices[t][i]);
						if (g > maxGroup) maxGroup = g;
					}
				for (int g = 0; g <= maxGroup; g++)
					for (int t = 0; t < TierCount; t++)
						for (int i = 0; i < _tierIndices[t].Count; i++)
							if (GroupOf(_tierIndices[t][i]) == g) {
								_workIndices.Add(_tierIndices[t][i]);
								_workNames.Add(_tierNames[t][i]);
							}
			}

			if (_workIndices.Count == 0) {
				_resultIndices.Clear();
				_resultNames.Clear();
				_resultCursor = 0;
				_isSearchActive = true;
				Speech.SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.SEARCH.NO_MATCH, bufferStr));
			} else {
				var tempIndices = _resultIndices;
				var tempNames = _resultNames;
				_resultIndices = _workIndices;
				_resultNames = _workNames;
				_workIndices = tempIndices;
				_workNames = tempNames;
				_resultCursor = 0;
				_isSearchActive = true;
				AnnounceCurrentResult();
			}
		}

		/// <summary>
		/// Cycle forward within start-of-string results only (tiers 0-1).
		/// Used for single-letter repeat navigation so holding a key
		/// doesn't wrap into mid-string or substring matches.
		/// </summary>
		private void CycleStartsWithResults() {
			if (_resultIndices.Count == 0) return;

			char letter = char.ToLowerInvariant(_buffer[0]);
			int count = 0;
			for (int i = 0; i < _resultNames.Count; i++) {
				if (_resultNames[i].Length > 0 && char.ToLowerInvariant(_resultNames[i][0]) == letter)
					count++;
				else
					break;
			}

			if (count == 0) return;

			_resultCursor = (_resultCursor + 1) % count;
			AnnounceCurrentResult();
		}

		/// <summary>
		/// Navigate within filtered results (wrapping).
		/// </summary>
		/// <param name="direction">1 for next, -1 for previous.</param>
		public void NavigateResults(int direction) {
			if (_resultIndices.Count == 0) return;

			int count = _resultIndices.Count;
			_resultCursor = ((_resultCursor + direction) % count + count) % count;
			AnnounceCurrentResult();
		}

		/// <summary>
		/// Jump to the first filtered result.
		/// </summary>
		public void JumpToFirstResult() {
			if (_resultIndices.Count == 0) return;

			_resultCursor = 0;
			AnnounceCurrentResult();
		}

		/// <summary>
		/// Jump to the last filtered result.
		/// </summary>
		public void JumpToLastResult() {
			if (_resultIndices.Count == 0) return;

			_resultCursor = _resultIndices.Count - 1;
			AnnounceCurrentResult();
		}

		private void AnnounceCurrentResult() {
			if (_resultIndices.Count == 0) return;

			if (_announceResult != null)
				_announceResult(_resultIndices[_resultCursor]);
			else
				Speech.SpeechPipeline.SpeakInterrupt(_resultNames[_resultCursor]);
		}

		private static bool IsAllSameChar(string s) {
			char first = s[0];
			for (int i = 1; i < s.Length; i++) {
				if (s[i] != first) return false;
			}
			return true;
		}

		/// <summary>
		/// Insertion-sort three parallel lists by name length ascending, with position as tiebreaker (stable, in-place).
		/// </summary>
		private static void SortByLength(List<int> indices, List<string> names, List<int> positions) {
			for (int i = 1; i < positions.Count; i++) {
				int pos = positions[i];
				int idx = indices[i];
				string name = names[i];
				int len = name.Length;
				int j = i - 1;
				while (j >= 0 && (names[j].Length > len || (names[j].Length == len && positions[j] > pos))) {
					positions[j + 1] = positions[j];
					indices[j + 1] = indices[j];
					names[j + 1] = names[j];
					j--;
				}
				positions[j + 1] = pos;
				indices[j + 1] = idx;
				names[j + 1] = name;
			}
		}

		/// <summary>
		/// Returns the match tier for a prefix against a name (both lowercase), or -1 for no match.
		/// 0 = start of string, whole word ("wood" in "wood club")
		/// 1 = start of string, prefix ("wood" in "wooden club")
		/// 2 = mid-string whole word ("wood" in "pine wood")
		/// 3 = mid-string word prefix ("wood" in "a wooden thing")
		/// 4 = substring anywhere ("wood" in "plywood")
		/// </summary>
		internal static int MatchTier(string lowerName, string lowerPrefix) {
			return MatchTier(lowerName, lowerPrefix, out _);
		}

		internal static int MatchTier(string lowerName, string lowerPrefix, out int position) {
			position = -1;
			lowerName = StringUtil.RemoveDiacritics(lowerName);
			lowerPrefix = StringUtil.RemoveDiacritics(lowerPrefix);
			int prefixLen = lowerPrefix.Length;
			if (prefixLen > lowerName.Length)
				return -1;

			// Check start of string
			if (string.Compare(lowerName, 0, lowerPrefix, 0, prefixLen, System.StringComparison.Ordinal) == 0) {
				position = 0;
				bool wholeWord = lowerName.Length == prefixLen || lowerName[prefixLen] == ' ' || lowerName[prefixLen] == ',';
				return wholeWord ? 0 : 1;
			}

			// Check word starts after spaces
			for (int i = 1; i < lowerName.Length; i++) {
				char prev = lowerName[i - 1];
				if (prev != ' ' && prev != ',') continue;
				if (lowerName[i] == ' ') continue;
				if (lowerName.Length - i < prefixLen) break;
				if (string.Compare(lowerName, i, lowerPrefix, 0, prefixLen, System.StringComparison.Ordinal) == 0) {
					int afterMatch = i + prefixLen;
					bool wholeWord = afterMatch >= lowerName.Length || lowerName[afterMatch] == ' ' || lowerName[afterMatch] == ',';
					position = i;
					return wholeWord ? 2 : 3;
				}
			}

			// Substring anywhere
			int idx = lowerName.IndexOf(lowerPrefix, System.StringComparison.Ordinal);
			if (idx >= 0) {
				position = idx;
				return 4;
			}

			return -1;
		}
	}
}
