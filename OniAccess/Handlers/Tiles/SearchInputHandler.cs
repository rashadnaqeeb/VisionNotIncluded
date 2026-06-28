using System;

namespace OniAccess.Handlers.Tiles {
	public class SearchInputHandler: OniAccess.Handlers.TextPromptHandler {
		public SearchInputHandler(Action<string> onSearch)
			: base((string)STRINGS.ONIACCESS.SCANNER.SEARCH.PROMPT, "", query => {
				if (query.Length > 0)
					onSearch(query);
			}) { }
	}
}
