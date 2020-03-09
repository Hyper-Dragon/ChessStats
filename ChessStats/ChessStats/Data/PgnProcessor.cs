using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;


namespace ChessStats.Data
{
    public static class PgnProcessor
    {
        public static PgnGame GetGameFromPgn(string source, string pgnText)
        {
            PgnGame pgnGame = new PgnGame()
            {
                GameAttributes = PgnHeader.GetHeaderFromText(pgnText),
                OriginalPgnText = pgnText,
                Source = source,
            };

            return pgnGame;
        }
    }
}
