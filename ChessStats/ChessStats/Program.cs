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
            const int MAX_CAPS_PAGES = 50;
            const int MAX_CAPS_PAGES_WITH_CACHE = 3;
            const string VERSION_NUMBER = "0.5";
            const string CACHE_VERSION_NUMBER = "0.5";

            Helpers.DisplayLogo(VERSION_NUMBER);

            //Set up data directories
            DirectoryInfo applicationPath = new DirectoryInfo(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
            DirectoryInfo resultsDir = applicationPath.CreateSubdirectory("ChessStatsResults");
            DirectoryInfo cacheDir = applicationPath.CreateSubdirectory($"ChessStatsCache-V{CACHE_VERSION_NUMBER}");

            if (args.Length != 1)
            {
                Console.WriteLine($">>ChessDotCom Fetch Failed");
                Console.WriteLine($"  You must specify a single valid chess.com username or -refresh");
                Console.WriteLine();
                Environment.Exit(-2);
            }

            string[] chessdotcomUsers = args[0].ToUpperInvariant() switch
            {
                "-REFRESH" => resultsDir.GetFiles("*-Summary.html")
                                      .Select(x => x.Name.Replace("-Summary.html", "", StringComparison.InvariantCultureIgnoreCase))
                                      .ToArray(),
                _ => new string[] { args[0] }
            };


            foreach (string user in chessdotcomUsers)
            {
                (PlayerProfile userRecord, PlayerStats userStats) = await PgnFromChessDotCom.FetchUserData(user).ConfigureAwait(false);

                //Replace username with correct case - api returns ID in lower case so extract from URL property
                string chessdotcomUsername = userRecord.Url.Replace("https://www.chess.com/member/", "", StringComparison.InvariantCultureIgnoreCase);

                Helpers.DisplaySection($"Fetching Data for {chessdotcomUsername}", true);

                //Get reporting graphics
                Helpers.StartTimedSection(">>Download report images");
                using HttpClient httpClient = new HttpClient();
                Uri userLogoUri = new Uri(string.IsNullOrEmpty(userRecord.Avatar) ? "https://images.chesscomfiles.com/uploads/v1/group/57796.67ee0038.160x160o.2dc0953ad64e.png" : userRecord.Avatar);
                string userLogoBase64 = Convert.ToBase64String(await httpClient.GetByteArrayAsync(userLogoUri).ConfigureAwait(false));

                Uri pawnUri = new Uri("https://www.chess.com/bundles/web/favicons/favicon-16x16.31f99381.png");
                string pawnFileBase64 = Convert.ToBase64String(await httpClient.GetByteArrayAsync(pawnUri).ConfigureAwait(false));
                string pawnFragment = $"<img src='data:image/png;base64,{pawnFileBase64}'/>";
                Helpers.EndTimedSection(">>Download complete");


                Helpers.StartTimedSection($">>Fetching and Processing Available CAPS Scores");
                Dictionary<string, List<CapsRecord>> capsScores = await CapsFromChessDotCom.GetCapsScores(cacheDir, chessdotcomUsername, MAX_CAPS_PAGES, MAX_CAPS_PAGES_WITH_CACHE).ConfigureAwait(false);
                Helpers.EndTimedSection(">>Finished Fetching and Processing Available CAPS Scores", true);

                List<ChessGame> gameList = new List<ChessGame>();
                Helpers.StartTimedSection($">>Fetching Games From Chess.Com");

                try
                {
                    gameList = await PgnFromChessDotCom.FetchGameRecordsForUser(chessdotcomUsername, cacheDir).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  >>Fetching Games From Chess.Com Failed");
                    Console.WriteLine($"    {ex.Message}");
                    Console.WriteLine();
                    Environment.Exit(-1);
                }

                Helpers.EndTimedSection($">>Finished Fetching Games From Chess.Com", true);
                Helpers.StartTimedSection($">>Processing Games");

                ProcessGameData(chessdotcomUsername, gameList,
                                out SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss,
                                                        int Draw, int MinRating, int MaxRating, int OpponentMinRating,
                                                        int OpponentMaxRating, int OpponentWorstLoss, int OpponentBestWin,
                                                        int TotalWin, int TotalDraw, int TotalLoss)> secondsPlayedRollup,
                                out SortedList<string, dynamic> secondsPlayedRollupMonthOnly,
                                out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhite,
                                out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlack,
                                out double totalSecondsPlayed);

                Helpers.EndTimedSection($">>Finished Processing Games");

                Helpers.StartTimedSection($">>Compiling Reports");

                //Extract reporting data
                (string whiteOpeningstextOut, string whiteOpeningshtmlOut) = DisplayOpeningsAsWhite(ecoPlayedRollupWhite);
                (string blackOpeningstextOut, string blackOpeningshtmlOut) = DisplayOpeningsAsBlack(ecoPlayedRollupBlack);
                (string playingStatstextOut, string playingStatshtmlOut) = DisplayPlayingStats(secondsPlayedRollup, userStats.ChessBullet?.Last.Rating, userStats.ChessBlitz?.Last.Rating, userStats.ChessRapid?.Last.Rating);
                (string timePlayedByMonthtextOut, string timePlayedByMonthhtmlOut) = DisplayTimePlayedByMonth(secondsPlayedRollupMonthOnly);
                (string capsTabletextOut, string capsTablehtmlOut) = DisplayCapsTable(capsScores);
                (_, string capsRollingAverageFivehtmlOut) = DisplayCapsRollingAverage(5, capsScores);
                (string capsRollingAverageTentextOut, string capsRollingAverageTenhtmlOut) = DisplayCapsRollingAverage(10, capsScores);
                (string totalSecondsPlayedtextOut, _) = DisplayTotalSecondsPlayed(totalSecondsPlayed);

                //Build the text report
                string textReport = BuildTextReport(chessdotcomUsername, whiteOpeningstextOut, blackOpeningstextOut, playingStatstextOut, timePlayedByMonthtextOut, capsTabletextOut, capsRollingAverageTentextOut, totalSecondsPlayedtextOut);

                //Build the HTML report
                string htmlReport = BuildHtmlReport(VERSION_NUMBER, userRecord, userStats, chessdotcomUsername, whiteOpeningshtmlOut, blackOpeningshtmlOut, playingStatshtmlOut, timePlayedByMonthhtmlOut, capsTablehtmlOut, capsRollingAverageFivehtmlOut, capsRollingAverageTenhtmlOut, userLogoBase64, pawnFragment);

                Helpers.EndTimedSection($">>Finished Compiling Reports");

                Helpers.StartTimedSection($">>Writing Results to {resultsDir.FullName}");
                Console.WriteLine($"  >>Writing PGN's");
                await WritePgnFilesToDisk(resultsDir, chessdotcomUsername, gameList).ConfigureAwait(false);

                Console.WriteLine($"  >>Writing CAPS Data");
                await WriteCapsTsvToDisk(resultsDir, chessdotcomUsername, capsScores).ConfigureAwait(false);

                Console.WriteLine($"  >>Writing Text Report");
                await WriteTextReportToDisk(VERSION_NUMBER, resultsDir, chessdotcomUsername, textReport).ConfigureAwait(false);

                Console.WriteLine($"  >>Writing Html Report");
                await WriteHtmlReportToDisk(resultsDir, chessdotcomUsername, htmlReport).ConfigureAwait(false);

                Console.WriteLine($"  >>Writing Raw Game Data (TODO)");
                Console.WriteLine($"  >>Writing Openings Data (TODO)");

                Helpers.EndTimedSection($">>Finished Writing Results", newLineAfter: true);

                Console.WriteLine(textReport.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("");
            }

            Helpers.PressToContinueIfDebug();
            Environment.Exit(0);
        }

        private static string BuildHtmlReport(string VERSION_NUMBER, PlayerProfile userRecord, PlayerStats userStats, string chessdotcomUsername, string whiteOpeningshtmlOut, string blackOpeningshtmlOut, string playingStatshtmlOut, string timePlayedByMonthhtmlOut, string capsTablehtmlOut, string capsRollingAverageFivehtmlOut, string capsRollingAverageTenhtmlOut, string userLogoBase64, string pawnFragment)
        {
            StringBuilder htmlReport = new StringBuilder();
            _ = htmlReport.AppendLine("<!DOCTYPE html>")
                          .AppendLine("<html lang='en'><head>")
                          .AppendLine($"<title>ChessStats for {chessdotcomUsername}</title>")
                          .AppendLine("<meta charset='UTF-8'>")
                          .AppendLine("<meta name='generator' content='ChessStats'> ")
                          .AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>")
                          .AppendLine("   <style>                                                                                                                                                                                  ")
                          .AppendLine("     *                                            {margin: 0;padding: 0;}                                                                                                                   ")
                          .AppendLine("     @media screen and (max-width: 1000px) and (min-width: 768px)  {.priority-4{display:none;}}                                                                                             ")
                          .AppendLine("     @media screen and (max-width: 768px)  and (min-width: 600px)  {.priority-4{display:none;}.priority-3{display:none;}}                                                                   ")
                          .AppendLine("     @media screen and (max-width: 600px)                          {.priority-4{display:none;}.priority-3{display:none;}.priority-2{display:none;}}                                         ")
                          .AppendLine("     body                                         {background-color:#312e2b;width: 90%; margin: auto; font-family: -apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif;}   ")
                          .AppendLine("     h1                                           {padding: 10px;text-align: left;font-size: 40px; color: hsla(0,0%,100%,.65);}                                                             ")
                          .AppendLine("     h1 small                                     {font-size: 15px; vertical-align: bottom}                                                                                                 ")
                          .AppendLine("     .headerLink                                  {color: #e58b09;}                                                                                                                         ")
                          .AppendLine("     h2                                           {clear:left;padding: 5px;text-align: left;font-size: 16px;background-color: rgba(0,0,0,.13);color: hsla(0,0%,100%,.65);}                  ")
                          .AppendLine("     table                                        {width: 100%;table-layout: fixed ;border-collapse: collapse; overflow-x:auto; }                                                           ")
                          .AppendLine("     thead                                        {text-align: center;background: #1583b7;color: white;font-size: 14px; font-weight: bold;}                                                 ")
                          .AppendLine("     tbody                                        {text-align: center;font-size: 11px;}                                                                                                     ")
                          .AppendLine("     td                                           {padding-right: 0px;}                                                                                                                     ")
                          .AppendLine("     tbody td:nth-child(n+2)                      {font-family: Courier New;}                                                                                                               ")
                          .AppendLine("     td:nth-child(1)                              {padding-left:10px; text-align: left; width: 100px ; font-weight: bold;}                                                                  ")
                          .AppendLine("     tbody tr:nth-child(odd)                      {background-color: #F9F9FF;}                                                                                                              ")
                          .AppendLine("     tbody tr:nth-child(even)                     {background-color: #F4F4FF;}                                                                                                              ")
                          .AppendLine("     .active {background-color: #769656}                                                                                                                                                    ")
                          .AppendLine("     .inactive {background-color: #a7a6a2}                                                                                                                                                  ")
                          .AppendLine("     .headRow {display: grid; grid-template-columns: 200px auto; grid-gap: 0px; border:0px; height: auto; padding: 0px; background-color: #2b2825; }                                        ")
                          .AppendLine("     .headRow > div {padding: 0px; }                                                                                                                                                        ")
                          .AppendLine("     .headBox img {vertical-align: middle}                                                                                                                                                  ")
                          .AppendLine("     .ratingRow {display: grid;grid-template-columns: auto auto auto;grid-gap: 20px;padding: 10px;}                                                                                         ")
                          .AppendLine("     .ratingRow > div {text-align: center;  padding: 0px;  color: whitesmoke;  font-size: 15px;  font-weight: bold;}                                                                        ")
                          .AppendLine("     .ratingBox {cursor: pointer;}                                                                                                                                                          ")
                          .AppendLine("     .yearSplit                                   {border-top: thin dotted; border-color: #1583b7;}                                                                                         ")
                          .AppendLine("     .higher                                      {background-color: hsla(120, 100%, 50%, 0.2);}                                                                                            ")
                          .AppendLine("     .lower                                       {background-color: hsla(0, 100%, 70%, 0.2);}                                                                                              ")
                          .AppendLine("     .whiteOpeningsTable thead td:nth-child(1)    {font-weight: bold;}                                                                                                                      ")
                          .AppendLine("     .blackOpeningsTable thead td:nth-child(1)    {font-weight: bold;}                                                                                                                      ")
                          .AppendLine("     .whiteOpeningsTable td:nth-child(1)          {padding-left:10px; text-align: left; width:50%; font-weight: normal;}                                                                    ")
                          .AppendLine("     .blackOpeningsTable td:nth-child(1)          {padding-left:10px; text-align: left; width:50%; font-weight: normal;}                                                                    ")
                          .AppendLine("     .capsRollingTable thead td:nth-child(2)      {text-align: left;}                                                                                                                       ")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(5)     {border-right: thin solid; border-color: #1583b7;}                                                                                         ")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(8)     {border-left: thin dotted; border-color: #1583b7;}                                                                                        ")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(11)    {border-left: thin dotted; border-color: #1583b7;}                                                                                        ")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(13)    {border-left: thin solid; border-color: #1583b7;}                                                                                         ")
                          .AppendLine("     .oneColumn                                   {float: left;width: 100%;}                                                                                                                ")
                          .AppendLine("     .oneRow:after                                {content: ''; display: table; clear: both;}                                                                                               ")
                          .AppendLine("     .footer                                      {text-align: right;color: white; font-size: 11px}                                                                                         ")
                          .AppendLine("     .footer a                                    {color: #e58b09;}                                                                                                                         ")
                          .AppendLine("   </style>                                                                                                                                                                                 ")
                          .AppendLine("</head><body>")
                          .AppendLine($"<div class='headRow'>")
                          .AppendLine($"<div class='headBox priority-2'>")
                          .AppendLine($"<a href='{userRecord.Url}'><img alt='logo' src='data:image/png;base64,{userLogoBase64}'/></a>")
                          .AppendLine($"</div>")
                          .AppendLine($"<div class='headBox'>").AppendLine($"<h1>")
                          .AppendLine($"Live Games Summary <br/>For <a class='headerLink' href='{userRecord.Url}'>{chessdotcomUsername}</a><br/>On {DateTime.UtcNow.ToShortDateString()}&nbsp;<small class='priority-2'>({DateTime.UtcNow.ToShortTimeString()} UTC)</small></h1>")
                          .AppendLine($"</div>")
                          .AppendLine($"</div>")
                          .AppendLine($"<div class='ratingRow'>")
                          .AppendLine($"<div class='ratingBox'>")
                          .AppendLine($"<div class='item1 {((userStats.ChessBullet != null) ? "active" : "inactive")}' onclick=\"window.location.href='https://www.chess.com/stats/live/bullet/{chessdotcomUsername}'\">")
                          .AppendLine($"Bullet {Helpers.ValueOrDash(userStats.ChessBullet?.Last.Rating)}<br/><span class='priority-2'>(Gliko RD&nbsp;{Helpers.ValueOrDash(userStats.ChessBullet?.Last.GlickoRd)})<br/></span>{((userStats.ChessBullet == null) ? "-" : userStats.ChessBullet?.Last.Date.ToShortDateString())}")
                          .AppendLine($"</div></div>")
                          .AppendLine($"<div class='ratingBox'>")
                          .AppendLine($"<div class='item2 {((userStats.ChessBlitz != null) ? "active" : "inactive")}' onclick=\"window.location.href='https://www.chess.com/stats/live/blitz/{chessdotcomUsername}'\">")
                          .AppendLine($"Blitz {Helpers.ValueOrDash(userStats.ChessBlitz?.Last.Rating)}<br/><span class='priority-2'>(Gliko RD&nbsp;{Helpers.ValueOrDash(userStats.ChessBlitz?.Last.GlickoRd)})<br/></span>{((userStats.ChessBlitz == null) ? "-" : userStats.ChessBlitz?.Last.Date.ToShortDateString())}")
                          .AppendLine($"</div></div>")
                          .AppendLine($"<div class='ratingBox'>")
                          .AppendLine($"<div class='item3 {((userStats.ChessRapid != null) ? "active" : "inactive")}' onclick=\"window.location.href='https://www.chess.com/stats/live/rapid/{chessdotcomUsername}'\">")
                          .AppendLine($"Rapid {Helpers.ValueOrDash(userStats.ChessRapid?.Last.Rating)}<br/><span class='priority-2'>(Gliko RD&nbsp;{Helpers.ValueOrDash(userStats.ChessRapid?.Last.GlickoRd)})<br/></span>{((userStats.ChessRapid == null) ? "-" : userStats.ChessRapid?.Last.Date.ToShortDateString())}")
                          .AppendLine($"</div></div>")
                          .AppendLine($"</div>")
                          .AppendLine($"<div class='onerow'><div class='onecolumn'>")
                          .AppendLine($"<h2>{pawnFragment}Openings Occurring More Than Once (Max 15)</h2>")
                          .AppendLine($"{whiteOpeningshtmlOut}")
                          .AppendLine($"{blackOpeningshtmlOut}")
                          .AppendLine($"<div class='priority-2'>")
                          .AppendLine($"<br/><h2>{pawnFragment}CAPS Scoring (Rolling 5 Game Average)</h2>")
                          .AppendLine(capsRollingAverageFivehtmlOut)
                          .AppendLine($"<h2>{pawnFragment}CAPS Scoring (Rolling 10 Game Average)</h2>")
                          .AppendLine(capsRollingAverageTenhtmlOut)
                          .AppendLine($"<h2>{pawnFragment}CAPS Scoring (Month Average > 4 Games)</h2>")
                          .AppendLine(capsTablehtmlOut)
                          .AppendLine($"</div>")
                          .AppendLine($"<br/><h2>{pawnFragment}Time Played by Time Control/Month</h2>")
                          .AppendLine(playingStatshtmlOut)
                          .AppendLine($"<h2>{pawnFragment}Time Played by Month (All Time Controls)</h2>")
                          .AppendLine(timePlayedByMonthhtmlOut)
                          .AppendLine($"<div class='footer'><br/><hr/><i>Generated by ChessStats (for <a href='https://chess.com'>Chess.com</a>)&nbsp;ver. {VERSION_NUMBER}<br/><a href='https://github.com/Hyper-Dragon/ChessStats'>https://github.com/Hyper-Dragon/ChessStats</a></i><br/><br/><br/></div>")
                          .AppendLine("</div></div></body></html>");
            return htmlReport.ToString();
        }

        private static async Task WritePgnFilesToDisk(DirectoryInfo resultsDir, string chessdotcomUsername, List<ChessGame> gameList)
        {
            foreach (string timeClass in (from x in gameList where x.IsRatedGame select x.TimeClass).Distinct().ToArray())
            {
                using StreamWriter pgnFileOutStream = File.CreateText($"{Path.Combine(resultsDir.FullName, $"{chessdotcomUsername}-Pgn-{timeClass}.pgn")}");

                foreach (ChessGame game in (from x in gameList where x.TimeClass == timeClass && x.IsRatedGame select x)
                                            .OrderBy(x => x.GameAttributes.GetAttributeAsNullOrDateTime(SupportedAttribute.Date, SupportedAttribute.StartTime)))
                {
                    await pgnFileOutStream.WriteAsync(game.Text).ConfigureAwait(false);
                    await pgnFileOutStream.WriteLineAsync().ConfigureAwait(false);
                    await pgnFileOutStream.WriteLineAsync().ConfigureAwait(false);
                }
            }
        }

        private static async Task WriteCapsTsvToDisk(DirectoryInfo resultsDir, string chessdotcomUsername, Dictionary<string, List<CapsRecord>> capsScores)
        {
            using StreamWriter capsFileOutStream = File.CreateText($"{Path.Combine(resultsDir.FullName, $"{chessdotcomUsername}-Caps-All.tsv")}");
            await capsFileOutStream.WriteLineAsync($"CAPS Data for {chessdotcomUsername}").ConfigureAwait(false);
            await capsFileOutStream.WriteLineAsync().ConfigureAwait(false);

            foreach (KeyValuePair<string, List<CapsRecord>> capsTimeControl in capsScores)
            {
                StringBuilder dateLine = new StringBuilder();
                StringBuilder monthLine = new StringBuilder();
                StringBuilder capsLine = new StringBuilder();

                foreach (CapsRecord capsRecord in capsTimeControl.Value.OrderBy(x => x.GameDate))
                {
                    dateLine.Append($"{capsRecord.GameDate.ToShortDateString()}\t")
                            .Append($"{capsRecord.GameYearMonth}\t")
                            .Append($"{capsRecord.Caps}\t");
                }

                await capsFileOutStream.WriteLineAsync($"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(capsTimeControl.Key)}").ConfigureAwait(false);
                await capsFileOutStream.WriteLineAsync(dateLine).ConfigureAwait(false);
                await capsFileOutStream.WriteLineAsync(monthLine).ConfigureAwait(false);
                await capsFileOutStream.WriteLineAsync(capsLine).ConfigureAwait(false);
                await capsFileOutStream.WriteLineAsync().ConfigureAwait(false);
            }

            await capsFileOutStream.FlushAsync().ConfigureAwait(false);
            capsFileOutStream.Close();
        }

        private static async Task WriteTextReportToDisk(string VERSION_NUMBER, DirectoryInfo resultsDir, string chessdotcomUsername, string textReport)
        {
            using StreamWriter textReportFileOutStream = File.CreateText($"{Path.Combine(resultsDir.FullName, $"{chessdotcomUsername}-Summary.txt")}");
            await textReportFileOutStream.WriteLineAsync($"{Helpers.GetDisplayLogo(VERSION_NUMBER)}").ConfigureAwait(false);
            await textReportFileOutStream.WriteLineAsync($"{textReport}").ConfigureAwait(false);
            await textReportFileOutStream.FlushAsync().ConfigureAwait(false);
            textReportFileOutStream.Close();
        }

        private static async Task WriteHtmlReportToDisk(DirectoryInfo resultsDir, string chessdotcomUsername, string htmlReport)
        {
            using FileStream htmlReportFileOutStream = File.Create($"{Path.Combine(resultsDir.FullName, $"{chessdotcomUsername}-Summary.html")}");
            await htmlReportFileOutStream.WriteAsync(Encoding.UTF8.GetBytes(htmlReport)).ConfigureAwait(false);
            await htmlReportFileOutStream.FlushAsync().ConfigureAwait(false);
            htmlReportFileOutStream.Close();
        }

        private static string BuildTextReport(string chessdotcomUsername, string whiteOpeningstextOut, string blackOpeningstextOut, string playingStatstextOut, string timePlayedByMonthtextOut, string capsTabletextOut, string capsRollingAverageTentextOut, string totalSecondsPlayedtextOut)
        {
            StringBuilder textReport = new StringBuilder();
            _ = textReport.AppendLine(Helpers.GetDisplaySection($"Live Chess Report for {chessdotcomUsername} : {DateTime.UtcNow.ToShortDateString()}@{DateTime.UtcNow.ToShortTimeString()} UTC", true))
                          .Append(whiteOpeningstextOut)
                          .Append(blackOpeningstextOut)
                          .Append(playingStatstextOut)
                          .Append(timePlayedByMonthtextOut)
                          .Append(capsTabletextOut)
                          .Append(capsRollingAverageTentextOut)
                          .Append(totalSecondsPlayedtextOut)
                          .Append(Helpers.GetDisplaySection("End of Report", true));

            return textReport.ToString();
        }

        private static void ProcessGameData(string chessdotcomUsername, List<ChessGame> gameList, out SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentWorstLoss, int OpponentBestWin, int TotalWin, int TotalDraw, int TotalLoss)> secondsPlayedRollup, out SortedList<string, dynamic> secondsPlayedRollupMonthOnly, out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhite, out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlack, out double totalSecondsPlayed)
        {
            //Initialise reporting lists
            secondsPlayedRollup = new SortedList<string, (int, int, int, int, int, int, int, int, int, int, int, int, int, int)>();
            secondsPlayedRollupMonthOnly = new SortedList<string, dynamic>();
            ecoPlayedRollupWhite = new SortedList<string, (string, int, int, int, int)>();
            ecoPlayedRollupBlack = new SortedList<string, (string, int, int, int, int)>();
            totalSecondsPlayed = 0;
            foreach (ChessGame game in gameList)
            {
                // Don't include daily games
                if (game.GameAttributes.Attributes["Event"] != "Live Chess")
                {
                    continue;
                }

                ExtractRatings(chessdotcomUsername, game, out string side, out int playerRating, out int opponentRating, out bool? isWin);
                CalculateOpening(ecoPlayedRollupWhite, ecoPlayedRollupBlack, game, side, isWin);
                CalculateGameTime(game, out DateTime parsedStartDate, out double seconds, out string gameTime);
                totalSecondsPlayed += seconds;
                UpdateGameTypeTimeTotals(secondsPlayedRollup, playerRating, opponentRating, isWin, parsedStartDate, seconds, gameTime);
                UpdateGameTimeTotals(secondsPlayedRollupMonthOnly, parsedStartDate, seconds);
            }
        }

        private static (string textOut, string htmlOut) DisplayCapsTable(Dictionary<string, List<CapsRecord>> capsScores)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection("CAPS Scoring (Month Average > 4 Games)", false));

            SortedList<string, (double, double, double, double, double, double)> capsTable = new SortedList<string, (double, double, double, double, double, double)>();
            SortedList<string, string[]> capsTableReformat = new SortedList<string, string[]>();

            foreach (KeyValuePair<string, List<CapsRecord>> capsScore in capsScores)
            {
                foreach (var extractedScore in capsScore.Value.GroupBy(t => new { Id = t.GameYearMonth })
                                                    .Where(i => i.Count() > 4)
                                                    .Select(g => new
                                                    {
                                                        Average = Math.Round(g.Average(p => p.Caps), 2).ToString().PadRight(5, '0'),
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

        private static (string textOut, string htmlOut) DisplayCapsRollingAverage(int averageOver, Dictionary<string, List<CapsRecord>> capsScores)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection($"CAPS Scoring (Rolling {averageOver} Game Average)", false));

            textOut.AppendLine("Control/Side      |   <-Newest                                                             Oldest-> ");
            textOut.AppendLine("------------------+---------------------------------------------------------------------------------");

            htmlOut.AppendLine("<table class='capsRollingTable'><thead><tr><td>Control/Side</td><td colspan='9'>Newest</td><td>Oldest</td></thead><tbody>");

            foreach (KeyValuePair<string, List<CapsRecord>> capsScore in capsScores)
            {
                if (capsScore.Value.Count > averageOver)
                {
                    List<double> latestCaps = capsScore.Value.Select(x => x.Caps).ToList<double>();

                    List<string> averages = Enumerable.Range(0, latestCaps.Count - averageOver - 1).
                                      Select(i => Math.Round(latestCaps.Skip(i).Take(averageOver).Average(), 2).ToString().PadRight(5, '0')).
                                      ToList();


                    textOut.AppendLine($"{ CultureInfo.CurrentCulture.TextInfo.ToTitleCase(capsScore.Key.PadRight(17))} |   {string.Join(" | ", averages.Take(10))}");
                    htmlOut.AppendLine($"<tr><td>{ CultureInfo.CurrentCulture.TextInfo.ToTitleCase(capsScore.Key)}</td><td>{string.Join("</td><td>", averages.Take(10))}</td></tr>");
                }
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }


        private static void ExtractRatings(string chessdotcomUsername, ChessGame game, out string side, out int playerRating, out int opponentRating, out bool? isWin)
        {
            side = game.GameAttributes.Attributes["White"].ToUpperInvariant() == chessdotcomUsername.ToUpperInvariant() ? "White" : "Black";
            playerRating = (game.IsRatedGame) ? ((side == "White") ? game.WhiteRating : game.BlackRating) : 0;
            opponentRating = (game.IsRatedGame) ? ((side == "White") ? game.BlackRating : game.WhiteRating) : 0;
            isWin = (game.GameAttributes.Attributes[SupportedAttribute.Result.ToString()]) switch
            {
                "1/2-1/2" => null,
                "1-0" => side == "White",
                "0-1" => side != "White",
                _ => throw new Exception($"Unrecorded game result found"),
            };
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

        private static void UpdateGameTypeTimeTotals(SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentWorstLoss, int OpponentBestWin, int TotalWin, int TotalDraw, int TotalLoss)> secondsPlayedRollup, int playerRating, int opponentRating, bool? isWin, DateTime parsedStartDate, double seconds, string gameTime)
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
                                            OpponentWorstLoss: ((isWin != null && isWin.Value == false && opponentRating != 0) ? Math.Min(opponentRating, secondsPlayedRollup[key].OpponentWorstLoss) : secondsPlayedRollup[key].OpponentWorstLoss),
                                            OpponentBestWin: ((isWin != null && isWin.Value == true) ? Math.Max(opponentRating, secondsPlayedRollup[key].OpponentBestWin) : secondsPlayedRollup[key].OpponentBestWin),
                                            TotalWin: (isWin != null && isWin.Value == true) ? secondsPlayedRollup[key].TotalWin + opponentRating : secondsPlayedRollup[key].TotalWin,
                                            TotalDraw: ((isWin == null) ? secondsPlayedRollup[key].TotalDraw + opponentRating : secondsPlayedRollup[key].TotalDraw),
                                            TotalLoss: (isWin != null && isWin.Value == false) ? secondsPlayedRollup[key].TotalLoss + opponentRating : secondsPlayedRollup[key].TotalLoss
                                            );
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
                                              OpponentWorstLoss: ((isWin != null && isWin.Value == false && opponentRating != 0) ? opponentRating : 9999),
                                              OpponentBestWin: ((isWin != null && isWin.Value == true) ? opponentRating : 0),
                                              TotalWin: ((isWin != null && isWin.Value == true) ? opponentRating : 0),
                                              TotalDraw: ((isWin == null) ? opponentRating : 0),
                                              TotalLoss: ((isWin != null && isWin.Value == false) ? opponentRating : 0)
                                              ));
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

        private static void CalculateOpening(SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhite, SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlack, ChessGame game, string side, bool? isWin)
        {
            try
            {
                string ecoHref = game.GameAttributes.Attributes["ECOUrl"];
                string ecoName = game.GameAttributes.Attributes["ECOUrl"].Replace(@"https://www.chess.com/openings/", "", true, CultureInfo.InvariantCulture).Replace("-", " ", true, CultureInfo.InvariantCulture);
                string ecoShortened = new Regex(@"^.*?(?=[0-9])").Match(ecoName).Value.Trim();
                string ecoKey = $"{game.GameAttributes.Attributes["ECO"]}-{((string.IsNullOrEmpty(ecoShortened)) ? ecoName : ecoShortened)}";
                SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollup = (side == "White") ? ecoPlayedRollupWhite : ecoPlayedRollupBlack;

                if (ecoPlayedRollup.ContainsKey(ecoKey))
                {
                    ecoPlayedRollup[ecoKey] = (ecoPlayedRollup[ecoKey].href,
                                               ecoPlayedRollup[ecoKey].total + 1,
                                               ecoPlayedRollup[ecoKey].winCount + ((isWin != null && isWin.Value) ? 1 : 0),
                                               ecoPlayedRollup[ecoKey].drawCount + ((isWin == null) ? 1 : 0),
                                               ecoPlayedRollup[ecoKey].lossCount + ((isWin != null && !isWin.Value) ? 1 : 0));
                }
                else
                {
                    ecoPlayedRollup.Add(ecoKey, (ecoHref,
                                                 1,
                                                 ((isWin != null && isWin.Value) ? 1 : 0),
                                                 ((isWin == null) ? 1 : 0),
                                                 ((isWin != null && !isWin.Value) ? 1 : 0)));
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
        private static (string textOut, string htmlOut) DisplayOpeningsAsWhite(SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhite)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection($"Openings Occurring More Than Once (Max 15)", false));
            textOut.AppendLine("Playing As White                                                        | Tot.");
            textOut.AppendLine("------------------------------------------------------------------------+------");

            htmlOut.AppendLine("<table class='whiteOpeningsTable'><thead><tr><td>Playing As White</td><td class='priority-2'>Win</td><td class='priority-2'>Draw</td><td class='priority-2'>Loss</td><td>Total</td></tr></thead><tbody>");

            foreach (KeyValuePair<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoCount in ecoPlayedRollupWhite.OrderByDescending(uses => uses.Value.total).Take(15))
            {
                if (ecoCount.Value.total < 2) { break; }

                //Calculate highlight class
                int activeCell = (ecoCount.Value.winCount > ecoCount.Value.lossCount) ? 0 : ((ecoCount.Value.winCount < ecoCount.Value.lossCount) ? 2 : 1);
                textOut.AppendLine($"{ecoCount.Key,-71} | {ecoCount.Value.total.ToString(CultureInfo.CurrentCulture),4}");
                htmlOut.AppendLine($"<tr><td><a href='{ecoCount.Value.href}'>{ecoCount.Key}</a></td><td{((activeCell == 0) ? " class='higher priority-2'" : " class='priority-2'")}>{ecoCount.Value.winCount.ToString().PadLeft(5, '$').Replace("$", "&nbsp;")}</td><td{((activeCell == 1) ? " class='higher priority-2'" : " class='priority-2'")}>{ecoCount.Value.drawCount.ToString().PadLeft(5, '$').Replace("$", "&nbsp;")}</td><td{((activeCell == 2) ? " class='lower priority-2'" : " class='priority-2'")}>{ecoCount.Value.lossCount.ToString().PadLeft(5, '$').Replace("$", "&nbsp;")}</td><td>{ecoCount.Value.total.ToString(CultureInfo.CurrentCulture).ToString().PadLeft(5, '$').Replace("$", "&nbsp;")}</td></tr>");
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static (string textOut, string htmlOut) DisplayOpeningsAsBlack(SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlack)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine("Playing As Black                                                        | Tot.");
            textOut.AppendLine("------------------------------------------------------------------------+------");

            htmlOut.AppendLine("<table class='blackOpeningsTable'><thead><tr><td>Playing As Black</td><td class='priority-2'>Win</td><td class='priority-2'>Draw</td><td class='priority-2'>Loss</td><td>Total</td></tr></thead><tbody>");

            foreach (KeyValuePair<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoCount in ecoPlayedRollupBlack.OrderByDescending(uses => uses.Value.total).Take(15))
            {
                if (ecoCount.Value.total < 2) { break; }

                //Calculate highlight class
                int activeCell = (ecoCount.Value.winCount > ecoCount.Value.lossCount) ? 0 : ((ecoCount.Value.winCount < ecoCount.Value.lossCount) ? 2 : 1);
                textOut.AppendLine($"{ecoCount.Key,-71} | {ecoCount.Value.total.ToString(CultureInfo.CurrentCulture),4}");
                htmlOut.AppendLine($"<tr><td><a href='{ecoCount.Value.href}'>{ecoCount.Key}</a></td><td{((activeCell == 0) ? " class='higher priority-2'" : " class='priority-2'")}>{ecoCount.Value.winCount.ToString().PadLeft(5, '$').Replace("$", "&nbsp;")}</td><td{((activeCell == 1) ? " class='higher priority-2'" : " class='priority-2'")}>{ecoCount.Value.drawCount.ToString().PadLeft(5, '$').Replace("$", "&nbsp;")}</td><td{((activeCell == 2) ? " class='lower priority-2'" : " class='priority-2'")}>{ecoCount.Value.lossCount.ToString().PadLeft(5, '$').Replace("$", "&nbsp;")}</td><td>{ecoCount.Value.total.ToString().PadLeft(5, '$').Replace("$", "&nbsp;")}</td></tr>");
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
        private static (string textOut, string htmlOut) DisplayPlayingStats(SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentWorstLoss, int OpponentBestWin, int TotalWin, int TotalDraw, int TotalLoss)> secondsPlayedRollup, int? bulletRating, int? blitzRating, int? rapidRating)
        {
            StringBuilder textOut = new StringBuilder();
            StringBuilder htmlOut = new StringBuilder();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection("Time Played/Ratings by Time Control/Month", false));
            textOut.AppendLine("Time Control/Month| Play Time | Rating Min/Max/+-  | Vs Min/BestWin/Max | Win  | Draw | Loss | Tot. ");
            string lastLine = "";

            foreach (KeyValuePair<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentWorstLoss, int OpponentBestWin, int TotalWin, int TotalDraw, int TotalLoss)> rolledUp in secondsPlayedRollup)
            {
                int ratingComparison = rolledUp.Key.Substring(0, 2).ToUpperInvariant() switch
                {
                    "BU" => rolledUp.Key.Contains("NR") ? 0 : bulletRating.Value,
                    "BL" => rolledUp.Key.Contains("NR") ? 0 : blitzRating.Value,
                    "RA" => rolledUp.Key.Contains("NR") ? 0 : rapidRating.Value,
                    _ => 0,
                };

                if (lastLine != rolledUp.Key.Substring(0, 10))
                {
                    textOut.AppendLine("------------------+-----------+--------------------+--------------------+------+------+------+------");
                    htmlOut.AppendLine($"{((string.IsNullOrEmpty(lastLine)) ? "" : "</tbody></table>")}<table class='playingStatsTable'><thead><tr><td>Time Control</td><td>Time</td><td>Min</td><td>Max</td><td>Rng +-</td><td class='priority-4'>Vs.Min</td><td class='priority-2'>Worst</td><td class='priority-2'>LossAv</td><td class='priority-2'>DrawAv</td><td class='priority-2'>WinAv</td><td class='priority-2'>Best</td><td class='priority-4'>Vs.Max</td><td class='priority-3'>Win</td><td class='priority-3'>Draw</td><td class='priority-3'>Loss</td><td class='priority-4'>Total</td></tr></thead><tbody>");
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
                                   $"{rolledUp.Value.Draw.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                   $"{rolledUp.Value.Loss.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)} | " +
                                   $"{rolledUp.Value.GameCount.ToString(CultureInfo.CurrentCulture).PadLeft(4).Replace("   0", "   -", true, CultureInfo.InvariantCulture)}"
                                   );

                htmlOut.AppendLine($"<tr><td>{rolledUp.Key}</td>" +
                                   $"<td>{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td>" +
                                   $"<td{((ratingComparison == 0) ? "" : ((ratingComparison < rolledUp.Value.MinRating) ? " class='lower'" : " class='higher'"))}>{rolledUp.Value.MinRating.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td{((ratingComparison == 0) ? "" : ((ratingComparison < rolledUp.Value.MaxRating) ? " class='lower'" : " class='higher'"))}>{rolledUp.Value.MaxRating.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td>{(rolledUp.Value.MaxRating - rolledUp.Value.MinRating).ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-4'>{rolledUp.Value.OpponentMinRating.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-2'>{rolledUp.Value.OpponentWorstLoss.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("9999", "   -", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-2'>{$"{((rolledUp.Value.Loss == 0) ? 0 : rolledUp.Value.TotalLoss / rolledUp.Value.Loss).ToString(CultureInfo.CurrentCulture)}".PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-2'>{$"{((rolledUp.Value.Draw == 0) ? 0 : rolledUp.Value.TotalDraw / rolledUp.Value.Draw).ToString(CultureInfo.CurrentCulture)}".PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-2'>{$"{((rolledUp.Value.Win == 0) ? 0 : rolledUp.Value.TotalWin / rolledUp.Value.Win).ToString(CultureInfo.CurrentCulture)}".PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-2'>{rolledUp.Value.OpponentBestWin.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-4'>{rolledUp.Value.OpponentMaxRating.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-3'>{rolledUp.Value.Win.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-3'>{rolledUp.Value.Draw.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-3'>{rolledUp.Value.Loss.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>" +
                                   $"<td class='priority-4'>{rolledUp.Value.GameCount.ToString(CultureInfo.CurrentCulture).PadLeft(4, '$').Replace("$$$0", "$$$-", true, CultureInfo.InvariantCulture).Replace("$", "&nbsp;", true, CultureInfo.InvariantCulture)}</td>"
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
            textOut.AppendLine("Month             |  Play Time  |   For Year  |  Cumulative ");

            htmlOut.AppendLine("<table class='playingStatsMonthTable'><thead><tr><td>Month</td><td>Play Time</td><td class='priority-2'>For Year</td><td>Cumulative</td></tr></thead><tbody>");

            TimeSpan cumulativeTime = new TimeSpan(0);
            TimeSpan cumulativeTimeForYear = new TimeSpan(0);
            string currentYear = "";
            string yearSplitClass = "";

            foreach (KeyValuePair<string, dynamic> rolledUp in secondsPlayedRollupMonthOnly)
            {
                if (rolledUp.Key.Substring(0, 4) != currentYear)
                {
                    textOut.AppendLine("------------------+-------------+-------------+-------------");

                    //Skip for 1st year (text div only)
                    yearSplitClass = string.IsNullOrEmpty(currentYear) ? "" : " class='yearSplit'";

                    currentYear = rolledUp.Key.Substring(0, 4);
                    cumulativeTimeForYear = new TimeSpan(0);
                }

                TimeSpan timeMonth = TimeSpan.FromSeconds(rolledUp.Value);
                cumulativeTime += timeMonth;
                cumulativeTimeForYear += timeMonth;

                _ = textOut.AppendLine($"{rolledUp.Key,-17} | " +
                                  $"{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ timeMonth.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')} | " +
                                  $"{((int)cumulativeTimeForYear.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ cumulativeTimeForYear.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ cumulativeTimeForYear.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')} | " +
                                  $"{((int)cumulativeTime.TotalHours).ToString(CultureInfo.CurrentCulture),5}:{ cumulativeTime.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}:{ cumulativeTime.Seconds.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}"
                                  );

                _ = htmlOut.AppendLine($"<tr{yearSplitClass}><td>{rolledUp.Key}</td>" +
                                   $"<td>{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture).PadLeft(5, '$').Replace("$", "&nbsp;")}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td>" +
                                   $"<td  class='priority-2'>{((int)cumulativeTimeForYear.TotalHours).ToString(CultureInfo.CurrentCulture).PadLeft(5, '$').Replace("$", "&nbsp;")}:{ cumulativeTimeForYear.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td>" +
                                   $"<td>{((int)cumulativeTime.TotalHours).ToString(CultureInfo.CurrentCulture).PadLeft(5, '$').Replace("$", "&nbsp;")}:{ cumulativeTime.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td></tr>"
                                  );

                //Reset until next year detected
                yearSplitClass = "";
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
