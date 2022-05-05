using ChessStats.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VectSharp;

namespace ChessStats.Helpers
{
    internal class StatsGraph
    {
        private const string NO_DATA_MSG = "Not enough data";
        private const double GRAPH_OFFSET_X = 5;
        private const double GRAPH_OFFSET_Y = 20;

        private const double LINE_GRAPH_WIDTH = 10;

        private Colour BKG_GRAD_START = Colour.FromRgba(0, 0, 0, 0);
        private Colour BKG_GRAD_END = Colour.FromRgba(255, 255, 255, 25);
        private Colour SCALE_HEAVY = Colour.FromRgba(102, 102, 102, 255);
        private Colour SCALE_LIGHT = Colour.FromRgba(150, 0, 0, 255);
        private Colour CAPS_WHITE = Colour.FromRgba(200, 200, 200, 200);
        private Colour CAPS_BLACK = Colour.FromRgba(255, 127, 39, 175);
        private Colour BAR_COL = Colour.FromRgba(215, 141, 58, 200);
        private Colour BAR_ALT_COL = Colour.FromRgba(208, 134, 56, 230);
        private Colour FONT_COL = Colour.FromRgba(225, 225, 85, 255);
        private Colour FONT_MSG_COL = Colour.FromRgba(225, 225, 85, 255);

        private readonly Font fontMessage;
        private readonly Font font;

        public double Width { get; private set; }
        public float TextSize { get; private set; }
        public float TextSizeMsg { get; private set; }

        internal StatsGraph(double width = 3840, float textSize = 140, float textSizeMsg = 100)
        {
            TextSize = textSize;
            TextSizeMsg = textSizeMsg;
            Width = width;

            font = new(FontFamily.ResolveFontFamily(FontFamily.StandardFontFamilies.TimesRoman), TextSize);
            fontMessage = new(FontFamily.ResolveFontFamily(FontFamily.StandardFontFamilies.TimesItalic), TextSizeMsg);
        }

        private Document CreateDocument(double height)
        {
            Document doc = new();
            doc.Pages.Add(new(Width, height));

            LinearGradientBrush bkgBrush = new(new Point(0, 0),
                                                         new Point(Width, height),
                                                         new GradientStop(BKG_GRAD_START, 0),
                                                         new GradientStop(BKG_GRAD_END, 1));

            doc.Pages[0].Graphics.FillRectangle(0, 0, Width, height, bkgBrush);

            return doc;
        }

        private void WriteNoDataMessage(VectSharp.Graphics gpr, double height)
        {
            gpr.FillText(new Point(GRAPH_OFFSET_X, height - fontMessage.MeasureText(NO_DATA_MSG).Height - GRAPH_OFFSET_Y), NO_DATA_MSG, fontMessage, FONT_MSG_COL);
        }

        private void WriteRangeMessage(VectSharp.Graphics gpr, double height, string topVal = "-", string bottomVal = "-")
        {
            gpr.FillText(new Point(GRAPH_OFFSET_X, GRAPH_OFFSET_Y), topVal, font, FONT_COL);
            gpr.FillText(new Point(GRAPH_OFFSET_X, height - font.MeasureText(bottomVal).Height - GRAPH_OFFSET_Y), bottomVal, font, FONT_COL);
        }

