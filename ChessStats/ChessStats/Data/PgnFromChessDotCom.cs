using ChessDotComSharp.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static ChessStats.Data.GameHeader;

namespace ChessStats.Data
{
    public static class PgnFromChessDotCom
    {
        public static async Task<(PlayerProfile userRecord, PlayerStats userStats)> FetchUserData(string username)
        {
            using ChessDotComSharp.ChessDotComClient client = new();
            PlayerProfile userRecord = await client.GetPlayerProfileAsync(username).ConfigureAwait(false);
            PlayerStats userStats = await client.GetPlayerStatsAsync(username).ConfigureAwait(false);

            return (userRecord, userStats);
        }

        public static async Task<List<ChessGame>> FetchGameRecordsForUser(string username, DirectoryInfo cacheDir)
        {
            Helpers.StatsConsole.ResetDisplayCounter();
            ConcurrentBag<ChessGame> PgnList = new();

            ArchivedGamesList monthlyArchive = await GetPlayerMonthlyArchive(username).ConfigureAwait(false);

            _ = Parallel.ForEach(monthlyArchive.Archives, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (dataForMonth) =>
            {
                string[] urlSplit = dataForMonth.Split('/');
                Task<PlayerArchivedGames> t2 = GetAllPlayerMonthlyGames(cacheDir, username, int.Parse(urlSplit[7], CultureInfo.InvariantCulture), int.Parse(urlSplit[8], CultureInfo.InvariantCulture));
                t2.Wait();

                try
                {
                    foreach (ArchiveGame game in t2.Result.Games)
                    {
                        if (game.Rules == GameVariant.Chess)
                        {
                            Helpers.StatsConsole.ProcessedDisplay(".");

                            PgnList.Add(new ChessGame()
                            {
                                Source = "ChessDotCom",
                                Text = game.Pgn,
                                IsRatedGame = game.IsRated,
                                Rules = GameVariant.Chess.ToString(),
                                TimeControl = game.TimeControl,
                                TimeClass = game.TimeClass.ToString(),
                                WhiteRating = game.IsRated ? game.White.Rating : 0,
                                BlackRating = game.IsRated ? game.Black.Rating : 0,
                                WhiteCaps = game?.Accuracies?.White ?? 0f,
                                BlackCaps = game?.Accuracies?.Black ?? 0f,
                                GameAttributes = GameHeader.GetHeaderAttributesFromPgn(game.Pgn)
                            });
                        }
                        else
                        {
                            Helpers.StatsConsole.ProcessedDisplay("X");
                        }
                    }
                }
                catch
                {
                    Helpers.StatsConsole.ProcessedDisplay("E");
                }
            });

            // Make sure the list is sorted...
            // This matters for the Last 40 Openings table
            return PgnList.OrderByDescending(o => o.GameAttributes.GetAttributeAsNullOrDateTime(SupportedAttribute.EndDate, SupportedAttribute.EndTime)).ToList();
        }

        private static async System.Threading.Tasks.Task<ArchivedGamesList> GetPlayerMonthlyArchive(string username)
        {
            using ChessDotComSharp.ChessDotComClient client = new();
            ArchivedGamesList myGames = await client.GetPlayerGameArchivesAsync(username).ConfigureAwait(true);
            return myGames;
        }

        //Api lock
        private static readonly SemaphoreSlim apiSemaphore = new(1, 1);

        private static async System.Threading.Tasks.Task<PlayerArchivedGames> GetAllPlayerMonthlyGames(DirectoryInfo cache, string username, int year, int month)
        {
            PlayerArchivedGames myGames;
            string cacheFileName = $"{Path.Combine(cache.FullName, $"{username}{year}{month.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}")}";

            if (File.Exists(cacheFileName))
            {
                using FileStream gameFileInStream = File.OpenRead(cacheFileName);
                myGames = await JsonSerializer.DeserializeAsync<PlayerArchivedGames>(gameFileInStream).ConfigureAwait(false);
            }
            else
            {
                //Prevent rate limit errors on the API
                await apiSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    using ChessDotComSharp.ChessDotComClient client = new();
                    myGames = await client.GetPlayerGameMonthlyArchiveAsync(username, year, month).ConfigureAwait(true);


                    // Never cache data for this or the previous month since we may have new CAPS scores generated
                    // (there may of course be older scores we don't have but that will be upto the user to clear the
                    //  cache if these are required)
                    if (!((DateTime.UtcNow.Year == year && DateTime.UtcNow.Month == month) ||
                          (DateTime.UtcNow.AddMonths(-1).Year == year && DateTime.UtcNow.AddMonths(-1).Month == month)))
                    {
                        using FileStream gameFileOutStream = File.Create(cacheFileName);
                        await JsonSerializer.SerializeAsync(gameFileOutStream, myGames).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _ = apiSemaphore.Release();
                }
            }

            return myGames;
        }
    }
}
