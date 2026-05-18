using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var format = "old {0} new {1}";
            var a = 1;
            var b = 2;
            var c = string.Format($"{format}", a, b);

            var time = GetStartTimeOfWorkUnit(DateTime.Now);
            var time1 = GetStartTimeOfWorkUnit(new DateTime(2023, 03, 28, 23, 32, 0));
        } 
        
        static DateTime GetStartTimeOfWorkUnit(DateTime dateTime)
        {            
            var hour = dateTime.Hour;
            var minute = dateTime.Minute;

            var totalMinute = hour * 60 + minute + 30;
            return dateTime.Date
                .AddMinutes(-30)
                .AddMinutes(120 * (totalMinute / 120));
        }
    }
}
