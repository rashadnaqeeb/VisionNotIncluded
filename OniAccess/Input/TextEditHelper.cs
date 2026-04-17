namespace OniAccess.Input {
	public class TextEditHelper {
		public bool IsEditing { get; private set; }
		private string _cachedValue;
		private System.Func<KInputTextField> _fieldAccessor;
		private System.Action _onEnd;

		private string _lastText;
		private int _lastCaret;
		private int _lastAnchor;
		private bool _suppressNextDiff;

		public void Begin(KInputTextField field, System.Action onEnd = null) {
			_cachedValue = field.text;
			_fieldAccessor = () => field;
			_onEnd = onEnd;
			IsEditing = true;
			field.ActivateInputField();
			Speech.SpeechPipeline.SpeakInterrupt($"{STRINGS.ONIACCESS.TEXT_EDIT.EDITING}, {field.text}");
			ResetBaseline(field);
		}

		public void Begin(System.Func<KInputTextField> accessor, System.Action onEnd = null) {
			var field = accessor();
			if (field == null) return;
			_cachedValue = field.text;
			_fieldAccessor = accessor;
			_onEnd = onEnd;
			IsEditing = true;
			field.gameObject.SetActive(true);
			field.text = _cachedValue;
			field.Select();
			field.ActivateInputField();
			Speech.SpeechPipeline.SpeakInterrupt($"{STRINGS.ONIACCESS.TEXT_EDIT.EDITING}, {_cachedValue}");
			ResetBaseline(field);
		}

		/// <summary>
		/// Call from the owner's Tick(). Returns true while editing (caller should
		/// block further input). Handles Enter to confirm, Ctrl+C/V, Up/Down to
		/// re-read, and observes caret/text/selection changes to announce them.
		/// </summary>
		public bool HandleTick() {
			if (!IsEditing) return false;
			var field = _fieldAccessor?.Invoke();
			if (field == null) return true;

			bool ctrlHeld = InputUtil.CtrlHeld();

			if (ctrlHeld && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.C)) {
				int anchor = field.selectionAnchorPosition;
				int caret = field.caretPosition;
				string text = field.text;
				if (anchor != caret) {
					int start = System.Math.Min(anchor, caret);
					int len = System.Math.Abs(caret - anchor);
					UnityEngine.GUIUtility.systemCopyBuffer = text.Substring(start, len);
				} else {
					UnityEngine.GUIUtility.systemCopyBuffer = text;
				}
				Speech.SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TEXT_EDIT.COPIED);
				return true;
			}

			if (ctrlHeld && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.V)) {
				field.text = UnityEngine.GUIUtility.systemCopyBuffer;
				Speech.SpeechPipeline.SpeakInterrupt($"{STRINGS.ONIACCESS.TEXT_EDIT.PASTED}, {field.text}");
				ResetBaseline(field);
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				Confirm();
				_onEnd?.Invoke();
				return true;
			}

			if (!ctrlHeld && (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)
					|| UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow))) {
				string text = field.text;
				if (string.IsNullOrEmpty(text)) {
					Speech.SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TEXT_EDIT.BLANK);
				} else {
					Speech.SpeechPipeline.SpeakInterrupt(text);
				}
				ResetBaseline(field);
				return true;
			}

			if (_suppressNextDiff) {
				_suppressNextDiff = false;
				_lastText = field.text;
				_lastCaret = field.caretPosition;
				_lastAnchor = field.selectionAnchorPosition;
				return true;
			}

			DiffAndAnnounce(field, ctrlHeld);
			return true;
		}

		/// <summary>
		/// Call from the owner's HandleKeyDown(). Returns true if the event was
		/// consumed (Escape to cancel).
		/// </summary>
		public bool HandleKeyDown(KButtonEvent e) {
			if (!IsEditing) return false;
			if (e.TryConsume(Action.Escape)) {
				Cancel();
				_onEnd?.Invoke();
				return true;
			}
			return false;
		}

		public void Confirm() {
			IsEditing = false;
			var field = _fieldAccessor?.Invoke();
			if (field != null) {
				field.DeactivateInputField();
			} else {
				Util.Log.Warn("TextEditHelper.Confirm: field accessor returned null, treating as cancel");
				Speech.SpeechPipeline.SpeakInterrupt($"{STRINGS.ONIACCESS.TEXT_EDIT.CANCELLED}, {_cachedValue}");
			}
		}

		public void Cancel() {
			IsEditing = false;
			var field = _fieldAccessor?.Invoke();
			if (field != null) {
				field.text = _cachedValue;
				field.DeactivateInputField();
				Speech.SpeechPipeline.SpeakInterrupt($"{STRINGS.ONIACCESS.TEXT_EDIT.CANCELLED}, {field.text}");
			} else {
				Util.Log.Warn("TextEditHelper.Cancel: field accessor returned null");
				Speech.SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TEXT_EDIT.CANCELLED);
			}
		}

		private void ResetBaseline(KInputTextField field) {
			_lastText = field.text;
			_lastCaret = field.caretPosition;
			_lastAnchor = field.selectionAnchorPosition;
			_suppressNextDiff = true;
		}

		private void DiffAndAnnounce(KInputTextField field, bool ctrlHeld) {
			string text = field.text;
			int caret = field.caretPosition;
			int anchor = field.selectionAnchorPosition;

			if (text == _lastText && caret == _lastCaret && anchor == _lastAnchor) {
				return;
			}

			if (text != _lastText) {
				int p = CommonPrefixLength(_lastText, text);
				int maxSuffix = System.Math.Min(_lastText.Length - p, text.Length - p);
				int s = CommonSuffixLength(_lastText, text, maxSuffix);
				int removedLen = _lastText.Length - p - s;
				int insertedLen = text.Length - p - s;
				if (removedLen > 0 && insertedLen == 0) {
					string removed = _lastText.Substring(p, removedLen);
					AnnounceDeletion(removed);
				}
				// insertion-only or replace: silent (typing rule; users review with Up/Down)
			} else {
				bool wasSelecting = _lastAnchor != _lastCaret;
				bool isSelecting = anchor != caret;
				if (isSelecting) {
					AnnounceSelectionChange(_lastAnchor, _lastCaret, anchor, caret, text);
				} else if (wasSelecting) {
					// Selection collapsed via plain arrow; announce where the caret landed
					AnnounceCharToRight(caret, text);
				} else {
					AnnounceCaretMove(caret, text, ctrlHeld);
				}
			}

			_lastText = text;
			_lastCaret = caret;
			_lastAnchor = anchor;
		}

		private static int CommonPrefixLength(string a, string b) {
			int n = System.Math.Min(a.Length, b.Length);
			int i = 0;
			while (i < n && a[i] == b[i]) i++;
			return i;
		}

		private static int CommonSuffixLength(string a, string b, int maxLen) {
			int i = 0;
			while (i < maxLen && a[a.Length - 1 - i] == b[b.Length - 1 - i]) i++;
			return i;
		}

		private static void AnnounceCaretMove(int newCaret, string text, bool ctrlHeld) {
			string toSpeak;
			if (ctrlHeld) {
				toSpeak = GetWordToRight(text, newCaret);
			} else if (newCaret >= text.Length) {
				toSpeak = (string)STRINGS.ONIACCESS.TEXT_EDIT.BLANK;
			} else {
				toSpeak = text[newCaret].ToString();
			}
			Speech.SpeechPipeline.SpeakInterrupt(toSpeak);
		}

		private static void AnnounceCharToRight(int newCaret, string text) {
			string toSpeak = newCaret >= text.Length
				? (string)STRINGS.ONIACCESS.TEXT_EDIT.BLANK
				: text[newCaret].ToString();
			Speech.SpeechPipeline.SpeakInterrupt(toSpeak);
		}

		private static void AnnounceDeletion(string removed) {
			Speech.SpeechPipeline.SpeakInterrupt(
				string.Format((string)STRINGS.ONIACCESS.TEXT_EDIT.DELETED_FMT, removed));
		}

		private static void AnnounceSelectionChange(int oldAnchor, int oldCaret, int newAnchor, int newCaret, string text) {
			if (oldAnchor == newAnchor) {
				int oldSize = System.Math.Abs(oldCaret - oldAnchor);
				int newSize = System.Math.Abs(newCaret - newAnchor);
				int start = System.Math.Min(oldCaret, newCaret);
				int len = System.Math.Abs(newCaret - oldCaret);
				if (len == 0) return;
				string substr = text.Substring(start, len);
				if (newSize > oldSize) {
					Speech.SpeechPipeline.SpeakInterrupt(
						string.Format((string)STRINGS.ONIACCESS.TEXT_EDIT.SELECTED_FMT, substr));
				} else if (newSize < oldSize) {
					Speech.SpeechPipeline.SpeakInterrupt(
						string.Format((string)STRINGS.ONIACCESS.TEXT_EDIT.UNSELECTED_FMT, substr));
				}
			} else {
				int start = System.Math.Min(newAnchor, newCaret);
				int len = System.Math.Abs(newCaret - newAnchor);
				if (len == 0) return;
				string substr = text.Substring(start, len);
				Speech.SpeechPipeline.SpeakInterrupt(
					string.Format((string)STRINGS.ONIACCESS.TEXT_EDIT.SELECTED_FMT, substr));
			}
		}

		private static string GetWordToRight(string text, int caret) {
			int i = caret;
			while (i < text.Length && !char.IsLetterOrDigit(text[i])) i++;
			if (i >= text.Length) return (string)STRINGS.ONIACCESS.TEXT_EDIT.BLANK;
			int start = i;
			while (i < text.Length && char.IsLetterOrDigit(text[i])) i++;
			return text.Substring(start, i - start);
		}
	}
}
