using ChessDotComSharp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChessStats.Data
{
    public static class PgnFromChessDotCom
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public static List<ChessGame> FetchGameRecordsForUser(string username, DirectoryInfo cacheDir)
        {
            Helpers.ResetDisplayCounter();
            List<ChessGame> PgnList = new List<ChessGame>();

            Task<ArchivedGamesList> t = GetPlayerMonthlyArchive(username);
            t.Wait();

            Parallel.ForEach(t.Result.Archives, new ParallelOptions { MaxDegreeOfParallelism = 1 }, (dataForMonth) =>
            {
                string[] urlSplit = dataForMonth.Split('/');
                Task<PlayerArchivedGames> t2 = GetAllPlayerMonthlyGames(cacheDir,username, int.Parse(urlSplit[7], CultureInfo.InvariantCulture), int.Parse(urlSplit[8], CultureInfo.InvariantCulture));
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

            return PgnList;
        }

        private static async System.Threading.Tasks.Task<ArchivedGamesList> GetPlayerMonthlyArchive(string username)
        {
            using ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
            ArchivedGamesList myGames = await client.GetPlayerGameArchivesAsync(username).ConfigureAwait(true);
            return myGames;
        }

        private static async System.Threading.Tasks.Task<PlayerArchivedGames> GetAllPlayerMonthlyGames(DirectoryInfo cache,string username, int year, int month)
        {
            PlayerArchivedGames myGames;
            string cacheFileName = $"{Path.Combine(cache.FullName,$"{username.ToLowerInvariant()}{year}{month.ToString().PadLeft(2,'0')}")}";
            
            if (File.Exists(cacheFileName)) 
            {
                using FileStream capsFileInStream = File.OpenRead(cacheFileName);
                myGames = await JsonSerializer.DeserializeAsync<PlayerArchivedGames>(capsFileInStream);
            }
            else 
            {
                using ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
                myGames = await client.GetPlayerGameMonthlyArchiveAsync(username, year, month).ConfigureAwait(true);

                // Never cache data for this month
                if ( !(DateTime.UtcNow.Year == year && DateTime.UtcNow.Month == month) )
                {
                    using var capsFileOutStream = File.Create(cacheFileName);
                    await JsonSerializer.SerializeAsync(capsFileOutStream, myGames).ConfigureAwait(false);
                }
            }

            return myGames;
        }
    }
}
