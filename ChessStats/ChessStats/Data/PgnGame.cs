using System.Text.Json;

namespace ChessStats.Data
{
    public class PgnGame
    {
        public PgnHeader GameAttributes { get; set; } = new PgnHeader();

        public string OriginalPgnText { get; set; }

        public string Source { get; set; }

        public string Serialize()
        {
            //return null;
            return JsonSerializer.Serialize<PgnGame>(this);
        }
    }
}



