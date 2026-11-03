using System;

namespace ChessStats.Data
{
    /// <summary>
    /// Raised when a player exists but has no completed games, so there is nothing to report.
    /// This is a normal outcome (a brand new or non-playing account), not a failure.
    /// </summary>
    public class NoGamesFoundException : Exception
    {
        public NoGamesFoundException(string username)
            : base($"'{username}' has no completed games on Chess.com, so there is nothing to report.")
        {
            Username = username;
        }

        public string Username { get; }
    }
}
