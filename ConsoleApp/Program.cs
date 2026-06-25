using System;
using System.Collections.Generic;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Starting Accumulator Delta-Tracking Unit Tests ===");
            try
            {
                TestMonotonicGrowth();
                TestRegisterResetOnResume();
                TestCommsDropoutNaNHandling();
                TestRecoverySelfHealing();
                
                Console.WriteLine("\n[SUCCESS] All unit tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[FAILURE] Test failed: {ex.Message}");
                Console.ResetColor();
                Environment.Exit(1);
            }
        }

        // Mock class mimicking the exact logic implemented in HinoTools.Data.Log.AlarmReportLogger
        class MockAccumulator
        {
            public Dictionary<string, double> AccumulatedTimers = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, double> PreviousTimerValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            public double PrevValue = 0;

            public void ResetAccumulators()
            {
                AccumulatedTimers.Clear();
                PreviousTimerValues.Clear();
            }

            public void UpdateAccumulator(string alias, double currentVal)
            {
                if (double.IsNaN(currentVal) || currentVal <= 0) return;

                if (!AccumulatedTimers.ContainsKey(alias))
                {
                    AccumulatedTimers[alias] = 0;
                }

                if (!PreviousTimerValues.ContainsKey(alias))
                {
                    double dbVal = AccumulatedTimers[alias];
                    if (currentVal >= dbVal)
                    {
                        double initialDelta = currentVal - dbVal;
                        AccumulatedTimers[alias] += initialDelta;
                    }
                    else
                    {
                        AccumulatedTimers[alias] += currentVal;
                    }
                    PreviousTimerValues[alias] = currentVal;
                    return;
                }

                double prevVal = PreviousTimerValues[alias];

                if (currentVal >= prevVal)
                {
                    double delta = currentVal - prevVal;
                    AccumulatedTimers[alias] += delta;
                }
                else
                {
                    // Reset detected: add currentVal as it is a genuine reset
                    AccumulatedTimers[alias] += currentVal;
                }

                PreviousTimerValues[alias] = currentVal;
            }

            public double GetAccumulatedValue(string alias)
            {
                if (AccumulatedTimers.TryGetValue(alias, out double val))
                {
                    return Math.Round(val, 2);
                }
                return 0;
            }

            public void RecoverAccumulator(string alias, double dbVal)
            {
                AccumulatedTimers[alias] = dbVal;
                PreviousTimerValues.Remove(alias);
            }
        }

        static void TestMonotonicGrowth()
        {
            Console.WriteLine("\nRunning TestMonotonicGrowth...");
            var mock = new MockAccumulator();
            string alias = "ThoiGianCapLieu";

            // Simulate normal progression from 0 to 5 seconds
            double[] readings = { 0, 1, 2, 3, 4, 5 };
            foreach (var r in readings)
            {
                mock.UpdateAccumulator(alias, r);
            }

            double result = mock.GetAccumulatedValue(alias);
            Console.WriteLine($"Monotonic Growth Result: {result}s (Expected: 5s)");
            if (result != 5)
            {
                throw new Exception($"Expected 5, but got {result}");
            }
        }

        static void TestRegisterResetOnResume()
        {
            Console.WriteLine("\nRunning TestRegisterResetOnResume...");
            var mock = new MockAccumulator();
            string alias = "ThoiGianCapLieu";

            // 1. Monotonic growth up to 14
            for (int i = 0; i <= 14; i++)
            {
                mock.UpdateAccumulator(alias, i);
            }
            mock.PrevValue = 14;

            // 2. Machine is stopped/paused (timer stays at 14)
            mock.UpdateAccumulator(alias, 14);

            // 3. First read during pause is 0 (PLC sends 0 briefly or drop connection)
            mock.UpdateAccumulator(alias, 0);

            // 4. Machine resumes, PLC timer starts from 1, 2, 3
            double[] resumeReadings = { 1, 2, 3 };
            foreach (var r in resumeReadings)
            {
                mock.UpdateAccumulator(alias, r);
            }

            double result = mock.GetAccumulatedValue(alias);
            Console.WriteLine($"Register Reset Result: {result}s (Expected: 17s)");
            if (result != 17)
            {
                throw new Exception($"Expected 17, but got {result}");
            }
        }

        static void TestCommsDropoutNaNHandling()
        {
            Console.WriteLine("\nRunning TestCommsDropoutNaNHandling...");
            var mock = new MockAccumulator();
            string alias = "ThoiGianCapLieu";

            // 1. Initial growth
            mock.UpdateAccumulator(alias, 10);
            mock.PrevValue = 10;
            mock.UpdateAccumulator(alias, 11);
            mock.PrevValue = 11;

            // 2. Comms drop (NaN) - fallback logic in PollAndLog replaces NaN with prev value
            double readVal1 = double.NaN;
            if (double.IsNaN(readVal1)) readVal1 = mock.PrevValue; // fallback to 11
            mock.UpdateAccumulator(alias, readVal1);
            // mock.PrevValue remains 11

            double readVal2 = double.NaN;
            if (double.IsNaN(readVal2)) readVal2 = mock.PrevValue; // fallback to 11
            mock.UpdateAccumulator(alias, readVal2);

            // 3. Comms restored, reads 12
            mock.UpdateAccumulator(alias, 12);
            mock.PrevValue = 12;

            double result = mock.GetAccumulatedValue(alias);
            Console.WriteLine($"Comms Dropout Result: {result}s (Expected: 12s)");
            if (result != 12)
            {
                throw new Exception($"Expected 12, but got {result}");
            }
        }

        static void TestRecoverySelfHealing()
        {
            Console.WriteLine("\nRunning TestRecoverySelfHealing...");
            var mock = new MockAccumulator();
            string alias = "ThoiGianCapLieu";

            // 1. Recover accumulator from database (e.g. 14s)
            mock.RecoverAccumulator(alias, 14);

            // 2. First read after restart is 15s (PLC has kept running, no reset)
            mock.UpdateAccumulator(alias, 15);

            // 3. Next read is 16s
            mock.UpdateAccumulator(alias, 16);

            double result = mock.GetAccumulatedValue(alias);
            Console.WriteLine($"Self-Healing Recovery Result (No PLC Reset): {result}s (Expected: 16s)");
            if (result != 16)
            {
                throw new Exception($"Expected 16, but got {result}");
            }

            // 4. Recover accumulator again to 14s, but simulate PLC reset occurred during restart (reads 2s)
            mock.ResetAccumulators();
            mock.RecoverAccumulator(alias, 14);
            mock.UpdateAccumulator(alias, 2); // First read is 2
            mock.UpdateAccumulator(alias, 3); // Second read is 3

            result = mock.GetAccumulatedValue(alias);
            Console.WriteLine($"Self-Healing Recovery Result (With PLC Reset): {result}s (Expected: 17s)");
            if (result != 17)
            {
                throw new Exception($"Expected 17, but got {result}");
            }
        }
    }
}

