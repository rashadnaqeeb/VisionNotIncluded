namespace OniAccess.Util {
	internal static class DiagnosticText {
		/// <summary>
		/// The value the colony diagnostic panel shows beside a diagnostic:
		/// its current or its averaged value, per its presentation setting.
		/// </summary>
		internal static string GetPresentedValue(ColonyDiagnostic diag) {
			var builder = KleiStringBuilder.BorrowFromPool();
			if (diag.presentationSetting == ColonyDiagnostic.PresentationSetting.CurrentValue)
				diag.AppendCurrentValueString(ref builder);
			else
				diag.AppendAverageValueString(ref builder);
			return builder.TakeContent();
		}
	}
}
