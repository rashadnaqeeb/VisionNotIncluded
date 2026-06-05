using System.Collections.Generic;

using HarmonyLib;

using OniAccess.Input;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Codex {
	/// <summary>
	/// Content tab: flat BaseMenuHandler that reads through article widgets
	/// sequentially. Rebuilt from CodexCache on each ChangeArticle call.
	/// Ctrl+Up/Down jumps between section headings (Title, Subtitle,
	/// CollapsibleHeader). Enter follows links.
	/// </summary>
	internal class ContentTab: BaseMenuHandler, IScreenTab {
		private readonly CodexScreenHandler _parent;

		/// <summary>
		/// Flat list of readable items in the current article.
		/// Rebuilt on every article change from CodexCache data.
		/// </summary>
		private readonly List<ContentItem> _items = new List<ContentItem>();

		/// <summary>
		/// Maps each ContentContainer to its first index in _items.
		/// Used for positioning the cursor on a specific SubEntry.
		/// </summary>
		private readonly List<(ContentContainer cc, int startIndex)> _containerMap
			= new List<(ContentContainer, int)>();

		private string _pendingSubEntryId;

		internal ContentTab(CodexScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.CODEX.CONTENT_TAB;

		public override string DisplayName => TabName;

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Ctrl+Up/Down", STRINGS.ONIACCESS.HELP.JUMP_GROUP),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Enter", STRINGS.ONIACCESS.CODEX.FOLLOW_LINK_HELP),
			new HelpEntry("Alt+Left/Backspace", STRINGS.ONIACCESS.HELP.GO_BACK),
			new HelpEntry("Alt+Right", STRINGS.ONIACCESS.CODEX.HISTORY_FORWARD_HELP),
		};

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			CurrentIndex = 0;
			_search.Clear();
			SuppressSearchThisFrame();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			RebuildWidgetList();
			ApplyPendingSubEntry();
			SpeakCurrentItemQueued();
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			if (base.Tick())
				return true;
			return TryHistoryNavigation();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// Article change notification
		// ========================================

		/// <summary>
		/// Called by CodexScreenHandler when ChangeArticle fires.
		/// Rebuilds the widget list and resets cursor.
		/// </summary>
		internal void OnArticleChanged() {
			CurrentIndex = 0;
			_search.Clear();
			SuppressSearchThisFrame();
			RebuildWidgetList();
			ApplyPendingSubEntry();
			SpeakCurrentItemQueued();
		}

		// ========================================
		// BaseMenuHandler abstracts
		// ========================================

		public override int ItemCount => _items.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].text;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (_items.Count == 0) {
				SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.CODEX.NO_ARTICLE);
				return;
			}
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			string text = _items[CurrentIndex].text;
			if (string.IsNullOrEmpty(text)) return;
			if (!string.IsNullOrEmpty(parentContext))
				text = parentContext + ", " + text;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(text));
		}

		// ========================================
		// Section jumping (Ctrl+Up/Down)
		// ========================================

		protected override void JumpNextGroup() {
			for (int i = CurrentIndex + 1; i < _items.Count; i++) {
				if (_items[i].isHeading) {
					CurrentIndex = i;
					PlaySound("HUD_Mouseover");
					SpeakCurrentItem();
					return;
				}
			}
			// Wrap to first heading
			for (int i = 0; i < CurrentIndex; i++) {
				if (_items[i].isHeading) {
					CurrentIndex = i;
					PlaySound("HUD_Click");
					SpeakCurrentItem();
					return;
				}
			}
		}

		protected override void JumpPrevGroup() {
			for (int i = CurrentIndex - 1; i >= 0; i--) {
				if (_items[i].isHeading) {
					CurrentIndex = i;
					PlaySound("HUD_Mouseover");
					SpeakCurrentItem();
					return;
				}
			}
			// Wrap to last heading
			for (int i = _items.Count - 1; i > CurrentIndex; i--) {
				if (_items[i].isHeading) {
					CurrentIndex = i;
					PlaySound("HUD_Click");
					SpeakCurrentItem();
					return;
				}
			}
		}

		// ========================================
		// Activation (Enter): links and videos
		// ========================================

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;

			var item = _items[CurrentIndex];

			if (item.video != null) {
				PlayVideo(item.video);
				return;
			}

			var links = item.links;
			if (links == null || links.Count == 0) return;

			if (links.Count == 1) {
				FollowLink(links[0].id);
				return;
			}

			// Multiple links: open popup sub-menu
			var linkMenu = new LinkMenuHandler(_parent, links);
			HandlerStack.Push(linkMenu);
		}

		private static void PlayVideo(CodexVideo video) {
			var clip = Assets.GetVideo(video.name);
			if (clip == null) return;
			VideoScreen.Instance.PlayVideo(clip);
			if (!string.IsNullOrEmpty(video.overlayName))
				VideoScreen.Instance.SetOverlayText(video.overlayName, video.overlayTexts);
		}

		internal void FollowLink(string entryId) {
			var codexScreen = _parent.CodexScreen;
			if (codexScreen == null) return;
			// If the link targets a sub-entry, set pending ID so the cursor
			// lands on the right section after ChangeArticle rebuilds.
			if (CodexCache.FindSubEntry(entryId) != null)
				_pendingSubEntryId = entryId;
			PlaySound("HUD_Click_Open");
			codexScreen.ChangeArticle(entryId);
		}

		// ========================================
		// Left/Right override — Alt+Arrow navigates history
		// ========================================

		protected override void HandleLeftRight(int direction, int stepLevel) {
			if (InputUtil.AltHeld())
				NavigateHistory(back: direction < 0);
		}

		// ========================================
		// History navigation (Alt+Left / Backspace / Alt+Right)
		// ========================================

		private bool TryHistoryNavigation() {
			bool back = UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Backspace);

			if (!back) return false;

			NavigateHistory(back: true);
			return true;
		}

		private void NavigateHistory(bool back) {
			var codexScreen = _parent.CodexScreen;
			if (codexScreen == null) return;

			var t = Traverse.Create(codexScreen);
			int idx = t.Field<int>("currentHistoryIdx").Value;
			var history = t.Field<List<CodexScreen.HistoryEntry>>("history").Value;

			if (back) {
				if (idx <= 0) {
					SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.CODEX.NO_BACK);
					return;
				}
				t.Method("HistoryStepBack").GetValue();
			} else {
				if (idx >= history.Count - 1) {
					SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.CODEX.NO_FORWARD);
					return;
				}
				t.Method("HistoryStepForward").GetValue();
			}
		}

		// ========================================
		// SubEntry cursor positioning
		// ========================================

		internal void SetPendingSubEntryId(string id) { _pendingSubEntryId = id; }

		private void SeekToSubEntry(string subEntryId) {
			var subEntry = CodexCache.FindSubEntry(subEntryId);
			if (subEntry?.contentContainers == null || subEntry.contentContainers.Count == 0) return;
			var target = subEntry.contentContainers[0];
			foreach (var (cc, startIndex) in _containerMap) {
				if (object.ReferenceEquals(cc, target) && startIndex < _items.Count) {
					CurrentIndex = startIndex;
					return;
				}
			}
		}

		private void ApplyPendingSubEntry() {
			if (_pendingSubEntryId == null) return;
			string id = _pendingSubEntryId;
			_pendingSubEntryId = null;
			SeekToSubEntry(id);
		}

		// ========================================
		// Widget list building
		// ========================================

		private void RebuildWidgetList() {
			_items.Clear();
			_containerMap.Clear();

			var codexScreen = _parent.CodexScreen;
			if (codexScreen == null) return;

			string entryId = codexScreen.activeEntryID;
			if (string.IsNullOrEmpty(entryId)) return;
			if (!CodexCache.entries.TryGetValue(entryId, out var entry)) return;
			if (entry.contentContainers == null) return;

			string lastLockedId = null;
			foreach (var cc in entry.contentContainers) {
				_containerMap.Add((cc, _items.Count));
				if (!CodexHelper.IsContainerVisible(cc)) continue;

				if (CodexHelper.IsContainerLocked(cc)) {
					// Only announce locked content once per lockID
					if (cc.lockID != lastLockedId) {
						lastLockedId = cc.lockID;
						_items.Add(new ContentItem {
							text = (string)STRINGS.ONIACCESS.CODEX.LOCKED_CONTENT,
							isHeading = false,
							links = null,
						});
					}
					continue;
				}
				lastLockedId = null;

				if (cc.content == null) continue;
				if (TryAddSubEntriesItem(cc, entry)) continue;
				for (int w = 0; w < cc.content.Count; w++) {
					var widget = cc.content[w];
					if (!CodexHelper.IsWidgetVisible(widget)) continue;

					// CodexElementCategoryList expands to header + individual elements
					if (widget is CodexElementCategoryList ecl) {
						AddElementCategoryItems(ecl);
						continue;
					}

					// Merge label-with-icon + following body text (e.g. critter drops: "Meat" + "4800 kcal")
					if (widget is CodexIndentedLabelWithIcon cili) {
						string label = WidgetTextExtractor.GetText(cili);
						string suffix = GetNextBodyText(cc.content, ref w);
						if (suffix != null)
							label += ", " + suffix;
						if (!string.IsNullOrEmpty(label))
							_items.Add(new ContentItem { text = label });
						continue;
					}

					var lines = WidgetTextExtractor.GetTextLines(widget, entryId);
					if (lines == null) continue;

					bool isHeading = WidgetTextExtractor.IsSectionHeading(widget);
					var links = WidgetTextExtractor.GetLinks(widget);
					var video = widget as CodexVideo;

					if (lines.Count == 1) {
						_items.Add(new ContentItem {
							text = lines[0],
							isHeading = isHeading,
							links = links,
							video = video,
						});
					} else {
						foreach (string line in lines) {
							_items.Add(new ContentItem {
								text = line,
								isHeading = false,
								links = null,
							});
						}
					}
				}
			}

			GroupMultiConverterEffects(entryId);
		}

		private void GroupMultiConverterEffects(string entryId) {
			var result = WidgetTextExtractor.GetGroupedConverterItems(entryId);
			if (result == null) return;

			var (groupedItems, inputCount, outputCount) = result.Value;

			// Remove input items from Requirements section
			string reqHeader = (string)STRINGS.CODEX.HEADERS.BUILDINGREQUIREMENTS;
			int reqIndex = FindHeadingIndex(reqHeader);
			if (reqIndex >= 0 && reqIndex + inputCount < _items.Count)
				_items.RemoveRange(reqIndex + 1, inputCount);

			// Replace output items in Effects section with grouped items
			string effectsHeader = (string)STRINGS.CODEX.HEADERS.BUILDINGEFFECTS;
			int effectsIndex = FindHeadingIndex(effectsHeader);
			if (effectsIndex < 0 || effectsIndex + outputCount >= _items.Count) return;

			int removeStart = effectsIndex + 1;
			_items.RemoveRange(removeStart, outputCount);

			for (int i = 0; i < groupedItems.Count; i++) {
				_items.Insert(removeStart + i, new ContentItem {
					text = groupedItems[i],
					isHeading = false,
					links = null,
				});
			}
		}

		private int FindHeadingIndex(string text) {
			for (int i = 0; i < _items.Count; i++) {
				if (_items[i].isHeading && _items[i].text == text)
					return i;
			}
			return -1;
		}

		private void AddElementCategoryItems(CodexElementCategoryList widget) {
			var items = WidgetTextExtractor.GetElementCategoryItems(widget);
			foreach (var (text, isHeader) in items) {
				_items.Add(new ContentItem {
					text = text,
					isHeading = isHeader,
					links = null,
				});
			}
		}

		/// <summary>
		/// If the next visible widget after index w is a body-style CodexText,
		/// returns its cleaned text and advances w past it. Otherwise returns null.
		/// </summary>
		private static string GetNextBodyText(List<ICodexWidget> widgets, ref int w) {
			for (int i = w + 1; i < widgets.Count; i++) {
				if (!CodexHelper.IsWidgetVisible(widgets[i])) continue;
				if (widgets[i] is CodexText ct && ct.style == CodexTextStyle.Body) {
					w = i;
					return Widgets.WidgetOps.CleanTooltipEntry(ct.text);
				}
				break;
			}
			return null;
		}

		/// <summary>
		/// Detects the SubEntries container and collapses it into a single item
		/// like "entries: Sweetle, Grubgrub". Identified structurally: a container
		/// whose only visible non-spacer widgets are one Subtitle and body
		/// CodexTexts that are all link markup (no plain body text).
		/// </summary>
		private bool TryAddSubEntriesItem(ContentContainer cc, CodexEntry entry) {
			if (entry.subEntries == null || entry.subEntries.Count < 2) return false;

			CodexText header = null;
			var names = new List<string>();
			var links = new List<(string id, string text)>();
			foreach (var widget in cc.content) {
				if (!CodexHelper.IsWidgetVisible(widget)) continue;
				if (widget is CodexSpacer) continue;
				if (widget is CodexText ct) {
					if (ct.style == CodexTextStyle.Subtitle) {
						if (header != null) return false;
						header = ct;
						continue;
					}
					var widgetLinks = CodexHelper.ExtractTextLinks(ct.text);
					if (widgetLinks.Count == 0) return false;
					// Every link must target a sub-entry of this article.
					// External links (to other codex articles) mean this
					// isn't the SubEntries container.
					foreach (var (id, _) in widgetLinks) {
						if (!IsSubEntryOfArticle(id, entry)) return false;
					}
					names.Add(Widgets.WidgetOps.CleanTooltipEntry(ct.text));
					links.AddRange(widgetLinks);
					continue;
				}
				return false;
			}
			if (header == null || names.Count < 2) return false;

			if (links.Count == 0) return false;

			string text = (string)STRINGS.ONIACCESS.CODEX.SUBENTRIES
				+ ": " + string.Join(", ", names);
			_items.Add(new ContentItem {
				text = text,
				isHeading = true,
				links = links,
			});
			return true;
		}

		private static bool IsSubEntryOfArticle(string linkId, CodexEntry entry) {
			foreach (var sub in entry.subEntries) {
				if (string.Equals(sub.id, linkId, System.StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		private void SpeakCurrentItemQueued() {
			if (_items.Count == 0) {
				SpeechPipeline.SpeakQueued(STRINGS.ONIACCESS.CODEX.NO_ARTICLE);
				return;
			}
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			string text = _items[CurrentIndex].text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(text));
		}

		// ========================================
		// Content item
		// ========================================

		private struct ContentItem {
			internal string text;
			internal bool isHeading;
			internal List<(string id, string text)> links;
			internal CodexVideo video;
		}
	}
}