        internal async Task<string> RenderCapsGraph(List<CapsRecord> capsScoresWhite, List<CapsRecord> capsScoresBlack,
                                                    int rollingAv, double height = 768, double maxCapsGames = 100)
        {
            return await Task<string>.Run(() =>
            {
                double[] whiteMovingAv = MovingAverage.CalculateMovingAv(capsScoresWhite.Select(item => item.Caps).ToList<double>(), rollingAv);
                double[] blackMovingAv = MovingAverage.CalculateMovingAv(capsScoresBlack.Select(item => item.Caps).ToList<double>(), rollingAv);
                double maxDataPoints = Math.Min(maxCapsGames, Math.Max(whiteMovingAv.Length, blackMovingAv.Length));
                double CapsStepX = Width / (maxDataPoints - 2);
                double CapsStepY = height / 100;

                Document doc = CreateDocument(height);
                VectSharp.Graphics gpr = doc.Pages[0].Graphics;

                if (maxDataPoints <= 2)
                {
                    WriteNoDataMessage(gpr, height);
                }
                else
                {
                    for (double i = height / 10; i < height; i += height / 10)
                    {
                        gpr.FillRectangle(0, i, Width, 3, SCALE_LIGHT);
                    }

                    gpr.FillRectangle(0, height / 4 * 1, Width, 6, SCALE_HEAVY);
                    gpr.FillRectangle(0, height / 4 * 2, Width, 6, SCALE_HEAVY);
                    gpr.FillRectangle(0, height / 4 * 3, Width, 6, SCALE_HEAVY);


                    GraphicsPath gpWhite = new();
                    GraphicsPath gpBlack = new();
                    List<Point> gpWhitePoints = new();
                    List<Point> gpBlackPoints = new();

                    if (whiteMovingAv.Length > 1)
                    {
                        _ = gpWhite.MoveTo(0, height - (whiteMovingAv[0] * CapsStepY));
                        gpWhitePoints.Add(new(0, height - (whiteMovingAv[0] * CapsStepY)));
                    }

                    if (blackMovingAv.Length > 1)
                    {
                        _ = gpBlack.MoveTo(0, height - (blackMovingAv[0] * CapsStepY));
                        gpBlackPoints.Add(new(0, height - (blackMovingAv[0] * CapsStepY)));
                    }

                    for (int i = 1; i < maxDataPoints - 1; i++)
                    {
                        if (i < whiteMovingAv.Length - 1)
                        {
                            _ = gpWhite.LineTo(i * CapsStepX, height - (whiteMovingAv[i] * CapsStepY));
                            gpWhitePoints.Add(new(i * CapsStepX, height - (whiteMovingAv[i] * CapsStepY)));
                        }

                        if (i < blackMovingAv.Length - 1)
                        {
                            _ = gpBlack.LineTo(i * CapsStepX, height - (blackMovingAv[i] * CapsStepY));
                            gpBlackPoints.Add(new(i * CapsStepX, height - (blackMovingAv[i] * CapsStepY)));
                        }
                    }

                    GraphicsPath gpWhiteSmooth = new();
                    _ = gpWhiteSmooth.AddSmoothSpline(gpWhitePoints.ToArray());
                    gpr.StrokePath(gpWhite, CAPS_WHITE, lineWidth: LINE_GRAPH_WIDTH);


                    GraphicsPath gpBlackSmooth = new();
                    _ = gpBlackSmooth.AddSmoothSpline(gpBlackPoints.ToArray());
                    gpr.StrokePath(gpBlackSmooth, CAPS_BLACK, lineWidth: LINE_GRAPH_WIDTH);
                }

                return Graphics.GetImageAsHtmlFragment(doc.Pages.First());
            }).ConfigureAwait(false);
        }

