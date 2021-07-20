using Microsoft.Extensions.FileProviders;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ChessStats
{
    public class GraphHelper : IDisposable
    {
        private bool disposedValue;
        public static Pen OrangePen => new Pen(Color.FromArgb(255, 229, 139, 9), 1);
        public static Pen DarkOrangePen => new Pen(Color.FromArgb(255, 222, 132, 9), 1);
        public static Pen RedPen => new Pen(Color.FromArgb(255, 200, 9, 9), 3);
        public static Pen WhitePen => new Pen(Color.FromArgb(255, 255, 255, 255), 1) { DashStyle = DashStyle.Dash };
        public static Brush TextBrush => Brushes.Yellow;
        public Bitmap GraphSurface { get; private set; }
        public Graphics DrawingSurface { get; private set; }
        public int Height => GraphSurface.Height;
        public int Width => GraphSurface.Width;
        public LinearGradientBrush LinGrBrush { get; private set; }
        public Pen BackgroundPen { get; private set; }
        public int LowVal { get; }
        public int HighVal { get; }
        public int BaseLine => Height;
        public int Range => HighVal - LowVal;
        public enum GraphLine { NONE, RATING, PERCENTAGE }
        public GraphHelper(int width, int lowVal = 0, int highVal = 0, GraphLine graphLines = GraphLine.NONE)
        {
            LowVal = lowVal;
            HighVal = highVal;

            LinGrBrush = new LinearGradientBrush(
                         new Point(0, 0),
                         new Point(width, Range),
                         Color.FromArgb(20, 49, 46, 43),
                         Color.FromArgb(20, 181, 180, 179));

            BackgroundPen = new Pen(LinGrBrush);

            GraphSurface = new System.Drawing.Bitmap(width, Range);

            DrawingSurface = Graphics.FromImage(GraphSurface);
            DrawingSurface.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            DrawingSurface.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            DrawingSurface.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            DrawingSurface.FillRectangle(LinGrBrush, 0, 0, width, Range);

            //Add horizontal lines
            if (graphLines == GraphLine.RATING)
            {
                for (int loop = HighVal % 100; loop < GraphSurface.Height; loop += 100)
                {
                    DrawingSurface.DrawLine(GraphHelper.WhitePen, 0, loop, Width, loop);
                }
            }
            else if (graphLines == GraphLine.PERCENTAGE)
            {
                for (int loop = 25; loop < 100; loop += 25)
                {
                    DrawingSurface.DrawLine(GraphHelper.WhitePen, 0, loop, Width, loop);
                }

                for (int loop = 70; loop < Width; loop += 70)
                {
                    DrawingSurface.DrawLine(GraphHelper.WhitePen, loop, lowVal, loop, highVal);
                }
            }
        }

        public int GetYAxisPoint(int actualValue)
        {
            return Height - (actualValue - LowVal);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    OrangePen.Dispose();
                    DarkOrangePen.Dispose();
                    RedPen.Dispose();
                    WhitePen.Dispose();
                    LinGrBrush.Dispose();
                    BackgroundPen.Dispose();
                    GraphSurface.Dispose();
                    DrawingSurface.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }


    public static class Helpers
    {
        private static readonly Stopwatch stopwatch = new Stopwatch();
        private static int gameCount = 0;
        private static readonly object displayLock = new object();

        public static string EncodeResourceImageAsHtmlFragment(string imageName)
        {
            string base64Img = "";

            using (var reader = (new EmbeddedFileProvider(Assembly.GetExecutingAssembly())).GetFileInfo($"Images.{imageName}").CreateReadStream())
            {
                var bitmapOut = new Bitmap(reader);
                using MemoryStream stream = new MemoryStream();
                bitmapOut.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                base64Img = Convert.ToBase64String(stream.ToArray());
            }

            return $"'data:image/png;base64,{base64Img}'";
        }


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
            if (image == null) { throw new ArgumentNullException(nameof(image)); }

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
                wrapMode.SetWrapMode(WrapMode.Clamp);
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
            textOut.AppendLine(@$" (________)   Hyper-Dragon :: Version {versionNo} :: 07/2021 :: https://github.com/Hyper-Dragon/ChessStats  ");
            textOut.AppendLine(@$"                                                                                                    ");

            return textOut.ToString();
        }

        public static string GetImageAsHtmlFragment(Bitmap bitmapOut)
        {
            if (bitmapOut == null) { throw new ArgumentNullException(nameof(bitmapOut)); }

            using MemoryStream stream = new MemoryStream();
            bitmapOut.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            string base64Img = Convert.ToBase64String(stream.ToArray());

            return $"<img src='data:image/png;base64,{base64Img}'/>";
        }

        public static string GetHtmlTail(Uri chessdotcomUrl, string versionNumber, string projectLink)
        {
            if (chessdotcomUrl == null) { throw new ArgumentNullException(nameof(chessdotcomUrl)); }

            return ($"<div class='footer'><br/><hr/><i>Generated by ChessStats (for <a href='{chessdotcomUrl.OriginalString}'>Chess.com</a>)&nbsp;ver. {versionNumber}<br/><a href='{projectLink}'>{projectLink}</a></i><br/><br/><br/></div>");
        }

        public static string GetHtmlTop(string pageTitle, string backgroundImage, string font700Fragment, string font800Fragment)
        {
            StringBuilder htmlReport = new StringBuilder();
            _ = htmlReport.AppendLine("<!DOCTYPE html>")
                          .AppendLine("<html lang='en'><head>")
                          .AppendLine($"<title>{pageTitle}</title>")
                          .AppendLine("<meta charset='UTF-8'>")
                          .AppendLine("<meta name='generator' content='ChessStats'> ")
                          .AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>")
                          .AppendLine("   <style>")
                          .AppendLine("     *                                            {margin: 0;padding: 0;}")
                          .AppendLine("     @media screen and (max-width: 1000px) and (min-width: 768px)  {.priority-4{display:none;}}")
                          .AppendLine("     @media screen and (max-width: 768px)  and (min-width: 600px)  {.priority-4{display:none;}.priority-3{display:none;}}")
                          .AppendLine("     @media screen and (max-width: 600px)                          {.priority-4{display:none;}.priority-3{display:none;}.priority-2{display:none;}}")
                          .AppendLine($"     @font-face {{ {font700Fragment} }}")
                          .AppendLine($"     @font-face {{ {font800Fragment} }}")
                          .AppendLine("     body                                         {background-image: url(" + $"{backgroundImage}" + "); background-color:#312e2b;width: 90%; margin: auto; font-family: -apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif;}")
                          .AppendLine("     h1                                           {font-family: Montserrat; font-weight: 800; padding: 10px;text-align: left;font-size: 40px; color: hsla(0,0%,100%,.65);}")
                          .AppendLine("     h1 small                                     {font-family: Montserrat; font-weight: 700; font-size: 15px; vertical-align: bottom}")
                          .AppendLine("     a:link                                       {color: rgb(217, 233, 238);}")
                          .AppendLine("     a:visited                                    {color: rgb(217, 233, 238);}")
                          .AppendLine("     a:hover                                      {color: #FCFC0C}")
                          .AppendLine("     a:active                                     {color: #C0F0FC}")
                          .AppendLine("     a.headerLink                                 {color: #e58b09}")
                          .AppendLine("     h2                                           {font-family: Montserrat; font-weight: 800;clear:left;padding: 5px;text-align: left;font-size: 20px;background-color: rgba(0,0,0,.13);color: hsla(0,0%,100%,.65);}")
                          .AppendLine("     table                                        {width: 100%;table-layout: fixed ;border-collapse: collapse; overflow-x:auto; }")
                          .AppendLine("     thead                                        {font-family: Montserrat; font-weight: 800;text-align: center;background: #769656;color: white;font-size: 15px; font-weight: bold;}")
                          .AppendLine("     tbody                                        {font-family: -apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif; text-align: center;font-size: 14px;}")
                          .AppendLine("     td                                           {padding-right: 0px;}")
                          .AppendLine("     td:nth-child(1)                              {padding-left:10px; text-align: left; width: 105px ; font-weight: bold;}")
                          .AppendLine("     tbody tr:nth-child(odd)                      {background-color:  rgba(255,255,255,0.25); color: rgb(245,245,245);}")
                          .AppendLine("     tbody tr:nth-child(even)                     {background-color:  rgba(255,255,255,0.15); color: rgb(245,245,245);}")
                          .AppendLine("     .active                                      {background-color: rgba(118,150,86, 0.6)}")
                          .AppendLine("     .inactive                                    {background-color: rgba(167,166,162, 0.6)}")
                          .AppendLine("     .headRow                                     {display: grid; grid-template-columns: 200px auto; grid-gap: 0px; border:0px; height: auto; padding: 0px; background-color: #2b2825; }")
                          .AppendLine("     .headRow > div                               {padding: 0px; }")
                          .AppendLine("     .headBox img                                 {vertical-align: middle}")
                          .AppendLine("     .ratingRow                                   {display: grid;grid-template-columns: auto auto auto;grid-gap: 20px;padding: 10px;}")
                          .AppendLine("     .ratingRow > div                             {font-family: Montserrat; font-weight: 700; text-align: center;  padding: 0px;  color: whitesmoke;  font-size: 15px;  font-weight: bold;}")
                          .AppendLine("     .ratingBox                                   {cursor: pointer;}")
                          .AppendLine("     .graphRow                                    {display: grid;grid-template-columns: auto auto auto;grid-gap: 10px;padding: 5px;}")
                          .AppendLine("     .graphRow > div                              {font-family: Montserrat; font-weight: 700; text-align: center;  padding: 0px;  color: whitesmoke;  font-size: 15px;  font-weight: bold;}")
                          .AppendLine("     .graphBox img                                { max-width:100%; height:auto; }")
                          .AppendLine("     .yearSplit                                   {border-top: thin dotted; border-color: #1583b7;}")
                          .AppendLine("     .higher                                      {background-color: hsla(120, 100%, 50%, 0.25);}")
                          .AppendLine("     .lower                                       {background-color: hsla(0, 100%, 70%, 0.4);}")
                          .AppendLine("     .whiteOpeningsTable thead td:nth-child(1)    {font-family: Montserrat; font-weight: 800;font-weight: bold;}")
                          .AppendLine("     .blackOpeningsTable thead td:nth-child(1)    {font-family: Montserrat; font-weight: 800;font-weight: bold;}")
                          .AppendLine("     .whiteOpeningsTable td:nth-child(1)          {padding-left:10px; text-align: left; width:50%; font-weight: normal;}")
                          .AppendLine("     .blackOpeningsTable td:nth-child(1)          {padding-left:10px; text-align: left; width:50%; font-weight: normal;}")
                          .AppendLine("     .capsRollingTable thead td:nth-child(2)      {text-align: left;}")
                          .AppendLine("     .capsRollingTable tbody td:nth-child(1)      {font-size: 12px;font-weight: bold;}")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(1)     {font-size: 12px;font-weight: bold;}")
                          //.AppendLine("     .playingStatsTable tbody td:nth-child(2)     {text-align: right;}")
                          //.AppendLine("     .playingStatsTable tbody td:nth-child(5)     {text-align: right;}")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(5)     {border-right: thin solid; border-color: #1583b7;}")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(8)     {border-left: thin dotted; border-color: #1583b7;}")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(11)    {border-left: thin dotted; border-color: #1583b7;}")
                          .AppendLine("     .playingStatsTable tbody td:nth-child(13)    {border-left: thin solid; border-color: #1583b7;}")
                          .AppendLine("     .oneColumn                                   {float: left;width: 100%;}")
                          .AppendLine("     .oneRow:after                                {content: ''; display: table; clear: both;}")
                          .AppendLine("     .footer                                      {font-family: Montserrat; font-weight: 700; text-align: right;color: white; font-size: 11px}")
                          .AppendLine("     .footer a                                    {color: #e58b09;}")
                          .AppendLine("   </style>")
                          .AppendLine("</head><body>");

            return htmlReport.ToString();
        }
    }
}
