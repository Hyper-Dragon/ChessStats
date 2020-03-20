using ChessDotComSharp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChessStats.Data
{
    public static class PgnFromChessDotCom
    {
        private static int gameCount = 0;
        private static readonly object displayLock = new object();

        private static void ProcessedDisplay(string outChar)
        {
            lock (displayLock)
            {
                if (gameCount++ > 99) { System.Console.WriteLine(); gameCount = 1; }
                System.Console.Write(outChar);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public static List<ChessGame> FetchGameRecordsForUser(string username)
        {
            List<ChessGame> PgnList = new List<ChessGame>();

            Task<ArchivedGamesList> t = GetPlayerMonthlyArchive(username);
            t.Wait();

            Parallel.ForEach(t.Result.Archives, new ParallelOptions { MaxDegreeOfParallelism = 3 }, (dataForMonth) =>
            {
                string[] urlSplit = dataForMonth.Split('/');
                Task<PlayerArchivedGames> t2 = GetAllPlayerMonthlyGames(username, int.Parse(urlSplit[7]), int.Parse(urlSplit[8]));
                t2.Wait();

                try
                {
                    foreach (ArchiveGame game in t2.Result.Games)
                    {

                        if (game.Rules == GameVariant.Chess)
                        {
                            ProcessedDisplay(".");

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
                            ProcessedDisplay("X");
                        }
                    }
                }
                catch
                {
                    //Ignore individual game errors
                    ProcessedDisplay("E");
                }
            });

            return PgnList;
        }

        private static async System.Threading.Tasks.Task<ArchivedGamesList> GetPlayerMonthlyArchive(string username)
        {
            ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
            ArchivedGamesList myGames = await client.GetPlayerGameArchivesAsync(username).ConfigureAwait(true);

            return myGames;
        }

        private static async System.Threading.Tasks.Task<PlayerArchivedGames> GetAllPlayerMonthlyGames(string username, int year, int month)
        {
            ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
            PlayerArchivedGames myGames = new PlayerArchivedGames();

            try
            {
                myGames = await client.GetPlayerGameMonthlyArchiveAsync(username, year, month).ConfigureAwait(true);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                System.Console.Write(ex.Message);
            }
#pragma warning restore CA1031 // Do not catch general exception types

            return myGames;
        }
    }
}
