
using ATSCADA;
using System;

namespace HinoTools.Alarm.Model
{
    public interface IAlarmHub
    {
        iDriver Driver { get; set; }

        bool IsActive { get; }        

        int Limit { get; }

        int Count { get; }

        Quality ConnectionQuality { get; }

        event Action ConstructionCompleted;

        event Action<Quality> ConnectionQualityChanged;

        event Action<AlarmItem> Pushed;

        event Action<string[]> Acknowledged;

        event Action Reseted;
        
        AlarmItem[] GetItems(int maxCount);

        void Acknowledge(string[] ids);
        
        void Reset();          
        
    }
}
