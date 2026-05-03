using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniAccess.Util;
using YamlDotNet.Serialization;

namespace OniAccess.Handlers.Tiles.FastTravel {
	/// <summary>
	/// Per-colony YAML store for fast travel points. Lives next to the save
	/// file in the colony folder, never inside the .sav itself, so a save
	/// shared with someone who doesn't have the mod loads with no compat issue.
	///
	/// Lazy-loaded: every public op calls EnsureLoaded(), which detects
	/// whether the active save path's colony folder has changed and reloads
	/// from disk if so. CRUD methods write immediately.
	/// </summary>
	public static class FastTravelStorage {
		private const string FileName = "oniaccess-bookmarks.yml";

		public class Data {
			public List<FastTravelPoint> Points { get; set; } = new List<FastTravelPoint>();
		}

		private static Data _data = new Data();
		private static string _loadedDir;

		/// <summary>
		/// Points belonging to the given world, sorted alphabetically.
		/// </summary>
		public static List<FastTravelPoint> GetForWorld(int worldId) {
			EnsureLoaded();
			return _data.Points
				.Where(p => p.WorldId == worldId)
				.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		public static FastTravelPoint Add(string name, int worldId, int cell) {
			EnsureLoaded();
			var point = new FastTravelPoint {
				Id = Guid.NewGuid().ToString("N"),
				Name = name,
				WorldId = worldId,
				Cell = cell,
			};
			_data.Points.Add(point);
			Save();
			return point;
		}

		public static void Rename(string id, string newName) {
			EnsureLoaded();
			var point = _data.Points.FirstOrDefault(p => p.Id == id);
			if (point == null) return;
			point.Name = newName;
			Save();
		}

		public static void Relocate(string id, int newCell) {
			EnsureLoaded();
			var point = _data.Points.FirstOrDefault(p => p.Id == id);
			if (point == null) return;
			point.Cell = newCell;
			Save();
		}

		public static void Remove(string id) {
			EnsureLoaded();
			_data.Points.RemoveAll(p => p.Id == id);
			Save();
		}

		/// <summary>
		/// Resolve the colony folder path from the active save file. Returns
		/// null if no save is active yet (e.g. before the first save has been
		/// written). Caller must handle null.
		/// </summary>
		private static string GetColonyDir() {
			string activePath;
			try {
				activePath = SaveLoader.GetActiveSaveFilePath();
			} catch (Exception ex) {
				Log.Warn($"FastTravelStorage: GetActiveSaveFilePath failed: {ex.Message}");
				return null;
			}
			if (string.IsNullOrEmpty(activePath)) return null;
			string dir = Path.GetDirectoryName(activePath);
			if (string.IsNullOrEmpty(dir)) return null;
			// If the active save lives in colony/auto_save/<file>, go up one
			// to the colony folder so manual and auto saves share one file.
			if (string.Equals(Path.GetFileName(dir), SaveLoader.AUTOSAVE_FOLDER, StringComparison.OrdinalIgnoreCase)) {
				dir = Path.GetDirectoryName(dir);
			}
			return dir;
		}

		private static void EnsureLoaded() {
			string dir = GetColonyDir();
			if (string.Equals(dir, _loadedDir, StringComparison.OrdinalIgnoreCase)) return;
			_loadedDir = dir;
			_data = new Data();
			if (string.IsNullOrEmpty(dir)) return;
			string path = Path.Combine(dir, FileName);
			if (!File.Exists(path)) return;
			try {
				string yaml = File.ReadAllText(path);
				var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
				var loaded = deserializer.Deserialize<Data>(yaml);
				if (loaded != null) {
					if (loaded.Points == null) loaded.Points = new List<FastTravelPoint>();
					_data = loaded;
					Log.Info($"FastTravelStorage: loaded {_data.Points.Count} points from {path}");
				}
			} catch (Exception ex) {
				Log.Warn($"FastTravelStorage: failed to load {path}: {ex.Message}");
				_data = new Data();
			}
		}

		private static void Save() {
			string dir = GetColonyDir();
			if (string.IsNullOrEmpty(dir)) {
				Log.Warn("FastTravelStorage: no active save path, skipping write");
				return;
			}
			try {
				Directory.CreateDirectory(dir);
				string path = Path.Combine(dir, FileName);
				using (var writer = new StreamWriter(path)) {
					new SerializerBuilder().EmitDefaults().Build().Serialize(writer, _data);
				}
			} catch (Exception ex) {
				Log.Warn($"FastTravelStorage: failed to save: {ex.Message}");
			}
		}
	}
}
