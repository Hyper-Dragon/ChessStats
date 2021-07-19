using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Dynamic;
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We are page scraping as CAPS Scores are unavailable via the API) - If error just show on the console")]


        public static async Task<Dictionary<string, List<CapsRecord>>> GetCapsScoresJson(DirectoryInfo cache, string chessdotcomUsername, int chessdotcomPlayerId, int maxPages, int maxPagesWithCache)
        {
            if (cache == null) { throw new ArgumentNullException(nameof(cache)); }

            Helpers.ResetDisplayCounter();

            using HttpClient client2 = new HttpClient();
            HttpResponseMessage response2 = await client2.GetAsync(new Uri($"https://www.chess.com/callback/user/daily/archive?all=0&userId={chessdotcomPlayerId}")).ConfigureAwait(false);
            string pageContents2 = await response2.Content.ReadAsStringAsync().ConfigureAwait(false);

            List<Root> myDeserializedClass = JsonSerializer.Deserialize<List<Root>>(pageContents2);




            string cacheFileName = $"{Path.Combine(cache.FullName, $"{chessdotcomUsername}Caps")}";
            Dictionary<string, List<CapsRecord>> capsScores = new Dictionary<string, List<CapsRecord>>();
            Dictionary<string, List<CapsRecord>> capsScoresCached = new Dictionary<string, List<CapsRecord>>();

            /*
            if (File.Exists(cacheFileName))
            {
                using FileStream capsFileInStream = File.OpenRead(cacheFileName);
                capsScoresCached = await JsonSerializer.DeserializeAsync<Dictionary<string, List<CapsRecord>>>(capsFileInStream).ConfigureAwait(false);

                //If we have cached records only pull back the first few pages
                maxPages = maxPagesWithCache;
            }
            */

            foreach (string control in new string[] { "bullet", "blitz", "rapid" })
            {
                foreach (string colour in new string[] { "white", "black" })
                {
                    string iterationKey = $"{control} {colour}";
                    capsScores.Add(iterationKey, new List<CapsRecord>());

                    foreach (var record in myDeserializedClass)
                    {
                        List<double> capsScoreWhite = new List<double>();

                        try
                        {
                            CapsRecord capsRecord = new CapsRecord()
                            {
                                Caps = record.User1Accuracy,
                                GameDate = DateTime.Parse(record.GameEndTime),
                            };

                            Helpers.ProcessedDisplay(".");
                            capsScores[iterationKey].Add(capsRecord);
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

                }
            }



           

            //Resolve cache
            /*
            if (capsScoresCached.Count != 0)
            {
                foreach (string capsKey in capsScores.Where(x => x.Value.Count > 0).Select(x => x.Key).ToArray())
                {
                    //Remove records from the cache equal or after the new data
                    DateTime firstDate = capsScores[capsKey].Select(x => x.GameDate).Min();
                    capsScoresCached[capsKey] = capsScoresCached[capsKey].Where(x => DateTime.Compare(x.GameDate, firstDate) <= 0).ToList<CapsRecord>();
                    capsScores[capsKey] = capsScores[capsKey].Where(x => DateTime.Compare(x.GameDate, firstDate) > 0).ToList<CapsRecord>();
                    //Merge the lists back together
                    capsScores[capsKey] = capsScores[capsKey].Concat(capsScoresCached[capsKey]).ToList<CapsRecord>();
                }
            }

            using FileStream capsFileOutStream = File.Create(cacheFileName);
            await JsonSerializer.SerializeAsync(capsFileOutStream, capsScores).ConfigureAwait(false);
            await capsFileOutStream.FlushAsync().ConfigureAwait(false);
            */

            return capsScores;
        }


        [Obsolete("GetCapsScores is deprecated, please use GetCapsScoresJson instead.", true)]
        public static async Task<Dictionary<string, List<CapsRecord>>> GetCapsScores(DirectoryInfo cache, string chessdotcomUsername, int maxPages, int maxPagesWithCache)
        {
            if (cache == null) { throw new ArgumentNullException(nameof(cache)); }

            Helpers.ResetDisplayCounter();
            string cacheFileName = $"{Path.Combine(cache.FullName, $"{chessdotcomUsername}Caps")}";
            Dictionary<string, List<CapsRecord>> capsScores = new Dictionary<string, List<CapsRecord>>();
            Dictionary<string, List<CapsRecord>> capsScoresCached = new Dictionary<string, List<CapsRecord>>();

            if (File.Exists(cacheFileName))
            {
                using FileStream capsFileInStream = File.OpenRead(cacheFileName);
                capsScoresCached = await JsonSerializer.DeserializeAsync<Dictionary<string, List<CapsRecord>>>(capsFileInStream).ConfigureAwait(false);

                //If we have cached records only pull back the first few pages
                maxPages = maxPagesWithCache;
            }

            foreach (string control in new string[] { "bullet", "blitz", "rapid" })
            {
                foreach (string colour in new string[] { "white", "black" })
                {
                    string iterationKey = $"{control} {colour}";
                    capsScores.Add(iterationKey, new List<CapsRecord>());

                    for (int page = 1; page <= maxPages; page++)
                    {
                        List<double> capsScoreWhite = new List<double>();

                        using HttpClient client = new HttpClient();
                        HttpResponseMessage response = await client.GetAsync(new Uri($"https://www.chess.com/games/archive/{chessdotcomUsername}?color={colour}&gameOwner=other_game&gameType=live&gameTypeslive%5B%5D={control}&rated=rated&timeSort=desc&page={page}")).ConfigureAwait(false);
                        string pageContents = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        HtmlDocument pageDocument = new HtmlDocument();
                        pageDocument.LoadHtml(pageContents);

                        HtmlNodeCollection nodeCollection = pageDocument.DocumentNode.SelectNodes("//*[contains(@class,'archive-games-table')]");

                        if (nodeCollection == null || nodeCollection[0].InnerText.Contains("No results found", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Helpers.ProcessedDisplay("X");
                            break;
                        }
                        else
                        {
                            foreach (HtmlNode row in nodeCollection[0].SelectNodes("//tr[contains(@class,'v-board-popover')]").Cast<HtmlNode>())
                            {
                                try
                                {
                                    CapsRecord capsRecord = new CapsRecord()
                                    {
                                        Caps = double.Parse(row.SelectNodes("td[contains(@class,'archive-games-analyze-cell')]/div")[(colour == "white") ? 0 : 1]
                                                               .InnerText, CultureInfo.InvariantCulture),
                                        GameDate = DateTime.Parse(row.SelectNodes("td[contains(@class,'archive-games-date-cell')]")[0]
                                                                     .InnerText
                                                                     .Trim(new char[] { ' ', '\n', '\r' })
                                                                     .Replace(",", "", StringComparison.InvariantCultureIgnoreCase), CultureInfo.InvariantCulture),
                                    };

                                    Helpers.ProcessedDisplay(".");
                                    capsScores[iterationKey].Add(capsRecord);
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
                        }
                    }
                }
            }

            //Resolve cache
            if (capsScoresCached.Count != 0)
            {
                foreach (string capsKey in capsScores.Where(x => x.Value.Count > 0).Select(x => x.Key).ToArray())
                {
                    //Remove records from the cache equal or after the new data
                    DateTime firstDate = capsScores[capsKey].Select(x => x.GameDate).Min();
                    capsScoresCached[capsKey] = capsScoresCached[capsKey].Where(x => DateTime.Compare(x.GameDate, firstDate) <= 0).ToList<CapsRecord>();
                    capsScores[capsKey] = capsScores[capsKey].Where(x => DateTime.Compare(x.GameDate, firstDate) > 0).ToList<CapsRecord>();
                    //Merge the lists back together
                    capsScores[capsKey] = capsScores[capsKey].Concat(capsScoresCached[capsKey]).ToList<CapsRecord>();
                }
            }

            using FileStream capsFileOutStream = File.Create(cacheFileName);
            await JsonSerializer.SerializeAsync(capsFileOutStream, capsScores).ConfigureAwait(false);
            await capsFileOutStream.FlushAsync().ConfigureAwait(false);

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
        public int CountryId { get; set; }

        [JsonPropertyName("countryName")]
        public string CountryName { get; set; }

        [JsonPropertyName("membershipLevel")]
        public int MembershipLevel { get; set; }

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
        public int CountryId { get; set; }

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
        public double DaysPerTurn { get; set; }

        [JsonPropertyName("moves")]
        public int Moves { get; set; }

        [JsonPropertyName("user1Rating")]
        public int User1Rating { get; set; }

        [JsonPropertyName("user2Rating")]
        public int User2Rating { get; set; }

        [JsonPropertyName("user1Result")]
        public double User1Result { get; set; }

        [JsonPropertyName("user2Result")]
        public double User2Result { get; set; }

        [JsonPropertyName("isTournament")]
        public bool IsTournament { get; set; }

        [JsonPropertyName("isTeamMatch")]
        public bool IsTeamMatch { get; set; }

        [JsonPropertyName("highlightSquares")]
        public string HighlightSquares { get; set; }

        [JsonPropertyName("gameEndTime")]
        public string GameEndTime { get; set; }

        [JsonPropertyName("isTimeout")]
        public bool IsTimeout { get; set; }

        [JsonPropertyName("isLive")]
        public bool IsLive { get; set; }

        [JsonPropertyName("isVsComputer")]
        public bool IsVsComputer { get; set; }

        [JsonPropertyName("gameType")]
        public GameType GameType { get; set; }

        [JsonPropertyName("gameTimeClass")]
        public string GameTimeClass { get; set; }

        [JsonPropertyName("baseTime1")]
        public int BaseTime1 { get; set; }

        [JsonPropertyName("timeIncrement")]
        public int TimeIncrement { get; set; }

        [JsonPropertyName("user1")]
        public User1 User1 { get; set; }

        [JsonPropertyName("user2")]
        public User2 User2 { get; set; }

        [JsonPropertyName("isArena")]
        public bool IsArena { get; set; }

        [JsonPropertyName("user1Accuracy")]
        public double User1Accuracy { get; set; }

        [JsonPropertyName("user2Accuracy")]
        public double User2Accuracy { get; set; }
    }

}
