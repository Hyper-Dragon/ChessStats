using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ChessStats.Helpers
{
    public static class StatsConsole
    {
        private static readonly Stopwatch stopwatch = new();
        private static int gameCount = 0;
        private static readonly object displayLock = new();

        public static void StartTimedSection(string msg, bool newLineFirst = false, bool newLineAfter = false)
        {
            stopwatch.Reset();
            stopwatch.Start();

            if (newLineFirst) { Console.WriteLine(); }
            Console.WriteLine(msg);
            if (newLineAfter) { Console.WriteLine(); }
        }

        public static void EndTimedSection(string msg, bool newLineFirst = false, bool newLineAfter = false)
        {
            stopwatch.Stop();

            if (newLineFirst) { Console.WriteLine(); }
            Console.WriteLine($"{msg} ({stopwatch.Elapsed.Hours}:{stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}:{stopwatch.Elapsed.Milliseconds})");
            if (newLineAfter) { Console.WriteLine(); }
        }

        public static void ProcessedDisplay(string outChar)
        {
            lock (displayLock)
            {
                if (gameCount++ > 99) { Console.WriteLine(); gameCount = 1; }
                Console.Write(outChar);
            }
        }

        public static void ResetDisplayCounter()
        {
            lock (displayLock)
            {
                gameCount = 0;
            }
        }

        public static string ValueOrDash(int? valueIn)
        {
            return !valueIn.HasValue || valueIn.Value == 0 ? "-" : valueIn.ToString();
        }

        public static string GetDisplaySection(string title, bool isHeader)
        {
            _ = title ?? throw new ArgumentNullException(nameof(title));

            const int HEAD_LEN = 50;
            const int FOOT_LEN = 30;

            int midRowLength = isHeader ? HEAD_LEN : FOOT_LEN;
            double spacerLength = (title.Length + 2d) / 2d;

            StringBuilder sb = new();

            _ = sb.Append('=', midRowLength - (int)Math.Ceiling(spacerLength));
            _ = sb.Append($" {title} ");
            _ = sb.Append('=', midRowLength - (int)Math.Floor(spacerLength));

            if (!isHeader)
            {
                _ = sb.AppendLine("");
            }

            return sb.ToString();
        }

        public static void DisplaySection(string title, bool isHeader)
        {
            Console.WriteLine(GetDisplaySection(title, isHeader));
        }

        public static string FixedWidth(string s, int width)
        {
            return new string(s.Take(width).ToArray()).PadRight(width);
        }

        public static void PressToContinue()
        {
            Console.Beep();
            Console.WriteLine("<Press a Key>");
            _ = Console.ReadKey();
        }

        public static void DisplayLogo(string versionNo, string releaseDate, string projectLink)
        {
            Console.WriteLine(GetDisplayLogo(versionNo, releaseDate, projectLink));
        }

        public static string GetDisplayLogo(string versionNo, string relDate, string projectLink)
        {
            StringBuilder textOut = new();

            _ = textOut.AppendLine(@$"           ");
            _ = textOut.AppendLine(@$"     ()    ");
            _ = textOut.AppendLine(@$"   <~~~~>  _________  .__                                   _________  __             __            ");
            _ = textOut.AppendLine(@$"    \__/   \_   ___ \ |  |__    ____    ______  ______     /   _____/_/  |_ _____   _/  |_   ______ ");
            _ = textOut.AppendLine(@$"   (____)  /    \  \/ |  |  \ _/ __ \  /  ___/ /  ___/     \_____  \ \   __\\__  \  \   __\ /  ___/ ");
            _ = textOut.AppendLine(@$"    |  |   \     \____|   Y  \\  ___/  \___ \  \___ \      /        \ |  |   / __ \_ |  |   \___ \  ");
            _ = textOut.AppendLine(@$"    |  |    \______  /|___|  / \___  >/____  >/____  >    /_______  / |__|  (____  / |__|  /____  > ");
            _ = textOut.AppendLine(@$"    |__|           \/      \/      \/      \/      \/             \/             \/for Chess.com\/  ");
            _ = textOut.AppendLine(@$"   /____\   ");
            _ = textOut.AppendLine(@$"  (______)  ");
            _ = textOut.AppendLine(@$" (________) Hyper-Dragon :: Version {versionNo} :: {relDate} :: {projectLink}");
            _ = textOut.AppendLine(@$"            ");

            return textOut.ToString();
        }
    }
}
