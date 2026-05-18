using ATSCADA.ToolExtensions.ExtensionMethods;
using HinoTools.Alarm.Model;
using System.ComponentModel;
using System.Windows.Forms;

namespace HinoTools.Alarm.Control
{
    public class AlarmCounter : Label
    {
        #region FIELDS 
       
        private IAlarmHub alarmHub;

        #endregion

        #region PROPERTIES

        [Category("Hino Settings")]
        public IAlarmHub AlarmHub
        {
            get => this.alarmHub;
            set
            {
                if (this.alarmHub != null) this.alarmHub.ConstructionCompleted -= ActionConstructionCompleted;
                this.alarmHub = value;
                if (this.alarmHub != null) this.alarmHub.ConstructionCompleted += ActionConstructionCompleted;
            }
        }

        #endregion

        public AlarmCounter() : base() { }

        #region METHODS

        private void ActionConstructionCompleted()
        {
            this.alarmHub.Pushed += (item) => UpdateCount();          
            this.alarmHub.ConnectionQualityChanged += (quality) => UpdateActive(quality == Quality.Good);
            UpdateCount();
        }

        private void UpdateCount()
        {
            this.SynchronizedInvokeAction(() =>
            {
                this.Text = $"{this.alarmHub.Count}";
            });
        }

        private void UpdateActive(bool isActive)
        {
            this.SynchronizedInvokeAction(() =>
            {
                this.Enabled = isActive;
                this.Text = isActive ? $"{this.alarmHub.Count}" : "";
            });
        }

        #endregion
    }
}
