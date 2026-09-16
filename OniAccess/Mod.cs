using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;
using OniAccess.Modularity;
using OniAccess.Util;
using UnityEngine;

[assembly: InternalsVisibleTo("OniAccess.Tests")]

namespace OniAccess {
	/// <summary>
	/// The permanent HOST: the DLL ONI's mod loader Assembly.LoadFrom's from the
	/// mod root and keeps locked for the life of the process. It owns only what
	/// can never hot-reload: this entry point, the native speech library preload,
	/// the dev server socket and its per-frame pump, and the module loader. Every
	/// feature lives in the byte-loaded module (Module/OniAccess.Module.dll,
	/// deliberately in a subfolder the loader never scans), which a rebuilt copy
	/// hot-swaps via POST /reload. Changing host code still needs a game restart,
	/// so keep this assembly minimal and its public surface stable.
	/// </summary>
	public sealed class Mod: KMod.UserMod2 {
		public static Mod Instance { get; private set; }
		public static string ModDir { get; private set; }
		public static string DataDir { get; private set; }
		public static string Version { get; private set; }

		/// <summary>True when the Windows Tolk override is in place; the module
		/// picks its speech backend from this.</summary>
		public static bool UseTolk { get; private set; }

		/// <summary>Subfolder of the mod holding the module. ONI's loader only
		/// loads DLLs at the mod root. (The host's own dependency for the dev
		/// server, Mono.CSharp.dll, must sit AT the root: the loader calls
		/// GetTypes on the host before any mod code runs, which resolves every
		/// field type, so nothing we install can redirect that lookup.)</summary>
		public const string ModuleDir = "Module";
		public const string ModuleFile = "OniAccess.Module.dll";

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("libdl", EntryPoint = "dlopen")]
		private static extern IntPtr DlOpen(string path, int flags);

		const int RTLD_NOW = 2;

		public override void OnLoad(Harmony harmony) {
			Instance = this;
			ModDir = Path.GetDirectoryName(typeof(Mod).Assembly.Location);
			DataDir = Path.Combine(global::Util.RootFolder(), "mods", "OniAccess");
			Version = typeof(Mod).Assembly.GetName().Version.ToString();

			// Switch logging from Console (test default) to Unity's Debug.Log
			LogUnityBackend.Install();

			PreloadNativeSpeech();

			// The host carries no patches; the module applies its own with a
			// per-generation Harmony id so a reload can strip them.
			base.OnLoad(harmony);

#if DEBUG
			var ticker = new GameObject("OniAccess_Host");
			UnityEngine.Object.DontDestroyOnLoad(ticker);
			ticker.AddComponent<Ticker>();
			Dev.DevServer.Instance.Start();
#endif

			ModuleLoader.LoadInitial(Path.Combine(ModDir, ModuleDir, ModuleFile));
			Log.Info($"Oni-Access host {Version} loaded");
		}

		// Native libraries cannot be unloaded, so they are the host's job: a dlopen
		// survives every module generation.
		private static void PreloadNativeSpeech() {
			// Check for Tolk override on Windows before loading Prism
			string tolkOverridePath = Path.Combine(DataDir, "tolk_override.dll");
			UseTolk = Application.platform == RuntimePlatform.WindowsPlayer
				&& File.Exists(tolkOverridePath);

			if (UseTolk) {
				Log.Info("Using Tolk override backend");
				string tempDir = Path.Combine(Path.GetTempPath(), "OniAccess");
				Directory.CreateDirectory(tempDir);

				// Copy all DLLs and INI files from DataDir to temp dir
				foreach (string file in Directory.GetFiles(DataDir, "*.dll")) {
					string fileName = Path.GetFileName(file);
					string destName = fileName.Equals("tolk_override.dll", StringComparison.OrdinalIgnoreCase)
						? "Tolk.dll"
						: fileName;
					File.Copy(file, Path.Combine(tempDir, destName), true);
				}
				foreach (string file in Directory.GetFiles(DataDir, "*.ini")) {
					File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)), true);
				}

				// Pre-load companion DLLs, then Tolk itself
				foreach (string dll in Directory.GetFiles(tempDir, "*.dll")) {
					if (Path.GetFileName(dll).Equals("Tolk.dll", StringComparison.OrdinalIgnoreCase))
						continue;
					LoadLibrary(dll);
				}
				if (LoadLibrary(Path.Combine(tempDir, "Tolk.dll")) == IntPtr.Zero)
					Log.Warn("Failed to pre-load Tolk.dll from temp dir");
				return;
			}

			// Pre-load the platform-specific Prism native library so that
			// PrismBackend's DllImport("prism") resolves correctly.
			string platform;
			string libName;
			switch (Application.platform) {
				case RuntimePlatform.WindowsPlayer:
					platform = "win-x64";
					libName = "prism.dll";
					break;
				case RuntimePlatform.LinuxPlayer:
					platform = "linux-x64";
					libName = "libprism.so";
					break;
				case RuntimePlatform.OSXPlayer:
					platform = "osx";
					libName = "libprism.dylib";
					break;
				default:
					Log.Error($"Unsupported platform: {Application.platform}");
					return;
			}
			string libPath = Path.Combine(ModDir, "native", platform, libName);
			if (Application.platform == RuntimePlatform.WindowsPlayer) {
				if (LoadLibrary(libPath) == IntPtr.Zero)
					Log.Warn($"Failed to pre-load Prism from: {libPath}");
			} else {
				if (DlOpen(libPath, RTLD_NOW) == IntPtr.Zero)
					Log.Warn($"Failed to pre-load Prism from: {libPath}");
			}
		}

#if DEBUG
		/// <summary>Per-frame pump for the dev server's main-thread jobs. The very
		/// negative execution order runs it before every game and module script in
		/// the frame, so an injected key is visible to the whole frame that follows.</summary>
		[DefaultExecutionOrder(-10000)]
		private sealed class Ticker: MonoBehaviour {
			private void Update() => Dev.DevServer.Instance.Pump();
		}
#endif
	}
}
