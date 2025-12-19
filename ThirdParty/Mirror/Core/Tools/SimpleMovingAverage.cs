// Simple Moving Average (SMA) implementation
// Keeps a sliding window of the last n entries and drops older data
// https://en.wikipedia.org/wiki/Moving_average#Simple_moving_average
using System;
using System.Collections.Generic;

namespace Mirror
{
    public class SimpleMovingAverage
    {
        readonly int maxSamples;
        readonly Queue<double> samples;
        double sum;

        public double Value => samples.Count > 0 ? sum / samples.Count : 0;
        public int Count => samples.Count;

        public SimpleMovingAverage(int n)
        {
            maxSamples = n;
            samples = new Queue<double>(n + 1);
            sum = 0;
        }

        public void Add(double newValue)
        {
            samples.Enqueue(newValue);
            sum += newValue;

            if (samples.Count > maxSamples)
            {
                sum -= samples.Dequeue();
            }
        }

        public void Reset()
        {
            samples.Clear();
            sum = 0;
        }
    }
}
