namespace OniAccess.Widgets {
	/// <summary>
	/// A slider widget (KSlider). Speaks formatted value, validates
	/// interactability, adjusts value with step math and boundary clamping.
	/// </summary>
	public class SliderWidget: Widget {
		public override string RoleKey => "slider";

		public override string GetSpeechText() {
			if (SpeechFunc != null) {
				string result = SpeechFunc()?.Trim();
				if (!string.IsNullOrEmpty(result)) return result;
			}
			var slider = Component as KSlider;
			if (slider != null)
				return $"{Label}, {WidgetOps.FormatSliderValue(slider)}";
			return Label;
		}

		public override bool IsValid() {
			if (GameObject != null && !GameObject.activeInHierarchy) return false;
			var slider = Component as KSlider;
			if (slider != null) return slider.interactable;
			return Component != null || GameObject != null;
		}

		public override bool IsAdjustable => true;

		/// <summary>
		/// Adjust the slider value by a step determined by the step level.
		/// Returns true if the value changed.
		/// </summary>
		public override bool Adjust(int direction, int stepLevel) {
			var slider = Component as KSlider;
			if (slider == null) return false;

			float step;
			if (slider.wholeNumbers) {
				step = Input.InputUtil.StepForLevel(stepLevel);
			} else {
				float range = slider.maxValue - slider.minValue;
				step = range * Input.InputUtil.FractionForLevel(stepLevel);
			}

			float oldValue = slider.value;
			slider.value = UnityEngine.Mathf.Clamp(
				slider.value + step * direction,
				slider.minValue, slider.maxValue);

			return slider.value != oldValue;
		}

		/// <summary>
		/// Returns a sound name for the slider's boundary state after adjustment.
		/// null if no boundary sound should play.
		/// </summary>
		public string GetBoundarySound(int direction) {
			var slider = Component as KSlider;
			if (slider == null) return null;

			if (slider.value <= slider.minValue && direction < 0)
				return "Slider_Boundary_Low";
			if (slider.value >= slider.maxValue && direction > 0)
				return "Slider_Boundary_High";
			return "Slider_Move";
		}
	}
}
