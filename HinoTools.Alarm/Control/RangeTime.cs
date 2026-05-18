using System;

namespace HinoTools.Alarm.Control
{
    public class RangeTime
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }
    }

    public class RangeTimeFactory
    {
        public static RangeTime Get(string filter)
        {
            var today = DateTime.Today;
            switch (filter)
            {
                case "All":
                    return new RangeTime()
                    {
                        StartTime = DateTime.MinValue,
                        EndTime = DateTime.MaxValue
                    };
                case "Today":
                    return new RangeTime()
                    {
                        StartTime = today,
                        EndTime = today.AddDays(1).AddTicks(-1)
                    };
                case "Yesterday":
                    return new RangeTime()
                    {
                        StartTime = today.AddDays(-1),
                        EndTime = today.AddTicks(-1)
                    };
                case "This week":
                    var dayOfWeek = (int)today.Date.DayOfWeek;
                    return new RangeTime()
                    {
                        StartTime = today.AddDays(-dayOfWeek),
                        EndTime = today.AddDays(7 - dayOfWeek).AddTicks(-1)
                    };
                case "This month":
                    var dayOfMonth = today.Day;
                    return new RangeTime()
                    {
                        StartTime = today.AddDays(1 - dayOfMonth),
                        EndTime = today.AddDays(1 - dayOfMonth).AddMonths(1).AddTicks(-1)
                    };
                default:
                    return new RangeTime()
                    {
                        StartTime = DateTime.MinValue,
                        EndTime = DateTime.MaxValue
                    };
            }
        }
    }

}
