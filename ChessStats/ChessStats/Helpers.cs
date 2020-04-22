using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace ChessStats
{
    public static class Helpers
    {
        private static readonly Stopwatch stopwatch = new Stopwatch();
        private static int gameCount = 0;
        private static readonly object displayLock = new object();

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
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            if(image == null) { throw new ArgumentNullException(nameof(image)); }

            Rectangle destRect = new Rectangle(0, 0, width, height);
            Bitmap destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using ImageAttributes wrapMode = new ImageAttributes();
                wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
            }

            return destImage;
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
            if (!valueIn.HasValue || valueIn.Value == 0)
            {
                return "-";
            }
            else
            {
                return valueIn.ToString();
            }
        }

        public static string GetDisplaySection(string title, bool isHeader)
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

            if (!isHeader)
            {
                sb.AppendLine("");
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
            Console.ReadKey();
        }

        public static void DisplayLogo(string versionNo)
        {
            Console.WriteLine(GetDisplayLogo(versionNo));
        }

        public static string GetDisplayLogo(string versionNo)
        {
            StringBuilder textOut = new StringBuilder();

            textOut.AppendLine(@$"                                                                                                    ");
            textOut.AppendLine(@$"     ()                                                                                             ");
            textOut.AppendLine(@$"   <~~~~>  _________  .__                                   _________  __             __            ");
            textOut.AppendLine(@$"    \__/   \_   ___ \ |  |__    ____    ______  ______     /   _____/_/  |_ _____   _/  |_   ______ ");
            textOut.AppendLine(@$"   (____)  /    \  \/ |  |  \ _/ __ \  /  ___/ /  ___/     \_____  \ \   __\\__  \  \   __\ /  ___/ ");
            textOut.AppendLine(@$"    |  |   \     \____|   Y  \\  ___/  \___ \  \___ \      /        \ |  |   / __ \_ |  |   \___ \  ");
            textOut.AppendLine(@$"    |  |    \______  /|___|  / \___  >/____  >/____  >    /_______  / |__|  (____  / |__|  /____  > ");
            textOut.AppendLine(@$"    |__|           \/      \/      \/      \/      \/             \/             \/for Chess.com\/  ");
            textOut.AppendLine(@$"   /____\                                                                                           ");
            textOut.AppendLine(@$"  (______)                                                                                          ");
            textOut.AppendLine(@$" (________)   Hyper-Dragon :: Version {versionNo} :: 04/2020 :: https://github.com/Hyper-Dragon/ChessStats  ");
            textOut.AppendLine(@$"                                                                                                    ");

            return textOut.ToString();
        }
    }
}
