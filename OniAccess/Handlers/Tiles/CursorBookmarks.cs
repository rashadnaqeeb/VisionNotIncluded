using System.Collections;
using System.Reflection;
using OniAccess.Handlers.Tiles.Scanner;
using UnityEngine;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Reads the game's UserNavigation bookmark data via reflection and
	/// provides jump-to-bookmark, orient-to-bookmark, and jump-home operations
	/// that move the tile cursor instead of panning the camera.
	/// </summary>
	public class CursorBookmarks {
		private readonly FieldInfo _hotkeyNavPointsField;
		private readonly FieldInfo _posField;
		private readonly FieldInfo _orthoSizeField;

		public CursorBookmarks() {
			var userNavType = typeof(UserNavigation);
			_hotkeyNavPointsField = userNavType.GetField(
				"hotkeyNavPoints",
				BindingFlags.Instance | BindingFlags.NonPublic);
			if (_hotkeyNavPointsField == null) {
				Util.Log.Warn("CursorBookmarks: hotkeyNavPoints field not found");
				return;
			}

			// NavPoint is a private nested struct — find it and cache its fields
			var navPointType = userNavType.GetNestedType(
				"NavPoint", BindingFlags.NonPublic);
			if (navPointType == null) {
				Util.Log.Warn("CursorBookmarks: NavPoint type not found");
				return;
			}
			_posField = navPointType.GetField("pos", BindingFlags.Public | BindingFlags.Instance);
			_orthoSizeField = navPointType.GetField("orthoSize", BindingFlags.Public | BindingFlags.Instance);
			if (_posField == null || _orthoSizeField == null)
				Util.Log.Warn("CursorBookmarks: NavPoint fields not found");
		}

		public string Goto(int index) {
			if (!TryReadBookmark(index, out Vector3 pos))
				return (string)STRINGS.ONIACCESS.BOOKMARKS.NO_BOOKMARK;
			int cell = Grid.PosToCell(pos);
			string speech = TileCursor.Instance.JumpTo(cell);
			if (speech != null)
				PlayRecallSound(index);
			return speech ?? (string)STRINGS.ONIACCESS.BOOKMARKS.NO_BOOKMARK;
		}

		public string Orient(int index) {
			if (!TryReadBookmark(index, out Vector3 pos))
				return (string)STRINGS.ONIACCESS.BOOKMARKS.NO_BOOKMARK;
			return OrientToCell(Grid.PosToCell(pos));
		}

		public static string OrientHome() {
			int cell = FindHomeCell();
			if (cell == Grid.InvalidCell)
				return (string)STRINGS.ONIACCESS.BOOKMARKS.NO_HOME;
			return OrientToCell(cell);
		}

		private static string OrientToCell(int targetCell) {
			int cursorCell = TileCursor.Instance.Cell;
			string coords = Util.GridCoordinates.Format(targetCell);
			string distance = AnnouncementFormatter.FormatDistance(cursorCell, targetCell);
			if (string.IsNullOrEmpty(distance))
				return string.Format(STRINGS.ONIACCESS.BOOKMARKS.ORIENT_HERE, coords);
			return string.Format(STRINGS.ONIACCESS.BOOKMARKS.ORIENT_DISTANCE, distance, coords);
		}

		public string Set(int index) {
			if (_hotkeyNavPointsField == null || _posField == null || _orthoSizeField == null)
				return null;
			var userNav = SaveGame.Instance.GetComponent<UserNavigation>();
			var list = _hotkeyNavPointsField.GetValue(userNav) as IList;
			if (list == null || index < 0 || index >= list.Count)
				return null;

			Vector3 pos = Grid.CellToPosCCC(TileCursor.Instance.Cell, Grid.SceneLayer.Move);
			float orthoSize = CameraController.Instance.baseCamera.orthographicSize;

			object navPoint = list[index];
			_posField.SetValue(navPoint, pos);
			_orthoSizeField.SetValue(navPoint, orthoSize);
			list[index] = navPoint;

			PlaySetSound(index);
			return string.Format((string)STRINGS.ONIACCESS.BOOKMARKS.BOOKMARK_SET, index + 1);
		}

		public static string JumpHome() {
			int cell = FindHomeCell();
			if (cell == Grid.InvalidCell)
				return (string)STRINGS.ONIACCESS.BOOKMARKS.NO_HOME;
			string speech = TileCursor.Instance.JumpTo(cell);
			if (speech != null)
				KMonoBehaviour.PlaySound(GlobalAssets.GetSound("Click_Notification"));
			return speech ?? (string)STRINGS.ONIACCESS.BOOKMARKS.NO_HOME;
		}

		public static int DigitKeyToIndex(KeyCode key) {
			if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9)
				return (int)(key - KeyCode.Alpha1);
			if (key == KeyCode.Alpha0)
				return 9;
			if (key >= KeyCode.Keypad1 && key <= KeyCode.Keypad9)
				return (int)(key - KeyCode.Keypad1);
			if (key == KeyCode.Keypad0)
				return 9;
			return -1;
		}

		private static int FindHomeCell() {
			var world = ClusterManager.Instance.activeWorld;
			if (world.IsModuleInterior) {
				try {
					var stations = Components.RocketControlStations.GetWorldItems(world.id);
					if (stations != null && stations.Count > 0)
						return Grid.PosToCell(stations[0].transform.GetPosition());
				} catch (System.Exception ex) {
					Util.Log.Warn($"CursorBookmarks.FindHomeCell: {ex.Message}");
				}
				return Grid.InvalidCell;
			}
			var telepad = GameUtil.GetActiveTelepad();
			if (telepad == null)
				return Grid.InvalidCell;
			return Grid.PosToCell(telepad.transform.GetPosition());
		}

		private static void PlaySetSound(int index) {
			string sound = GlobalAssets.GetSound("UserNavPoint_set");
			if (sound != null) {
				var instance = KFMOD.BeginOneShot(sound, Vector3.zero);
				instance.setParameterByName("userNavPoint_ID", index);
				KFMOD.EndOneShot(instance);
			}
		}

		private static void PlayRecallSound(int index) {
			string sound = GlobalAssets.GetSound("UserNavPoint_recall");
			if (sound != null) {
				var instance = KFMOD.BeginOneShot(sound, Vector3.zero);
				instance.setParameterByName("userNavPoint_ID", index);
				KFMOD.EndOneShot(instance);
			}
		}

		private bool TryReadBookmark(int index, out Vector3 pos) {
			pos = Vector3.zero;
			if (_hotkeyNavPointsField == null || _posField == null || _orthoSizeField == null)
				return false;
			var userNav = SaveGame.Instance.GetComponent<UserNavigation>();
			var list = _hotkeyNavPointsField.GetValue(userNav) as IList;
			if (list == null || index < 0 || index >= list.Count)
				return false;
			object navPoint = list[index];
			float orthoSize = (float)_orthoSizeField.GetValue(navPoint);
			if (orthoSize == 0f)
				return false;
			pos = (Vector3)_posField.GetValue(navPoint);
			return true;
		}
	}
}
