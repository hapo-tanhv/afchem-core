using HinoTools.Alarm.Model.Event;
using System.ServiceModel;

namespace HinoTools.Alarm.Service.Event
{
    [ServiceContract(CallbackContract = typeof(IEventServiceCallback), SessionMode = SessionMode.Required)]
    public interface IEventService
    {
        [OperationContract(IsOneWay = true, IsInitiating = true)]
        void Connect();

        [OperationContract(IsOneWay = true, IsTerminating = true)]
        void Disconnect();

        [OperationContract(IsOneWay = false)]
        bool Ping();

        [OperationContract(IsOneWay = false)]
        EventItem[] GetItems(int maxCount);

        [OperationContract(IsOneWay = false)]
        void Push(EventItem eventItem);
    }
}
