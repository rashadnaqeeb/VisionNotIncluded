namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Decides each step of a skip from the cell it leaves and the cell it
	/// enters, for lines whose identity a per-cell signature cannot carry
	/// (a bridge continues a pipe across cells it is not registered on).
	/// </summary>
	public interface ILineSkipStrategy {
		bool Continues(int from, int to, Direction direction);
	}
}
