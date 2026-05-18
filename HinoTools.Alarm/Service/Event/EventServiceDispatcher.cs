using HinoTools.Alarm.Model.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace HinoTools.Alarm.Service.Event
{
    public class EventServiceDispatcher
    {
        #region FILEDS

        private static volatile EventServiceDispatcher instance;

        private static readonly object keyLock = new object();

        private static readonly object editLock = new object();

        private readonly List<IEventServiceCallback> callbackList;

        private readonly SemaphoreSlim mutex = new SemaphoreSlim(1, 1);

        private IEventHub eventHub;

        #endregion

        #region PROPERTIES

        public bool IsActive { get; private set; }

        public IEventHub EventHub
        {
            get => this.eventHub;
            set
            {
                if (this.eventHub != null)
                {
                    this.eventHub.Pushed -= ActionEventPushed;                   
                    IsActive = false;
                }
                this.eventHub = value;
                if (this.eventHub != null)
                {
                    this.eventHub.Pushed += ActionEventPushed;                    
                    IsActive = true;
                }
            }
        }

        public static EventServiceDispatcher Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (keyLock)
                    {
                        if (instance == null)
                        {
                            instance = new EventServiceDispatcher();
                        }
                    }
                }
                return instance;
            }
        }

        #endregion

        #region CONSTRUCTOR

        private EventServiceDispatcher()
        {
            this.callbackList = new List<IEventServiceCallback>();
        }

        #endregion

        #region METHODS

        public void Register(IEventServiceCallback callback)
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
            UnResgister(sender as IEventServiceCallback);
        }

        public void UnResgister(IEventServiceCallback callback)
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

        private void ActionEventPushed(EventItem item)
        {
            if (!IsActive) return;
            InvokeMethod("PushCallback", item);
        }
       
        private async void InvokeMethod(string methodName, params object[] parameters)
        {
            await mutex.WaitAsync();
            await Task.Delay(100);
            try
            {
                var type = typeof(IEventServiceCallback);
                var methodInfo = type.GetMethod(methodName);
                foreach (IEventServiceCallback callback in this.callbackList.ToList())
                {
                    try
                    {
                        if ((callback as ICommunicationObject).State == CommunicationState.Opened)
                        {
                            methodInfo.Invoke(callback, parameters);
                            continue;
                        }
                        UnResgister(callback);
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
