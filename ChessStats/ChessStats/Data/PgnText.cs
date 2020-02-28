using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessStats.Data
{
    public class PgnText
    {
        public string Source { get; set; }
        public string Text { get; set; }
        public string TextHeaderOnly { get; set; }
        public string TextGameOnly { get; set; }

        public string Serialize()
        {
            //return null;
            return JsonSerializer.Serialize<PgnText>(this);
        }
    }
}
