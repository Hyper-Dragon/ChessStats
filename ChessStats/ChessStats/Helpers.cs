using System;
using System.Linq;
using System.Text;

namespace ChessStats
{
    public static class Helpers
    {
        public static void displaySection(string title, bool isHeader)
        {
            const int HEAD_LEN = 48;
            const int FOOT_LEN = 30;

            int midRowLength = (isHeader) ? HEAD_LEN : FOOT_LEN;
            double spacerLength = (title.Length + 2d) / 2d;

            var sb = new StringBuilder();

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

        public static void PressToContinue()
        {
            Console.Beep();
            Console.WriteLine("<Press a Key>");
            Console.ReadKey();
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
            Console.WriteLine(@"    |__|           \/      \/      \/      \/      \/             \/             \/             \/  ");
            Console.WriteLine(@"   /____\                                                                                           ");
            Console.WriteLine(@"  (______)                                                                                          ");
            Console.WriteLine(@" (________)                                                            Version 0.1 Alpha - Feb 2020 ");
            Console.WriteLine(@"                                                                                                    ");
            Console.WriteLine(@"                                                                                                    ");
        }
    }
}
