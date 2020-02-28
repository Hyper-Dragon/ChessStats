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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChessStats
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string chessdotcomUsername = args[0];

            Helpers.DisplayLogo();
            Helpers.displaySection("Fetching PGN's", true);

            var p = new PgnFromChessDotCom();

            var gameList = p.FetchGameRecordsForUser(chessdotcomUsername);

            Helpers.displaySection("Processing PGN's", true);
            Helpers.displaySection("Extracting Headers", false);

            ConcurrentBag<PgnGame> processedGameList = new ConcurrentBag<PgnGame>();

            Parallel.ForEach(gameList, (game) =>
            {
                processedGameList.Add(PgnProcessor.GetGameFromPgn("ChessDotCom", game.Text));
            });

            Helpers.displaySection("Calculating Totals", false);

            SortedList<string, int> secondsPlayedRollup = new SortedList<string, int>();
            SortedList<string, int> ecoPlayedRollup = new SortedList<string, int>();
            double totalSecondsPlayed = 0;

            foreach (var game in processedGameList)
            {
                // Don't include daily games
                if (game.GameAttributes.Attributes["Event"] != "Live Chess") continue;

                var side = game.GameAttributes.Attributes["White"].ToUpperInvariant() == chessdotcomUsername.ToUpperInvariant() ? "White" : "Black";

                //var ecoName = new Regex(@"^.*?(?=[0-9])").Match(game.GameAttributes.Attributes["ECOUrl"].Replace(@"https://www.chess.com/openings/", "").Replace("-", " ")).Value;
                var ecoName = game.GameAttributes.Attributes["ECOUrl"].Replace(@"https://www.chess.com/openings/", "").Replace("-", " ");

                var ecoKey = $"{side}-{game.GameAttributes.Attributes["ECO"]}-{ecoName}";

                if (ecoPlayedRollup.ContainsKey(ecoKey))
                {
                    ecoPlayedRollup[ecoKey]++;
                }
                else
                {
                    ecoPlayedRollup.Add(ecoKey, 1);
                }

                
                var gameStartDate = game.GameAttributes.Attributes["Date"];
                var gameStartTime = game.GameAttributes.Attributes["StartTime"];
                var gameEndDate =   game.GameAttributes.Attributes["EndDate"];
                var gameEndTime = game.GameAttributes.Attributes["EndTime"];

                DateTime parsedStartDate;
                DateTime parsedEndDate;

                var startDateParsed = DateTime.TryParseExact($"{gameStartDate} {gameStartTime}", "yyyy.MM.dd HH:mm:ss", null, DateTimeStyles.AssumeUniversal, out parsedStartDate);
                var endDateParsed = DateTime.TryParseExact($"{gameEndDate} {gameEndTime}", "yyyy.MM.dd HH:mm:ss", null, DateTimeStyles.AssumeUniversal, out parsedEndDate);
                var seconds = System.Math.Abs((parsedEndDate - parsedStartDate).TotalSeconds);

                // see: https://support.chess.com/article/330-why-are-there-different-ratings-in-live-chess
                var timeControlSplit = game.GameAttributes.Attributes["TimeControl"].Split('+');
                var gameTimeEst = (int.Parse(timeControlSplit[0])) + 
                                  ((timeControlSplit.Length == 1) ? 0: (int.Parse(timeControlSplit[1]) * 40));

                var gameTime = "";

                if (gameTimeEst < (60*3) )
                {
                    gameTime = "Bullet";
                }
                else if (gameTimeEst < (60*10) )
                {
                    gameTime = "Blitz";
                }
                else
                {
                    gameTime = "Rapid";
                }

            string key = $"{parsedStartDate.Year}-{((parsedStartDate.Month < 10) ? "0" : "")}{parsedStartDate.Month} {gameTime}";
                
                totalSecondsPlayed += seconds;

                if (secondsPlayedRollup.ContainsKey(key))
                {
                    secondsPlayedRollup[key] += (int)seconds;
                }
                else
                {
                    secondsPlayedRollup.Add(key, (int)seconds);
                }
            }

            Helpers.displaySection("Report", true);

            System.Console.WriteLine("");
            foreach (var rolledUp in secondsPlayedRollup)
            {
                TimeSpan timeMonth = TimeSpan.FromSeconds(rolledUp.Value);
                System.Console.WriteLine($"{rolledUp.Key.PadRight(20,' ')} :: {((int)timeMonth.TotalHours).ToString().PadLeft(3, ' ')}:{ timeMonth.Minutes.ToString().PadLeft(2, '0')}:{ timeMonth.Seconds.ToString().PadLeft(2, '0')} :: {rolledUp.Value} seconds");
            }
            System.Console.WriteLine("");

            TimeSpan time = TimeSpan.FromSeconds(totalSecondsPlayed);
            Console.WriteLine($"Time Played: {((int)time.TotalHours).ToString().PadLeft(3, ' ')}:{ time.Minutes.ToString().PadLeft(2, '0')}:{ time.Seconds.ToString().PadLeft(2, '0')} :: {totalSecondsPlayed} seconds");


            foreach(var ecoCount in ecoPlayedRollup)
            {
                Console.WriteLine($"{ecoCount.Key.PadRight(75,' ')} :: {ecoCount.Value}");
            }

            Helpers.PressToContinue();
        }
    }
}
