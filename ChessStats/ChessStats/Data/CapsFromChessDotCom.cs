using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChessStats.Data
{
    public static class CapsFromChessDotCom
    {
        public static async Task<Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>>> GetCapsScores(DirectoryInfo cache, string chessdotcomUsername, int maxPages)
        {
            Helpers.ResetDisplayCounter();

            string cacheFileName = $"{Path.Combine(cache.FullName, $"{chessdotcomUsername.ToLowerInvariant()}Caps")}";
            Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>> capsScores = new Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>>();

            if (File.Exists(cacheFileName))
            {
                using FileStream capsFileInStream = File.OpenRead(cacheFileName);
                capsScores = await JsonSerializer.DeserializeAsync<Dictionary<string, List<(double Caps, DateTime GameDate, string GameYearMonth)>>>(capsFileInStream);
                capsScores.Clear();
            }

            foreach (string control in new string[] { "bullet", "blitz", "rapid" })
            {
                foreach (string colour in new string[] { "white", "black" })
                {
                    string iterationKey = $"{control} {colour}";
                    capsScores.Add(iterationKey, new List<(double Caps, DateTime GameDate, string GameYearMonth)>());

                    for (int page = 1; page <= maxPages; page++)
                    {
                        List<double> capsScoreWhite = new List<double>();

                        using HttpClient client = new HttpClient();
                        HttpResponseMessage response = await client.GetAsync(new Uri($"https://www.chess.com/games/archive/{chessdotcomUsername}?color={colour}&gameOwner=other_game&gameType=live&gameTypeslive%5B%5D={control}&rated=rated&timeSort=desc&page={page}")).ConfigureAwait(false);
                        string pageContents = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        HtmlDocument pageDocument = new HtmlDocument();
                        pageDocument.LoadHtml(pageContents);

                        HtmlNodeCollection nodeCollection = pageDocument.DocumentNode.SelectNodes("//*[contains(@class,'archive-games-table')]");

                        if (nodeCollection == null || nodeCollection[0].InnerText.Contains("No results found."))
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
                                    double caps = double.Parse(row.SelectNodes("td[contains(@class,'archive-games-analyze-cell')]/div")[(colour == "white") ? 0 : 1].InnerText);
                                    DateTime gameDate = DateTime.Parse(row.SelectNodes("td[contains(@class,'archive-games-date-cell')]")[0].InnerText.Trim(new char[] { ' ', '\n', '\r' }).Replace(",", ""));
                                    string GameYearMonth = $"{gameDate.Year}-{gameDate.Month.ToString().PadLeft(2, '0')}";

                                    Helpers.ProcessedDisplay(".");
                                    capsScores[iterationKey].Add((caps, gameDate, GameYearMonth));
                                }
                                catch (System.NullReferenceException)
                                {
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

            using FileStream capsFileOutStream = File.Create(cacheFileName);
            await JsonSerializer.SerializeAsync(capsFileOutStream, capsScores).ConfigureAwait(false);
            await capsFileOutStream.FlushAsync().ConfigureAwait(false);

            return capsScores;
        }
    }
}
