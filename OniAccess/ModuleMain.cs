using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using OniAccess.Audio;
using OniAccess.Handlers;
using OniAccess.Handlers.Tiles.Sections;
using OniAccess.Input;
using OniAccess.Modularity;
using OniAccess.Speech;
using OniAccess.Util;
using OniAccess.Widgets;
using UnityEngine;

namespace OniAccess {
	/// <summary>
	/// The reloadable feature MODULE: everything the mod does, minus what can
	/// never reload (the host in Mod.cs: entry point, native preload, dev-server
	/// socket, module loader). The host byte-loads this assembly from
	/// Module/OniAccess.Module.dll and drives it through IModModule: Load once,
	/// Dispose to undo every persistent game-side hook so a fresh generation can
	/// hot-swap in without a restart. Host services are plain statics on Mod and
	/// Log; the module references the host assembly directly.
	/// </summary>
	public sealed class ModuleMain: IModModule {
		private Harmony _harmony;
		private GameObject _inputGo;
		private GameObject _audioGo;

		public void Load() {
			ConfigManager.Load(Mod.DataDir);

			// Per-generation Harmony id: Dispose unpatches by id, and a fixed id
			// would strip a newer generation's patches if an old Dispose ran late.
			_harmony = new Harmony("OniAccess.gen" + ModuleLoader.Generation);
			_harmony.PatchAll(typeof(ModuleMain).Assembly);

			if (Mod.UseTolk)
				SpeechEngine.Initialize(new TolkBackend());
			else
				SpeechOutputSelector.Start();
			TextFilter.InitializeDefaults();
			StatusFilter.Initialize();

			// Persistent KeyPoller MonoBehaviour for unbound key detection
			// (Shift+/, arrows -- keys ONI doesn't generate KButtonEvents for)
			_inputGo = new GameObject("OniAccess_Input");
			UnityEngine.Object.DontDestroyOnLoad(_inputGo);
			_inputGo.AddComponent<KeyPoller>();
			_inputGo.AddComponent<SpeechTicker>();

			_audioGo = new GameObject("OniAccess_Audio");
			UnityEngine.Object.DontDestroyOnLoad(_audioGo);
			_audioGo.AddComponent<EarconScheduler>();
			_audioGo.AddComponent<Sonifier>();
			_audioGo.AddComponent<ShapeEarconPlayer>();
			_audioGo.AddComponent<FollowMovementEarcon>();
			_audioGo.AddComponent<ScannerDirectionEarcon>();
			new SonifierController();
			new FootstepPlayer();

			// Register screen-to-handler mappings for ContextDetector
			ContextDetector.RegisterMenuHandlers();
			SideScreenOverrides.RegisterAll();

			ModUtil.RegisterForTranslation(typeof(STRINGS.ONIACCESS));

			if (ModuleLoader.Generation == 1) {
				// Boot: the InputInit.Awake patch registers ModInputRouter and
				// KeyPoller's first frame finds the main menu. Push BaselineHandler
				// so the stack is never empty.
				HandlerStack.Push(new BaselineHandler());
			} else {
				Reattach();
			}

#if DEBUG
			Dev.DevModuleRoutes.Register(_harmony);
#endif

			Log.Info($"Oni-Access module gen {ModuleLoader.Generation} loaded ({typeof(ModuleMain).Assembly.GetName().Name})");
		}

		// A reload lands mid-session: the one-shot game events the first generation
		// hooked (InputInit.Awake, Localization.Initialize) will not fire again, so
		// their work is redone here, then the handler stack is rebuilt for whatever
		// is on screen.
		private static void Reattach() {
			ModInputRouter.Register();
			TranslationLoader.LoadModTranslations();
			LocString.CreateLocStringKeys(typeof(STRINGS.ONIACCESS), "STRINGS.");
			ContextDetector.DetectAndActivate();
			SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.SPEECH.MOD_LOADED, Mod.Version));
		}

		/// <summary>Undo every persistent game-side hook Load created (see the
		/// IModModule contract). Runs BEFORE the next generation loads. Patches go
		/// first so nothing of this generation runs while it is being torn down.</summary>
		public void Dispose() {
#if DEBUG
			Step("dev routes", Dev.DevModuleRoutes.Unregister);
#endif
			Step("harmony", () => _harmony.UnpatchAll(_harmony.Id));
			Step("handlers", HandlerStack.DeactivateAll); // OnDeactivate drops the handlers' game event subscriptions
			Step("input router", ModInputRouter.Unregister);
			Step("input object", () => UnityEngine.Object.Destroy(_inputGo));
			Step("audio object", () => UnityEngine.Object.Destroy(_audioGo));
			Step("footsteps", FootstepPlayer.Destroy);
			Step("speech", SpeechEngine.Shutdown);
			Step("translation registry", UnregisterTranslation);
		}

		private static void Step(string what, System.Action action) {
			try {
				action();
			} catch (Exception e) {
				Log.Error($"[dispose] {what}: {e}");
			}
		}

		// Localization keeps every registered assembly in a private list; leave
		// the dead generation out of it so language reloads only touch live types.
		private static void UnregisterTranslation() {
			var table = Traverse.Create(typeof(Localization)).Field("translatable_assemblies")
				.GetValue<Dictionary<string, List<Assembly>>>();
			List<Assembly> list;
			if (table != null && table.TryGetValue(typeof(STRINGS.ONIACCESS).Namespace, out list))
				list.Remove(typeof(ModuleMain).Assembly);
			else
				Log.Warn("[dispose] Localization.translatable_assemblies not found; the old module stays registered for translation");
		}
	}
}
