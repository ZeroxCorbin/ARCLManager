using System;
using ARCLTaskQueue;

namespace ARCL
{
    public class RangeDeviceManager: GroupedTaskQueue
    {
        /// <summary>
        /// Raised when the External IO is sycronized with the EM.
        /// </summary>
        public delegate void InSyncEventHandler(object sender, bool state);
        public event InSyncEventHandler InSync;
        /// <summary>
        /// True when the External IO is sycronized with the EM.
        /// </summary>
        public bool IsSynced { get; private set; } = false;

        private ARCLConnection Connection { get; set; }

        private ConfigManager ConfigManager { get; set; }
        public RangeDeviceManager(ARCLConnection connection) => Connection = connection;

        private void Connection_RangeDeviceCurrentUpdate(object sender, ARCLTypes.RangeDeviceUpdateEventArgs data)
        {
            throw new System.NotImplementedException();
        }

        private void Connection_RangeDeviceCumulativeUpdate(object sender, ARCLTypes.RangeDeviceUpdateEventArgs data)
        {
            throw new System.NotImplementedException();
        }

        public void Start()
        {
            ConfigManager = new ConfigManager(Connection);
            ConfigManager.Start();


        }
        public void Stop()
        {
            if (IsSynced)
            {
                IsSynced = false;
                this.Queue(false, new Action(() => InSync?.Invoke(this, false)));
            }
                
            

            Connection?.StopReceiveAsync();
        }
    }
}
