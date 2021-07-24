using ChessDotComSharp.Models;
using ChessStats.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        private const int MAX_CAPS_PAGES = 50;
        private const int MAX_CAPS_PAGES_WITH_CACHE = 3;
        private const string VERSION_NUMBER = "0.7";
        private const string RESULTS_DIR_NAME = "ChessStatsResults";
        private const string CACHE_DIR_NAME = "ChessStatsCache";
        private const string CACHE_VERSION_NUMBER = "1";
        private const string CHESSCOM_URL = "https://chess.com";
        private const string MEMBER_URL = "https://www.chess.com/member/";
        private const string OPENING_URL = "https://www.chess.com/openings/";
        private const string STATS_BASE_URL = "https://www.chess.com/stats";
        private const string PROJECT_LINK = "https://github.com/Hyper-Dragon/ChessStats";
        private const string DEFAULT_USER_IMAGE = "https://betacssjs.chesscomfiles.com/bundles/web/images/black_400.918cdaa6.png";
        private const string INDEX_PAGE_IMAGE = "https://betacssjs.chesscomfiles.com/bundles/web/images/black_400.918cdaa6.png";
        private const string REPORT_HEADING_ICON = "https://www.chess.com/bundles/web/favicons/favicon-16x16.31f99381.png";
        private const string FONT_700_WOFF2_URL = "https://www.chess.com/bundles/web/fonts/montserrat-700.5e7b9b6f.woff2";
        private const string FONT_800_WOFF2_URL = "https://www.chess.com/bundles/web/fonts/montserrat-800.92157f3f.woff2";
        private const string CAPS_URL = "https://www.chess.com/callback/user/daily/archive?all=1&userId=";
        private const int GRAPH_WIDTH = 700;
        private const int GRAPH_HEIGHT_STATS = 300;
        private const int GRAPH_HEIGHT_AVERAGE = 200;

        private static string bkgImageBase64 = "";
        private static string favIconBase64 = "";
        private static string userLogoBase64 = "";
        private static string indexPageLogoFragment = "";
        private static string pawnFragment = "";
        private static string font700Fragment = "";
        private static string font800Fragment = "";

        private static async Task Main(string[] args)
        {
            Helpers.DisplayLogo(VERSION_NUMBER);

            try
            {
                (bool hasRunErrors, bool hasCmdLineOptionSet) = await RunChessStats(args).ConfigureAwait(false);

                if (hasRunErrors)
                {
                    Console.WriteLine("*** WARNING: Errors occurred during run - check output above ***");
                    Console.WriteLine("");

                    if (!hasCmdLineOptionSet) { Helpers.PressToContinue(); }
                    Environment.Exit(-1);
                }
                else
                {
                    if (!hasCmdLineOptionSet) { Helpers.PressToContinue(); }
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("");
                Console.WriteLine("*** Fatal Error - Unable to Continue ***");
                Console.WriteLine($"{ex.Message}");
                Console.WriteLine("");

                if (args.Length != 1) { Helpers.PressToContinue(); }

                Environment.Exit(-2);
            }
        }

        private static async Task<(bool hasRunErrors, bool hasCmdLineOptionSet)> RunChessStats(string[] args)
        {
            bool hasRunErrors = false;
            bool hasCmdLineOptionSet = true;
            bool isCapsIncluded = true;

            //Set up data directories
            DirectoryInfo applicationPath = new (Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
            DirectoryInfo baseResultsDir = applicationPath.CreateSubdirectory(RESULTS_DIR_NAME);
            DirectoryInfo baseCacheDir = applicationPath.CreateSubdirectory($"{CACHE_DIR_NAME}/CacheV{CACHE_VERSION_NUMBER}");

            //Load Embeded Resources
            bkgImageBase64 = Helpers.EncodeResourceImageAsHtmlFragment("SeamlessBkg01.png");
            favIconBase64 = Helpers.EncodeResourceImageAsHtmlFragment("FavIcon.png");

            while (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.WriteLine($"You must specify a single valid chess.com username or -refresh");
                Console.Write("> ");
                args = Console.ReadLine().Trim()
                                         .Split()
                                         .Where(x => !string.IsNullOrWhiteSpace(x))
                                         .Select(x => x.Trim()).ToArray<string>();
                hasCmdLineOptionSet = false;
            }

            string[] chessdotcomUsers = args[0].ToUpperInvariant() switch
            {
                "-REFRESH" => baseResultsDir.GetDirectories()
                                            .Select(x => x.Name)
                                            .ToArray(),
                _ => new string[] { args[0] }
            };

            //Get reporting graphics
            Helpers.StartTimedSection(">>Download report images/fonts");

            using (HttpClient httpClient = new())
            {
                string indexPageLogo = Convert.ToBase64String(await httpClient.GetByteArrayAsync(new Uri(INDEX_PAGE_IMAGE)).ConfigureAwait(false));
                indexPageLogoFragment = $"<img width='200px' height='200px' src='data:image/png;base64,{indexPageLogo}'/>";

                string pawnFileBase64 = Convert.ToBase64String(await httpClient.GetByteArrayAsync(new Uri(REPORT_HEADING_ICON)).ConfigureAwait(false));
                pawnFragment = $"<img src='data:image/png;base64,{pawnFileBase64}'/>";

                string font700FragmentBase64 = Convert.ToBase64String(await httpClient.GetByteArrayAsync(new Uri(FONT_700_WOFF2_URL)).ConfigureAwait(false));
                font700Fragment = $"font-display:swap; font-family:Montserrat; font-style:normal; font-weight:700; src: url('data:font/ttf;base64,{font700FragmentBase64}') format('woff2');";

                string font800FragmentBase64 = Convert.ToBase64String(await httpClient.GetByteArrayAsync(new Uri(FONT_800_WOFF2_URL)).ConfigureAwait(false));
                font800Fragment = $"font-display:swap; font-family:Montserrat; font-style:normal; font-weight:800; src: url('data:font/ttf;base64,{font800FragmentBase64}') format('woff2');";
            }

            Helpers.EndTimedSection(">>Download complete");

            foreach (string user in chessdotcomUsers)
            {
                PlayerProfile userRecord = null;
                PlayerStats userStats = null;

                try
                {
                    Helpers.StartTimedSection($">>Confirming user {user}", newLineFirst: true);
                    (PlayerProfile userRecordIn, PlayerStats userStatsIn) = await PgnFromChessDotCom.FetchUserData(user).ConfigureAwait(false);
                    userRecord = userRecordIn;
                    userStats = userStatsIn;
                    Helpers.EndTimedSection(">>User OK", newLineAfter: true);
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"  >>ERROR: {ex.Message}");
                    Helpers.EndTimedSection(">>Finished Downloading user record", newLineAfter: true);
                    hasRunErrors = true;
                    continue;
                }

                //Replace username with correct case - api returns ID in lower case so extract from URL property
                string chessdotcomUsername = userRecord.Url.Replace(MEMBER_URL, "", StringComparison.InvariantCultureIgnoreCase);
                int chessdotcomPlayerId = userRecord.PlayerId;

                //Create output directory
                DirectoryInfo resultsDir = baseResultsDir.CreateSubdirectory(chessdotcomUsername);
                DirectoryInfo cacheDir = baseCacheDir.CreateSubdirectory(chessdotcomUsername);

                Helpers.DisplaySection($"Fetching Data for {chessdotcomUsername}", true);

                //Get reporting graphics
                Helpers.StartTimedSection(">>Download user profile image");

                using (HttpClient httpClient = new ())
                {
                    Uri userLogoUri = new(string.IsNullOrEmpty(userRecord.Avatar) ? DEFAULT_USER_IMAGE : userRecord.Avatar);
                    userLogoBase64 = Convert.ToBase64String(await httpClient.GetByteArrayAsync(userLogoUri).ConfigureAwait(false));
                }

                Helpers.EndTimedSection(">>Download complete");

                Dictionary<string, List<CapsRecord>> capsScores = new();

                if (isCapsIncluded)
                {
                    Helpers.StartTimedSection($">>Fetching and Processing Available CAPS Scores");
                    capsScores = await CapsFromChessDotCom.GetCapsScoresJson(chessdotcomPlayerId, CAPS_URL).ConfigureAwait(false);
                    Helpers.EndTimedSection(">>Finished Fetching and Processing Available CAPS Scores", true);
                }
                else
                {
                    Helpers.StartTimedSection($">>Skipping CAPS Processing due to Chess.Com site changes");
                    Helpers.EndTimedSection(">>Skipped", false);
                }

                List<ChessGame> gameList = new();
                Helpers.StartTimedSection($">>Fetching Games From Chess.Com");

                try
                {
                    gameList = await PgnFromChessDotCom.FetchGameRecordsForUser(chessdotcomUsername, cacheDir).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("");
                    Console.WriteLine($"  >>Fetching Games From Chess.Com Failed");
                    Console.WriteLine($"    {ex.Message}");
                    throw;
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
                                out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhiteRecent,
                                out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlackRecent,
                                out List<(DateTime gameDate, int rating, string gameType)> ratingsPostGame,
                                out double totalSecondsPlayed);

                Helpers.EndTimedSection($">>Finished Processing Games");

                Helpers.StartTimedSection($">>Compiling Report Data");

                //Extract reporting data
                (string whiteOpeningstextOut, string whiteOpeningshtmlOut) = DisplayOpeningsAsWhite(ecoPlayedRollupWhite);
                (string blackOpeningstextOut, string blackOpeningshtmlOut) = DisplayOpeningsAsBlack(ecoPlayedRollupBlack);
                (string whiteOpeningsRecenttextOut, string whiteOpeningsRecenthtmlOut) = DisplayOpeningsAsWhite(ecoPlayedRollupWhiteRecent);
                (string blackOpeningsRecenttextOut, string blackOpeningsRecenthtmlOut) = DisplayOpeningsAsBlack(ecoPlayedRollupBlackRecent);
                (string playingStatstextOut, string playingStatshtmlOut, List<(string TimeControl, int VsMin, int Worst, int LossAv, int DrawAv, int WinAv, int Best, int VsMax)> graphData) = DisplayPlayingStats(secondsPlayedRollup, userStats.ChessBullet?.Last.Rating, userStats.ChessBlitz?.Last.Rating, userStats.ChessRapid?.Last.Rating);
                (string timePlayedByMonthtextOut, string timePlayedByMonthhtmlOut) = DisplayTimePlayedByMonth(secondsPlayedRollupMonthOnly);
                (_, string capsRollingAverageFivehtmlOut, Dictionary<string, double[]> capsAverageFiveOut) = DisplayCapsRollingAverage(3, capsScores);
                (string totalSecondsPlayedtextOut, _) = DisplayTotalSecondsPlayed(totalSecondsPlayed);

                Helpers.EndTimedSection($">>Finished Compiling Report Data");

                Helpers.StartTimedSection($">>Rendering Graphs");
                Task<string> graphT1 = RenderRatingGraph(userStats?.ChessBullet?.Last?.Rating, ratingsPostGame.Where(x => x.gameType == "Bullet").ToList());
                Task<string> graphT2 = RenderRatingGraph(userStats?.ChessBlitz?.Last?.Rating, ratingsPostGame.Where(x => x.gameType == "Blitz").ToList());
                Task<string> graphT3 = RenderRatingGraph(userStats?.ChessRapid?.Last?.Rating, ratingsPostGame.Where(x => x.gameType == "Rapid").ToList());

                Task<string> graphT4 = RenderAverageStatsGraph(graphData.Where(x => x.TimeControl.Contains("Bullet", StringComparison.InvariantCultureIgnoreCase)).OrderBy(x => x.TimeControl).ToList());
                Task<string> graphT5 = RenderAverageStatsGraph(graphData.Where(x => x.TimeControl.Contains("Blitz", StringComparison.InvariantCultureIgnoreCase)).OrderBy(x => x.TimeControl).ToList());
                Task<string> graphT6 = RenderAverageStatsGraph(graphData.Where(x => x.TimeControl.Contains("Rapid", StringComparison.InvariantCultureIgnoreCase)).OrderBy(x => x.TimeControl).ToList());

                Task<string> graphT9 = RenderCapsAverageGraph(capsAverageFiveOut.ToDictionary(x => x.Key, x => x.Value));

                _ = await Task.WhenAll(graphT1, graphT2, graphT3,
                                       graphT4, graphT5, graphT6,
                                       graphT9).ConfigureAwait(false);

                string bulletGraphHtmlFragment = graphT1.Result;
                string blitzGraphHtmlFragment = graphT2.Result;
                string rapidGraphHtmlFragment = graphT3.Result;

                string bulletAvStatsGraphHtmlFragment = graphT4.Result;
                string blitzAvStatsraphHtmlFragment = graphT5.Result;
                string rapidAvStatsraphHtmlFragment = graphT6.Result;

                string rapidThreeAvCaps = graphT9.Result;

                Helpers.EndTimedSection($">>Finished Rendering Graphs");

                Helpers.StartTimedSection($">>Building Reports");
                //Build the text report
                Task<string> reportT1 = BuildTextReport(isCapsIncluded, chessdotcomUsername, whiteOpeningstextOut, blackOpeningstextOut, playingStatstextOut,
                                                          timePlayedByMonthtextOut, "", "",
                                                          totalSecondsPlayedtextOut);

                //Build the HTML report
                Task<string> reportT2 = BuildHtmlReport(isCapsIncluded, VERSION_NUMBER, userRecord, userStats, chessdotcomUsername, whiteOpeningshtmlOut, 
                                                        blackOpeningshtmlOut,
                                                        whiteOpeningsRecenthtmlOut, blackOpeningsRecenthtmlOut,
                                                        playingStatshtmlOut, timePlayedByMonthhtmlOut, capsRollingAverageFivehtmlOut,
                                                        userLogoBase64, pawnFragment,
                                                        bulletGraphHtmlFragment, blitzGraphHtmlFragment, rapidGraphHtmlFragment,
                                                        bulletAvStatsGraphHtmlFragment, blitzAvStatsraphHtmlFragment, rapidAvStatsraphHtmlFragment,
                                                        rapidThreeAvCaps);

                _ = await Task.WhenAll(reportT1, reportT2).ConfigureAwait(false);
                string textReport = reportT1.Result;
                string htmlReport = reportT2.Result;


                Helpers.EndTimedSection($">>Finished Building Reports");

                Helpers.StartTimedSection($">>Writing Results to {resultsDir.FullName}");

                foreach (string report in new string[] { "PGN", "CAPS", "TXT", "HTML" })
                {
                    try
                    {
                        switch (report)
                        {
                            case "PGN":
                                Console.WriteLine($"  >>Writing PGN's");
                                await WritePgnFilesToDisk(resultsDir, chessdotcomUsername, gameList).ConfigureAwait(false);
                                break;
                            case "CAPS":
                                Console.WriteLine($"  {((isCapsIncluded) ? ">>Writing CAPS Data" : ">>CAPS Data Skipped")}");
                                await WriteCapsTsvToDisk(resultsDir, chessdotcomUsername, capsScores).ConfigureAwait(false);
                                break;
                            case "TXT":
                                Console.WriteLine($"  >>Writing Text Report");
                                await WriteTextReportToDisk(VERSION_NUMBER, resultsDir, chessdotcomUsername, textReport).ConfigureAwait(false);
                                break;
                            case "HTML":
                                Console.WriteLine($"  >>Writing Html Report");
                                await WriteHtmlReportToDisk(resultsDir, chessdotcomUsername, htmlReport).ConfigureAwait(false);
                                break;
                            default: throw new NotImplementedException();
                        }
                    }
                    catch (System.IO.IOException ex)
                    {
                        Console.WriteLine($"    >>ERROR: {ex.Message}");
                        hasRunErrors = true;
                    }
                }

                Helpers.EndTimedSection($">>Finished Writing Results", newLineAfter: false);

                Console.WriteLine(textReport.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("");

                //Clear out user data before next run to avoid out of memory problems
                capsScores = null;
                gameList = null;
                secondsPlayedRollup = null;
                secondsPlayedRollupMonthOnly = null;
                ecoPlayedRollupWhite = ecoPlayedRollupBlack = null;
                ratingsPostGame = null;
                graphData = null;
                graphT1 = graphT2 = graphT3 = graphT4 = graphT5 = graphT6 = graphT9 = reportT1 = reportT2 = null;
                bulletGraphHtmlFragment = blitzGraphHtmlFragment = rapidGraphHtmlFragment = bulletAvStatsGraphHtmlFragment = blitzAvStatsraphHtmlFragment = rapidAvStatsraphHtmlFragment = null;
                rapidThreeAvCaps = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Helpers.StartTimedSection($">>Rebuilding HTML Index at {baseResultsDir.FullName}");

            SortedList<string, (DateTime lastUpdate,
                                bool hasHtml, bool hasTxt,
                                bool hasBulletPgn, bool hasBlitzPgn,
                                bool hasRapidPgn, bool hasCaps)> index = new();

            foreach (DirectoryInfo dir in baseResultsDir.GetDirectories())
            {
                FileInfo[] fileInfo = dir.GetFiles();

                if (fileInfo.Length > 0)
                {
                    index.Add(dir.Name, (lastUpdate: fileInfo.Select(x => x.LastWriteTimeUtc).Max(),
                                         hasHtml: fileInfo.Any(x => x.Name.EndsWith($"-Summary.html", StringComparison.InvariantCultureIgnoreCase)),
                                         hasTxt: fileInfo.Any(x => x.Name.EndsWith($"-Summary.txt", StringComparison.InvariantCultureIgnoreCase)),
                                         hasBulletPgn: fileInfo.Any(x => x.Name.EndsWith($"-Pgn-Bullet.pgn", StringComparison.InvariantCultureIgnoreCase)),
                                         hasBlitzPgn: fileInfo.Any(x => x.Name.EndsWith($"-Pgn-Blitz.pgn", StringComparison.InvariantCultureIgnoreCase)),
                                         hasRapidPgn: fileInfo.Any(x => x.Name.EndsWith($"-Pgn-Rapid.pgn", StringComparison.InvariantCultureIgnoreCase)),
                                         hasCaps: fileInfo.Any(x => x.Name.EndsWith($"-Caps-All.tsv", StringComparison.InvariantCultureIgnoreCase))
                                        ));
                }
            }

            StringBuilder htmlOut = new();
            _ = htmlOut.Append(Helpers.GetHtmlTop($"ChessStats Index", bkgImageBase64, favIconBase64, font700Fragment, font800Fragment))
                       .AppendLine($"<div class='headRow'>")
                       .AppendLine($"<div class='headBox priority-2'>")
                       .AppendLine($"{indexPageLogoFragment}")
                       .AppendLine($"</div>")
                       .AppendLine($"<div class='headBox'>").AppendLine($"<h1>")
                       .AppendLine($"ChessStats Index<br/>On {DateTime.UtcNow.ToShortDateString()}&nbsp;<small class='priority-2'>({DateTime.UtcNow.ToShortTimeString()} UTC)</small></h1>")
                       .AppendLine($"</div>")
                       .AppendLine($"</div><br/>")
                       .AppendLine($"<div class='onerow'><div class='onecolumn'>")
                       .AppendLine($"<h2>{pawnFragment}Available Files</h2>")
                       .AppendLine("<table><thead>")
                       .AppendLine("<tr><td>User</td><td>Html</td><td class='priority-2'>Text</td><td>Bullet</td><td>Blitz</td><td>Rapid</td><td class='priority-2'>CAPs</td><td class='priority-3'>Updated (UTC)</td></tr>")
                       .AppendLine("</thead><tbody>");

            foreach (KeyValuePair<string, (DateTime lastUpdate, bool hasHtml, bool hasTxt, bool hasBulletPgn, bool hasBlitzPgn, bool hasRapidPgn, bool hasCaps)> userRecords in index)
            {
                int daysFromLastUpdate = (DateTime.UtcNow - userRecords.Value.lastUpdate).Days;

                _ = htmlOut.AppendLine("<tr>")
                           .Append($"<td>{userRecords.Key}</td>")
                           .Append($"<td{((userRecords.Value.hasHtml) ? "" : " class='lower'")}>{((userRecords.Value.hasHtml) ? $"<a href='./{userRecords.Key}/{userRecords.Key}-Summary.html'>Report" : "&nbsp;")}</td>")
                           .Append($"<td class='priority-2{((userRecords.Value.hasTxt) ? "'" : " lower'")}>{((userRecords.Value.hasTxt) ? $"<a href='./{userRecords.Key}/{userRecords.Key}-Summary.txt'>TXT" : "&nbsp;")}</td>")
                           .Append($"<td>{((userRecords.Value.hasBulletPgn) ? $"<a href='./{userRecords.Key}/{userRecords.Key}-Pgn-Bullet.pgn'>PGN" : "&nbsp;")}</td>")
                           .Append($"<td>{((userRecords.Value.hasBlitzPgn) ? $"<a href='./{userRecords.Key}/{userRecords.Key}-Pgn-Blitz.pgn'>PGN" : "&nbsp;")}</td>")
                           .Append($"<td>{((userRecords.Value.hasRapidPgn) ? $"<a href='./{userRecords.Key}/{userRecords.Key}-Pgn-Rapid.pgn'>PGN" : "&nbsp;")}</td>")
                           .Append($"<td class='priority-2{((userRecords.Value.hasCaps) ? "'" : " lower'")}>{((userRecords.Value.hasCaps) ? $"<a href='./{userRecords.Key}/{userRecords.Key}-Caps-All.tsv'>TSV" : "&nbsp;")}</td>")
                           .Append($"<td class='priority-3{((daysFromLastUpdate < 1) ? " higher'" : ((daysFromLastUpdate >= 3) ? "'" : " lower'"))}>{userRecords.Value.lastUpdate.ToShortDateString()}@{userRecords.Value.lastUpdate.ToShortTimeString()}</td>")
                           .AppendLine("</tr>");
            }
            htmlOut.AppendLine("</tbody></table>")
                   .AppendLine(Helpers.GetHtmlTail(new Uri(CHESSCOM_URL), VERSION_NUMBER, PROJECT_LINK))
                   .AppendLine("</div></div></body></html>");


            using FileStream indexFileOutStream = File.Create($"{Path.Combine(baseResultsDir.FullName, $"index.html")}");
            await indexFileOutStream.WriteAsync(Encoding.UTF8.GetBytes(htmlOut.ToString())).ConfigureAwait(false);
            await indexFileOutStream.FlushAsync().ConfigureAwait(false);
            indexFileOutStream.Close();

            Helpers.EndTimedSection($">>Finished Rebuilding HTML Index", newLineAfter: true);

            return (hasRunErrors, hasCmdLineOptionSet);
        }

        private static async Task<string> BuildHtmlReport(bool isCapsIncluded, string VERSION_NUMBER, PlayerProfile userRecord, PlayerStats userStats,
                                                          string chessdotcomUsername, string whiteOpeningshtmlOut, string blackOpeningshtmlOut,
                                                          string whiteOpeningsRecenthtmlOut, string blackOpeningsRecenthtmlOut,
                                                          string playingStatshtmlOut, string timePlayedByMonthhtmlOut,
                                                          string capsRollingAverageFivehtmlOut, 
                                                          string userLogoBase64, string pawnFragment, string bulletGraphHtmlFragment,
                                                          string blitzGraphHtmlFragment, string rapidGraphHtmlFragment,
                                                          string bulletAvStatsGraphHtmlFragment, string blitzAvStatsGraphHtmlFragment,
                                                          string rapidAvStatsGraphHtmlFragment, string rapidFiveAvCaps)
        {
            return await Task<string>.Run(() =>
            {
                var htmlOut = new StringBuilder();

                _ = htmlOut.Append(Helpers.GetHtmlTop($"ChessStats for {chessdotcomUsername}", bkgImageBase64, favIconBase64, font700Fragment, font800Fragment))
                           .AppendLine($"<div class='headRow'>")
                           .AppendLine($"<div class='headBox priority-2'>")
                           .AppendLine($"<a href='{userRecord.Url}'><img width='200px' height='200px' alt='logo' src='data:image/png;base64,{userLogoBase64}'/></a>")
                           .AppendLine($"</div>")
                           .AppendLine($"<div class='headBox'>").AppendLine($"<h1>")
                           .AppendLine($"Live Games Summary <br/>For <a class='headerLink' href='{userRecord.Url}'>{chessdotcomUsername}</a><br/>On {DateTime.UtcNow.ToShortDateString()}&nbsp;<small class='priority-2'>({DateTime.UtcNow.ToShortTimeString()} UTC)</small></h1>")
                           .AppendLine($"</div>")
                           .AppendLine($"</div><br/>")
                           .AppendLine($"<div class='ratingRow'>")
                           .AppendLine($"<div class='ratingBox'>")
                           .AppendLine($"<div class='item1 {((userStats.ChessBullet != null) ? "active" : "inactive")}' onclick=\"window.location.href='{STATS_BASE_URL}/live/bullet/{chessdotcomUsername}'\">")
                           .AppendLine($"Bullet {Helpers.ValueOrDash(userStats.ChessBullet?.Last.Rating)}<br/><span class='priority-2'>(Gliko RD&nbsp;{Helpers.ValueOrDash(userStats.ChessBullet?.Last.GlickoRd)})<br/></span>{((userStats.ChessBullet == null) ? "-" : userStats.ChessBullet?.Last.Date.ToShortDateString())}")
                           .AppendLine($"</div></div>")
                           .AppendLine($"<div class='ratingBox'>")
                           .AppendLine($"<div class='item2 {((userStats.ChessBlitz != null) ? "active" : "inactive")}' onclick=\"window.location.href='{STATS_BASE_URL}/live/blitz/{chessdotcomUsername}'\">")
                           .AppendLine($"Blitz {Helpers.ValueOrDash(userStats.ChessBlitz?.Last.Rating)}<br/><span class='priority-2'>(Gliko RD&nbsp;{Helpers.ValueOrDash(userStats.ChessBlitz?.Last.GlickoRd)})<br/></span>{((userStats.ChessBlitz == null) ? "-" : userStats.ChessBlitz?.Last.Date.ToShortDateString())}")
                           .AppendLine($"</div></div>")
                           .AppendLine($"<div class='ratingBox'>")
                           .AppendLine($"<div class='item3 {((userStats.ChessRapid != null) ? "active" : "inactive")}' onclick=\"window.location.href='{STATS_BASE_URL}/live/rapid/{chessdotcomUsername}'\">")
                           .AppendLine($"Rapid {Helpers.ValueOrDash(userStats.ChessRapid?.Last.Rating)}<br/><span class='priority-2'>(Gliko RD&nbsp;{Helpers.ValueOrDash(userStats.ChessRapid?.Last.GlickoRd)})<br/></span>{((userStats.ChessRapid == null) ? "-" : userStats.ChessRapid?.Last.Date.ToShortDateString())}")
                           .AppendLine($"</div></div>")
                           .AppendLine($"</div>")
                           .AppendLine($"<div class='onerow'><div class='onecolumn'>")
                           .AppendLine($"<br/><h2>{pawnFragment}Last 40 Openings</h2>")
                           .AppendLine($"{whiteOpeningsRecenthtmlOut}")
                           .AppendLine($"{blackOpeningsRecenthtmlOut}")
                           .AppendLine($"<br/><h2>{pawnFragment}All Openings (Max 15)</h2>")
                           .AppendLine($"{whiteOpeningshtmlOut}")
                           .AppendLine($"{blackOpeningshtmlOut}");

                if (isCapsIncluded)
                {
                    _ = htmlOut.AppendLine($"<div class='priority-2'>")
                               .AppendLine($"  <br/>")
                               .AppendLine($"  <h2>{pawnFragment}CAPs Rolling 3 Game Avg.</h2>")
                               .AppendLine($"  <div class='priority-2'>")
                               .AppendLine($"    <div class='graphCapsRow'>           ")
                               .AppendLine($"      {capsRollingAverageFivehtmlOut}")
                               .AppendLine($"      <div class='graphCapsBox'>")
                               .AppendLine($"        {rapidFiveAvCaps}")
                               .AppendLine($"      </div>")
                               .AppendLine($"    </div>")
                               .AppendLine($"  </div>")
                               .AppendLine($"</div>");
                }

                _ = htmlOut.AppendLine($"<div class='priority-2'>")
                           .AppendLine($"<br/><h2>{pawnFragment}Ratings/Win Loss Avg.</h2>")
                           .AppendLine($"<div class='graphRow'>")
                           .AppendLine($"<div class='graphBox'>{bulletGraphHtmlFragment}</div>")
                           .AppendLine($"<div class='graphBox'>{blitzGraphHtmlFragment}</div>")
                           .AppendLine($"<div class='graphBox'>{rapidGraphHtmlFragment}</div>")
                           .AppendLine($"</div>")
                           .AppendLine($"</div>")
                           .AppendLine($"<div class='priority-2'>")
                           .AppendLine($"<div class='graphRow'>")
                           .AppendLine($"<div class='graphBox'>{bulletAvStatsGraphHtmlFragment}</div>")
                           .AppendLine($"<div class='graphBox'>{blitzAvStatsGraphHtmlFragment}</div>")
                           .AppendLine($"<div class='graphBox'>{rapidAvStatsGraphHtmlFragment}</div>")
                           .AppendLine($"</div>")
                           .AppendLine($"</div>")
                           .AppendLine($"<br/><h2>{pawnFragment}Stats by Time Control/Month</h2>")
                           .AppendLine(playingStatshtmlOut)
                           .AppendLine($"<br/><h2>{pawnFragment}Time Played by Month</h2>")
                           .AppendLine(timePlayedByMonthhtmlOut)
                           .AppendLine(Helpers.GetHtmlTail(new Uri(CHESSCOM_URL), VERSION_NUMBER, PROJECT_LINK))
                           .AppendLine("</div></div>")
                           .AppendLine("  </body>")
                           .AppendLine("</html>");

                return htmlOut.ToString();
            }).ConfigureAwait(false);
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
                StringBuilder dateLine =  new();
                StringBuilder monthLine = new();
                StringBuilder capsLine =  new();

                foreach (CapsRecord capsRecord in capsTimeControl.Value)
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

        private static async Task<string> BuildTextReport(bool isCapsIncluded, string chessdotcomUsername, string whiteOpeningstextOut, string blackOpeningstextOut, string playingStatstextOut, string timePlayedByMonthtextOut, string capsTabletextOut, string capsRollingAverageTentextOut, string totalSecondsPlayedtextOut)
        {
            return await Task<string>.Run(() =>
            {
                StringBuilder textReport = new();
                _ = textReport.AppendLine(Helpers.GetDisplaySection($"Live Chess Report for {chessdotcomUsername} : {DateTime.UtcNow.ToShortDateString()}@{DateTime.UtcNow.ToShortTimeString()} UTC", true))
                              .Append(whiteOpeningstextOut)
                              .Append(blackOpeningstextOut)
                              .Append(playingStatstextOut)
                              .Append(timePlayedByMonthtextOut);

                if (isCapsIncluded)
                {
                    _ = textReport.Append(capsTabletextOut)
                                  .Append(capsRollingAverageTentextOut);
                }

                _ = textReport.Append(totalSecondsPlayedtextOut)
                              .Append(Helpers.GetDisplaySection("End of Report", true));

                return textReport.ToString();
            }).ConfigureAwait(false);
        }

        private static async Task<string> RenderCapsAverageGraph(Dictionary<string, double[]> graphData)
        {
            return await Task<string>.Run(() =>
            {
                if (graphData == null || graphData.Count < 2)
                {
                    using GraphHelper graphHelperBlank = new(GRAPH_WIDTH, highVal: 150);
                    graphHelperBlank.DrawingSurface.DrawString($"Not enough data", new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Italic), GraphHelper.TextBrush, 1, graphHelperBlank.Height - 30);

                    return Helpers.GetImageAsHtmlFragment(graphHelperBlank.GraphSurface);
                }


                int stepWidth = (int) Math.Ceiling(((double)GRAPH_WIDTH)/6d);

                using GraphHelper graphHelper = new(GRAPH_WIDTH - stepWidth,
                                                    0,
                                                    100,
                                                    GraphHelper.GraphLine.PERCENTAGE);

                //Draw Graph
                foreach (var graphItem in graphData)
                {
                    Pen sidePen = graphItem.Key.Contains("White", StringComparison.InvariantCultureIgnoreCase) ?
                                                                new Pen(Color.Yellow, 3f) : new Pen(Color.Red, 3f);

                    for (int loop = 1; loop < graphItem.Value.Length; loop++)
                    {
                        graphHelper.DrawingSurface.
                                    DrawLine(sidePen,
                                             (loop - 1) * stepWidth,
                                             graphHelper.GetYAxisPoint((int)graphItem.Value[loop - 1]),
                                             loop * stepWidth,
                                             graphHelper.GetYAxisPoint((int)graphItem.Value[loop])
                                             );
                    }
                }

                //Resize graph for output
                using Bitmap bitmapOut = Helpers.ResizeImage(graphHelper.GraphSurface, GRAPH_WIDTH, 150);

                //Add ratings
                using Graphics resizedSurface = Graphics.FromImage(bitmapOut);
                resizedSurface.DrawString($"CAPs (New->Old)", new Font(FontFamily.GenericSansSerif, 14f), GraphHelper.TextBrush, 1, bitmapOut.Height - 30);

                return Helpers.GetImageAsHtmlFragment(bitmapOut);

            }).ConfigureAwait(false);
        }

        private static async Task<string> RenderRatingGraph(int? currentRating, List<(DateTime gameDate, int rating, string gameType)> ratingsPostGame)
        {
            return await Task<string>.Run(() =>
            {
                //If less than 10 games don't graph
                if (ratingsPostGame.Count < 10)
                {
                    using GraphHelper graphHelperBlank = new(GRAPH_WIDTH, highVal: GRAPH_HEIGHT_STATS);
                    graphHelperBlank.DrawingSurface.DrawString($"Not enough data", new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Italic), GraphHelper.TextBrush, 1, graphHelperBlank.Height - 30);

                    return Helpers.GetImageAsHtmlFragment(graphHelperBlank.GraphSurface);
                }

                (DateTime gameDate, int rating)[] ratingsPostGameOrdered = ratingsPostGame.OrderBy(x => x.gameDate).Select(x => (x.gameDate, x.rating)).ToArray();

                int stepWidth = Math.Max(GRAPH_WIDTH / ratingsPostGameOrdered.Length, 1);

                using GraphHelper graphHelper = new(Math.Max(ratingsPostGameOrdered.Length, ratingsPostGameOrdered.Length * stepWidth),
                                                                ratingsPostGame.Select(x => x.rating).Min(),
                                                                ratingsPostGame.Select(x => x.rating).Max(),
                                                                GraphHelper.GraphLine.RATING);

                //Draw Graph
                int graphX = 0;
                DateTime lastDate = DateTime.MinValue;
                Pen currentPen = GraphHelper.OrangePen;

                for (int loop = 0; loop < ratingsPostGameOrdered.Length; loop++)
                {
                    //Switch pen when the month changes
                    if (ratingsPostGameOrdered[loop].gameDate.Month != lastDate.Month)
                    {
                        currentPen = (currentPen.Color.Name == GraphHelper.OrangePen.Color.Name) ? GraphHelper.DarkOrangePen : GraphHelper.OrangePen;
                    }

                    for (int innerLoop = 0; innerLoop < stepWidth; innerLoop++)
                    {
                        graphHelper.DrawingSurface.DrawLine(currentPen,
                                                            graphX,
                                                            graphHelper.GetYAxisPoint(ratingsPostGameOrdered[loop].rating),
                                                            graphX,
                                                            graphHelper.BaseLine);
                        graphX++;
                    }

                    lastDate = ratingsPostGameOrdered[loop].gameDate;
                }

                //Add line for current rating
                if (currentRating != null)
                {
                    graphHelper.DrawingSurface.DrawLine(GraphHelper.RedPen,
                                                        0,
                                                        graphHelper.GetYAxisPoint(currentRating.Value),
                                                        graphHelper.Width,
                                                        graphHelper.GetYAxisPoint(currentRating.Value));
                }

                //Resize graph for output
                using Bitmap bitmapOut = Helpers.ResizeImage(graphHelper.GraphSurface, GRAPH_WIDTH, GRAPH_HEIGHT_STATS);

                //Add ratings
                using Graphics resizedSurface = Graphics.FromImage(bitmapOut);
                resizedSurface.DrawString($"{graphHelper.HighVal}", new Font(FontFamily.GenericSansSerif, 18f), GraphHelper.TextBrush, 1, 1);
                resizedSurface.DrawString($"{graphHelper.LowVal}", new Font(FontFamily.GenericSansSerif, 18f), GraphHelper.TextBrush, 1, bitmapOut.Height - 30);

                return Helpers.GetImageAsHtmlFragment(bitmapOut);

            }).ConfigureAwait(false);
        }

        private static async Task<string> RenderAverageStatsGraph(List<(string TimeControl, int VsMin, int Worst, int LossAv, int DrawAv, int WinAv, int Best, int VsMax)> graphData)
        {
            return await Task<string>.Run(() =>
            {
                bool isGraphRequired = true;
                int graphMin = 0;
                int graphMax = 0;

                if (graphData == null || graphData.Count < 2)
                {
                    isGraphRequired = false;
                }
                else
                {
                    graphMin = graphData.Where(x => x.WinAv != 0 && x.LossAv != 0).Select(x => x.WinAv).DefaultIfEmpty(0).Min();
                    graphMax = graphData.Where(x => x.WinAv != 0 && x.LossAv != 0).Select(x => x.LossAv).DefaultIfEmpty(0).Max();

                    if (graphMin == 0 || graphMax == 0)
                    {
                        isGraphRequired = false;
                    }
                }

                //If less than 10 games don't graph
                if (!isGraphRequired)
                {
                    using GraphHelper graphHelperBlank = new(GRAPH_WIDTH, highVal: GRAPH_HEIGHT_AVERAGE);
                    graphHelperBlank.DrawingSurface.DrawString($"Not enough data", new Font(FontFamily.GenericSansSerif, 10f,FontStyle.Italic), GraphHelper.TextBrush, 1, graphHelperBlank.Height - 30);

                    return Helpers.GetImageAsHtmlFragment(graphHelperBlank.GraphSurface);
                }

                int stepWidth = Math.Max(GRAPH_WIDTH / graphData.Count, 1);

                using GraphHelper graphHelper = new(Math.Max(graphData.Count, graphData.Count * stepWidth),
                                                             graphMin,
                                                             graphMax,
                                                             GraphHelper.GraphLine.RATING);

                //Draw Graph
                Pen currentPen = GraphHelper.OrangePen;
                int graphX = 0;

                for (int loop = 0; loop < graphData.Count; loop++)
                {
                    for (int innerLoop = 0; innerLoop < stepWidth; innerLoop++)
                    {
                        if (graphData[loop].WinAv != 0 &&
                            graphData[loop].LossAv != 0)
                        {
                            graphHelper.DrawingSurface.
                                        DrawLine(currentPen,
                                                 graphX,
                                                 graphHelper.GetYAxisPoint(graphData[loop].WinAv),
                                                 graphX,
                                                 graphHelper.GetYAxisPoint(graphData[loop].LossAv));
                        }

                        graphX++;
                    }

                    currentPen = (currentPen.Color.Name == GraphHelper.OrangePen.Color.Name) ? GraphHelper.DarkOrangePen : GraphHelper.OrangePen;
                }


                //Resize graph for output
                using Bitmap bitmapOut = Helpers.ResizeImage(graphHelper.GraphSurface, GRAPH_WIDTH, GRAPH_HEIGHT_AVERAGE);

                //Add ratings
                using Graphics resizedSurface = Graphics.FromImage(bitmapOut);
                resizedSurface.DrawString($"{graphHelper.HighVal} (Av Loss)", new Font(FontFamily.GenericSansSerif, 18f), GraphHelper.TextBrush, 1, 1);
                resizedSurface.DrawString($"{graphHelper.LowVal} (Av Win)", new Font(FontFamily.GenericSansSerif, 18f), GraphHelper.TextBrush, 1, bitmapOut.Height - 30);

                return Helpers.GetImageAsHtmlFragment(bitmapOut);

            }).ConfigureAwait(false);
        }

        private static void ProcessGameData(string chessdotcomUsername, List<ChessGame> gameList,
                                            out SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentWorstLoss, int OpponentBestWin, int TotalWin, int TotalDraw, int TotalLoss)> secondsPlayedRollup,
                                            out SortedList<string, dynamic> secondsPlayedRollupMonthOnly,
                                            out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhite,
                                            out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlack,
                                            out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhiteRecent,
                                            out SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlackRecent,
                                            out List<(DateTime gameDate, int rating, string gameType)> ratingsPostGame,
                                            out double totalSecondsPlayed)
        {
            //Initialise reporting lists
            secondsPlayedRollup = new SortedList<string, (int, int, int, int, int, int, int, int, int, int, int, int, int, int)>();
            secondsPlayedRollupMonthOnly = new SortedList<string, dynamic>();
            ecoPlayedRollupWhite = new SortedList<string, (string, int, int, int, int)>();
            ecoPlayedRollupBlack = new SortedList<string, (string, int, int, int, int)>();
            ecoPlayedRollupWhiteRecent = new SortedList<string, (string, int, int, int, int)>();
            ecoPlayedRollupBlackRecent = new SortedList<string, (string, int, int, int, int)>();
            ratingsPostGame = new List<(DateTime gameDate, int rating, string gameType)>();
            totalSecondsPlayed = 0;

            int _gameCount = 0;

            foreach (ChessGame game in gameList)
            {
                // Don't include daily games
                if (game.GameAttributes.Attributes["Event"] != "Live Chess")
                {
                    continue;
                }

                ExtractRatings(chessdotcomUsername, game, out string side, out int playerRating, out int opponentRating, out bool? isWin);
                CalculateGameTime(game, out DateTime parsedStartDate, out double seconds, out string gameTime);

                UpdateOpening(ecoPlayedRollupWhite, ecoPlayedRollupBlack, game, side, isWin);

                if (_gameCount++ < 40)
                {
                    UpdateOpening(ecoPlayedRollupWhiteRecent, ecoPlayedRollupBlackRecent, game, side, isWin);
                }

                UpdateGameTypeTimeTotals(secondsPlayedRollup, playerRating, opponentRating, isWin, parsedStartDate, seconds, gameTime);
                UpdateGameTimeTotals(secondsPlayedRollupMonthOnly, parsedStartDate, seconds);
                UpdateAllGameRatingsList(ratingsPostGame, game, playerRating);

                totalSecondsPlayed += seconds;
            }
        }

        private static void UpdateAllGameRatingsList(List<(DateTime gameDate, int rating, string gameType)> ratingsPostGame, ChessGame game, int playerRating)
        {
            if (game.IsRatedGame && (new string[] { "Rapid", "Bullet", "Blitz" }).Contains(game.TimeClass))
            {
                DateTime gameDate = game.GameAttributes.GetAttributeAsNullOrDateTime(SupportedAttribute.EndDate, SupportedAttribute.EndTime).Value;
                ratingsPostGame.Add((gameDate, playerRating, game.TimeClass));
            }
        }

        private static (string textOut, string htmlOut, Dictionary<string, double[]> capsAverageOut) DisplayCapsRollingAverage(int averageOver, Dictionary<string, List<CapsRecord>> capsScores)
        {
            StringBuilder textOut = new ();
            StringBuilder htmlOut = new ();
            Dictionary<string, double[]> capsAverageOut = new();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection($"CAPS Scoring (Rolling {averageOver} Game Average)", false));

            textOut.AppendLine("Control/Side      |   <-Newest                                                             Oldest-> ");
            textOut.AppendLine("------------------+---------------------------------------------------------------------------------");

            htmlOut.AppendLine("<table class='capsRollingTable'><thead><tr><td>Playing As..</td><td colspan='6'>New->Old</td></tr></thead><tbody>");

            foreach (KeyValuePair<string, List<CapsRecord>> capsScore in capsScores)
            {
                if (capsScore.Value.Count > averageOver)
                {
                    List<double> latestCaps = capsScore.Value.Select(x => x.Caps).ToList<double>();
                    List<string> averages = Enumerable.Range(0, (latestCaps.Count + 1) - averageOver)
                                                      .Select(i => Math.Round(latestCaps.Skip(i).Take(averageOver).Average(), 2).ToString("00.00", CultureInfo.InvariantCulture))
                                                      .ToList();

                    string[] avList = averages.Take(10).ToArray();
                    _ = textOut.AppendLine($"{ CultureInfo.InvariantCulture.TextInfo.ToTitleCase(capsScore.Key.PadRight(17))} |   {string.Join(" | ", avList)}");


                    htmlOut.Append($"<tr><td>{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(capsScore.Key)}</td>");
                    double[] scoreList = new double[] { 0, 0, 0, 0, 0, 0 };

                    for (int loop = 0; loop < scoreList.Length; loop++)
                    {
                        htmlOut.AppendLine($"<td>{((loop < avList.Length) ? avList[loop] : "0.00")}</td>");
                        scoreList[loop] = ((loop < avList.Length) ? double.Parse(avList[loop], CultureInfo.InvariantCulture) : 0);
                    }

                    htmlOut.AppendLine($"</tr>");
                    capsAverageOut.Add(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(capsScore.Key), scoreList);
                }
                else
                {
                    htmlOut.Append($"<tr><td>-</td>");
                    for (int loop = 0; loop < 6; loop++)
                    {
                        htmlOut.AppendLine($"<td>0.00</td>");
                    }
                    htmlOut.AppendLine($"</tr>");
                }
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString(), capsAverageOut);
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

        private static void UpdateOpening(SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhite, SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlack, ChessGame game, string side, bool? isWin)
        {
            try
            {
                string ecoHref = game.GameAttributes.Attributes["ECOUrl"];
                string ecoName = game.GameAttributes.Attributes["ECOUrl"].Replace(OPENING_URL, "", true, CultureInfo.InvariantCulture).Replace("-", " ", true, CultureInfo.InvariantCulture);
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
            catch
            {
                //ECO missing from Pgn so just ignore
            }
        }


        private static (string textOut, string htmlOut) DisplayOpeningsAsWhite(SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupWhite)
        {
            StringBuilder textOut = new();
            StringBuilder htmlOut = new();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection($"All Openings (Max 15)", false));
            textOut.AppendLine("Playing As White                                                        | Tot.");
            textOut.AppendLine("------------------------------------------------------------------------+------");

            htmlOut.AppendLine("<table class='whiteOpeningsTable'><thead><tr><td>Playing As White</td><td class='priority-2'>Win</td><td class='priority-2'>Draw</td><td class='priority-2'>Loss</td><td>Total</td></tr></thead><tbody>");

            foreach (KeyValuePair<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoCount in ecoPlayedRollupWhite.OrderByDescending(uses => uses.Value.total).Take(15))
            {
                //if (ecoCount.Value.total < 2) { break; }

                //Calculate highlight class
                int activeCell = (ecoCount.Value.winCount > ecoCount.Value.lossCount) ? 0 : ((ecoCount.Value.winCount < ecoCount.Value.lossCount) ? 2 : 1);
                textOut.AppendLine($"{ecoCount.Key,-71} | {ecoCount.Value.total.ToString(CultureInfo.CurrentCulture),4}");
                htmlOut.AppendLine($"<tr><td><a target='opening' href='{ecoCount.Value.href}'>{ecoCount.Key}</a></td><td{((activeCell == 0) ? " class='higher priority-2'" : " class='priority-2'")}>{ecoCount.Value.winCount.ToString(CultureInfo.InvariantCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCultureIgnoreCase)}</td><td{((activeCell == 1) ? " class='higher priority-2'" : " class='priority-2'")}>{ecoCount.Value.drawCount.ToString(CultureInfo.InvariantCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCultureIgnoreCase)}</td><td{((activeCell == 2) ? " class='lower priority-2'" : " class='priority-2'")}>{ecoCount.Value.lossCount.ToString(CultureInfo.InvariantCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCultureIgnoreCase)}</td><td>{ecoCount.Value.total.ToString(CultureInfo.CurrentCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCultureIgnoreCase)}</td></tr>");
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        private static (string textOut, string htmlOut) DisplayOpeningsAsBlack(SortedList<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoPlayedRollupBlack)
        {
            StringBuilder textOut = new();
            StringBuilder htmlOut = new();

            textOut.AppendLine("");
            textOut.AppendLine("Playing As Black                                                        | Tot.");
            textOut.AppendLine("------------------------------------------------------------------------+------");

            htmlOut.AppendLine("<table class='blackOpeningsTable'><thead><tr><td>Playing As Black</td><td class='priority-2'>Win</td><td class='priority-2'>Draw</td><td class='priority-2'>Loss</td><td>Total</td></tr></thead><tbody>");

            foreach (KeyValuePair<string, (string href, int total, int winCount, int drawCount, int lossCount)> ecoCount in ecoPlayedRollupBlack.OrderByDescending(uses => uses.Value.total).Take(15))
            {
                //if (ecoCount.Value.total < 2) { break; }

                //Calculate highlight class
                int activeCell = (ecoCount.Value.winCount > ecoCount.Value.lossCount) ? 0 : ((ecoCount.Value.winCount < ecoCount.Value.lossCount) ? 2 : 1);
                textOut.AppendLine($"{ecoCount.Key,-71} | {ecoCount.Value.total.ToString(CultureInfo.CurrentCulture),4}");
                htmlOut.AppendLine($"<tr><td><a href='{ecoCount.Value.href}'>{ecoCount.Key}</a></td><td{((activeCell == 0) ? " class='higher priority-2'" : " class='priority-2'")}>{ecoCount.Value.winCount.ToString(CultureInfo.InvariantCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCulture)}</td><td{((activeCell == 1) ? " class='higher priority-2'" : " class='priority-2'")}>{ecoCount.Value.drawCount.ToString(CultureInfo.InvariantCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCulture)}</td><td{((activeCell == 2) ? " class='lower priority-2'" : " class='priority-2'")}>{ecoCount.Value.lossCount.ToString(CultureInfo.InvariantCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCulture)}</td><td>{ecoCount.Value.total.ToString(CultureInfo.InvariantCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCulture)}</td></tr>");
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        private static (string textOut, string htmlOut, List<(string TimeControl, int VsMin, int Worst, int LossAv, int DrawAv, int WinAv, int Best, int VsMax)> graphData) DisplayPlayingStats(SortedList<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentWorstLoss, int OpponentBestWin, int TotalWin, int TotalDraw, int TotalLoss)> secondsPlayedRollup, int? bulletRating, int? blitzRating, int? rapidRating)
        {
            StringBuilder textOut = new();
            StringBuilder htmlOut = new();
            List<(string TimeControl, int VsMin, int Worst, int LossAv, int DrawAv, int WinAv, int Best, int VsMax)> graphData = new();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection("Time Played/Ratings by Time Control/Month", false));
            textOut.AppendLine("Time Control/Month| Play Time | Rating Min/Max/+-  | Vs Min/BestWin/Max | Win  | Draw | Loss | Tot. ");
            string lastLine = "";

            foreach (KeyValuePair<string, (int SecondsPlayed, int GameCount, int Win, int Loss, int Draw, int MinRating, int MaxRating, int OpponentMinRating, int OpponentMaxRating, int OpponentWorstLoss, int OpponentBestWin, int TotalWin, int TotalDraw, int TotalLoss)> rolledUp in secondsPlayedRollup)
            {
                int ratingComparison = rolledUp.Key.Substring(0, 2).ToUpperInvariant() switch
                {
                    "BU" => rolledUp.Key.Contains("NR", StringComparison.InvariantCulture) ? 0 : bulletRating.Value,
                    "BL" => rolledUp.Key.Contains("NR", StringComparison.InvariantCulture) ? 0 : blitzRating.Value,
                    "RA" => rolledUp.Key.Contains("NR", StringComparison.InvariantCulture) ? 0 : rapidRating.Value,
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

                if (!rolledUp.Key.Contains("NR", StringComparison.InvariantCulture))
                {
                    graphData.Add((rolledUp.Key,
                                   rolledUp.Value.OpponentMinRating,
                                   rolledUp.Value.OpponentWorstLoss,
                                   ((rolledUp.Value.Loss == 0) ? 0 : rolledUp.Value.TotalLoss / rolledUp.Value.Loss),
                                   ((rolledUp.Value.Draw == 0) ? 0 : rolledUp.Value.TotalDraw / rolledUp.Value.Draw),
                                   ((rolledUp.Value.Win == 0) ? 0 : rolledUp.Value.TotalWin / rolledUp.Value.Win),
                                   rolledUp.Value.OpponentBestWin,
                                   rolledUp.Value.OpponentMaxRating));
                }
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString(), graphData);
        }

        private static (string textOut, string htmlOut) DisplayTimePlayedByMonth(SortedList<string, dynamic> secondsPlayedRollupMonthOnly)
        {
            StringBuilder textOut = new();
            StringBuilder htmlOut = new();

            textOut.AppendLine("");
            textOut.AppendLine(Helpers.GetDisplaySection("Time Played by Month (All Time Controls)", false));
            textOut.AppendLine("Month             |  Play Time  |   For Year  |  Cumulative ");

            htmlOut.AppendLine("<table class='playingStatsMonthTable'><thead><tr><td>Month</td><td>Play Time</td><td class='priority-2'>For Year</td><td>Cumulative</td></tr></thead><tbody>");

            TimeSpan cumulativeTime = new(0);
            TimeSpan cumulativeTimeForYear = new(0);
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
                                       $"<td>{((int)timeMonth.TotalHours).ToString(CultureInfo.CurrentCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCulture)}:{ timeMonth.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td>" +
                                       $"<td  class='priority-2'>{((int)cumulativeTimeForYear.TotalHours).ToString(CultureInfo.CurrentCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCulture)}:{ cumulativeTimeForYear.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td>" +
                                       $"<td>{((int)cumulativeTime.TotalHours).ToString(CultureInfo.CurrentCulture).PadLeft(5, '$').Replace("$", "&nbsp;", StringComparison.InvariantCulture)}:{ cumulativeTime.Minutes.ToString(CultureInfo.CurrentCulture).PadLeft(2, '0')}</td></tr>"
                                      );

                //Reset until next year detected
                yearSplitClass = "";
            }

            htmlOut.AppendLine("</tbody></table>");

            return (textOut.ToString(), htmlOut.ToString());
        }

        private static (string textOut, string htmlOut) DisplayTotalSecondsPlayed(double totalSecondsPlayed)
        {
            StringBuilder textOut = new();
            StringBuilder htmlOut = new();

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
