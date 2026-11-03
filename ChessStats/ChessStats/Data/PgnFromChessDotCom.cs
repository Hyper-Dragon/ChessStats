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
using static ChessStats.Data.GameHeader;

namespace ChessStats.Data
{
    public static class PgnFromChessDotCom
    {
        public static async Task<(PlayerProfile userRecord, PlayerStats userStats)> FetchUserData(string username)
        {
            using ChessDotComSharp.ChessDotComClient client = new(Helpers.ChessDotComApi.UserAgent);
            PlayerProfile userRecord = await client.GetPlayerProfileAsync(username).ConfigureAwait(false);
            PlayerStats userStats = await client.GetPlayerStatsAsync(username).ConfigureAwait(false);

            return (userRecord, userStats);
        }

        public static async Task<List<ChessGame>> FetchGameRecordsForUser(string username, DirectoryInfo cacheDir)
        {
            Helpers.StatsConsole.ResetDisplayCounter();
            ConcurrentBag<ChessGame> PgnList = new();

            ConcurrentBag<string> failures = new();

            ArchivedGamesList monthlyArchive = await GetPlayerMonthlyArchive(username).ConfigureAwait(false);

            if (monthlyArchive?.Archives is null || monthlyArchive.Archives.Count == 0)
            {
                throw new InvalidOperationException($"Chess.com returned no game archives for '{username}'.");
            }

            _ = Parallel.ForEach(monthlyArchive.Archives, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (dataForMonth) =>
            {
                if (!TryGetArchiveYearMonth(dataForMonth, out int year, out int month))
                {
                    Helpers.StatsConsole.ProcessedDisplay("E");
                    failures.Add($"{dataForMonth} (unrecognised archive URL)");
                    return;
                }

                try
                {
                    Task<PlayerArchivedGames> t2 = GetAllPlayerMonthlyGames(cacheDir, username, year, month);
                    t2.Wait();

                    foreach (ArchiveGame game in t2.Result.Games)
                    {
                        if (game.Rules == GameVariant.Chess)
                        {
                            Helpers.StatsConsole.ProcessedDisplay(".");

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
                                WhiteCaps = game?.Accuracies?.White ?? 0f,
                                BlackCaps = game?.Accuracies?.Black ?? 0f,
                                GameAttributes = GameHeader.GetHeaderAttributesFromPgn(game.Pgn)
                            });
                        }
                        else
                        {
                            Helpers.StatsConsole.ProcessedDisplay("X");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // A single bad month must not take the whole run down, but it must not be
                    // silent either - previously it was just an "E" in the progress dots.
                    Helpers.StatsConsole.ProcessedDisplay("E");
                    failures.Add($"{year}-{month.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}: {ex.Message}");
                }
            });

            if (!failures.IsEmpty)
            {
                Console.WriteLine();
                Console.WriteLine($"  >>WARNING: {failures.Count} archive month(s) could not be read - the report will be incomplete:");

                foreach (string failure in failures.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(10))
                {
                    Console.WriteLine($"    - {failure}");
                }

                if (failures.Count > 10)
                {
                    Console.WriteLine($"    ... and {failures.Count - 10} more");
                }
            }

            // Make sure the list is sorted...
            // This matters for the Last 40 Openings table
            return PgnList.OrderByDescending(o => o.GameAttributes.GetAttributeAsNullOrDateTime(SupportedAttribute.EndDate, SupportedAttribute.EndTime)).ToList();
        }

        /// <summary>
        /// Extracts the year and month from an archive URL such as
        /// https://api.chess.com/pub/player/name/games/2024/07 without relying on fixed positions.
        /// </summary>
        private static bool TryGetArchiveYearMonth(string archiveUrl, out int year, out int month)
        {
            year = 0;
            month = 0;

            if (string.IsNullOrWhiteSpace(archiveUrl))
            {
                return false;
            }

            string[] segments = archiveUrl.TrimEnd('/').Split('/');

            return segments.Length >= 2
                && int.TryParse(segments[^2], NumberStyles.None, CultureInfo.InvariantCulture, out year)
                && int.TryParse(segments[^1], NumberStyles.None, CultureInfo.InvariantCulture, out month)
                && month is >= 1 and <= 12;
        }

