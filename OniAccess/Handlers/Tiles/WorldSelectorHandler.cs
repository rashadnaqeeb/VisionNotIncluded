using System.Collections.Generic;

using OniAccess.Widgets;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Navigable list of all discovered worlds (Spaced Out DLC).
	/// Opened by W from TileCursorHandler, closed on Escape.
	/// Enter switches to the selected world via ActiveWorldStarWipe.
	/// </summary>
	public class WorldSelectorHandler: BaseMenuHandler {
		private struct WorldItem {
			public int WorldId;
			public string Label;
		}

		private readonly List<WorldItem> _items = new List<WorldItem>();

		public override string DisplayName => STRINGS.ONIACCESS.HANDLERS.WORLD_SELECTOR;

		public override int ItemCount => _items.Count;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public WorldSelectorHandler() : base(null) {
			HelpEntries = BuildHelpEntries();
		}

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].Label;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			Speech.SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(BuildSpeech(CurrentIndex)));
		}

		public override void OnActivate() {
			BuildItems();
			PlaySound("HUD_Click_Open");
			base.OnActivate();
			if (_items.Count > 0)
				Speech.SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(BuildSpeech(0)));
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			int worldId = _items[CurrentIndex].WorldId;
			CameraController.Instance.ActiveWorldStarWipe(worldId);
			Close();
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;
			if (e.TryConsume(Action.Escape)) {
				Close();
				return true;
			}
			return false;
		}

		private void Close() {
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
		}

		private void BuildItems() {
			_items.Clear();
			if (ClusterManager.Instance == null) return;

			var worlds = new List<WorldContainer>();
			foreach (var world in ClusterManager.Instance.WorldContainers) {
				if (world.IsDiscovered) worlds.Add(world);
			}
			worlds.Sort((a, b) => {
				float ta = a.IsModuleInterior ? float.PositiveInfinity : a.DiscoveryTimestamp;
				float tb = b.IsModuleInterior ? float.PositiveInfinity : b.DiscoveryTimestamp;
				return ta.CompareTo(tb);
			});

			// Reinsert moons immediately after their parent (mirrors WorldSelector.SortRows)
			for (int i = 0; i < worlds.Count; i++) {
				var w = worlds[i];
				if (w.ParentWorldId == w.id || w.ParentWorldId == 255) continue;
				int parentPos = -1;
				for (int j = 0; j < worlds.Count; j++) {
					if (worlds[j].id == w.ParentWorldId) { parentPos = j; break; }
				}
				if (parentPos >= 0 && parentPos < i) {
					worlds.RemoveAt(i);
					worlds.Insert(parentPos + 1, w);
				}
			}

			foreach (var world in worlds) {
				string name = world.GetComponent<ClusterGridEntity>().Name;
				_items.Add(new WorldItem { WorldId = world.id, Label = name });
			}
		}

		private string BuildSpeech(int index) {
			int worldId = _items[index].WorldId;
			var world = ClusterManager.Instance.GetWorld(worldId);
			string name = world.GetComponent<ClusterGridEntity>().Name;

			bool isActive = worldId == ClusterManager.Instance.activeWorldId;

			var parts = new List<string>();
			if (isActive) parts.Add((string)STRINGS.ONIACCESS.WORLD_SELECTOR.ACTIVE_LABEL);
			parts.Add(name);

			string worldType = world.IsModuleInterior
				? (string)STRINGS.ONIACCESS.WORLD_SELECTOR.ROCKET
				: Strings.Get(world.worldType);
			if (!string.IsNullOrEmpty(worldType))
				parts.Add(worldType);

			if (ColonyDiagnosticUtility.Instance.diagnosticDisplaySettings.ContainsKey(worldId)) {
				var opinion = ColonyDiagnosticUtility.Instance.GetWorldDiagnosticResult(worldId);
				string severity = TileCursorHandler.OpinionWord(opinion);
				string status = world.GetStatus();
				parts.Add(severity);
				if (!string.IsNullOrEmpty(status)) parts.Add(status);
			}

			return string.Join(", ", parts);
		}
	}
}
