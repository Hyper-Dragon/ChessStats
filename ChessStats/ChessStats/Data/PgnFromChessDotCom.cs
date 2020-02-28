using System;
using System.Collections.Generic;
using ChessDotComSharp.Models;


namespace ChessStats.Data
{
    public class PgnFromChessDotCom
    {
        public PgnFromChessDotCom() { }

        public List<PgnText> FetchGameRecordsForUser(string username)
        {
            Console.WriteLine($">>>Starting ChessDotCom Fetch {username}");

            var PgnList = new List<PgnText>();
            int gameCount = 0;

            var t = GetPlayerMonthlyArchive(username);
            t.Wait();

            foreach (var dataForMonth in t.Result.Archives)
            {
                var urlSplit = dataForMonth.Split('/');
                var t2 = GetAllPlayerMonthlyGames(username, Int32.Parse(urlSplit[7]), Int32.Parse(urlSplit[8]));
                t2.Wait();

                foreach (var game in t2.Result.Games)
                {
                    try
                    {
                        Console.Write(".");
                        if (++gameCount > 80) { System.Console.WriteLine(); gameCount = 0; }

                        //Console.WriteLine($"Found {game.White.Username} vs {game.Black.Username}");
                        PgnList.Add(new PgnText() { Source = "ChessDotCom", Text = game.Pgn, TextGameOnly = "", TextHeaderOnly = "" });
                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.Message);
                    }
                }
            }

            Console.WriteLine("");
            Console.WriteLine($">>>Finished ChessDotCom Fetch {username}");

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
            var myGames = await client.GetPlayerGameMonthlyArchiveAsync(username, year, month);

            return myGames;
        }

    }
}
