using System;
using System.Collections.Generic;

namespace ChessStats.Helpers
{
    internal class MovingAverage
    {
        private readonly int rollingAv;
        private readonly double[] values;

        private int index = 0;
        private double total = 0;

        public MovingAverage(int k)
        {
            if (k <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(k), "Must be greater than 0");
            }

            rollingAv = k;
            values = new double[k];
        }

        public double Update(double nextInput)
        {
            total = total - values[index] + nextInput;
            values[index] = nextInput;
            index = (index + 1) % rollingAv;

            return total / rollingAv;
        }

        public static double[] CalculateMovingAv(List<double> values, int k)
        {
            MovingAverage movingAv = new(k);
            List<double> movingAvOut = new();

            for (int loop = 0; loop < values.Count; loop++)
            {
                if (loop < k)
                {
                    _ = movingAv.Update(values[loop]);
                }
                else
                {
                    movingAvOut.Add(movingAv.Update(values[loop]));
                }
            }

            return movingAvOut.ToArray();
        }
    }
}