using HinoTools.Alarm.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace HinoTools.Alarm.Service
{
    public class AlarmServiceDispatcher
    {
        #region FILEDS

        private static volatile AlarmServiceDispatcher instance;

        private static readonly object keyLock = new object();

        private static readonly object editLock = new object();

        private readonly List<IAlarmServiceCallback> callbackList;

        private readonly SemaphoreSlim mutex = new SemaphoreSlim(1, 1);

        private IAlarmHub alarmHub;

        #endregion

        #region PROPERTIES

        public bool IsActive { get; private set; }

        public IAlarmHub AlarmHub
        {
            get => this.alarmHub;
            set
            {
                if (this.alarmHub != null)
                {
                    this.alarmHub.Pushed -= ActionEventPushed;
                    this.alarmHub.Acknowledged -= ActionEventAcknowledged;
                    this.alarmHub.Reseted -= ActionEventReseted;
                    IsActive = false;
                }
                this.alarmHub = value;
                if (this.alarmHub != null)
                {
                    this.alarmHub.Pushed += ActionEventPushed;
                    this.alarmHub.Acknowledged += ActionEventAcknowledged;
                    this.alarmHub.Reseted += ActionEventReseted;
                    IsActive = true;
                }
            }
        }

        public static AlarmServiceDispatcher Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (keyLock)
                    {
                        if (instance == null)
                        {
                            instance = new AlarmServiceDispatcher();
                        }
                    }
                }
                return instance;
            }
        }

        #endregion

        #region CONSTRUCTOR

        private AlarmServiceDispatcher()
        {
            this.callbackList = new List<IAlarmServiceCallback>();
        }

        #endregion

        #region METHODS

        public void Register(IAlarmServiceCallback callback)
        {
            try
            {
                if (!IsActive) return;
                if (callback is null) return;
                if (callbackList.Contains(callback)) return;
                if (callback is ICommunicationObject communicationObject)
                {
                    communicationObject.Faulted += CommunicationObjectFaultedOrClosed;
                    communicationObject.Closed += CommunicationObjectFaultedOrClosed;
                    lock (editLock)
                        callbackList.Add(callback);
                }
            }
            catch { }
        }

        private void CommunicationObjectFaultedOrClosed(object sender, EventArgs e)
        {
            UnResgister(sender as IAlarmServiceCallback);
        }

        public void UnResgister(IAlarmServiceCallback callback)
        {
            try
            {
                if (!IsActive) return;
                if (callbackList.Contains(callback))
                {
                    if (callback is ICommunicationObject communicationObject)
                    {
                        try
                        {
                            communicationObject.Faulted -= CommunicationObjectFaultedOrClosed;
                            communicationObject.Closed -= CommunicationObjectFaultedOrClosed;
                        }
                        catch { }
                        if (callbackList.Contains(callback))
                            lock (editLock)
                                callbackList.Remove(callback);
                    }
                }
            }
            catch { }
        }

        private void ActionEventPushed(AlarmItem item)
        {
            if (!IsActive) return;
            InvokeMethod("PushCallback", item);
        }

        private void ActionEventAcknowledged(string[] ids)
        {
            if (!IsActive) return;
            InvokeMethod("AcknowledgeCallback", new object[] { ids });
        }

        private void ActionEventReseted()
        {
            if (!IsActive) return;
            InvokeMethod("ResetCallback");
        }

        private async void InvokeMethod(string methodName, params object[] parameters)
        {
            await mutex.WaitAsync();
            await Task.Delay(100);
            try
            {
                var type = typeof(IAlarmServiceCallback);
                var methodInfo = type.GetMethod(methodName);
                if (methodInfo is null) return;
                foreach (IAlarmServiceCallback callback in this.callbackList.ToList())
                {
                    try
                    {
                        if (callback is ICommunicationObject communicationObject)
                        {
                            switch (communicationObject.State)
                            {
                                case CommunicationState.Opened:
                                    methodInfo?.Invoke(callback, parameters);
                                    break;
                                case CommunicationState.Closed:
                                case CommunicationState.Faulted:
                                    UnResgister(callback);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    catch
                    {
                        UnResgister(callback);
                    }
                }
            }
            finally
            {                
                mutex.Release();
            }            
        }

        #endregion
    }
}
