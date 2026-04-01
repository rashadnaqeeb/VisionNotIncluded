namespace OniAccess.Widgets {
	/// <summary>
	/// A text input widget (KInputTextField, KNumberInputField, KInputField).
	/// Resolves the underlying input field for text editing.
	/// </summary>
	public class TextInputWidget: Widget {
		public override string GetSpeechText() {
			var field = GetTextField();
			if (field != null && !string.IsNullOrEmpty(field.text))
				return field.text;
			return base.GetSpeechText();
		}

		/// <summary>
		/// Resolve the KInputTextField from the component, handling
		/// KNumberInputField.field and KInputField.field indirection.
		/// </summary>
		public KInputTextField GetTextField() {
			var direct = Component as KInputTextField;
			if (direct != null) return direct;

			var knum = Component as KNumberInputField;
			if (knum != null) return knum.field;

			var kinput = Component as KInputField;
			if (kinput != null) return kinput.field;

			return null;
		}
	}
}
