using HinoTools.Alarm.Model.Event;
using System.ServiceModel;

namespace HinoTools.Alarm.Service.Event
{
    public interface IEventServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void PushCallback(EventItem item);       
    }
}
