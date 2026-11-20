namespace OniAccess.Util {
	/// <summary>Formats diagnostic values using the game's presentation setting.</summary>
	internal static class DiagnosticText {
		internal static string GetValue(ColonyDiagnostic diagnostic) {
			var builder = KleiStringBuilder.BorrowFromPool();
			try {
				if (diagnostic.presentationSetting == ColonyDiagnostic.PresentationSetting.CurrentValue)
					diagnostic.AppendCurrentValueString(ref builder);
				else
					diagnostic.AppendAverageValueString(ref builder);
				return builder.CopyContent();
			} finally {
				builder.Dispose();
			}
		}
	}
}
