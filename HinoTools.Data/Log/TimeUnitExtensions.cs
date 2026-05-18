using System;

namespace HinoTools.Data.Log
{
    public static class TimeUnitExtensions
    {
        public static DateTime GetStartTimeWithUnit(this DateTime dateTime, TimeUnit timeUnit)
        {
            switch (timeUnit)
            {
                case TimeUnit.Minute:
                    return dateTime.GetMinuteStart();             
                case TimeUnit.QuarterHour:
                    return dateTime.GetQuarterHourStart();
                case TimeUnit.HalfHour:
                    return dateTime.GetHalfHourStart();
                case TimeUnit.Hour:
                    return dateTime.GetHourStart();
                
                default:
                    return DateTime.MinValue;
            }
        }

        public static DateTime GetEndTimeWithUnit(this DateTime dateTime, TimeUnit timeUnit)
        {
            switch (timeUnit)
            {
                case TimeUnit.Minute:
                    return dateTime.GetMinuteEnd();
                case TimeUnit.QuarterHour:
                    return dateTime.GetQuarterHourEnd();
                case TimeUnit.HalfHour:
                    return dateTime.GetHalfHourEnd();
                case TimeUnit.Hour:
                    return dateTime.GetHourEnd();

                default:
                    return DateTime.MaxValue;
            }
        }

        #region MINUTE        

        public static DateTime GetMinuteStart(this DateTime dateTime)
        {
            var timeNow = dateTime.TimeOfDay;
            var hour = timeNow.Hours;
            var minute = timeNow.Minutes;

            return dateTime.Date
                .AddHours(hour)
                .AddMinutes(minute);
        }

        public static DateTime GetMinuteEnd(this DateTime dateTime)
        {
            var timeNow = dateTime.TimeOfDay;
            var hour = timeNow.Hours;
            var minute = timeNow.Minutes;

            return dateTime.Date
                .AddHours(hour)
                .AddMinutes(minute + 1)
                .AddTicks(-1);
        }
        #endregion

        #region QUARTER HOUR        

        public static DateTime GetQuarterHourStart(this DateTime dateTime)
        {
            var timeNow = dateTime.TimeOfDay;
            var hour = timeNow.Hours;
            var minute = timeNow.Minutes;

            return dateTime.Date
                .AddHours(hour)
                .AddMinutes(minute - minute % 15);
        }

        public static DateTime GetQuarterHourEnd(this DateTime dateTime)
        {
            var timeNow = DateTime.Now.TimeOfDay;
            var hour = timeNow.Hours;
            var minute = timeNow.Minutes;

            return dateTime.Date
                .AddHours(hour)
                .AddMinutes(minute - minute % 15 + 15)
                .AddTicks(-1);
        }


        #endregion

        #region HALF HOUR

        public static DateTime GetHalfHourStart(this DateTime dateTime)
        {
            var timeNow = DateTime.Now.TimeOfDay;
            var hour = timeNow.Hours;
            var minute = timeNow.Minutes;

            return dateTime.Date
                .AddHours(hour)
                .AddMinutes(minute - minute % 30);
        }

        public static DateTime GetHalfHourEnd(this DateTime dateTime)
        {
            var timeNow = DateTime.Now.TimeOfDay;
            var hour = timeNow.Hours;
            var minute = timeNow.Minutes;

            return dateTime.Date
                .AddHours(hour)
                .AddMinutes(minute - minute % 30 + 30)
                .AddTicks(-1);
        }

        #endregion

        #region HOUR     

        public static DateTime GetHourStart(this DateTime dateTime)
        {
            var timeNow = DateTime.Now.TimeOfDay;
            var hour = timeNow.Hours;

            return dateTime.Date
                .AddHours(hour);
        }

        public static DateTime GetHourEnd(this DateTime dateTime)
        {
            var timeNow = DateTime.Now.TimeOfDay;
            var hour = timeNow.Hours;

            return dateTime.Date
                .AddHours(hour + 1)
                .AddTicks(-1);
        }

        #endregion
    }
}
