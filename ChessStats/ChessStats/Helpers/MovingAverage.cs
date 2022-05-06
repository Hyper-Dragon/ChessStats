using System;
using System.Collections.Generic;

namespace ChessStats.Helpers
{
    internal class MovingAverage
    {
        private readonly int _k;
        private readonly double[] _values;

        private int _index = 0;
        private double _sum = 0;

        public MovingAverage(int k)
        {
            if (k <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(k), "Must be greater than 0");
            }

            _k = k;
            _values = new double[k];
        }

        public double Update(double nextInput)
        {
            // calculate the new sum
            _sum = _sum - _values[_index] + nextInput;

            // overwrite the old value with the new one
            _values[_index] = nextInput;

            // increment the index (wrapping back to 0)
            _index = (_index + 1) % _k;

            // calculate the average
            return _sum / _k;
        }

        public static double[] CalculateMovingAv(List<double> values, int k)
        {
            MovingAverage movingAv = new(k);
            List<double> movingAvOut = new();

            for (int i = 0; i < values.Count; i++)
            {
                if (i < k)
                {
                    _ = movingAv.Update(values[i]);
                }
                else
                {
                    movingAvOut.Add(movingAv.Update(values[i]));
                }
            }

            return movingAvOut.ToArray();
        }
    }
}
