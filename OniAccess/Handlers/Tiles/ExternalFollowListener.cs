using System;
using UnityEngine;
using OniAccess.Speech;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Detects when the base game's Follow Cam is activated externally
	/// (e.g. clicking a critter and using Follow Cam) and attaches
	/// status/chore event listeners so announcements are spoken.
	/// </summary>
	public class ExternalFollowListener {
		private GameObject _followedObject;
		private bool _isDupe;
		private Action<StatusItemGroup.Entry, StatusItemCategory> _onStatusAdded;
		private Action<StatusItemGroup.Entry, bool> _onStatusRemoved;
		private readonly Util.GameEventSubscription _choreChangedSub = new Util.GameEventSubscription();

		public void TickFollow(bool dupeFollowing, bool botFollowing) {
			try {
				var followTarget = CameraController.Instance.followTarget;
				if (_followedObject != null) {
					if (followTarget == null
						|| dupeFollowing || botFollowing
						|| followTarget.gameObject != _followedObject) {
						StopFollow();
					}
					return;
				}
				if (followTarget != null && !dupeFollowing && !botFollowing)
					AttachFollow(followTarget.gameObject);
			} catch (Exception ex) {
				Log.Warn($"ExternalFollowListener.TickFollow: {ex}");
			}
		}

		private void AttachFollow(GameObject target) {
			_followedObject = target;
			_isDupe = target.GetComponent<MinionIdentity>() != null;
			_onStatusAdded = OnStatusAdded;
			_onStatusRemoved = OnStatusRemoved;
			var group = target.GetComponent<KSelectable>().GetStatusItemGroup();
			group.OnAddStatusItem = (Action<StatusItemGroup.Entry, StatusItemCategory>)
				Delegate.Combine(group.OnAddStatusItem, _onStatusAdded);
			group.OnRemoveStatusItem = (Action<StatusItemGroup.Entry, bool>)
				Delegate.Combine(group.OnRemoveStatusItem, _onStatusRemoved);
			_choreChangedSub.Subscribe(target, -1988963660, OnChoreChanged);
		}

		public void StopFollow() {
			if (_followedObject == null) return;
			try {
				var selectable = _followedObject.GetComponent<KSelectable>();
				if (selectable != null) {
					var group = selectable.GetStatusItemGroup();
					if (group != null) {
						group.OnAddStatusItem = (Action<StatusItemGroup.Entry, StatusItemCategory>)
							Delegate.Remove(group.OnAddStatusItem, _onStatusAdded);
						group.OnRemoveStatusItem = (Action<StatusItemGroup.Entry, bool>)
							Delegate.Remove(group.OnRemoveStatusItem, _onStatusRemoved);
					}
				}
				_choreChangedSub.Unsubscribe();
			} catch (Exception ex) {
				Log.Warn($"ExternalFollowListener.StopFollow: {ex}");
			}
			_followedObject = null;
			_onStatusAdded = null;
			_onStatusRemoved = null;
		}

		private void OnStatusAdded(StatusItemGroup.Entry entry, StatusItemCategory category) {
			try {
				if (_isDupe && category == Db.Get().StatusItemCategories.Main)
					return;
				SpeechPipeline.SpeakQueued(entry.GetName());
			} catch (Exception ex) {
				Log.Warn($"ExternalFollowListener.OnStatusAdded: {ex}");
			}
		}

		private void OnStatusRemoved(StatusItemGroup.Entry entry, bool immediate) {
			try {
				if (_isDupe && entry.category == Db.Get().StatusItemCategories.Main)
					return;
				SpeechPipeline.SpeakQueued(string.Format(
					(string)STRINGS.ONIACCESS.DUPES.FOLLOW.STATUS_ENDED,
					entry.GetName()));
			} catch (Exception ex) {
				Log.Warn($"ExternalFollowListener.OnStatusRemoved: {ex}");
			}
		}

		private void OnChoreChanged(object data) {
			try {
				if (_followedObject == null) return;
				var choreDriver = _followedObject.GetComponent<ChoreDriver>();
				if (choreDriver == null) return;
				var chore = choreDriver.GetCurrentChore();
				if (chore == null) {
					SpeechPipeline.SpeakQueued((string)STRINGS.ONIACCESS.DUPES.IDLE);
					return;
				}
				SpeechPipeline.SpeakQueued(chore.choreType.Name);
			} catch (Exception ex) {
				Log.Warn($"ExternalFollowListener.OnChoreChanged: {ex}");
			}
		}
	}
}
