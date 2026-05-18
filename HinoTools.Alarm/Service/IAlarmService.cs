using HinoTools.Alarm.Model;
using System.ServiceModel;

namespace HinoTools.Alarm.Service
{
    [ServiceContract(CallbackContract = typeof(IAlarmServiceCallback), SessionMode = SessionMode.Required)]
    public interface IAlarmService
    {
        [OperationContract(IsOneWay = true, IsInitiating = true)]
        void Connect();

        [OperationContract(IsOneWay = true, IsTerminating = true)]
        void Disconnect();

        [OperationContract(IsOneWay = false)]
        bool Ping();

        [OperationContract(IsOneWay = false)]
        AlarmItem[] GetItems(int maxCount);        

        [OperationContract(IsOneWay = true)]
        void Acknowledge(string[] ids);

        [OperationContract(IsOneWay = true)]
        void Reset();
    }
}