        private static async System.Threading.Tasks.Task<ArchivedGamesList> GetPlayerMonthlyArchive(string username)
        {
            using ChessDotComSharp.ChessDotComClient client = new(Helpers.ChessDotComApi.UserAgent);
            ArchivedGamesList myGames = await client.GetPlayerGameArchivesAsync(username).ConfigureAwait(true);
            return myGames;
        }

        //Api lock
        private static readonly SemaphoreSlim apiSemaphore = new(1, 1);

        private static async System.Threading.Tasks.Task<PlayerArchivedGames> GetAllPlayerMonthlyGames(DirectoryInfo cache, string username, int year, int month)
        {
            string cacheFileName = $"{Path.Combine(cache.FullName, $"{username}{year}{month.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}")}";

            PlayerArchivedGames cachedGames = await TryReadCacheFile(cacheFileName).ConfigureAwait(false);
            if (cachedGames is not null)
            {
                return cachedGames;
            }

            //Prevent rate limit errors on the API
            await apiSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                using ChessDotComSharp.ChessDotComClient client = new(Helpers.ChessDotComApi.UserAgent);
                PlayerArchivedGames myGames = await client.GetPlayerGameMonthlyArchiveAsync(username, year, month).ConfigureAwait(true);

                // Never cache data for this or the previous month since we may have new CAPS scores generated
                // (there may of course be older scores we don't have but that will be upto the user to clear the
                //  cache if these are required)
                if (!((DateTime.UtcNow.Year == year && DateTime.UtcNow.Month == month) ||
                      (DateTime.UtcNow.AddMonths(-1).Year == year && DateTime.UtcNow.AddMonths(-1).Month == month)))
                {
                    await WriteCacheFile(cacheFileName, myGames).ConfigureAwait(false);
                }

                return myGames;
            }
            finally
            {
                _ = apiSemaphore.Release();
            }
        }

        /// <summary>
        /// Reads a cached month, returning null when the file is missing or unusable. A truncated
        /// cache entry (interrupted run, full disk) used to make every later run fail as well.
        /// </summary>
        private static async Task<PlayerArchivedGames> TryReadCacheFile(string cacheFileName)
        {
            if (!File.Exists(cacheFileName))
            {
                return null;
            }

            try
            {
                using FileStream gameFileInStream = File.OpenRead(cacheFileName);
                PlayerArchivedGames cachedGames = await JsonSerializer.DeserializeAsync<PlayerArchivedGames>(gameFileInStream).ConfigureAwait(false);

                return cachedGames?.Games is null ? null : cachedGames;
            }
            catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
            {
                Console.WriteLine($"  >>WARNING: ignoring unreadable cache file {Path.GetFileName(cacheFileName)} - {ex.Message}");

                try
                {
                    File.Delete(cacheFileName);
                }
                catch (Exception deleteEx) when (deleteEx is IOException or UnauthorizedAccessException)
                {
                    Console.WriteLine($"     ...could not delete it - {deleteEx.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// Writes through a temporary file so an interrupted run cannot leave a half-written entry.
        /// </summary>
        private static async Task WriteCacheFile(string cacheFileName, PlayerArchivedGames games)
        {
            string temporaryFileName = $"{cacheFileName}.tmp";
            bool moved = false;

            try
            {
                using (FileStream gameFileOutStream = File.Create(temporaryFileName))
                {
                    await JsonSerializer.SerializeAsync(gameFileOutStream, games).ConfigureAwait(false);
                }

                File.Move(temporaryFileName, cacheFileName, overwrite: true);
                moved = true;
            }
            finally
            {
                if (!moved && File.Exists(temporaryFileName))
                {
                    try
                    {
                        File.Delete(temporaryFileName);
                    }
                    catch (IOException)
                    {
                        // Nothing more we can do - a stray .tmp file is harmless.
                    }
                }
            }
        }
    }
}
