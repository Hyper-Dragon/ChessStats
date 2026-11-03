namespace ChessStats.Helpers
{
    /// <summary>
    /// Single source of truth for how ChessStats identifies itself to Chess.com.
    /// Chess.com requires a recognisable User-Agent containing developer contact
    /// information (see "Breaking Change: User-Agent Contact Info Required",
    /// 2023-06-20), so do not impersonate a browser here.
    /// </summary>
    internal static class ChessDotComApi
    {
        /// <summary>
        /// Taken from the assembly version (set by &lt;Version&gt; in ChessStats.csproj) so the
        /// banner, the report footers, the User-Agent and the exe file metadata can never
        /// drift apart - bump the version in the project file only.
        /// </summary>
        public static string Version { get; } = typeof(ChessDotComApi).Assembly.GetName().Version.ToString(3);

        public const string ReleaseDate = "11/2026";
        public const string ProjectLink = "https://hyper-dragon.github.io/ChessStats/";

        public static string UserAgent { get; } = $"ChessStats/{Version} (+{ProjectLink})";
    }
}
