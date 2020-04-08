using ChessStats.Data;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static ChessStats.Data.GameHeader;

namespace ChessStats
{
    internal class Program
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async Task Main(string[] args)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Stopwatch stopwatch = new Stopwatch();
            Helpers.DisplayLogo();

            if (args.Length != 1)
            {
                System.Console.WriteLine($">>ChessDotCom Fetch Failed");
                System.Console.WriteLine($"  You must specify a single valid chess.com username");
                System.Console.WriteLine();
                Environment.Exit(-2);
            }

            string chessdotcomUsername = args[0];
            List<ChessGame> gameList = new List<ChessGame>();
            Helpers.DisplaySection($"Fetching Games for {chessdotcomUsername}", true);

            stopwatch.Reset();
            stopwatch.Start();

            System.Console.WriteLine($">>Fetching CAPS Scores");
            Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScores = new Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>>();
            await GetCapsScores(chessdotcomUsername, capsScores).ConfigureAwait(false);
            System.Console.WriteLine($">>Finished Processing CAPS Scores ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");

            stopwatch.Reset();
            stopwatch.Start();

            System.Console.WriteLine($">>Starting ChessDotCom Fetch");

            try
            {
                stopwatch.Start();
                gameList = PgnFromChessDotCom.FetchGameRecordsForUser(chessdotcomUsername);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                System.Console.WriteLine($">>ChessDotCom Fetch Failed");
                System.Console.WriteLine($"  {ex.Message}");
                System.Console.WriteLine();
                Environment.Exit(-1);
            }
#pragma warning restore CA1031 // Do not catch general exception types

            stopwatch.Stop();
            System.Console.WriteLine($"");
            System.Console.WriteLine($">>Finished ChessDotCom Fetch ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");
            System.Console.WriteLine($">>Processing Games");

