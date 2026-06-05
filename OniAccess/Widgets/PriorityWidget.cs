using UnityEngine;

namespace OniAccess.Widgets {
	/// <summary>
	/// Represents the master priority setting on a Prioritizable entity.
	/// Left/Right adjusts within basic 1-9 and topPriority (emergency).
	/// Speech reads the live priority value each time.
	/// </summary>
	public class PriorityWidget: Widget {
		public override string RoleKey => NavRoles.Priority;

		private static readonly PrioritySetting[] Steps = BuildSteps();

		private static PrioritySetting[] BuildSteps() {
			var steps = new PrioritySetting[10];
			for (int i = 0; i < 9; i++)
				steps[i] = new PrioritySetting(
					PriorityScreen.PriorityClass.basic, i + 1);
			steps[9] = new PrioritySetting(
				PriorityScreen.PriorityClass.topPriority, 1);
			return steps;
		}

		public Prioritizable Prioritizable { get; set; }

		public override void UpdateFrom(Widget source) {
			base.UpdateFrom(source);
			Prioritizable = ((PriorityWidget)source).Prioritizable;
		}

		public override bool IsAdjustable => true;

		public override bool IsValid() {
			return Prioritizable != null && Prioritizable.IsPrioritizable();
		}

		public override string GetSpeechText() {
			if (Prioritizable == null) return Label;
			var p = Prioritizable.GetMasterPriority();
			string value = FormatPriority(p);
			return $"{Label}, {value}";
		}

		public override bool Adjust(int direction, int stepLevel) {
			if (Prioritizable == null) return false;
			var current = Prioritizable.GetMasterPriority();
			int idx = FindStepIndex(current);
			if (idx < 0) idx = 4;

			int newIdx = Mathf.Clamp(idx + direction, 0, Steps.Length - 1);
			if (newIdx == idx) return false;

			Prioritizable.SetMasterPriority(Steps[newIdx]);
			return true;
		}

		private static int FindStepIndex(PrioritySetting p) {
			for (int i = 0; i < Steps.Length; i++) {
				if (Steps[i].priority_class == p.priority_class
						&& Steps[i].priority_value == p.priority_value)
					return i;
			}
			Util.Log.Warn(
				$"PriorityWidget: unrecognized priority " +
				$"class={p.priority_class} value={p.priority_value}");
			return -1;
		}

		private static string FormatPriority(PrioritySetting p) {
			if (p.priority_class == PriorityScreen.PriorityClass.topPriority)
				return (string)STRINGS.ONIACCESS.TOOLS.PRIORITY_EMERGENCY;
			return string.Format(
				STRINGS.ONIACCESS.TOOLS.PRIORITY_BASIC, p.priority_value);
		}
	}
}
