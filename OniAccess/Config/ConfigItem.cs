using System;
using UnityEngine;

namespace OniAccess.Config {
	public abstract class ConfigItem {
		public string Label { get; }

		protected ConfigItem(string label) {
			Label = label;
		}

		public abstract string GetDisplayValue();
		public abstract void Cycle(int direction);
	}

	public class BoolConfigItem: ConfigItem {
		private readonly Func<bool> _getter;
		private readonly Action<bool> _setter;

		public BoolConfigItem(string label, Func<bool> getter, Action<bool> setter)
			: base(label) {
			_getter = getter;
			_setter = setter;
		}

		public override string GetDisplayValue() {
			return _getter()
				? (string)STRINGS.ONIACCESS.STATES.ON
				: (string)STRINGS.ONIACCESS.STATES.OFF;
		}

		public override void Cycle(int direction) {
			_setter(!_getter());
			ConfigManager.Save();
		}
	}

	public class FloatConfigItem: ConfigItem {
		private readonly Func<float> _getter;
		private readonly Action<float> _setter;
		private readonly float _min;
		private readonly float _max;

		public FloatConfigItem(string label, Func<float> getter, Action<float> setter,
				float min, float max)
			: base(label) {
			_getter = getter;
			_setter = setter;
			_min = min;
			_max = max;
		}

		public override string GetDisplayValue() {
			return _getter().ToString("F2");
		}

		public override void Cycle(int direction) {
			Adjust(direction, 0.01f);
		}

		public void Adjust(int direction, float step) {
			float value = _getter() + direction * step;
			value = Mathf.Clamp(value, _min, _max);
			value = Mathf.Round(value * 100f) / 100f;
			_setter(value);
			ConfigManager.Save();
		}
	}

	/// <summary>
	/// A config row that runs an action on Enter instead of cycling a value
	/// (e.g. opening a sub-menu). The optional value provider supplies the
	/// spoken suffix; when null the row speaks only its label.
	/// </summary>
	public class ActionConfigItem: ConfigItem {
		private readonly System.Action _action;
		private readonly Func<string> _valueProvider;

		public ActionConfigItem(string label, System.Action action, Func<string> valueProvider = null)
			: base(label) {
			_action = action;
			_valueProvider = valueProvider;
		}

		public override string GetDisplayValue() {
			return _valueProvider != null ? _valueProvider() : "";
		}

		public override void Cycle(int direction) {
			_action();
		}
	}

	public class EnumConfigItem<T>: ConfigItem where T : struct {
		private readonly Func<T> _getter;
		private readonly Action<T> _setter;
		private readonly T[] _values;
		private readonly Func<T, string> _valueLabeler;

		public EnumConfigItem(string label, Func<T> getter, Action<T> setter,
				T[] values, Func<T, string> valueLabeler)
			: base(label) {
			_getter = getter;
			_setter = setter;
			_values = values;
			_valueLabeler = valueLabeler;
		}

		public override string GetDisplayValue() {
			return _valueLabeler(_getter());
		}

		public override void Cycle(int direction) {
			T current = _getter();
			int index = Array.IndexOf(_values, current);
			if (index < 0) index = 0;
			index = (index + direction + _values.Length) % _values.Length;
			_setter(_values[index]);
			ConfigManager.Save();
		}
	}
}
