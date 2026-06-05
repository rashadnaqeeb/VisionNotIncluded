using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Klei.AI;
using OniAccess.Handlers.Tiles.Scanner;
using OniAccess.Speech;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Owns dupe cycle navigation for [ / ] and \ keys.
	/// All dupe data is re-queried live on each call; _dupeIndex is the
	/// only mod-side state.
	/// </summary>
	public class DupeNavigator {
		private int _dupeIndex;
		private MinionIdentity _followedDupe;
		private Action<StatusItemGroup.Entry, StatusItemCategory> _onStatusAdded;
		private Action<StatusItemGroup.Entry, bool> _onStatusRemoved;
		private Action<object> _onChoreChanged;
		private Action<object> _onEquipped;
		private Action<object> _onUnequipped;

		/// <summary>
		/// Returns the dupe at the current cycle index, or null if no dupes
		/// exist on the active world.
		/// </summary>
		public MinionIdentity GetCurrentDupe() {
			var dupes = GetWorldDupes();
			if (dupes.Count == 0) return null;
			if (_dupeIndex >= dupes.Count)
				_dupeIndex = dupes.Count - 1;
			return dupes[_dupeIndex];
		}

		public void CycleDupe(int direction) {
			try {
				var dupes = GetWorldDupes();
				if (dupes.Count == 0) {
					BaseScreenHandler.PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.DUPES.NO_DUPLICANTS);
					return;
				}
				_dupeIndex = ((_dupeIndex + direction) % dupes.Count + dupes.Count) % dupes.Count;
				SpeechPipeline.SpeakInterrupt(BuildAnnouncement(dupes[_dupeIndex], TileCursor.Instance.Cell));
				if (_followedDupe != null)
					SwitchFollowTarget();
			} catch (System.Exception ex) {
				Log.Error($"DupeNavigator.CycleDupe: {ex}");
			}
		}

		public void JumpOrSelect() {
			try {
				var dupes = GetWorldDupes();
				if (dupes.Count == 0) {
					BaseScreenHandler.PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.DUPES.NO_DUPLICANTS);
					return;
				}
				if (_dupeIndex >= dupes.Count)
					_dupeIndex = dupes.Count - 1;
				var mi = dupes[_dupeIndex];
				int dupeCell = Grid.PosToCell(mi);
				if (TileCursor.Instance.Cell == dupeCell) {
					if (!(PlayerController.Instance.ActiveTool is SelectTool))
						SelectTool.Instance.Activate();
					var selectable = mi.GetComponent<KSelectable>();
					SelectTool.Instance.Select(null);
					SelectTool.Instance.Select(selectable);
				} else {
					string speech = TileCursor.Instance.JumpTo(dupeCell);
					if (speech != null)
						SpeechPipeline.SpeakInterrupt(speech);
				}
			} catch (System.Exception ex) {
				Log.Error($"DupeNavigator.JumpOrSelect: {ex}");
			}
		}

		public string StartFollow() {
			try {
				var mi = GetCurrentDupe();
				if (mi == null) {
					BaseScreenHandler.PlaySound("Negative");
					return null;
				}
				StopFollow();
				AttachFollow(mi);
				return string.Format(
					(string)STRINGS.ONIACCESS.DUPES.FOLLOW.FOLLOWING,
					mi.GetProperName());
			} catch (Exception ex) {
				Log.Error($"DupeNavigator.StartFollow: {ex}");
				return null;
			}
		}

		public bool IsFollowing => _followedDupe != null;

		public void TickFollow() {
			if (_followedDupe == null) return;
			if (CameraController.Instance.followTarget == null)
				StopFollow();
		}

		public void StopFollowAndClear() {
			if (_followedDupe == null) return;
			StopFollow();
			CameraController.Instance.ClearFollowTarget();
		}

		private void StopFollow() {
			if (_followedDupe == null) return;
			try {
				var selectable = _followedDupe.GetComponent<KSelectable>();
				if (selectable != null) {
					var group = selectable.GetStatusItemGroup();
					if (group != null) {
						group.OnAddStatusItem = (Action<StatusItemGroup.Entry, StatusItemCategory>)
							Delegate.Remove(group.OnAddStatusItem, _onStatusAdded);
						group.OnRemoveStatusItem = (Action<StatusItemGroup.Entry, bool>)
							Delegate.Remove(group.OnRemoveStatusItem, _onStatusRemoved);
					}
				}
				_followedDupe.gameObject.Unsubscribe(-1988963660, _onChoreChanged);
				_followedDupe.gameObject.Unsubscribe((int)GameHashes.EquippedItemEquipper, _onEquipped);
				_followedDupe.gameObject.Unsubscribe((int)GameHashes.UnequippedItemEquipper, _onUnequipped);
			} catch (Exception ex) {
				Log.Warn($"DupeNavigator.StopFollow: {ex}");
			}
			_followedDupe = null;
			_onStatusAdded = null;
			_onStatusRemoved = null;
			_onChoreChanged = null;
			_onEquipped = null;
			_onUnequipped = null;
		}

		private void SwitchFollowTarget() {
			try {
				StopFollow();
				var mi = GetCurrentDupe();
				if (mi == null) return;
				AttachFollow(mi);
			} catch (Exception ex) {
				Log.Error($"DupeNavigator.SwitchFollowTarget: {ex}");
			}
		}

		private void AttachFollow(MinionIdentity mi) {
			_followedDupe = mi;
			_onStatusAdded = OnStatusAdded;
			_onStatusRemoved = OnStatusRemoved;
			_onChoreChanged = OnChoreChanged;
			_onEquipped = OnEquipped;
			_onUnequipped = OnUnequipped;
			var group = mi.GetComponent<KSelectable>().GetStatusItemGroup();
			group.OnAddStatusItem = (Action<StatusItemGroup.Entry, StatusItemCategory>)
				Delegate.Combine(group.OnAddStatusItem, _onStatusAdded);
			group.OnRemoveStatusItem = (Action<StatusItemGroup.Entry, bool>)
				Delegate.Combine(group.OnRemoveStatusItem, _onStatusRemoved);
			mi.gameObject.Subscribe(-1988963660, _onChoreChanged);
			mi.gameObject.Subscribe((int)GameHashes.EquippedItemEquipper, _onEquipped);
			mi.gameObject.Subscribe((int)GameHashes.UnequippedItemEquipper, _onUnequipped);
			CameraController.Instance.SetFollowTarget(mi.transform);
		}

		private void OnStatusAdded(StatusItemGroup.Entry entry, StatusItemCategory category) {
			try {
				if (category == Db.Get().StatusItemCategories.Main)
					return;
				SpeechPipeline.SpeakQueued(entry.GetName());
			} catch (Exception ex) {
				Log.Warn($"DupeNavigator.OnStatusAdded: {ex}");
			}
		}

		private void OnStatusRemoved(StatusItemGroup.Entry entry, bool immediate) {
			try {
				if (entry.category == Db.Get().StatusItemCategories.Main)
					return;
				SpeechPipeline.SpeakQueued(string.Format(
					(string)STRINGS.ONIACCESS.DUPES.FOLLOW.STATUS_ENDED,
					entry.GetName()));
			} catch (Exception ex) {
				Log.Warn($"DupeNavigator.OnStatusRemoved: {ex}");
			}
		}

		private void OnEquipped(object data) {
			try {
				var prefabID = data as KPrefabID;
				if (!WornSuit.IsSuit(prefabID))
					return;
				SpeechPipeline.SpeakQueued(prefabID.GetComponent<Equippable>().def.Name);
			} catch (Exception ex) {
				Log.Warn($"DupeNavigator.OnEquipped: {ex}");
			}
		}

		private void OnUnequipped(object data) {
			try {
				var prefabID = data as KPrefabID;
				if (!WornSuit.IsSuit(prefabID))
					return;
				SpeechPipeline.SpeakQueued(string.Format(
					(string)STRINGS.ONIACCESS.DUPES.FOLLOW.STATUS_ENDED,
					prefabID.GetComponent<Equippable>().def.Name));
			} catch (Exception ex) {
				Log.Warn($"DupeNavigator.OnUnequipped: {ex}");
			}
		}

		private void OnChoreChanged(object data) {
			try {
				if (_followedDupe == null) return;
				string task = BuildTaskPart(_followedDupe);
				SpeechPipeline.SpeakQueued(task);
			} catch (Exception ex) {
				Log.Warn($"DupeNavigator.OnChoreChanged: {ex}");
			}
		}

		private static IList<MinionIdentity> GetWorldDupes() {
			int worldId = ClusterManager.Instance.activeWorldId;
			return Components.LiveMinionIdentities.GetWorldItems(worldId);
		}

		private string BuildAnnouncement(MinionIdentity mi, int cursorCell) {
			string name = mi.GetProperName();
			string task = BuildTaskPart(mi);
			string statuses = BuildStatusPart(mi);
			bool trapped = IsTrapped(mi);
			string position = AnnouncementFormatter.FormatDistance(cursorCell, Grid.PosToCell(mi));
			var parts = new System.Text.StringBuilder(name);
			if (trapped)
				parts.Append(", ").Append(
					(string)STRINGS.UI.COLONY_DIAGNOSTICS.TRAPPEDDUPLICANTDIAGNOSTIC.ALL_NAME);
			if (statuses != null)
				parts.Append(", ").Append(statuses);
			if (position.Length > 0)
				parts.Append(", ").Append(position);
			parts.Append(", ").Append(task);
			return parts.ToString();
		}

		private static bool IsTrapped(MinionIdentity mi) {
			try {
				if (!CheckMinionBasicallyIdle(mi))
					return false;
				int worldId = mi.GetComponent<Navigator>().GetMyWorldId();
				if (ClusterManager.Instance.GetWorld(worldId).IsModuleInterior)
					return false;
				var nav = mi.GetComponent<Navigator>();
				foreach (var other in Components.LiveMinionIdentities.GetWorldItems(worldId)) {
					if (other != mi && !CheckMinionBasicallyIdle(other)
						&& nav.CanReach(other.GetComponent<IApproachable>()))
						return false;
				}
				var telepads = Components.Telepads.GetWorldItems(worldId);
				if (telepads != null && telepads.Count > 0
					&& nav.CanReach(telepads[0].GetComponent<IApproachable>()))
					return false;
				var receivers = Components.WarpReceivers.GetWorldItems(worldId);
				if (receivers != null && receivers.Count > 0) {
					foreach (var receiver in receivers) {
						if (nav.CanReach(receiver.GetComponent<IApproachable>()))
							return false;
					}
				}
				foreach (var bed in Components.NormalBeds.WorldItemsEnumerate(worldId, true)) {
					var assignable = bed.assignable;
					if (assignable != null && assignable.IsAssignedTo(mi)
						&& nav.CanReach(bed.approachable))
						return false;
				}
				return true;
			} catch (System.Exception ex) {
				Log.Warn($"DupeNavigator.IsTrapped: {ex}");
				return false;
			}
		}

		private static bool CheckMinionBasicallyIdle(MinionIdentity minion) {
			var kpid = minion.GetComponent<KPrefabID>();
			return kpid.HasTag(GameTags.Idle)
				|| kpid.HasTag(GameTags.RecoveringBreath)
				|| kpid.HasTag(GameTags.MakingMess);
		}

		private static readonly FieldInfo ShowProgressBarField = AccessTools.Field(
			typeof(Workable), "showProgressBar");

		private static string BuildTaskPart(MinionIdentity mi) {
			try {
				var chore = mi.GetComponent<ChoreDriver>().GetCurrentChore();
				if (chore == null)
					return (string)STRINGS.ONIACCESS.DUPES.IDLE;
				string name = chore.choreType.Name;
				string target = GetChoreTarget(chore, mi);
				string task = target != null ? $"{name}, {target}" : name;
				var worker = mi.GetComponent<WorkerBase>();
				if (worker != null) {
					var workable = worker.GetWorkable();
					if (workable != null && ShowProgressBarField.GetValue(workable) is true) {
						float pct = workable.GetPercentComplete();
						if (pct >= 0f)
							task = $"{task}, {(int)(pct * 100)}%";
					}
				}
				return task;
			} catch (System.Exception ex) {
				Log.Warn($"DupeNavigator.BuildTaskPart: {ex}");
				return (string)STRINGS.ONIACCESS.DUPES.IDLE;
			}
		}

		private static string GetChoreTarget(Chore chore, MinionIdentity mi) {
			var fetchArea = chore as FetchAreaChore;
			if (fetchArea != null && fetchArea.smi.deliveries.Count > 0) {
				var dest = fetchArea.smi.deliveries[0].destination;
				if (dest != null)
					return dest.gameObject.GetProperName();
			}
			var fetchChore = chore as FetchChore;
			if (fetchChore != null && fetchChore.destination != null)
				return fetchChore.destination.gameObject.GetProperName();
			string target = chore.gameObject.GetProperName();
			if (target == mi.GetProperName())
				return null;
			return target;
		}

		// Each entry maps a check to a spoken label. Add/remove/reorder
		// entries here to change which statuses are announced.
		private StatusCheck[] _statusChecks;

		private StatusCheck[] GetStatusChecks() {
			if (_statusChecks != null)
				return _statusChecks;
			var dupeItems = Db.Get().DuplicantStatusItems;
			var creatureItems = Db.Get().CreatureStatusItems;
			_statusChecks = new StatusCheck[] {
				new StatusCheck(
					mi => mi.GetComponent<Health>().State == Health.HealthState.Incapacitated,
					(string)STRINGS.ONIACCESS.DUPES.INCAPACITATED),
				new StatusCheck(
					mi => mi.GetComponent<Health>().State == Health.HealthState.Critical,
					(string)STRINGS.ONIACCESS.DUPES.HEALTH_CRITICAL),
				new StatusCheck(
					mi => mi.GetComponent<Health>().State == Health.HealthState.Injured,
					(string)STRINGS.ONIACCESS.DUPES.HEALTH_INJURED),
				new StatusCheck(dupeItems.SevereWounds,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.SEVEREWOUNDS.NAME),
				new StatusCheck(dupeItems.Suffocating,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.SUFFOCATING.NAME),
				new StatusCheck(dupeItems.HoldingBreath,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.HOLDINGBREATH.NAME),
				new StatusCheck(dupeItems.NervousBreakdown,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.NERVOUSBREAKDOWN.NAME),
				new StatusCheck(dupeItems.Stressed,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.STRESSED.NAME),
				new StatusCheck(creatureItems.Scalding,
					(string)STRINGS.CREATURES.STATUSITEMS.SCALDING.NAME),
				new StatusCheck(dupeItems.ExitingHot,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.EXITINGHOT.NAME),
				new StatusCheck(dupeItems.ExitingCold,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.EXITINGCOLD.NAME),
				new StatusCheck(
					mi => mi.GetComponent<MinionModifiers>().sicknesses.IsInfected(),
					(string)STRINGS.ONIACCESS.DUPES.SICK),
				new StatusCheck(dupeItems.Starving,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.STARVING.NAME),
				new StatusCheck(dupeItems.EntombedChore,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.ENTOMBEDCHORE.NAME),
				new StatusCheck(dupeItems.Fleeing,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.FLEEING.NAME),
				new StatusCheck(dupeItems.BionicCriticalBattery,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.BIONICCRITICALBATTERY.NAME),
				new StatusCheck(dupeItems.BionicOfflineIncapacitated,
					(string)STRINGS.DUPLICANTS.STATUSITEMS.BIONICOFFLINEINCAPACITATED.NAME),
				new StatusCheck(WornSuit.GetName),
			};
			return _statusChecks;
		}

		private string BuildStatusPart(MinionIdentity mi) {
			var results = new List<string>();
			var selectable = mi.GetComponent<KSelectable>();
			try {
				foreach (var check in GetStatusChecks()) {
					string label = check.Resolve(mi, selectable);
					if (!string.IsNullOrEmpty(label))
						results.Add(label);
				}
			} catch (System.Exception ex) {
				Log.Warn($"DupeNavigator.BuildStatusPart: {ex}");
			}
			return results.Count > 0 ? string.Join(", ", results) : null;
		}

		private struct StatusCheck {
			private readonly StatusItem _statusItem;
			private readonly System.Func<MinionIdentity, bool> _predicate;
			private readonly System.Func<MinionIdentity, string> _resolver;
			private readonly string _label;

			public StatusCheck(StatusItem statusItem, string label) {
				_statusItem = statusItem;
				_predicate = null;
				_resolver = null;
				_label = label;
			}

			public StatusCheck(System.Func<MinionIdentity, bool> predicate, string label) {
				_statusItem = null;
				_predicate = predicate;
				_resolver = null;
				_label = label;
			}

			// Resolver returns the label itself (or null when it does not apply),
			// letting the spoken text vary per dupe (e.g. the specific suit name).
			public StatusCheck(System.Func<MinionIdentity, string> resolver) {
				_statusItem = null;
				_predicate = null;
				_resolver = resolver;
				_label = null;
			}

			/// <summary>
			/// Returns the label to speak if this status applies, or null if not.
			/// </summary>
			public string Resolve(MinionIdentity mi, KSelectable selectable) {
				if (_resolver != null)
					return _resolver(mi);
				bool active = _predicate != null
					? _predicate(mi)
					: selectable.HasStatusItem(_statusItem);
				return active ? _label : null;
			}
		}
	}
}
