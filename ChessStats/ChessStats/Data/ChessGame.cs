namespace ChessStats.Data
{
    public class ChessGame
    {
        public string Source { get; set; }
        public string Text { get; set; }
        public bool IsRatedGame { get; set; }
        public int BlackRating { get; set; }
        public int WhiteRating { get; set; }
        public string Rules { get; set; }
        public string TimeClass { get; set; }
        public string TimeControl { get; set; }
        public GameHeader GameAttributes { get; set; } = new GameHeader();
    }
}
