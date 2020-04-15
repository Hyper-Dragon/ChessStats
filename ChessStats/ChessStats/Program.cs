using ChessDotComSharp.Models;
using ChessStats.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
            const int MAX_CAPS_PAGES = 30;
            const string VERSION_NUMBER = "0.5";

            //Set up data directories
            DirectoryInfo applicationPath = new DirectoryInfo(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
            DirectoryInfo resultsDir = applicationPath.CreateSubdirectory("ChessStatsResults");
            DirectoryInfo cacheDir = applicationPath.CreateSubdirectory("ChessStatsCache-V0.5");

            Stopwatch stopwatch = new Stopwatch();
            Helpers.DisplayLogo(VERSION_NUMBER);

            if (args.Length != 1)
            {
                Console.WriteLine($">>ChessDotCom Fetch Failed");
                Console.WriteLine($"  You must specify a single valid chess.com username");
                Console.WriteLine();
                Environment.Exit(-2);
            }

            PlayerProfile userRecord = null;
            PlayerStats userStats = null;
            (userRecord, userStats) = await PgnFromChessDotCom.FetchUserData(args[0]).ConfigureAwait(false);

            //Replace username with correct case - api returns ID in lower case so extract from URL property
            string chessdotcomUsername = userRecord.Url.Replace("https://www.chess.com/member/", "");

            stopwatch.Reset();
            stopwatch.Start();

            Helpers.DisplaySection($"Fetching Data for {chessdotcomUsername}", true);

            Console.WriteLine($">>Fetching and Processing Available CAPS Scores");

            Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScores = await CapsFromChessDotCom.GetCapsScores(cacheDir, chessdotcomUsername, MAX_CAPS_PAGES).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine($">>Finished Fetching and Processing Available CAPS Scores ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");

            stopwatch.Reset();
            stopwatch.Start();

            List <ChessGame> gameList = new List<ChessGame>();
            Console.WriteLine($">>Fetching Games From Chess.Com");

            try
            {
                gameList = await PgnFromChessDotCom.FetchGameRecordsForUser(chessdotcomUsername, cacheDir).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                Console.WriteLine($"  >>Fetching Games From Chess.Com Failed");
                Console.WriteLine($"    {ex.Message}");
                Console.WriteLine();
                Environment.Exit(-1);
            }
#pragma warning restore CA1031 // Do not catch general exception types

            stopwatch.Stop();
            Console.WriteLine($"");
            Console.WriteLine($">>Finished Fetching Games From Chess.Com ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");
            Console.WriteLine($">>Processing Games");

            //Initialise reporting lists
            SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentBestWin)> secondsPlayedRollup = new SortedList<string, (int, int, int, int, int, int, int, int, int, int)>();
            SortedList<string, dynamic> secondsPlayedRollupMonthOnly = new SortedList<string, dynamic>();
            SortedList<string, (string href, int total)> ecoPlayedRollupWhite = new SortedList<string, (string,int)>();
            SortedList<string, (string href, int total)> ecoPlayedRollupBlack = new SortedList<string, (string, int)>();
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

            Console.WriteLine($">>Finished Processing Games ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");
            stopwatch.Reset();
            stopwatch.Start();

            Console.WriteLine($">>Compiling Reports");

            (string whiteOpeningstextOut, string whiteOpeningshtmlOut) = DisplayOpeningsAsWhite(ecoPlayedRollupWhite);
            (string blackOpeningstextOut, string blackOpeningshtmlOut) = DisplayOpeningsAsBlack(ecoPlayedRollupBlack);
            (string playingStatstextOut, string playingStatshtmlOut) = DisplayPlayingStats(secondsPlayedRollup);
            (string timePlayedByMonthtextOut, string timePlayedByMonthhtmlOut) = DisplayTimePlayedByMonth(secondsPlayedRollupMonthOnly);
            (string capsTabletextOut, string capsTablehtmlOut) = DisplayCapsTable(capsScores);
            (string capsRollingAverageFivetextOut, string capsRollingAverageFivehtmlOut) = DisplayCapsRollingAverage(5, capsScores);
            (string capsRollingAverageTentextOut, string capsRollingAverageTenhtmlOut) = DisplayCapsRollingAverage(10,capsScores);
            (string totalSecondsPlayedtextOut, string totalSecondsPlayedhtmlOut) = DisplayTotalSecondsPlayed(totalSecondsPlayed);

            StringBuilder textReport = new StringBuilder();
            textReport.Append(Helpers.GetDisplaySection($"Live Chess Report for {chessdotcomUsername} : {DateTime.Now.ToLongDateString()}", true));
            textReport.AppendLine();
            textReport.Append(whiteOpeningstextOut);
            textReport.Append(blackOpeningstextOut);
            textReport.Append(playingStatstextOut);
            textReport.Append(timePlayedByMonthtextOut);
            textReport.Append(capsTabletextOut);
            textReport.Append(capsRollingAverageTentextOut);
            textReport.Append(totalSecondsPlayedtextOut);
            textReport.Append(Helpers.GetDisplaySection("End of Report", true));

            StringBuilder htmlReport = new StringBuilder();
            //htmlReport.AppendLine("<!DOCTYPE html>");
            htmlReport.AppendLine("<html lang='en'><head>");
            htmlReport.AppendLine($"<title>ChessStats for {chessdotcomUsername}</title>");
            htmlReport.AppendLine("<meta charset='UTF-8'>");
            htmlReport.AppendLine("<meta name='generator' content='ChessStats'> ");
            //htmlReport.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            htmlReport.AppendLine("<style>");
            htmlReport.AppendLine("*                                      {margin: 0;padding: 0;}                                                                                                                   ");
            htmlReport.AppendLine("body                                   {background-color:#312e2b;width: 90%; margin: auto; font-family: -apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif;}   ");
            htmlReport.AppendLine("h1                                     {padding: 10px;text-align: left;font-size: 20px;background-color: rgba(0,0,0,.13);color: hsla(0,0%,100%,.65);}                            ");
            htmlReport.AppendLine(".headerLink                            {color: #e58b09;}                                                                                                                         ");         
            htmlReport.AppendLine("h2                                     {padding: 5px;text-align: left;font-size: 16px;background-color: rgba(0,0,0,.13);color: hsla(0,0%,100%,.65);}                             ");
            htmlReport.AppendLine("table                                  {width: 100%;background: white;table-layout: fixed ;border-collapse: collapse; overflow-x:auto; }                                         ");
            htmlReport.AppendLine("thead                                  {text-align: center;background: darkgrey;color: white;font-size: 14px; font-weight: bold;}                                                ");
            htmlReport.AppendLine("tbody                                  {text-align: right;font-size: 13px;}                                                                                                      ");
            htmlReport.AppendLine("td                                     {padding-right: 10px;}                                                                                                                    ");
            htmlReport.AppendLine("td:nth-child(1)                        {text-align: left; width:20%; font-weight: bold;}                                                                                         ");
            htmlReport.AppendLine("tbody tr:nth-child(odd)                {background-color: lightestgrey;}                                                                                                         ");
            htmlReport.AppendLine("tbody tr:nth-child(even)               {background-color: aliceblue;}                                                                                                            ");
            htmlReport.AppendLine(".whiteOpeningsTable thead td:nth-child(1)    {font-weight: bold;}                                                                                                                ");
            htmlReport.AppendLine(".blackOpeningsTable thead td:nth-child(1)    {font-weight: bold;}                                                                                                                ");
            htmlReport.AppendLine(".whiteOpeningsTable td:nth-child(1)    {text-align: left; width:90%; font-weight: normal;}                                                                                       ");
            htmlReport.AppendLine(".blackOpeningsTable td:nth-child(1)    {text-align: left; width:90%; font-weight: normal;}                                                                                       ");
            htmlReport.AppendLine(".capsRollingTable thead td:nth-child(2){text-align: left;}                                                                                                                       ");
            htmlReport.AppendLine(".oneColumn                             {float: left;width: 100%;}                                                                                                                ");
            htmlReport.AppendLine(".oneRow: after                         {content: ''; display: table; clear: both;}                                                                                               ");
            htmlReport.AppendLine(".twoColumn                             {float: left;width: 50%;}                                                                                                                 ");
            htmlReport.AppendLine(".twoRow: after                         {content: '';display: table;clear: both;}                                                                                                 ");
            htmlReport.AppendLine(".footer                                {text-align: right;color: white;}                                                                                                         ");
            htmlReport.AppendLine(".footer a                              {color: #e58b09;}                                                                                                                         ");                             
            htmlReport.AppendLine("</style></head><body>");

            using HttpClient httpClient = new HttpClient();
            Uri userLogo = new Uri(string.IsNullOrEmpty(userRecord.Avatar) ? "https://images.chesscomfiles.com/uploads/v1/group/57796.67ee0038.160x160o.2dc0953ad64e.png" : userRecord.Avatar);
            var d = await httpClient.GetByteArrayAsync(userLogo).ConfigureAwait(false);
            string o = Convert.ToBase64String(d);
            htmlReport.AppendLine($"<a href='{userRecord.Url}'><img alt='logo' src='data:image/png;base64,{o}'/><a>");

            htmlReport.AppendLine($"<h1>Live Games Report for <a class='headerLink' href='{userRecord.Url}'>{chessdotcomUsername}</a> <small>({DateTime.Now.ToShortDateString()}@{DateTime.Now.ToShortTimeString()})<small></h1>");
            htmlReport.AppendLine($"<h2>Openings Occurring More Than Once (Max 15)</h2>");
            htmlReport.AppendLine($"<div class='tworow'>");
            htmlReport.AppendLine($"<div class='twocolumn'>{whiteOpeningshtmlOut}</div>");
            htmlReport.AppendLine($"<div class='twocolumn'>{blackOpeningshtmlOut}</div>");
            htmlReport.AppendLine($"</div><br/><div class='onerow'><div class='onecolumn'>");
            htmlReport.AppendLine($"<h2>CAPS Scoring (Rolling 10 Game Average)</h2>");
            htmlReport.AppendLine(capsRollingAverageTenhtmlOut);
            htmlReport.AppendLine($"<h2>CAPS Scoring (Rolling 5 Game Average)</h2>");
            htmlReport.AppendLine(capsRollingAverageFivehtmlOut);
            htmlReport.AppendLine($"<h2>CAPS Scoring (Month Average > 4 Games)</h2>");
            htmlReport.AppendLine(capsTablehtmlOut);
            htmlReport.AppendLine($"<h2>Time Played by Time Control/Month</h2>");
            htmlReport.AppendLine(playingStatshtmlOut);
            htmlReport.AppendLine($"<h2>Time Played by Month (All Time Controls)</h2>");
            htmlReport.AppendLine(timePlayedByMonthhtmlOut);
            //htmlReport.AppendLine($"<h2>Total Play Time (Live Chess)</h2>");
            //htmlReport.AppendLine(totalSecondsPlayedhtmlOut);
            htmlReport.AppendLine($"<div class='footer'><br/><hr/><i>Generated by ChessStats (for <a href='https://chess.com'>Chess.com</a>) :: <a href='https://www.chess.com/member/hyper-dragon'>Hyper-Dragon</a> :: Version {VERSION_NUMBER} :: 04/2020 :: <a href='https://github.com/Hyper-Dragon/ChessStats'>https://github.com/Hyper-Dragon/ChessStats</a></i><br/><br/><br/></div>");
            htmlReport.AppendLine("</div></div></body></html>");

            Console.WriteLine($">>Finished Compiling Reports ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");
            stopwatch.Reset();
            stopwatch.Start();

            //Write PGN by Time Control to Disk
            Console.WriteLine($">>Writing Results to {resultsDir.FullName}");
            Console.WriteLine($"  >>Writing PGN's");
            foreach (string timeClass in (from x in gameList
                                          where x.IsRatedGame
                                          select x.TimeClass).Distinct().ToArray())
            {
                using StreamWriter pgnFileOutStream = File.CreateText($"{Path.Combine(resultsDir.FullName, $"{chessdotcomUsername}-Pgn-{timeClass}.pgn")}");

                foreach (ChessGame game in (from x in gameList
                                            where x.TimeClass == timeClass && x.IsRatedGame
                                            select x).
                                      OrderBy(x => x.GameAttributes.GetAttributeAsNullOrDateTime(SupportedAttribute.Date, SupportedAttribute.StartTime)))
                {
                    await pgnFileOutStream.WriteAsync(game.Text).ConfigureAwait(false);
                    await pgnFileOutStream.WriteLineAsync().ConfigureAwait(false);
                    await pgnFileOutStream.WriteLineAsync().ConfigureAwait(false);
                }
            }

            Console.WriteLine($"  >>Writing CAPS Data");

            using StreamWriter capsFileOutStream = File.CreateText($"{Path.Combine(resultsDir.FullName, $"{chessdotcomUsername}-Caps-All.tsv")}");
            await capsFileOutStream.WriteLineAsync($"CAPS Data for {chessdotcomUsername}").ConfigureAwait(false);
            await capsFileOutStream.WriteLineAsync().ConfigureAwait(false);

            foreach (KeyValuePair<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsTimeControl in capsScores)
            {
                StringBuilder dateLine = new StringBuilder();
                StringBuilder monthLine = new StringBuilder();
                StringBuilder capsLine = new StringBuilder();

                foreach ((double Caps, DateTime GameDate, string GameYearMonth) in capsTimeControl.Value.OrderBy(x => x.GameDate))
                {
                    dateLine.Append($"{GameDate.ToShortDateString()}\t");
                    monthLine.Append($"{GameYearMonth}\t");
                    capsLine.Append($"{Caps}\t");
                }

                await capsFileOutStream.WriteLineAsync($"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(capsTimeControl.Key)}").ConfigureAwait(false);
                await capsFileOutStream.WriteLineAsync(dateLine).ConfigureAwait(false);
                await capsFileOutStream.WriteLineAsync(monthLine).ConfigureAwait(false);
                await capsFileOutStream.WriteLineAsync(capsLine).ConfigureAwait(false);
                await capsFileOutStream.WriteLineAsync().ConfigureAwait(false);
            }

            await capsFileOutStream.FlushAsync().ConfigureAwait(false);
            capsFileOutStream.Close();

            Console.WriteLine($"  >>Writing Text Report");

            using StreamWriter textReportFileOutStream = File.CreateText($"{Path.Combine(resultsDir.FullName, $"{chessdotcomUsername}-Summary.txt")}");
            await textReportFileOutStream.WriteLineAsync($"{Helpers.GetDisplayLogo(VERSION_NUMBER)}").ConfigureAwait(false);
            await textReportFileOutStream.WriteLineAsync($"{textReport}").ConfigureAwait(false);
            await textReportFileOutStream.FlushAsync().ConfigureAwait(false);
            textReportFileOutStream.Close();

            Console.WriteLine($"  >>Writing Html Report");

            using var htmlReportFileOutStream = File.Create($"{Path.Combine(resultsDir.FullName, $"{chessdotcomUsername}-Summary.html")}");
            await htmlReportFileOutStream.WriteAsync(Encoding.UTF8.GetBytes(htmlReport.ToString())).ConfigureAwait(false);
            await htmlReportFileOutStream.FlushAsync().ConfigureAwait(false);
            htmlReportFileOutStream.Close();

            Console.WriteLine($"  >>Writing Raw Game Data (TODO)");
            Console.WriteLine($"  >>Writing Openings Data (TODO)");

            Console.WriteLine($">>Finished Writing Results ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");
            Console.WriteLine("");

            stopwatch.Stop();

            Console.WriteLine(textReport.ToString());
            Console.WriteLine("");

            Helpers.PressToContinueIfDebug();
            Environment.Exit(0);
        }

        private static (string textOut, string htmlOut) DisplayCapsTable(Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScores)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection("CAPS Scoring (Month Average > 4 Games)", false));

            SortedList<string, (double, double, double, double, double, double)> capsTable = new SortedList<string, (double, double, double, double, double, double)>();
            SortedList<string, string[]> capsTableReformat = new SortedList<string, string[]>();

            foreach (KeyValuePair<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScore in capsScores)
            {
                foreach (var extractedScore in capsScore.Value.GroupBy(t => new { Id = t.GameYearMonth })
                                                    .Where(i => i.Count() > 4)
                                                    .Select(g => new
                                                    {
                                                        Average = Math.Round(g.Average(p => p.Caps), 2).ToString().PadRight(5, ' '),
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

            textOut.AppendLine($"                  |      Bullet     |     Blitz     |     Rapid     ");
            textOut.AppendLine($"Month             |   White | Black | White | Black | White | Black ");
            textOut.AppendLine($"------------------+---------+-------+-------+-------+-------+-------");

            htmlOut.AppendLine("<table class='capsByMonthTable'><thead><tr><td>Month</td><td>Bullet White</td><td>Bullet Black</td><td>Blitz White</td><td>Blitz Black</td><td>Rapid White</td><td>Rapid Black</td></thead><tbody>");

            foreach (KeyValuePair<string, string[]> line in capsTableReformat)
            {
                textOut.AppendLine($"{ line.Key,-17 } |   {string.Join(" | ", line.Value)}");
                htmlOut.AppendLine($"<tr><td>{line.Key}</td><td>{string.Join("</td><td>", line.Value)}</td></tr>");
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        private static (string textOut, string htmlOut) DisplayCapsRollingAverage(int averageOver, Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScores)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection($"CAPS Scoring (Rolling {averageOver} Game Average)", false));

            textOut.AppendLine("Control/Side      |   <-Newest                                                             Oldest-> ");
            textOut.AppendLine("------------------+---------------------------------------------------------------------------------");

            htmlOut.AppendLine("<table class='capsRollingTable'><thead><tr><td>Control/Side</td><td colspan='9'>Newest</td><td>Oldest</td></thead><tbody>");

            foreach (KeyValuePair<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScore in capsScores)
            {
                if (capsScore.Value.Count > averageOver)
                {
                    List<double> latestCaps = capsScore.Value.Select(x => x.Caps).ToList<double>();

                    List<string> averages = Enumerable.Range(0, latestCaps.Count - averageOver - 1).
                                      Select(i => Math.Round(latestCaps.Skip(i).Take(averageOver).Average(), 2).ToString().PadRight(5)).
                                      ToList();


                    textOut.AppendLine($"{ CultureInfo.CurrentCulture.TextInfo.ToTitleCase(capsScore.Key.PadRight(17))} |   {string.Join(" | ", averages.Take(10))}");
                    htmlOut.AppendLine($"<tr><td>{ CultureInfo.CurrentCulture.TextInfo.ToTitleCase(capsScore.Key)}</td><td>{string.Join("</td><td>", averages.Take(10))}</td></tr>");
                }
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
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

        private static void CalculateOpening(SortedList<string, (string href, int total)> ecoPlayedRollupWhite, SortedList<string, (string href, int total)> ecoPlayedRollupBlack, ChessGame game, string side)
        {
            try
            {
                string ecoHref = game.GameAttributes.Attributes["ECOUrl"];
                string ecoName = game.GameAttributes.Attributes["ECOUrl"].Replace(@"https://www.chess.com/openings/", "", true, CultureInfo.InvariantCulture).Replace("-", " ", true, CultureInfo.InvariantCulture);
                string ecoShortened = new Regex(@"^.*?(?=[0-9])").Match(ecoName).Value.Trim();
                string ecoKey = $"{game.GameAttributes.Attributes["ECO"]}-{((string.IsNullOrEmpty(ecoShortened)) ? ecoName : ecoShortened)}";
                SortedList<string, (string href, int total)> ecoPlayedRollup = (side == "White") ? ecoPlayedRollupWhite : ecoPlayedRollupBlack;

                if (ecoPlayedRollup.ContainsKey(ecoKey))
                {
                    ecoPlayedRollup[ecoKey] = (ecoPlayedRollup[ecoKey].href, ecoPlayedRollup[ecoKey].total+1);
                }
                else
                {
                    ecoPlayedRollup.Add(ecoKey, (ecoHref,1));
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
        private static (string textOut, string htmlOut) DisplayOpeningsAsWhite(SortedList<string, (string href,int total)> ecoPlayedRollupWhite)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection($"Openings Occurring More Than Once (Max 15)", false));
            textOut.AppendLine("Playing As White                                                        | Tot.");
            textOut.AppendLine("------------------------------------------------------------------------+------");

            htmlOut.AppendLine("<table class='whiteOpeningsTable'><thead><tr><td>Playing As White</td><td>Total</td></tr></thead><tbody>");

            foreach (KeyValuePair<string, (string href, int total)> ecoCount in ecoPlayedRollupWhite.OrderByDescending(uses => uses.Value.total).Take(15))
            {
                if (ecoCount.Value.total < 2) { break; }
                textOut.AppendLine($"{ecoCount.Key,-71} | {ecoCount.Value.total.ToString(CultureInfo.CurrentCulture),4}");
                htmlOut.AppendLine($"<tr><td><a href='{ecoCount.Value.href}'>{ecoCount.Key}</a></td><td>{ecoCount.Value.total.ToString(CultureInfo.CurrentCulture),4}</td></tr>");
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static (string textOut, string htmlOut) DisplayOpeningsAsBlack(SortedList<string, (string href, int total)> ecoPlayedRollupBlack)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine("Playing As Black                                                        | Tot.");
            textOut.AppendLine("------------------------------------------------------------------------+------");

            htmlOut.AppendLine("<table class='blackOpeningsTable'><thead><tr><td>Playing As Black</td><td>Total</td></tr></thead><tbody>");

            foreach (KeyValuePair<string, (string href, int total)> ecoCount in ecoPlayedRollupBlack.OrderByDescending(uses => uses.Value.total).Take(15))
            {
                if (ecoCount.Value.total < 2) { break; }
                textOut.AppendLine($"{ecoCount.Key,-71} | {ecoCount.Value.total.ToString(CultureInfo.CurrentCulture),4}");
                htmlOut.AppendLine($"<tr><td><a href='{ecoCount.Value.href}'>{ecoCount.Key}</a></td><td>{ecoCount.Value.total.ToString(CultureInfo.CurrentCulture),4}</td></tr>");
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static (string textOut, string htmlOut) DisplayPlayingStats(SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentBestWin)> secondsPlayedRollup)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection("Time Played/Ratings by Time Control/Month", false));
            textOut.AppendLine("Time Control/Month| Play Time | Rating Min/Max/+-  | Vs Min/BestWin/Max | Win  | Loss | Draw | Tot. ");
            string lastLine = "";

            foreach (KeyValuePair<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentBestWin)> rolledUp in secondsPlayedRollup)
            {
                if (lastLine != rolledUp.Key.Substring(0, 10))
                {
                    textOut.AppendLine("------------------+-----------+--------------------+--------------------+------+------+------+------");
                    htmlOut.AppendLine($"{((string.IsNullOrEmpty(lastLine))?"":"</tbody></table>")}<table><thead class='playingStatsTable'><tr><td>Time Control/Month</td><td>Time</td><td>Min</td><td>Max</td><td>+-</td><td>Min</td><td>BestWin</td><td>Max</td><td>Win</td><td>Loss</td><td>Draw</td><td>Total</td></tr></thead><tbody>");
                }

                lastLine = rolledUp.Key.Substring(0, 10);
                TimeSpan timeMonth = TimeSpan.FromSeconds(rolledUp.Value.SecondsPlayed);
                textOut.AppendLine($"{rolledUp.Key,-17} | " +
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

                htmlOut.AppendLine($"<tr><td>{rolledUp.Key,-17}</td>" +
                         $"<td>{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture),3}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ timeMonth.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td>" +
                         $"<td>{rolledUp.Value.MinRating.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{rolledUp.Value.MaxRating.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{(rolledUp.Value.MaxRating - rolledUp.Value.MinRating).ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{rolledUp.Value.OpponentMinRating.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{rolledUp.Value.OpponentBestWin.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{rolledUp.Value.OpponentMaxRating.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{rolledUp.Value.Win.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{rolledUp.Value.Loss.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{rolledUp.Value.Draw.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>" +
                         $"<td>{rolledUp.Value.GameCount.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}</td>"
                         );
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static (string textOut, string htmlOut) DisplayTimePlayedByMonth(SortedList<string, dynamic> secondsPlayedRollupMonthOnly)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection("Time Played by Month (All Time Controls)", false));
            textOut.AppendLine("Month             |  Play Time  | Cumulative  |  For Year ");

            htmlOut.AppendLine("<table class='playingStatsMonthTable'><thead><tr><td>Month</td><td>Play Time</td><td>Cumulative</td><td>For Year</td></tr></thead><tbody>");

            TimeSpan cumulativeTime = new TimeSpan(0);
            TimeSpan cumulativeTimeForYear = new TimeSpan(0);
            string currentYear = "";

            foreach (KeyValuePair<string, dynamic> rolledUp in secondsPlayedRollupMonthOnly)
            {
                if (rolledUp.Key.Substring(0, 4) != currentYear)
                {
                    textOut.AppendLine("------------------+-------------+-------------+-------------");
                    currentYear = rolledUp.Key.Substring(0, 4);
                    cumulativeTimeForYear = new TimeSpan(0);
                }

                TimeSpan timeMonth = TimeSpan.FromSeconds(rolledUp.Value);
                cumulativeTime += timeMonth;
                cumulativeTimeForYear += timeMonth;

                textOut.AppendLine($"{rolledUp.Key,-17} | " +
                                  $"{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ timeMonth.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')} | " +
                                  $"{((int)cumulativeTime.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ cumulativeTime.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ cumulativeTime.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')} | " +
                                  $"{((int)cumulativeTimeForYear.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ cumulativeTimeForYear.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ cumulativeTimeForYear.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}"
                                  );

                htmlOut.AppendLine($"<tr><td>{rolledUp.Key,-17}</td>" +
                                   $"<td>{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ timeMonth.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td>" +
                                   $"<td>{((int)cumulativeTime.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ cumulativeTime.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ cumulativeTime.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td>" +
                                   $"<td>{((int)cumulativeTimeForYear.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ cumulativeTimeForYear.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ cumulativeTimeForYear.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td></tr>"
                                  );
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        private static (string textOut, string htmlOut) DisplayTotalSecondsPlayed(double totalSecondsPlayed)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection("Total Play Time (Live Chess)", false));

            htmlOut.AppendLine("<table class='playingTimeTable'><tbody>");


            TimeSpan time = TimeSpan.FromSeconds(totalSecondsPlayed);
            textOut.AppendLine($"Time Played (hh:mm:ss): {((int)time.TotalHours).ToString(CultureInfo.CurrentCulture),6}:{ time.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ time.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}");
            textOut.AppendLine("");

            htmlOut.AppendLine($"<tr><td>Time Played (hh:mm:ss)</td><td>{((int)time.TotalHours).ToString(CultureInfo.CurrentCulture),6}:{ time.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ time.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td><tr>");
            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }
    }
}
