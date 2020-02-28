using ChessStats.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChessStats
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Helpers.DisplayLogo();
            Helpers.displaySection("Fetching Pgns", true);

            var p = new PgnFromChessDotCom();
            var gameList = p.FetchGameRecordsForUser("");

            Helpers.displaySection("Extracting Headers", true);

            foreach (var game in gameList)
            {
                var t = PgnProcessor.GetGameFromPgn("ChessDotCom", game.Text);
            }
        }
    }
}
