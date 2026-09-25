namespace OniAccess.Input {
	/// <summary>
	/// Shared modifier-key helpers used by handlers that detect their own keys
	/// via UnityEngine.Input.GetKeyDown in Tick().
	/// Key names in code and help are written for Windows. On macOS, Ctrl stays
	/// Control except where macOS or Mac habit claims the combo: Ctrl with an
	/// arrow key or Space is Option (Control+arrows switch Spaces and open Mission
	/// Control, Control+Space switches input source), Ctrl+F and text-field
	/// copy and paste are Command, and Alt is Command.
	/// </summary>
	public static class InputUtil {
		public static readonly bool IsMac =
			UnityEngine.SystemInfo.operatingSystemFamily == UnityEngine.OperatingSystemFamily.MacOSX;

		/// <summary>Any modifier the mod reads, on any platform. Guards plain-key commands.</summary>
		public static bool AnyModifierHeld() => AnyCtrlHeld() || ShiftHeld() || AltHeld();

		public static bool ShiftHeld() {
			return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift)
				|| UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift);
		}

		/// <summary>Control on every platform. For Ctrl combos macOS leaves alone.</summary>
		public static bool CtrlHeld() {
			return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl)
				|| UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl);
		}

		/// <summary>Ctrl, but Option on Mac. For Ctrl with an arrow key or Space.</summary>
		public static bool CtrlOptionHeld() {
			return IsMac ? OptionHeld() : CtrlHeld();
		}

		/// <summary>Ctrl, but Command on Mac. For find and text-field copy and paste.</summary>
		public static bool CtrlCmdHeld() {
			return IsMac ? CommandHeld() : CtrlHeld();
		}

		/// <summary>Any of the Ctrl family on this platform. For guards that keep a
		/// command from firing under a Ctrl combo.</summary>
		public static bool AnyCtrlHeld() => CtrlHeld() || CtrlOptionHeld();

		/// <summary>Alt, but Command on Mac.</summary>
		public static bool AltHeld() {
			return IsMac ? CommandHeld() : OptionHeld();
		}

		/// <summary>The physical Command key. Mac only; the game cannot see it.</summary>
		public static bool CommandHeld() {
			return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftCommand)
				|| UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightCommand);
		}

		// The physical Alt key, labelled Option on Mac.
		private static bool OptionHeld() {
			return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt)
				|| UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightAlt);
		}

		/// <summary>
		/// Returns 0-9 if an Alpha or Keypad digit key was pressed this frame, -1 otherwise.
		/// </summary>
		public static int GetDigitKeyDown() {
			for (int i = 0; i <= 9; i++) {
				if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha0 + i)
					|| UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Keypad0 + i))
					return i;
			}
			return -1;
		}

		private static readonly float[] WholeSteps = { 1f, 10f, 100f, 1000f };
		private static readonly float[] FractionalSteps = { 0.01f, 0.1f, 0.25f, 0.5f };

		/// <summary>
		/// Whole-number slider step for the given level (1, 10, 100, 1000).
		/// </summary>
		public static float StepForLevel(int level) {
			return WholeSteps[UnityEngine.Mathf.Clamp(level, 0, WholeSteps.Length - 1)];
		}

		/// <summary>
		/// Fractional slider step as a proportion of the range (1%, 10%, 25%, 50%).
		/// </summary>
		public static float FractionForLevel(int level) {
			return FractionalSteps[UnityEngine.Mathf.Clamp(level, 0, FractionalSteps.Length - 1)];
		}

		/// <summary>
		/// Returns a step level for Left/Right based on modifier keys held:
		/// 0 = plain, 1 = Shift, 2 = Ctrl, 3 = Ctrl+Shift (Option on Mac).
		/// </summary>
		public static int GetStepLevel() {
			bool ctrl = CtrlOptionHeld();
			bool shift = ShiftHeld();
			if (ctrl && shift) return 3;
			if (ctrl) return 2;
			if (shift) return 1;
			return 0;
		}
	}
}
