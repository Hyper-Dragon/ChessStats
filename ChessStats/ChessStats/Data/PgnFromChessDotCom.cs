using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChessDotComSharp.Models;

namespace ChessStats.Data
{
    public static class PgnFromChessDotCom
    {
        static int gameCount = 0;
        static object displayLock = new object();

        static void ProcessedDisplay(string outChar)
        {
            lock (displayLock)
            {
                if (gameCount++ > 80) { System.Console.WriteLine(); gameCount = 1; }
                System.Console.Write(outChar);
            }
        }

        public static List<ChessGame> FetchGameRecordsForUser(string username)
        {
            var PgnList = new List<ChessGame>();
            
            var t = GetPlayerMonthlyArchive(username);
            t.Wait();

            Parallel.ForEach(t.Result.Archives, (dataForMonth) =>
            {
                var urlSplit = dataForMonth.Split('/');
                var t2 = GetAllPlayerMonthlyGames(username, Int32.Parse(urlSplit[7]), Int32.Parse(urlSplit[8]));
                t2.Wait();

                foreach (var game in t2.Result.Games)
                {
                    try
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
                                BlackRating = game.IsRated ? game.Black.Rating : 0
                            });
                        }
                        else
                        {
                            ProcessedDisplay("X");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.Message);
                    }
                }
            });

            return PgnList;
        }


        static async System.Threading.Tasks.Task<ArchivedGamesList> GetPlayerMonthlyArchive(string username)
        {
            ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
            var myGames = await client.GetPlayerGameArchivesAsync(username);

            return myGames;
        }


        static async System.Threading.Tasks.Task<PlayerArchivedGames> GetAllPlayerMonthlyGames(string username, int year, int month)
        {
            ChessDotComSharp.ChessDotComClient client = new ChessDotComSharp.ChessDotComClient();
            PlayerArchivedGames myGames = new PlayerArchivedGames();

            try
            {
                myGames = await client.GetPlayerGameMonthlyArchiveAsync(username, year, month).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Console.Write(ex.Message);
            }

            return myGames;
        }
    }
}
