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
            return PgnProcessor.GetGameFromPgn(source, pgnText, "", "");
        }

        public static PgnGame GetGameFromPgn(string source, string pgnText, string pgnHeaderText, string pgnGameText)
        {

            PgnGame pgnGame = new PgnGame()
            {
                GameAttributes = PgnHeader.GetHeaderFromText(pgnText),
                OriginalPgnText = pgnText,
                Source = source,
            };


            // Pre-Process Game Text
            var moveQueue = new Queue<string>();

            if (source == "ChessDotCom")
            {
                var pgnStrippedText = pgnText.Substring(pgnText.IndexOf("1. "));
                pgnStrippedText = Regex.Replace(pgnStrippedText, "{.*?}", "");
                (pgnStrippedText + " -").Replace('.', ' ').Split(' ').ToList().ForEach(x => { if (!int.TryParse(x.Trim(), out _) && !string.IsNullOrEmpty(x)) moveQueue.Enqueue(x.Trim()); });
            }
            else
            {
                throw new NotImplementedException($"Requested source not implemented");
            }

            return pgnGame;

        }
    }
}