        internal async Task<string> RenderRatingGraph(List<(DateTime gameDate, int rating,
                                                      string gameType)> ratingsPostGame, double height = 1920)
        {
            return await Task<string>.Run(() =>
            {
                Document doc = CreateDocument(height);
                VectSharp.Graphics gpr = doc.Pages[0].Graphics;

                //If less than 10 games don't graph
                if (ratingsPostGame.Count < 10)
                {
                    WriteNoDataMessage(gpr, height);
                }
                else
                {
                    (DateTime gameDate, int rating)[] ratingsPostGameOrdered = ratingsPostGame.OrderBy(x => x.gameDate).Select(x => (x.gameDate, x.rating)).ToArray();
                    int graphMin = ratingsPostGame.Select(x => x.rating).Min();
                    int graphMax = ratingsPostGame.Select(x => x.rating).Max();

                    double CapsStepX = Width / ratingsPostGame.Count;
                    double CapsStepY = height / (graphMax - graphMin);

                    for (double i = graphMax % 100; i < height; i += 100)
                    {
                        gpr.FillRectangle(0,
                                          i * CapsStepY,
                                          Width,
                                          3,
                                          SCALE_HEAVY);
                    }

                    //Draw Graph
                    DateTime lastDate = DateTime.MinValue;

                    Colour brush01 = BAR_COL;
                    Colour brush02 = BAR_ALT_COL;

                    Colour currentBrush = brush01;

                    for (int loop = 0; loop < ratingsPostGame.Count; loop++)
                    {
                        //Switch pen when the month changes
                        if (ratingsPostGameOrdered[loop].gameDate.Month != lastDate.Month)
                        {
                            currentBrush = currentBrush == brush01 ? brush02 : brush01;
                        }

                        gpr.FillRectangle(loop * CapsStepX,
                                          (graphMax - ratingsPostGameOrdered[loop].rating) * CapsStepY,
                                          CapsStepX,
                                          ratingsPostGame[loop].rating * CapsStepY,
                                          currentBrush);

                        lastDate = ratingsPostGameOrdered[loop].gameDate;
                    }

                    WriteRangeMessage(gpr, height, $"{graphMin}", $"{graphMax}");

                    gpr.FillRectangle(0,
                                      (graphMax - ratingsPostGameOrdered[^1].rating) * CapsStepY,
                                      Width,
                                      6,
                                      Colour.FromRgba(255, 0, 0, 225));
                }

                return Graphics.GetImageAsHtmlFragment(doc.Pages.First());

            }).ConfigureAwait(false);
        }

        internal async Task<string> RenderAverageStatsGraph(List<(string TimeControl, int VsMin, int Worst, int LossAv, int DrawAv,
                                                            int WinAv, int Best, int VsMax)> graphData, double height = 1280)
        {
            return await Task<string>.Run(() =>
            {
                int graphMinCalc = graphData.Where(x => x.WinAv != 0 && x.LossAv != 0).Select(x => x.WinAv).DefaultIfEmpty(0).Min();
                int graphMaxCalc = graphData.Where(x => x.WinAv != 0 && x.LossAv != 0).Select(x => x.LossAv).DefaultIfEmpty(0).Max();

                Document doc = CreateDocument(height);
                VectSharp.Graphics gpr = doc.Pages[0].Graphics;

                if (graphData == null || graphData.Count < 2 || graphMinCalc == 0 || graphMaxCalc == 0)
                {
                    WriteNoDataMessage(gpr, height);
                }
                else
                {
                    int graphMin = Math.Min(graphMinCalc, graphMaxCalc);
                    int graphMax = Math.Max(graphMinCalc, graphMaxCalc);

                    double CapsStepX = Width / graphData.Count;
                    double CapsStepY = height / (graphMax - graphMin);

                    for (double i = graphMax % 100; i < height; i += 100)
                    {
                        gpr.FillRectangle(0,
                                          i * CapsStepY,
                                          Width,
                                          3,
                                          SCALE_HEAVY);
                    }

                    for (int loop = 0; loop < graphData.Count; loop++)
                    {
                        if (graphData[loop].WinAv != 0 &&
                            graphData[loop].LossAv != 0)
                        {
                            gpr.FillRectangle(loop * CapsStepX,
                                              (graphMax - graphData[loop].LossAv) * CapsStepY,
                                              CapsStepX,
                                              (graphData[loop].LossAv - graphData[loop].WinAv) * CapsStepY,
                                              BAR_COL);
                        }
                    }

                    WriteRangeMessage(gpr, height, $"{graphMin}", $"{graphMax}");
                }

                return Graphics.GetImageAsHtmlFragment(doc.Pages.First());
            }).ConfigureAwait(false);
        }
    }
}
