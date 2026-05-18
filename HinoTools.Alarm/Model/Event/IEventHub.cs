using ATSCADA;
using System;

namespace HinoTools.Alarm.Model.Event
{
    public interface IEventHub
    {
        iDriver Driver { get; set; }

        bool IsActive { get; }

        Quality ConnectionQuality { get; }

        event Action ConstructionCompleted;

        event Action<Quality> ConnectionQualityChanged;

        event Action<EventItem> Pushed;

        EventItem[] GetItems(int maxCount);

        void Push(EventItem eventItem);
    }
}
