using System;
using System.Collections.Generic;
using UnityEngine;
using OniAccess.Handlers.Tiles.Scanner;
using OniAccess.Speech;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Owns bot cycle navigation for Shift+[ / Shift+] keys.
	/// Parallel to DupeNavigator but targets all autonomous bot types
	/// via Components.LiveRobotsIdentities.
	/// All bot data is re-queried live on each call; _botIndex is the
	/// only mod-side state.
	/// </summary>
	public class BotNavigator {
		private int _botIndex;
		private GameObject _followedBot;
		private Action<StatusItemGroup.Entry, StatusItemCategory> _onStatusAdded;
		private Action<StatusItemGroup.Entry, bool> _onStatusRemoved;
		private readonly Util.GameEventSubscription _choreChangedSub = new Util.GameEventSubscription();

		/// <summary>
		/// Returns the GameObject of the bot at the current cycle index,
		/// or null if no bots exist on the active world.
		/// </summary>
		public GameObject GetCurrentBot() {
			var bots = GetWorldBots();
			if (bots.Count == 0) return null;
			if (_botIndex >= bots.Count)
				_botIndex = bots.Count - 1;
			return bots[_botIndex];
		}

		public void CycleBot(int direction) {
			try {
				var bots = GetWorldBots();
				if (bots.Count == 0) {
					BaseScreenHandler.PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BOTS.NO_BOTS);
					return;
				}
				_botIndex = ((_botIndex + direction) % bots.Count + bots.Count) % bots.Count;
				SpeechPipeline.SpeakInterrupt(
					BuildAnnouncement(bots[_botIndex], TileCursor.Instance.Cell, bots));
				if (_followedBot != null)
					SwitchFollowTarget();
			} catch (Exception ex) {
				Log.Error($"BotNavigator.CycleBot: {ex}");
			}
		}

		public void JumpOrSelect() {
			try {
				var bots = GetWorldBots();
				if (bots.Count == 0) {
					BaseScreenHandler.PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BOTS.NO_BOTS);
					return;
				}
				if (_botIndex >= bots.Count)
					_botIndex = bots.Count - 1;
				var bot = bots[_botIndex];
				int botCell = Grid.PosToCell(bot);
				if (TileCursor.Instance.Cell == botCell) {
					if (!(PlayerController.Instance.ActiveTool is SelectTool))
						SelectTool.Instance.Activate();
					var selectable = bot.GetComponent<KSelectable>();
					SelectTool.Instance.Select(null);
					SelectTool.Instance.Select(selectable);
				} else {
					string speech = TileCursor.Instance.JumpTo(botCell);
					if (speech != null)
						SpeechPipeline.SpeakInterrupt(speech);
				}
			} catch (Exception ex) {
				Log.Error($"BotNavigator.JumpOrSelect: {ex}");
			}
		}

		public string StartFollow() {
			try {
				var bot = GetCurrentBot();
				if (bot == null) {
					BaseScreenHandler.PlaySound("Negative");
					return null;
				}
				StopFollow();
				AttachFollow(bot);
				return string.Format(
					(string)STRINGS.ONIACCESS.DUPES.FOLLOW.FOLLOWING,
					bot.GetProperName());
			} catch (Exception ex) {
				Log.Error($"BotNavigator.StartFollow: {ex}");
				return null;
			}
		}

		public bool IsFollowing => _followedBot != null;

		public void TickFollow() {
			if (_followedBot == null) return;
			if (CameraController.Instance.followTarget == null)
				StopFollow();
		}

		public void StopFollowAndClear() {
			if (_followedBot == null) return;
			StopFollow();
			CameraController.Instance.ClearFollowTarget();
		}

		private void StopFollow() {
			if (_followedBot == null) return;
			try {
				var selectable = _followedBot.GetComponent<KSelectable>();
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
				Log.Warn($"BotNavigator.StopFollow: {ex}");
			}
			_followedBot = null;
			_onStatusAdded = null;
			_onStatusRemoved = null;
		}

		private void SwitchFollowTarget() {
			try {
				StopFollow();
				var bot = GetCurrentBot();
				if (bot == null) return;
				AttachFollow(bot);
			} catch (Exception ex) {
				Log.Error($"BotNavigator.SwitchFollowTarget: {ex}");
			}
		}

		private void AttachFollow(GameObject bot) {
			_followedBot = bot;
			_onStatusAdded = OnStatusAdded;
			_onStatusRemoved = OnStatusRemoved;
			var group = bot.GetComponent<KSelectable>().GetStatusItemGroup();
			group.OnAddStatusItem = (Action<StatusItemGroup.Entry, StatusItemCategory>)
				Delegate.Combine(group.OnAddStatusItem, _onStatusAdded);
			group.OnRemoveStatusItem = (Action<StatusItemGroup.Entry, bool>)
				Delegate.Combine(group.OnRemoveStatusItem, _onStatusRemoved);
			_choreChangedSub.Subscribe(bot, -1988963660, OnChoreChanged);
			CameraController.Instance.SetFollowTarget(bot.transform);
		}

		private void OnStatusAdded(StatusItemGroup.Entry entry, StatusItemCategory category) {
			try {
				SpeechPipeline.SpeakQueued(entry.GetName());
			} catch (Exception ex) {
				Log.Warn($"BotNavigator.OnStatusAdded: {ex}");
			}
		}

		private void OnStatusRemoved(StatusItemGroup.Entry entry, bool immediate) {
			try {
				SpeechPipeline.SpeakQueued(string.Format(
					(string)STRINGS.ONIACCESS.DUPES.FOLLOW.STATUS_ENDED,
					entry.GetName()));
			} catch (Exception ex) {
				Log.Warn($"BotNavigator.OnStatusRemoved: {ex}");
			}
		}

		private void OnChoreChanged(object data) {
			try {
				if (_followedBot == null) return;
				string task = BuildTaskPart(_followedBot);
				SpeechPipeline.SpeakQueued(task);
			} catch (Exception ex) {
				Log.Warn($"BotNavigator.OnChoreChanged: {ex}");
			}
		}

		/// <summary>
		/// Returns bots on the active world, sorted for stable naming.
		/// Covers all 5 bot types: Sweepy, Scout Rover, Morb Rover, Flydo,
		/// and Remote Worker.
		/// </summary>
		private static List<GameObject> GetWorldBots() {
			int worldId = ClusterManager.Instance.activeWorldId;
			var seen = new HashSet<int>();
			var result = new List<GameObject>();
			foreach (var brain in Components.Brains.GetWorldItems(worldId)) {
				var go = brain.gameObject;
				if (!go.GetComponent<KPrefabID>().HasTag(GameTags.Robot)) continue;
				seen.Add(go.GetInstanceID());
				result.Add(go);
			}
			var docks = Components.RemoteWorkerDocks.GetItems(worldId);
			if (docks != null) {
				foreach (var dock in docks) {
					var rw = dock.RemoteWorker;
					if (rw == null) continue;
					if (rw.Docked) continue;
					var go = rw.gameObject;
					if (seen.Add(go.GetInstanceID()))
						result.Add(go);
				}
			}
			result.Sort((a, b) => a.GetComponent<KPrefabID>().InstanceID
				.CompareTo(b.GetComponent<KPrefabID>().InstanceID));
			return result;
		}

		private string BuildAnnouncement(GameObject bot, int cursorCell, List<GameObject> worldBots) {
			string name = GetDisplayName(bot, worldBots);
			string task = BuildTaskPart(bot);
			string statuses = BuildStatusPart(bot);
			string position = AnnouncementFormatter.FormatDistance(
				cursorCell, Grid.PosToCell(bot));
			var parts = new System.Text.StringBuilder(name);
			if (statuses != null)
				parts.Append(", ").Append(statuses);
			if (position.Length > 0)
				parts.Append(", ").Append(position);
			parts.Append(", ").Append(task);
			return parts.ToString();
		}

		/// <summary>
		/// Returns a display name with disambiguation numbering when multiple
		/// bots on the same world share the same GetProperName().
		/// </summary>
		private static string GetDisplayName(GameObject bot, List<GameObject> worldBots) {
			string baseName = bot.GetProperName();
			var sameNameIds = new List<int>();
			for (int i = 0; i < worldBots.Count; i++) {
				if (worldBots[i].GetProperName() == baseName)
					sameNameIds.Add(worldBots[i].GetComponent<KPrefabID>().InstanceID);
			}
			if (sameNameIds.Count <= 1)
				return baseName;
			sameNameIds.Sort();
			int botId = bot.GetComponent<KPrefabID>().InstanceID;
			int number = sameNameIds.IndexOf(botId) + 1;
			return $"{baseName} {number}";
		}

		private static string BuildTaskPart(GameObject bot) {
			try {
				var choreDriver = bot.GetComponent<ChoreDriver>();
				if (choreDriver == null)
					return (string)STRINGS.ONIACCESS.DUPES.IDLE;
				var chore = choreDriver.GetCurrentChore();
				if (chore == null)
					return (string)STRINGS.ONIACCESS.DUPES.IDLE;
				string name = chore.choreType.Name;
				string target = GetChoreTarget(chore, bot);
				if (target != null)
					return $"{name}, {target}";
				return name;
			} catch (Exception ex) {
				Log.Warn($"BotNavigator.BuildTaskPart: {ex}");
				return (string)STRINGS.ONIACCESS.DUPES.IDLE;
			}
		}

		private static string GetChoreTarget(Chore chore, GameObject bot) {
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
			if (target == bot.GetProperName())
				return null;
			return target;
		}

		private StatusCheck[] _statusChecks;

		private StatusCheck[] GetStatusChecks() {
			if (_statusChecks != null)
				return _statusChecks;
			var robotItems = Db.Get().RobotStatusItems;
			var creatureItems = Db.Get().CreatureStatusItems;
			var dupeItems = Db.Get().DuplicantStatusItems;
			_statusChecks = new StatusCheck[] {
				new StatusCheck(robotItems.LowBattery),
				new StatusCheck(robotItems.LowBatteryNoCharge),
				new StatusCheck(robotItems.DeadBattery),
				new StatusCheck(robotItems.DeadBatteryFlydo),
				new StatusCheck(robotItems.CantReachStation),
				new StatusCheck(robotItems.DustBinFull),
				new StatusCheck(robotItems.Working),
				new StatusCheck(robotItems.UnloadingStorage),
				new StatusCheck(robotItems.MovingToChargeStation),
				new StatusCheck(creatureItems.HealthStatus),
				new StatusCheck(dupeItems.UnreachableDock),
				new StatusCheck(dupeItems.NoHomeDock),
				new StatusCheck(dupeItems.RemoteWorkerLowPower),
				new StatusCheck(dupeItems.RemoteWorkerOutOfPower),
				new StatusCheck(dupeItems.RemoteWorkerHighGunkLevel),
				new StatusCheck(dupeItems.RemoteWorkerFullGunkLevel),
				new StatusCheck(dupeItems.RemoteWorkerLowOil),
				new StatusCheck(dupeItems.RemoteWorkerOutOfOil),
			};
			return _statusChecks;
		}

		private string BuildStatusPart(GameObject bot) {
			var results = new List<string>();
			var selectable = bot.GetComponent<KSelectable>();
			try {
				foreach (var check in GetStatusChecks()) {
					if (selectable.HasStatusItem(check.Item))
						results.Add(check.Item.Name);
				}
			} catch (Exception ex) {
				Log.Warn($"BotNavigator.BuildStatusPart: {ex}");
			}
			return results.Count > 0 ? string.Join(", ", results) : null;
		}

		private struct StatusCheck {
			public readonly StatusItem Item;

			public StatusCheck(StatusItem item) {
				Item = item;
			}
		}
	}
}
