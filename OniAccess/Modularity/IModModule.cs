using System;

namespace OniAccess.Modularity {
	/// <summary>
	/// The reloadable feature module. The host (OniAccess.dll, the DLL the game's
	/// mod loader locks) knows the module only through this interface; the module
	/// references the host assembly directly for its services (Mod statics, Log,
	/// the dev server). One implementor per module assembly. The loader
	/// instantiates it and calls <see cref="Load"/> once on the Unity main thread.
	///
	/// <see cref="IDisposable.Dispose"/> must undo every PERSISTENT game-side
	/// effect Load created: Harmony patches (per-generation id), game event
	/// subscriptions, spawned GameObjects and components, the input handler
	/// registered in the game's input tree, speech backends, translation
	/// registrations, dev-server routes. Statics inside the old generation
	/// become garbage; only hooks INTO the game or the host outlive it. The
	/// loader disposes the old generation BEFORE loading the new one, because
	/// the mod's systems are process-global and two live generations would
	/// double-patch and double-speak.
	/// </summary>
	public interface IModModule: IDisposable {
		void Load();
	}
}
