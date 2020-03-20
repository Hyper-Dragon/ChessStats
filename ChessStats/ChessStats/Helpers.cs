using System;
using System.Linq;
using System.Text;

namespace ChessStats
{
    public static class Helpers
    {
        public static void DisplaySection(string title, bool isHeader)
        {
            _ = title ?? throw new ArgumentNullException(nameof(title));

            const int HEAD_LEN = 50;
            const int FOOT_LEN = 30;

            int midRowLength = (isHeader) ? HEAD_LEN : FOOT_LEN;
            double spacerLength = (title.Length + 2d) / 2d;

            StringBuilder sb = new StringBuilder();

            sb.Append('=', midRowLength - (int)Math.Ceiling(spacerLength));
            sb.Append($" {title} ");
            sb.Append('=', midRowLength - (int)Math.Floor(spacerLength));

            Console.WriteLine(sb.ToString());

            if (!isHeader)
            {
                Console.WriteLine("");
            }
        }

        public static string FixedWidth(string s, int width)
        {
            return new string(s.Take(width).ToArray()).PadRight(width);
        }

        public static void PressToContinueIfDebug()
        {
#if DEBUG
            Console.Beep();
            Console.WriteLine("<Press a Key>");
            Console.ReadKey();
#endif
        }

        public static void DisplayLogo()
        {
            Console.WriteLine(@"                                                                                                    ");
            Console.WriteLine(@"     ()                                                                                             ");
            Console.WriteLine(@"   <~~~~>  _________  .__                                   _________  __             __            ");
            Console.WriteLine(@"    \__/   \_   ___ \ |  |__    ____    ______  ______     /   _____/_/  |_ _____   _/  |_   ______ ");
            Console.WriteLine(@"   (____)  /    \  \/ |  |  \ _/ __ \  /  ___/ /  ___/     \_____  \ \   __\\__  \  \   __\ /  ___/ ");
            Console.WriteLine(@"    |  |   \     \____|   Y  \\  ___/  \___ \  \___ \      /        \ |  |   / __ \_ |  |   \___ \  ");
            Console.WriteLine(@"    |  |    \______  /|___|  / \___  >/____  >/____  >    /_______  / |__|  (____  / |__|  /____  > ");
            Console.WriteLine(@"    |__|           \/      \/      \/      \/      \/             \/             \/for Chess.com\/  ");
            Console.WriteLine(@"   /____\                                                                                           ");
            Console.WriteLine(@"  (______)                                                                                          ");
            Console.WriteLine(@" (________)   Hyper-Dragon :: Version 0.2 :: 03/2020                                                ");
            Console.WriteLine(@"                                                                                                    ");
        }
    }
}
