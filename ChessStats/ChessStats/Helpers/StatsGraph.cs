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

        private const double MSG_OFFSET_X = 5;
        private const double MSG_OFFSET_Y = 10;
        private const double CUR_RATING_BAR_HEIGHT = 30;
        private const double GRAPH_LINE_WIDTH = 15;
        private const double SCALE_MINOR_HEIGHT = 4;
        private const double SCALE_MAJOR_HEIGHT = 8;
        private const bool IS_SMOOTH_CAPS = true;

        private Colour COL_BKG_GRAD_START = Colour.FromRgba(0, 0, 0, 0);
        private Colour COL_BKG_GRAD_END = Colour.FromRgba(255, 255, 255, 25);
        private Colour COL_SCALE_MINOR = Colour.FromRgba(76, 76, 75, 255);
        private Colour COL_SCALE_MAJOR = Colour.FromRgba(99, 99, 98, 255);
        private Colour COL_CAPS_WHITE = Colour.FromRgba(200, 200, 200, 200);
        private Colour COL_CAPS_BLACK = Colour.FromRgba(255, 127, 39, 175);
        private Colour COL_BAR = Colour.FromRgba(229, 139, 9, 200);
        private Colour COL_BAR_ALT = Colour.FromRgba(209, 96, 2, 230);
        private Colour COL_FONT = Colour.FromRgba(225, 225, 85, 255);
        private Colour COL_FONT_MSG = Colour.FromRgba(225, 225, 85, 255);
        private Colour COL_RATING = Colour.FromRgba(85, 105, 66, 175);

        private readonly Font fontMessage;
        private readonly Font font;

        public double GraphWidth { get; private set; }
        public float TextSize { get; private set; }
        public float TextSizeMsg { get; private set; }

        internal StatsGraph(double width = 3840, float textSize = 140, float textSizeMsg = 100)
        {
            TextSize = textSize;
            TextSizeMsg = textSizeMsg;
            GraphWidth = width;

            font = new(FontFamily.ResolveFontFamily(FontFamily.StandardFontFamilies.TimesRoman), TextSize);
            fontMessage = new(FontFamily.ResolveFontFamily(FontFamily.StandardFontFamilies.TimesItalic), TextSizeMsg);
        }

        private Document CreateDocument(double height)
        {
            Document doc = new();
            doc.Pages.Add(new(GraphWidth, height));

            LinearGradientBrush bkgBrush = new(new Point(0, 0),
                                                         new Point(GraphWidth, height),
                                                         new GradientStop(COL_BKG_GRAD_START, 0),
                                                         new GradientStop(COL_BKG_GRAD_END, 1));

            doc.Pages[0].Graphics.FillRectangle(0, 0, GraphWidth, height, bkgBrush);

            return doc;
        }

        private void WriteNoDataMessage(VectSharp.Graphics gpr, double height)
        {
            WriteMessage(gpr, height, NO_DATA_MSG);
        }

        private void WriteMessage(VectSharp.Graphics gpr, double height, string message)
        {
            gpr.FillText(new Point(MSG_OFFSET_X, height - fontMessage.MeasureText(message).Height - MSG_OFFSET_Y),
                         message, fontMessage, COL_FONT_MSG);
        }

        private void WriteRangeMessage(VectSharp.Graphics gpr, double height, string bottomVal = "", string topVal = "")
        {
            if (!string.IsNullOrEmpty(topVal))
            {
                gpr.FillText(new Point(MSG_OFFSET_X, MSG_OFFSET_Y), topVal, font, COL_FONT);
            }

            if (!string.IsNullOrEmpty(bottomVal))
            {
                gpr.FillText(new Point(MSG_OFFSET_X, height - font.MeasureText(bottomVal).Height - MSG_OFFSET_Y),
                                 bottomVal, font, COL_FONT);
            }
        }

        private void RenderPercentageScale(VectSharp.Graphics gpr, double height, bool showMinorScale = true)
        {
            for (double i = 1; i < 10 && showMinorScale; i++)
            {
                gpr.FillRectangle(0, (height / 10 * i) - (SCALE_MINOR_HEIGHT / 2), GraphWidth, SCALE_MINOR_HEIGHT, COL_SCALE_MINOR);
            }

            gpr.FillRectangle(0, (height / 4 * 1) - (SCALE_MAJOR_HEIGHT / 2), GraphWidth, SCALE_MAJOR_HEIGHT, COL_SCALE_MAJOR);
            gpr.FillRectangle(0, (height / 4 * 2) - (SCALE_MAJOR_HEIGHT / 2), GraphWidth, SCALE_MAJOR_HEIGHT, COL_SCALE_MAJOR);
            gpr.FillRectangle(0, (height / 4 * 3) - (SCALE_MAJOR_HEIGHT / 2), GraphWidth, SCALE_MAJOR_HEIGHT, COL_SCALE_MAJOR);
        }

        internal async Task<string> RenderCapsGraph(List<CapsRecord> capsScoresWhite, List<CapsRecord> capsScoresBlack,
                                                    int rollingAv = 3, double height = 768, double maxCapsGames = 100)
        {
            return await Task<string>.Run(() =>
            {
                //Make sure the data is sorted correctly
                capsScoresWhite = capsScoresWhite.OrderBy(item => item.GameDate).TakeLast((int)maxCapsGames).ToList();
                capsScoresBlack = capsScoresBlack.OrderBy(item => item.GameDate).TakeLast((int)maxCapsGames).ToList();


                double[] whiteMovingAv = MovingAverage.CalculateMovingAv(capsScoresWhite.Select(item => item.Caps).ToList<double>(), rollingAv);
                double[] blackMovingAv = MovingAverage.CalculateMovingAv(capsScoresBlack.Select(item => item.Caps).ToList<double>(), rollingAv);
                double maxDataPoints = Math.Min(maxCapsGames, Math.Max(whiteMovingAv.Length, blackMovingAv.Length));
                double CapsStepX = GraphWidth / (maxDataPoints - 2);
                double CapsStepY = height / 100;

                Document doc = CreateDocument(height);
                VectSharp.Graphics gpr = doc.Pages[0].Graphics;

                if (maxDataPoints <= 2)
                {
                    WriteNoDataMessage(gpr, height);
                }
                else
                {
                    RenderPercentageScale(gpr, height);

                    GraphicsPath gpWhite = new();
                    GraphicsPath gpBlack = new();
                    List<Point> gpWhitePoints = new();
                    List<Point> gpBlackPoints = new();

                    //Shift the data to the right if we don't have enough data points
                    int whiteOffsetX = Math.Max(0,blackMovingAv.Length - whiteMovingAv.Length);
                    int blackOffsetX = Math.Max(0,whiteMovingAv.Length - blackMovingAv.Length);

                    if (whiteMovingAv.Length > 1)
                    {
                        _ = gpWhite.MoveTo(whiteOffsetX * CapsStepX, height - (whiteMovingAv[0] * CapsStepY));
                        gpWhitePoints.Add(new(whiteOffsetX * CapsStepX, height - (whiteMovingAv[0] * CapsStepY)));
                    }

                    if (blackMovingAv.Length > 1)
                    {
                        _ = gpBlack.MoveTo(blackOffsetX * CapsStepX, height - (blackMovingAv[0] * CapsStepY));
                        gpBlackPoints.Add(new(blackOffsetX * CapsStepX, height - (blackMovingAv[0] * CapsStepY)));
                    }

                    for (int loopY = 1; loopY < whiteMovingAv.Length - 1 ; loopY++)
                    { 
                            _ = gpWhite.LineTo((loopY + whiteOffsetX) * CapsStepX, height - (whiteMovingAv[loopY] * CapsStepY));
                            gpWhitePoints.Add(new((loopY + whiteOffsetX) * CapsStepX, height - (whiteMovingAv[loopY] * CapsStepY)));
                    }

                    for (int loopY = 1; loopY < blackMovingAv.Length - 1; loopY++)
                    {
                            _ = gpBlack.LineTo((loopY + blackOffsetX) * CapsStepX, height - (blackMovingAv[loopY] * CapsStepY));
                            gpBlackPoints.Add(new((loopY + blackOffsetX) * CapsStepX, height - (blackMovingAv[loopY] * CapsStepY)));
                    }

                    GraphicsPath gpWhiteSmooth = new();
                    _ = gpWhiteSmooth.AddSmoothSpline(gpWhitePoints.ToArray());
                    gpr.StrokePath(IS_SMOOTH_CAPS ? gpWhiteSmooth : gpWhite, COL_CAPS_WHITE, lineWidth: GRAPH_LINE_WIDTH);


                    GraphicsPath gpBlackSmooth = new();
                    _ = gpBlackSmooth.AddSmoothSpline(gpBlackPoints.ToArray());
                    gpr.StrokePath(IS_SMOOTH_CAPS ? gpBlackSmooth : gpBlack, COL_CAPS_BLACK, lineWidth: GRAPH_LINE_WIDTH);

                    WriteMessage(gpr, height, $"* Based on the last {whiteMovingAv.Length}/{blackMovingAv.Length} games with available CAPs scores");
                }

                return Imaging.GetImageAsHtmlFragment(doc.Pages.First());
            }).ConfigureAwait(false);
        }


        internal async Task<string> RenderAllCapsGraph(List<CapsRecord> capsScores, double height = 768)
        {
            return await Task<string>.Run(() =>
            {
                Document doc = CreateDocument(height);
                VectSharp.Graphics gpr = doc.Pages[0].Graphics;


                if (capsScores.Count == 0)
                {
                    WriteNoDataMessage(gpr, height);
                }
                else
                {
                    capsScores = capsScores.OrderBy(item => item.GameDate).ToList();
                    double RatingStepX = GraphWidth / capsScores.Count;
                    double RatingStepY = height / 100;

                    RenderPercentageScale(gpr, height, false);

                    DateTime lastDate = DateTime.MinValue;
                    Colour brush01 = COL_BAR;
                    Colour brush02 = COL_BAR_ALT;
                    Colour currentBrush = brush01;

                    for (int loop = 0; loop < capsScores.Count; loop++)
                    {
                        //Switch brush when the month changes
                        if (capsScores[loop].GameDate.Month != lastDate.Month)
                        {
                            currentBrush = currentBrush == brush01 ? brush02 : brush01;
                        }

                        //Draw the bar
                        if (capsScores[loop].Caps > 0)
                        {
                            gpr.FillRectangle(loop * RatingStepX, (100 - capsScores[loop].Caps) * RatingStepY,
                                              RatingStepX, height - ((100 - capsScores[loop].Caps) * RatingStepY),
                                              currentBrush);
                        }

                        lastDate = capsScores[loop].GameDate;
                    }

                    //WriteRangeMessage(gpr, height, "", "100%");
                    WriteMessage(gpr, height, $"* CAPs % over {capsScores.Count} games");
                }

                return Imaging.GetImageAsHtmlFragment(doc.Pages.First());
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

                    double RatingStepX = GraphWidth / ratingsPostGame.Count;
                    double RatingStepY = height / (graphMax - graphMin);

                    for (double loopY = graphMax % 100; loopY < (graphMax - graphMin); loopY += 100)
                    {
                        gpr.FillRectangle(0, (loopY * RatingStepY) - (SCALE_MAJOR_HEIGHT / 2),
                                          GraphWidth, SCALE_MAJOR_HEIGHT,
                                          COL_SCALE_MAJOR);
                    }

                    //Draw Graph
                    DateTime lastDate = DateTime.MinValue;

                    Colour brush01 = COL_BAR;
                    Colour brush02 = COL_BAR_ALT;

                    Colour currentBrush = brush01;

                    for (int loop = 0; loop < ratingsPostGame.Count; loop++)
                    {
                        //Switch brush when the month changes
                        if (ratingsPostGameOrdered[loop].gameDate.Month != lastDate.Month)
                        {
                            currentBrush = currentBrush == brush01 ? brush02 : brush01;
                        }

                        gpr.FillRectangle(loop * RatingStepX, (graphMax - ratingsPostGameOrdered[loop].rating) * RatingStepY,
                                          RatingStepX, height - ((graphMax - ratingsPostGameOrdered[loop].rating) * RatingStepY),
                                          currentBrush);

                        lastDate = ratingsPostGameOrdered[loop].gameDate;
                    }

                    WriteRangeMessage(gpr, height, $"{graphMin}", $"{graphMax}");

                    gpr.FillRectangle(0, ((graphMax - ratingsPostGameOrdered[^1].rating) * RatingStepY) - (CUR_RATING_BAR_HEIGHT / 2),
                                      GraphWidth, CUR_RATING_BAR_HEIGHT,
                                      COL_RATING);
                }

                return Imaging.GetImageAsHtmlFragment(doc.Pages.First());

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

                    double RatingStepX = GraphWidth / graphData.Count;
                    double RatingStepY = height / (graphMax - graphMin);

                    for (double loopY = graphMax % 100; loopY < (graphMax - graphMin); loopY += 100)
                    {
                        gpr.FillRectangle(0, (loopY * RatingStepY) - (SCALE_MAJOR_HEIGHT / 2),
                                          GraphWidth, SCALE_MAJOR_HEIGHT,
                                          COL_SCALE_MAJOR);
                    }

                    for (int loop = 0; loop < graphData.Count; loop++)
                    {
                        if (graphData[loop].WinAv != 0 &&
                            graphData[loop].LossAv != 0)
                        {
                            gpr.FillRectangle(loop * RatingStepX, (graphMax - graphData[loop].LossAv) * RatingStepY,
                                              RatingStepX, (graphData[loop].LossAv - graphData[loop].WinAv) * RatingStepY,
                                              COL_BAR);
                        }
                    }

                    WriteRangeMessage(gpr, height, $"{graphMin}", $"{graphMax}");
                }

                return Imaging.GetImageAsHtmlFragment(doc.Pages.First());
            }).ConfigureAwait(false);
        }
    }
}
