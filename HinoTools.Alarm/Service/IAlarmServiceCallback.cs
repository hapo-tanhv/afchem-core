using HinoTools.Alarm.Model;
using System;
using System.ServiceModel;

namespace HinoTools.Alarm.Service
{
    public interface IAlarmServiceCallback
    {
        [OperationContract(IsOneWay = true)]        
        void PushCallback(AlarmItem item);

        [OperationContract(IsOneWay = true)]
        void AcknowledgeCallback(string[] ids);

        [OperationContract(IsOneWay = true)]
        void ResetCallback();
    }
}
