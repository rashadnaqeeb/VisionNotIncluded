using System;

namespace OniAccess.Util {
	/// <summary>
	/// One game event subscription, held by the handle the game returned.
	/// A handle is only meaningful on the object that issued it, so the object
	/// is kept with it: a handle replayed on another object (a new Game after
	/// a save load) could remove someone else's subscription.
	/// </summary>
	internal sealed class GameEventSubscription {
		private KMonoBehaviour _target;
		private int _id = -1;

		/// <summary>Replaces any subscription this instance already holds.</summary>
		internal void Subscribe(KMonoBehaviour target, int hash, Action<object> handler) {
			Unsubscribe();
			_target = target;
			_id = target.Subscribe(hash, handler);
		}

		internal void Subscribe(UnityEngine.GameObject target, int hash, Action<object> handler)
			=> Subscribe(target.GetComponent<KMonoBehaviour>(), hash, handler);

		/// <summary>
		/// Safe to call when nothing is held, and after the target was destroyed
		/// (its event system went with it).
		/// </summary>
		internal void Unsubscribe() {
			if (_target != null)
				_target.Unsubscribe(ref _id);
			_target = null;
			_id = -1;
		}
	}
}
