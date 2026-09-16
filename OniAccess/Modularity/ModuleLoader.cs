using System;
using System.IO;
using System.Reflection;
using OniAccess.Util;

namespace OniAccess.Modularity {
	/// <summary>
	/// Loads and hot-swaps the feature module (Module/OniAccess.Module.dll under
	/// the mod folder). ONI's DLLLoader Assembly.LoadFrom's every DLL at the mod
	/// root and locks them, but never looks in subfolders, so the module lives in
	/// Module/ and is loaded FROM BYTES: the file is never locked, rebuilding and
	/// copying it while the game runs is safe, and <see cref="Reload"/> swaps
	/// generations without a restart.
	///
	/// Mono binds non-strong-named assemblies by SIMPLE NAME, so byte-loading an
	/// unchanged name returns the OLD image and the reload silently no-ops. The
	/// module project therefore stamps a fresh AssemblyName (OniAccess.Module_
	/// yyyyMMddHHmmss) into every build; <see cref="Describe"/> exposes the loaded
	/// identity so a swap can be verified. Old generations leak (net48 has no
	/// collectible load contexts), which is fine for a dev loop.
	/// </summary>
	public static class ModuleLoader {
		public static Assembly CurrentAssembly { get; private set; }
		public static int Generation { get; private set; }

		private static IModModule _module;
		private static string _path;

		/// <summary>Load the first generation. A missing DLL is logged as an error
		/// (the mod is not working, and the player must find out).</summary>
		public static void LoadInitial(string modulePath) {
			_path = modulePath;
			LoadGeneration();
		}

		/// <summary>Dispose the current generation and load a fresh one from the same path.</summary>
		public static string Reload() {
			DisposeCurrent();
			return LoadGeneration() ? Describe() : "[reload failed] see Player.log\n";
		}

		public static string Describe() {
			if (_module == null) return "module: none loaded\n";
			return "module: gen " + Generation
				+ "\nassembly: " + CurrentAssembly.GetName().Name
				+ "\nfile: " + _path + " (written " + File.GetLastWriteTime(_path).ToString("yyyy-MM-dd HH:mm:ss") + ")\n";
		}

		private static void DisposeCurrent() {
			if (_module == null) return;
			try {
				_module.Dispose();
			} catch (Exception e) {
				Log.Error($"[module dispose] gen {Generation}: {e}");
			}
			_module = null;
		}

		private static bool LoadGeneration() {
			try {
				if (!File.Exists(_path)) {
					Log.Error($"[module] not found: {_path}");
					return false;
				}
				var asm = Assembly.Load(File.ReadAllBytes(_path));
				Type impl = null;
				foreach (var t in asm.GetTypes()) {
					if (typeof(IModModule).IsAssignableFrom(t) && !t.IsAbstract) {
						impl = t;
						break;
					}
				}
				if (impl == null) {
					Log.Error($"[module] no IModModule implementor in {asm.GetName().Name}");
					return false;
				}
				var module = (IModModule)Activator.CreateInstance(impl);
				Generation++;
				CurrentAssembly = asm;
				_module = module;
				module.Load();
				Log.Info($"[module] gen {Generation} loaded: {asm.GetName().Name}");
				return true;
			} catch (Exception e) {
				Log.Error($"[module load] {e}");
				return false;
			}
		}
	}
}
