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

namespace ChessStats.Data
{
    public static class PgnFromChessDotCom
    {
        public static async Task<(PlayerProfile userRecord, PlayerStats userStats)> FetchUserData(string username)
        {
            using ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
            PlayerProfile userRecord = await client.GetPlayerProfileAsync(username).ConfigureAwait(true);
            PlayerStats userStats = await client.GetPlayerStatsAsync(username).ConfigureAwait(true);

            return (userRecord, userStats);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public static async  Task<List<ChessGame>> FetchGameRecordsForUser(string username, DirectoryInfo cacheDir)
        {
            Helpers.ResetDisplayCounter();
            ConcurrentBag<ChessGame> PgnList = new ConcurrentBag<ChessGame>();

            ArchivedGamesList monthlyArchive = await GetPlayerMonthlyArchive(username).ConfigureAwait(false);
            
            Parallel.ForEach(monthlyArchive.Archives, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (dataForMonth) =>
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
                            Helpers.ProcessedDisplay(".");

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
                                GameAttributes = GameHeader.GetHeaderAttributesFromPgn(game.Pgn)
                            });
                        }
                        else
                        {
                            Helpers.ProcessedDisplay("X");
                        }
                    }
                }
                catch
                {
                    Helpers.ProcessedDisplay("E");
                }
            });

            return PgnList.ToList();
        }

        private static async System.Threading.Tasks.Task<ArchivedGamesList> GetPlayerMonthlyArchive(string username)
        {
            using ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
            ArchivedGamesList myGames = await client.GetPlayerGameArchivesAsync(username).ConfigureAwait(true);
            return myGames;
        }

        //Api lock
        private static readonly SemaphoreSlim apiSemaphore = new SemaphoreSlim(1, 1);

        private static async System.Threading.Tasks.Task<PlayerArchivedGames> GetAllPlayerMonthlyGames(DirectoryInfo cache, string username, int year, int month)
        {
            PlayerArchivedGames myGames;
            string cacheFileName = $"{Path.Combine(cache.FullName, $"{username}{year}{month.ToString().PadLeft(2, '0')}")}";

            if (File.Exists(cacheFileName))
            {
                using FileStream gameFileInStream = File.OpenRead(cacheFileName);
                myGames = await JsonSerializer.DeserializeAsync<PlayerArchivedGames>(gameFileInStream);
            }
            else
            {
                //Prevent rate limit errors on the API
                await apiSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    using ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
                    myGames = await client.GetPlayerGameMonthlyArchiveAsync(username, year, month).ConfigureAwait(true);

                    // Never cache data for this month
                    if (!(DateTime.UtcNow.Year == year && DateTime.UtcNow.Month == month))
                    {
                        using FileStream gameFileOutStream = File.Create(cacheFileName);
                        await JsonSerializer.SerializeAsync(gameFileOutStream, myGames).ConfigureAwait(false);
                    }
                }
                finally
                {
                    apiSemaphore.Release();
                }
            }

            return myGames;
        }
    }
}
