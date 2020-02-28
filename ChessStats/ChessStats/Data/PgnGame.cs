using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessStats.Data
{
    public class PgnGame
    {
        public PgnHeader GameAttributes { get; set; } = new PgnHeader();
        //public PgnMoves GameMoves { get; set; } = new PgnMoves();

        public string StartFen { get; set; } = "";
        //public PgnPosition StartPosition { get; set; }

        public string OriginalPgnText { get; set; }

        public string Source { get; set; }

        public string Serialize()
        {
            //return null;
            return JsonSerializer.Serialize<PgnGame>(this);
        }
    }
}



