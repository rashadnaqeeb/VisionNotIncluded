#if DEBUG
using System.Collections.Generic;
using System.Text;

namespace OniAccess.Dev {
	/// <summary>
	/// Thread-safe ring buffer of the lines the mod has spoken, so the dev driver
	/// (which cannot hear the screen reader) can read back what was said. Each
	/// line has a stable monotonic index; callers poll with a cursor and get only
	/// what is new. Lives in the host so the buffer and cursor survive a module
	/// reload. DEBUG-only.
	/// </summary>
	internal sealed class SpeechLog {
		public const int Capacity = 500;

		private readonly object _lock = new object();
		private readonly List<string> _lines = new List<string>();
		private long _base; // global index of _lines[0]

		public void Add(string text) {
			if (string.IsNullOrEmpty(text)) return;
			lock (_lock) {
				_lines.Add(text);
				if (_lines.Count > Capacity) {
					_lines.RemoveAt(0);
					_base++;
				}
			}
		}

		/// <summary>Render lines with global index >= <paramref name="since"/>, one
		/// per line as "index: text". <paramref name="next"/> is the cursor to pass
		/// next time for newer lines only.</summary>
		public string Render(long since, out long next) {
			lock (_lock) {
				long end = _base + _lines.Count; // exclusive
				if (since < _base) since = _base;
				var sb = new StringBuilder();
				for (long i = since; i < end; i++)
					sb.Append(i).Append(": ").Append(_lines[(int)(i - _base)]).Append('\n');
				next = end;
				return sb.ToString();
			}
		}
	}
}
#endif
