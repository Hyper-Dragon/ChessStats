using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
                capsScoresCached = await JsonSerializer.DeserializeAsync<Dictionary<string, List<CapsRecord>>>(capsFileInStream);

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

                        if (nodeCollection == null || nodeCollection[0].InnerText.Contains("No results found",StringComparison.InvariantCultureIgnoreCase))
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
}
