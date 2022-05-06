using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChessStats.Data
{
    public class CapsRecord
    {
        public enum GameEndState { WHITE, BLACK, DRAW };
        public GameEndState GameResult { get; set; }
        public string ResultReson { get; set; }
        public double Caps { get; set; }
        public string TimeClass { get; set; }
        public bool IsWin { get; set; }
        public bool IsDraw { get; set; }
        public DateTime GameDate { get; set; }
        public string GameYearMonth => $"{GameDate.Year}-{GameDate.Month.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}";
    }

    public static class CapsFromChessDotCom
    {
        public static async Task<Dictionary<string, List<CapsRecord>>> GetCapsScoresJson(string chessdotcomUsername, List<ChessGame> gameList)
        {
            return await Task.Run<Dictionary<string, List<CapsRecord>>>(() =>
            {
                Helpers.StatsConsole.ResetDisplayCounter();

                Dictionary<string, List<CapsRecord>> capsScores = new() {
                                                                            { $"White", new List<CapsRecord>() },
                                                                            { $"Black", new List<CapsRecord>() }
                                                                        };

                foreach (ChessGame game in gameList)
                {
                    if (game.WhiteCaps > 0 && game.BlackCaps > 0 && game.IsRatedGame)
                    {
                        string[] dateSplit = game.GameAttributes.Attributes["Date"].Split('.');

                        capsScores[$"{((game.GameAttributes.Attributes["White"] == chessdotcomUsername) ? "White" : "Black")}"]
                            .Add(new CapsRecord()
                            {
                                GameResult = (game.GameAttributes.Attributes["Result"] == "1-0") ? CapsRecord.GameEndState.WHITE :
                                             (game.GameAttributes.Attributes["Result"] == "0-1") ? CapsRecord.GameEndState.BLACK :
                                             CapsRecord.GameEndState.DRAW,
                                ResultReson = game.GameAttributes.Attributes["Termination"],
                                IsWin = (game.GameAttributes.Attributes["White"] == chessdotcomUsername &&
                                           game.GameAttributes.Attributes["Result"] == "1-0") ||
                                          (game.GameAttributes.Attributes["Black"] == chessdotcomUsername &&
                                           game.GameAttributes.Attributes["Result"] == "0-1"),
                                IsDraw = game.GameAttributes.Attributes["White"] == chessdotcomUsername &&
                                           game.GameAttributes.Attributes["Result"] != "1-0" &&
                                          game.GameAttributes.Attributes["Black"] == chessdotcomUsername &&
                                           game.GameAttributes.Attributes["Result"] != "0-1",
                                TimeClass = game.TimeClass,
                                Caps = (game.GameAttributes.Attributes["White"] == chessdotcomUsername) ? game.WhiteCaps : game.BlackCaps,
                                GameDate = new DateTime(int.Parse(dateSplit[0]), int.Parse(dateSplit[1]), int.Parse(dateSplit[2]))
                            });
                    }
                }

                return capsScores;
            });
        }
    }

    public class GameType
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("isChess960")]
        public bool IsChess960 { get; set; }

        [JsonPropertyName("isKingOfTheHill")]
        public bool IsKingOfTheHill { get; set; }

        [JsonPropertyName("isThreeCheck")]
        public bool IsThreeCheck { get; set; }

        [JsonPropertyName("isBughouse")]
        public bool IsBughouse { get; set; }

        [JsonPropertyName("isCrazyHouse")]
        public bool IsCrazyHouse { get; set; }
    }

    public class User1
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public object Title { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("countryId")]
        public int? CountryId { get; set; }

        [JsonPropertyName("countryName")]
        public string CountryName { get; set; }

        [JsonPropertyName("membershipLevel")]
        public int? MembershipLevel { get; set; }

        [JsonPropertyName("flairCode")]
        public string FlairCode { get; set; }
    }

    public class User2
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public object Title { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("countryId")]
        public int? CountryId { get; set; }

        [JsonPropertyName("countryName")]
        public string CountryName { get; set; }

        [JsonPropertyName("membershipLevel")]
        public int MembershipLevel { get; set; }

        [JsonPropertyName("flairCode")]
        public string FlairCode { get; set; }
    }

    public class Root
    {
        [JsonPropertyName("id")]
        public object Id { get; set; }

        [JsonPropertyName("fen")]
        public string Fen { get; set; }

        [JsonPropertyName("daysPerTurn")]
        public double? DaysPerTurn { get; set; }

        [JsonPropertyName("moves")]
        public int? Moves { get; set; }

        [JsonPropertyName("user1Rating")]
        public int? User1Rating { get; set; }

        [JsonPropertyName("user2Rating")]
        public int? User2Rating { get; set; }

        [JsonPropertyName("user1Result")]
        public double? User1Result { get; set; }

        [JsonPropertyName("user2Result")]
        public double? User2Result { get; set; }

        [JsonPropertyName("isTournament")]
        public bool? IsTournament { get; set; }

        [JsonPropertyName("isTeamMatch")]
        public bool? IsTeamMatch { get; set; }

        [JsonPropertyName("highlightSquares")]
        public string HighlightSquares { get; set; }

        [JsonPropertyName("gameEndTime")]
        public string GameEndTime { get; set; }

        [JsonPropertyName("isTimeout")]
        public bool? IsTimeout { get; set; }

        [JsonPropertyName("isLive")]
        public bool? IsLive { get; set; }

        [JsonPropertyName("isVsComputer")]
        public bool? IsVsComputer { get; set; }

        [JsonPropertyName("gameType")]
        public GameType GameType { get; set; }

        [JsonPropertyName("gameTimeClass")]
        public string GameTimeClass { get; set; }

        [JsonPropertyName("baseTime1")]
        public int? BaseTime1 { get; set; }

        [JsonPropertyName("timeIncrement")]
        public int? TimeIncrement { get; set; }

        [JsonPropertyName("user1")]
        public User1 User1 { get; set; }

        [JsonPropertyName("user2")]
        public User2 User2 { get; set; }

        [JsonPropertyName("isArena")]
        public bool? IsArena { get; set; }

        [JsonPropertyName("user1Accuracy")]
        public double? User1Accuracy { get; set; }

        [JsonPropertyName("user2Accuracy")]
        public double? User2Accuracy { get; set; }
    }

}
