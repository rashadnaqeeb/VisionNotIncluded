using System;
using System.Runtime.InteropServices;
using System.Text;
using OniAccess.Util;

namespace OniAccess.Speech {
	public class PrismBackend: ISpeechBackend {
		[StructLayout(LayoutKind.Sequential)]
		private struct PrismConfig {
			public byte version;
		}

		const int PRISM_OK = 0;
		const int PRISM_ERROR_NOT_SPEAKING = 10;

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern PrismConfig prism_config_init();

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_init(ref PrismConfig cfg);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern void prism_shutdown(IntPtr ctx);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_registry_acquire_best(IntPtr ctx);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_backend_name(IntPtr backend);

		// Prism requires UTF-8 and rejects anything else with PRISM_ERROR_INVALID_UTF8.
		// .NET has no UTF-8 CharSet on Framework, so the text is converted to a
		// null-terminated UTF-8 byte[] by the caller and marshaled as a raw pointer.
		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern int prism_backend_speak(IntPtr backend, byte[] text, bool interrupt);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern int prism_backend_stop(IntPtr backend);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern void prism_backend_free(IntPtr backend);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_error_string(int error);

		private IntPtr _context = IntPtr.Zero;
		private IntPtr _backend = IntPtr.Zero;
		private bool _initialized = false;
		private bool _available = false;

		public bool IsInitialized => _initialized;
		public bool IsAvailable => _available;

		public bool Initialize() {
			if (_initialized) return _available;

			try {
				var config = prism_config_init();
				_context = prism_init(ref config);
				if (_context == IntPtr.Zero) {
					Log.Error("prism_init returned null");
					_initialized = true;
					_available = false;
					return false;
				}

				_backend = prism_registry_acquire_best(_context);
				_available = _backend != IntPtr.Zero;
				_initialized = true;

				if (_available) {
					IntPtr namePtr = prism_backend_name(_backend);
					string name = namePtr != IntPtr.Zero
						? Marshal.PtrToStringAnsi(namePtr)
						: "unknown";
					Log.Info($"Prism backend initialized with: {name}");
				} else {
					Log.Warn("No Prism speech backend available");
				}

				return _available;
			} catch (DllNotFoundException ex) {
				Log.Error($"Prism native library not found: {ex}");
				_initialized = true;
				_available = false;
				return false;
			} catch (Exception ex) {
				Log.Error($"Prism init failed: {ex}");
				_initialized = true;
				_available = false;
				return false;
			}
		}

		public void Shutdown() {
			if (!_initialized) return;

			try {
				if (_backend != IntPtr.Zero) {
					prism_backend_free(_backend);
					_backend = IntPtr.Zero;
				}
				if (_context != IntPtr.Zero) {
					prism_shutdown(_context);
					_context = IntPtr.Zero;
				}
				Log.Info("Prism backend shutdown");
			} catch (Exception ex) {
				Log.Warn($"Prism shutdown error: {ex}");
			} finally {
				_initialized = false;
				_available = false;
			}
		}

		/// <summary>
		/// Encodes text as the null-terminated UTF-8 bytes Prism expects. Prism reads
		/// to the first null and validates the bytes as UTF-8, so the terminator is
		/// mandatory and the encoding must be UTF-8 (not the system ANSI code page).
		/// </summary>
		internal static byte[] ToUtf8(string text) {
			int count = Encoding.UTF8.GetByteCount(text);
			var bytes = new byte[count + 1]; // trailing slot stays 0 = null terminator
			Encoding.UTF8.GetBytes(text, 0, text.Length, bytes, 0);
			return bytes;
		}

		public void Say(string text, bool interrupt) {
			if (!_available || string.IsNullOrEmpty(text)) return;

			try {
				int err = prism_backend_speak(_backend, ToUtf8(text), interrupt);
				if (err != PRISM_OK) {
					IntPtr msgPtr = prism_error_string(err);
					string msg = msgPtr != IntPtr.Zero
						? Marshal.PtrToStringAnsi(msgPtr)
						: $"error code {err}";
					Log.Warn($"Prism speech error: {msg}");
				}
			} catch (Exception ex) {
				Log.Warn($"Prism speech error: {ex}");
			}
		}

		public void Stop() {
			if (!_available) return;

			try {
				int err = prism_backend_stop(_backend);
				if (err != PRISM_OK && err != PRISM_ERROR_NOT_SPEAKING) {
					IntPtr msgPtr = prism_error_string(err);
					string msg = msgPtr != IntPtr.Zero
						? Marshal.PtrToStringAnsi(msgPtr)
						: $"error code {err}";
					Log.Warn($"Prism stop error: {msg}");
				}
			} catch (Exception ex) {
				Log.Warn($"Prism stop error: {ex}");
			}
		}
	}
}