            //Initialise reporting lists
            SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentBestWin)> secondsPlayedRollup = new SortedList<string, (int, int, int, int, int, int, int, int, int, int)>();
            SortedList<string, dynamic> secondsPlayedRollupMonthOnly = new SortedList<string, dynamic>();
            SortedList<string, int> ecoPlayedRollupWhite = new SortedList<string, int>();
            SortedList<string, int> ecoPlayedRollupBlack = new SortedList<string, int>();
            double totalSecondsPlayed = 0;

            stopwatch.Reset();
            stopwatch.Start();

            foreach (ChessGame game in gameList)
            {
                // Don't include daily games
                if (game.GameAttributes.Attributes["Event"] != "Live Chess")
                {
                    continue;
                }

                ExtractRatings(chessdotcomUsername, game, out string side, out int playerRating, out int opponentRating, out bool? isWin);
                CalculateOpening(ecoPlayedRollupWhite, ecoPlayedRollupBlack, game, side);
                CalculateGameTime(game, out DateTime parsedStartDate, out double seconds, out string gameTime);
                totalSecondsPlayed += seconds;
                UpdateGameTypeTimeTotals(secondsPlayedRollup, playerRating, opponentRating, isWin, parsedStartDate, seconds, gameTime);
                UpdateGameTimeTotals(secondsPlayedRollupMonthOnly, parsedStartDate, seconds);
            }

            stopwatch.Stop();
            System.Console.WriteLine($">>Finished Processing Games ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");
            System.Console.WriteLine("");

            Helpers.DisplaySection($"Live Chess Report for {chessdotcomUsername} : {DateTime.Now.ToLongDateString()}", true);
            DisplayOpeningsAsWhite(ecoPlayedRollupWhite);
            DisplayOpeningsAsBlack(ecoPlayedRollupBlack);
            DisplayPlayingStats(secondsPlayedRollup);
            DisplayTimePlayedByMonth(secondsPlayedRollupMonthOnly);
            DisplayCapsTable(capsScores);
            DisplayCapsRollingAverage(capsScores);
            DisplayTotalSecondsPlayed(totalSecondsPlayed);
            Helpers.DisplaySection("End of Report", true);

            Console.WriteLine("");
            Helpers.PressToContinueIfDebug();
            Environment.Exit(0);
        }

        private static void DisplayCapsTable(Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScores)
        {
            Console.WriteLine("");
            Helpers.DisplaySection("CAPS Scoring (Month Average > 4 games)", false);

            SortedList<string, (double, double, double, double, double, double)> capsTable = new SortedList<string, (double, double, double, double, double, double)>();
            SortedList<string, string[]> capsTableReformat = new SortedList<string, string[]>();

            foreach (KeyValuePair<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScore in capsScores)
            {
                foreach (var extractedScore in capsScore.Value.GroupBy(t => new { Id = t.GameYearMonth })
                                                    .Where(i => i.Count() > 4)
                                                    .Select(g => new
                                                    {
                                                        Average = (Math.Round(g.Average(p => p.Caps), 2)).ToString().PadRight(5, ' '),
                                                        Month = g.Key.Id,
                                                        Control = capsScore.Key.Split()[0],
                                                        Side = capsScore.Key.Split()[1],
                                                        Id = $"{capsScore.Key} {g.Key.Id}"
                                                    }))
                {

                    if (!capsTableReformat.ContainsKey(extractedScore.Month))
                    {
                        capsTableReformat.Add(extractedScore.Month, new string[] { "  -  ", "  -  ", "  -  ", "  -  ", "  -  ", "  -  " });
                    }

                    capsTableReformat[extractedScore.Month][extractedScore.Control switch
                    {
                        "bullet" => extractedScore.Side == "white" ? 0 : 1,
                        "blitz" => extractedScore.Side == "white" ? 2 : 3,
                        _ => extractedScore.Side == "white" ? 4 : 5,
                    }] = extractedScore.Average;
                }
            }

            Console.WriteLine($"                  |      Bullet     |     Blitz     |     Rapid     ");
            Console.WriteLine($"Month             |   White | Black | White | Black | White | Black ");
            Console.WriteLine($"------------------+---------+-------+-------+-------+-------+-------");

            foreach (KeyValuePair<string, string[]> line in capsTableReformat)
            {
                Console.WriteLine($"{ line.Key,-17 } |   {string.Join(" | ", line.Value)}");
            }
        }

        private static void DisplayCapsRollingAverage(Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScores)
        {
            int width = 10;
            Console.WriteLine("");
            Helpers.DisplaySection($"CAPS Scoring (Rolling {width} Game Average)", false);

            Console.WriteLine("Control/Side      |   <-Newest                                                             Oldest-> ");
            Console.WriteLine("------------------+---------------------------------------------------------------------------------");

            foreach (KeyValuePair<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScore in capsScores)
            {
                if (capsScore.Value.Count > width)
                {
                    List<double> latestCaps = capsScore.Value.Select(x => x.Caps).ToList<double>();

                    List<string> averages = Enumerable.Range(0, latestCaps.Count - width - 1).
                                      Select(i => (Math.Round(latestCaps.Skip(i).Take(width).Average(), 2)).ToString().PadRight(5)).
                                      ToList();


                    Console.WriteLine($"{ CultureInfo.CurrentCulture.TextInfo.ToTitleCase(capsScore.Key.PadRight(17))} |   {string.Join(" | ", averages.Take(10))}");
                }
            }
        }

        private static async Task GetCapsScores(string chessdotcomUsername, Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScores)
        {
            foreach (string control in new string[] { "bullet", "blitz", "rapid" })
            {
                foreach (string colour in new string[] { "white", "black" })
                {
                    string iterationKey = $"{control} {colour}";
                    capsScores.Add(iterationKey, new List<(double Caps, DateTime GameDate, string GameYearMonth)>());

                    for (int page = 1; page <= 10; page++)
                    {
                        List<double> capsScoreWhite = new List<double>();

                        using HttpClient client = new HttpClient();
                        HttpResponseMessage response = await client.GetAsync(new Uri($"https://www.chess.com/games/archive/{chessdotcomUsername}?color={colour}&gameOwner=other_game&gameType=live&gameTypeslive%5B%5D={control}&rated=rated&timeSort=desc&page={page}")).ConfigureAwait(false);
                        string pageContents = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        HtmlDocument pageDocument = new HtmlDocument();
                        pageDocument.LoadHtml(pageContents);

                        HtmlNodeCollection nodeCollection = pageDocument.DocumentNode.SelectNodes("//*[contains(@class,'archive-games-table')]");

                        if (nodeCollection == null || nodeCollection[0].InnerText.Contains("No results found."))
                        {
                            break;
                        }
                        else
                        {
                            foreach (HtmlNode row in nodeCollection[0].SelectNodes("//tr[contains(@class,'v-board-popover')]").Cast<HtmlNode>())
                            {
                                try
                                {
                                    double caps = double.Parse(row.SelectNodes("td[contains(@class,'archive-games-analyze-cell')]/div")[(colour == "white") ? 0 : 1].InnerText);
                                    DateTime gameDate = DateTime.Parse(row.SelectNodes("td[contains(@class,'archive-games-date-cell')]")[0].InnerText.Trim(new char[] { ' ', '\n', '\r' }).Replace(",", ""));
                                    string GameYearMonth = $"{gameDate.Year}-{gameDate.Month.ToString().PadLeft(2, '0')}";


                                    capsScores[iterationKey].Add((caps, gameDate, GameYearMonth));
                                }
                                catch (Exception)
                                {
                                    //Console.WriteLine(ex.ToString()); 
                                }
                            }
                        }
                    }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0066:Convert switch statement to expression", Justification = "Does not handle => null")]
        private static void ExtractRatings(string chessdotcomUsername, ChessGame game, out string side, out int playerRating, out int opponentRating, out bool? isWin)
        {
            side = game.GameAttributes.Attributes["White"].ToUpperInvariant() == chessdotcomUsername.ToUpperInvariant() ? "White" : "Black";
            playerRating = (game.IsRatedGame) ? ((side == "White") ? game.WhiteRating : game.BlackRating) : 0;
            opponentRating = (game.IsRatedGame) ? ((side == "White") ? game.BlackRating : game.WhiteRating) : 0;
            switch (game.GameAttributes.Attributes[SupportedAttribute.Result.ToString()])
            {
                case "1/2-1/2":
                    isWin = null;
                    break;
                case "1-0":
                    isWin = side == "White";
                    break;
                case "0-1":
                    isWin = side != "White";
                    break;
                default:
                    throw new Exception($"Unrecorded game result found");
            }
        }

        private static void UpdateGameTimeTotals(SortedList<string, dynamic> secondsPlayedRollupMonthOnly, DateTime parsedStartDate, double seconds)
        {
            string keyMonthOnly = $"{parsedStartDate.Year}-{((parsedStartDate.Month < 10) ? "0" : "")}{parsedStartDate.Month}";
            if (secondsPlayedRollupMonthOnly.ContainsKey(keyMonthOnly))
            {
                secondsPlayedRollupMonthOnly[keyMonthOnly] += (int)seconds;
            }
            else
            {
                secondsPlayedRollupMonthOnly.Add(keyMonthOnly, (int)seconds);
            }
        }

        private static void UpdateGameTypeTimeTotals(SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentBestWin)> secondsPlayedRollup, int playerRating, int opponentRating, bool? isWin, DateTime parsedStartDate, double seconds, string gameTime)
        {
            string key = $"{gameTime} {parsedStartDate.Year}-{((parsedStartDate.Month < 10) ? "0" : "")}{parsedStartDate.Month}";
            if (secondsPlayedRollup.ContainsKey(key))
            {
                secondsPlayedRollup[key] = (SecondsPlayed: secondsPlayedRollup[key].SecondsPlayed + (int)seconds,
                                            GameCount: secondsPlayedRollup[key].GameCount + 1,
                                            Win: ((isWin != null && isWin.Value == true) ? secondsPlayedRollup[key].Win + 1 : secondsPlayedRollup[key].Win),
                                            Loss: ((isWin != null && isWin.Value == false) ? secondsPlayedRollup[key].Loss + 1 : secondsPlayedRollup[key].Loss),
                                            Draw: ((isWin == null) ? secondsPlayedRollup[key].Draw + 1 : secondsPlayedRollup[key].Draw),
                                            MinRating: Math.Min(playerRating, secondsPlayedRollup[key].MinRating),
                                            MaxRating: Math.Max(playerRating, secondsPlayedRollup[key].MaxRating),
                                            OpponentMinRating: Math.Min(opponentRating, secondsPlayedRollup[key].OpponentMinRating),
                                            OpponentMaxRating: Math.Max(opponentRating, secondsPlayedRollup[key].OpponentMaxRating),
                                            OpponentBestWin: ((isWin != null && isWin.Value == true) ? Math.Max(opponentRating, secondsPlayedRollup[key].OpponentBestWin) : secondsPlayedRollup[key].OpponentBestWin));
            }
            else
            {
                secondsPlayedRollup.Add(key, (SecondsPlayed: (int)seconds,
                                              GameCount: 1,
                                              Win: ((isWin != null && isWin.Value == true) ? 1 : 0),
                                              Loss: ((isWin != null && isWin.Value == false) ? 1 : 0),
                                              Draw: ((isWin == null) ? 1 : 0),
                                              MinRating: playerRating,
                                              MaxRating: playerRating,
                                              OpponentMinRating: opponentRating,
                                              OpponentMaxRating: opponentRating,
                                              OpponentBestWin: ((isWin != null && isWin.Value == true) ? opponentRating : 0)));
            }
        }

        private static void CalculateGameTime(ChessGame game, out DateTime parsedStartDate, out double seconds, out string gameTime)
        {
            string gameStartDate = game.GameAttributes.Attributes["Date"];
            string gameStartTime = game.GameAttributes.Attributes["StartTime"];
            string gameEndDate = game.GameAttributes.Attributes["EndDate"];
            string gameEndTime = game.GameAttributes.Attributes["EndTime"];

            _ = DateTime.TryParseExact($"{gameStartDate} {gameStartTime}", "yyyy.MM.dd HH:mm:ss", null, DateTimeStyles.AssumeUniversal, out parsedStartDate);
            _ = DateTime.TryParseExact($"{gameEndDate} {gameEndTime}", "yyyy.MM.dd HH:mm:ss", null, DateTimeStyles.AssumeUniversal, out DateTime parsedEndDate);
            seconds = System.Math.Abs((parsedEndDate - parsedStartDate).TotalSeconds);
            gameTime = $"{game.TimeClass,-6}{((game.IsRatedGame) ? "   " : " NR")}";
        }

        private static void CalculateOpening(SortedList<string, int> ecoPlayedRollupWhite, SortedList<string, int> ecoPlayedRollupBlack, ChessGame game, string side)
        {
            try
            {
                string ecoName = game.GameAttributes.Attributes["ECOUrl"].Replace(@"https://www.chess.com/openings/", "", true, CultureInfo.InvariantCulture).Replace("-", " ", true, CultureInfo.InvariantCulture);
                string ecoShortened = new Regex(@"^.*?(?=[0-9])").Match(ecoName).Value.Trim();
                string ecoKey = $"{game.GameAttributes.Attributes["ECO"]}-{((string.IsNullOrEmpty(ecoShortened)) ? ecoName : ecoShortened)}";
                SortedList<string, int> ecoPlayedRollup = (side == "White") ? ecoPlayedRollupWhite : ecoPlayedRollupBlack;

                if (ecoPlayedRollup.ContainsKey(ecoKey))
                {
                    ecoPlayedRollup[ecoKey]++;
                }
                else
                {
                    ecoPlayedRollup.Add(ecoKey, 1);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                //ECO missing from Pgn so just ignore
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static void DisplayOpeningsAsWhite(SortedList<string, int> ecoPlayedRollupWhite)
        {
            Console.WriteLine("");
            Helpers.DisplaySection($"Openings Occurring More Than Once (Max 15)", false);
            Console.WriteLine("Playing As White                                                        | Tot.");
            Console.WriteLine("------------------------------------------------------------------------+------");

            foreach (KeyValuePair<string, int> ecoCount in ecoPlayedRollupWhite.OrderByDescending(uses => uses.Value).Take(15))
            {
                if (ecoCount.Value < 2) { break; }
                Console.WriteLine($"{ecoCount.Key,-71} | {ecoCount.Value.ToString(CultureInfo.CurrentCulture),4}");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static void DisplayOpeningsAsBlack(SortedList<string, int> ecoPlayedRollupBlack)
        {
            Console.WriteLine("");
            Console.WriteLine("Playing As Black                                                        | Tot.");
            Console.WriteLine("------------------------------------------------------------------------+------");

            foreach (KeyValuePair<string, int> ecoCount in ecoPlayedRollupBlack.OrderByDescending(uses => uses.Value).Take(15))
            {
                if (ecoCount.Value < 2) { break; }
                Console.WriteLine($"{ecoCount.Key,-71} | {ecoCount.Value.ToString(CultureInfo.CurrentCulture),4}");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static void DisplayPlayingStats(SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentBestWin)> secondsPlayedRollup)
        {
            Console.WriteLine("");
            Helpers.DisplaySection("Time Played by Time Class/Month", false);
            Console.WriteLine("Time Class/Month  | Play Time | Rating Min/Max/+-  | Vs Min/BestWin/Max | Win  | Loss | Draw | Tot. ");
            string lastLine = "";

            foreach (KeyValuePair<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentBestWin)> rolledUp in secondsPlayedRollup)
            {
                if (lastLine != rolledUp.Key.Substring(0, 10))
                {
                    Console.WriteLine("------------------+-----------+--------------------+--------------------+------+------+------+------");
                }

                lastLine = rolledUp.Key.Substring(0, 10);
                TimeSpan timeMonth = TimeSpan.FromSeconds(rolledUp.Value.SecondsPlayed);
                System.Console.WriteLine($"{rolledUp.Key,-17} | " +
                                         $"{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture),3}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ timeMonth.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')} | " +
                                         $"{rolledUp.Value.MinRating.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{rolledUp.Value.MaxRating.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{(rolledUp.Value.MaxRating - rolledUp.Value.MinRating).ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{rolledUp.Value.OpponentMinRating.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{rolledUp.Value.OpponentBestWin.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{rolledUp.Value.OpponentMaxRating.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{rolledUp.Value.Win.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{rolledUp.Value.Loss.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{rolledUp.Value.Draw.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                         $"{rolledUp.Value.GameCount.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}"
                                         );
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static void DisplayTimePlayedByMonth(SortedList<string, dynamic> secondsPlayedRollupMonthOnly)
        {
            Console.WriteLine("");
            Helpers.DisplaySection("Time Played by Month", false);
            Console.WriteLine("Month             | Play Time ");
            Console.WriteLine("------------------+-----------");
            foreach (KeyValuePair<string, dynamic> rolledUp in secondsPlayedRollupMonthOnly)
            {
                TimeSpan timeMonth = TimeSpan.FromSeconds(rolledUp.Value);
                System.Console.WriteLine($"{rolledUp.Key,-17} | " +
                                         $"{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture),3}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ timeMonth.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}");
            }
        }

        private static void DisplayTotalSecondsPlayed(double totalSecondsPlayed)
        {
            Console.WriteLine("");
            Helpers.DisplaySection("Total Play Time (Live Chess)", false);
            TimeSpan time = TimeSpan.FromSeconds(totalSecondsPlayed);
            Console.WriteLine($"Time Played (hh:mm:ss): {((int)time.TotalHours).ToString(CultureInfo.CurrentCulture),3}:{ time.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ time.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}");
            Console.WriteLine("");
        }
    }
}
