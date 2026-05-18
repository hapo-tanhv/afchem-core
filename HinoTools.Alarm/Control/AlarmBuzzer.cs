using HinoTools.Alarm.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Media;
using System.Reflection;

namespace HinoTools.Alarm.Control
{
    public partial class AlarmBuzzer : Component
    {
        #region FIELDS   

        private bool isActive;

        private bool isPlay;

        private SoundPlayer soundPlayer;

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

        [Category("Hino Settings")]
        public string SoundLocation { get; set; } = "~\\alarm.wav";

        #endregion

        #region CONSTRUCTORS

        public AlarmBuzzer()
        {
            InitializeComponent();
        }

        public AlarmBuzzer(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        #endregion

        #region METHODS

        private void ActionConstructionCompleted()
        {
            var location = GetLocation();
            if (!File.Exists(location)) return;

            this.soundPlayer = new SoundPlayer();
            this.soundPlayer.SoundLocation = location;
            this.soundPlayer.LoadCompleted += (sender, e) =>
            {
                this.alarmHub.Pushed += CheckAlarm;                         
            };
            this.soundPlayer.Load();
            this.isActive = true;
        }
      
        private void CheckAlarm(AlarmItem alarmItem)
        {
            if (alarmItem.Status == AlarmStatus.ALARM)
                On();
            else
            {
                if (this.alarmHub.Count == 0)
                    Off();
            }
        }

        public void On()
        {
            if (!this.isActive) return;
            if (!this.isPlay)
            {
                this.soundPlayer.PlayLooping();
                this.isPlay = true;
            }
        }

        public void Off()
        {
            if (!this.isActive) return;
            if (this.isPlay)
            {
                this.soundPlayer.Stop();
                this.isPlay = false;
            }

        }    
        

        private string GetLocation()
        {
            if (!SoundLocation.StartsWith("~\\")) return SoundLocation;

            var locationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(locationPath, SoundLocation.Replace("~\\", ""));
        }

        #endregion
    }
}
