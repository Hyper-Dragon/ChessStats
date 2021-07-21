using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChessStats.Data
{
    public class CapsRecord
    {
        public double Caps { get; set; }
        public DateTime GameDate { get; set; }
        public string GameYearMonth => $"{GameDate.Year}-{GameDate.Month.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}";
    }

    public static class CapsFromChessDotCom
    {
        public static async Task<Dictionary<string, List<CapsRecord>>> GetCapsScoresJson(int chessdotcomPlayerId, string capsUrl)
        {
            Helpers.ResetDisplayCounter();

            using HttpClient client2 = new HttpClient();
            HttpResponseMessage response2 = await client2.GetAsync(new Uri($"{capsUrl}{chessdotcomPlayerId}")).ConfigureAwait(false);
            string pageContents2 = await response2.Content.ReadAsStringAsync().ConfigureAwait(false);

            List<Root> deserializedArchiveRecords = JsonSerializer.Deserialize<List<Root>>(pageContents2, new JsonSerializerOptions() { IgnoreNullValues = true });

            Dictionary<string, List<CapsRecord>> capsScores = new Dictionary<string, List<CapsRecord>>
            {
                { $"White", new List<CapsRecord>() },
                { $"Black", new List<CapsRecord>() }
            };

            foreach (var record in deserializedArchiveRecords)
            {
                List<double> capsScoreWhite = new List<double>();
                List<double> capsScoreBlack = new List<double>();

                try
                {
                    if (record.User1Accuracy.HasValue && record.User2Accuracy.HasValue) 
                    {
                        CapsRecord capsRecord = new CapsRecord()
                        {
                            Caps = record.User1.Id == chessdotcomPlayerId ? record.User1Accuracy.Value : record.User2Accuracy.Value,
                            GameDate = DateTime.Parse(record.GameEndTime),
                        };

                        Helpers.ProcessedDisplay(".");
                        capsScores[$"{((record.User1.Id == chessdotcomPlayerId) ? "White" : "Black")}"].Add(capsRecord);
                    }
                }
                catch (System.NullReferenceException)
                {
                    //Value missing
                    Helpers.ProcessedDisplay("-");
                }
                catch
                {
                    Helpers.ProcessedDisplay("E");
                }
            }

            //}
            //}

            return capsScores;
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
