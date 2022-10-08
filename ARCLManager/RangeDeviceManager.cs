using ARCLTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ARCL
{
    /// <summary>
    /// This class is used to manage the Range Devices of a Mobile robot.
    /// When you call Start(), the Devices dictionary will be loaded with all of the available range devices.
    /// Call WaitForSync() to wait for the Devices (dictionary) to be loaded.
    /// </summary>
    public class RangeDeviceManager
    {
        /// <summary>
        /// The Delegate for the SyncStateChange Event.
        /// </summary>
        /// <param name="sender">A reference to this class.</param>
        /// <param name="syncState">The state of the dictionary Keys. See the SyncState property for details.</param>
        public delegate void SyncStateChangeEventHandler(object sender, SyncStateEventArgs syncState);
        /// <summary>
        /// Raised when SyncState property changes.
        /// See the SyncState property for details.
        /// </summary>
        public event SyncStateChangeEventHandler SyncStateChange;
        /// <summary>
        /// The state of the Managers dictionary.
        /// State= WAIT; Wait to access the dictionary.
        ///              Calling Start() or Stop() sets this state.
        /// State= DELAYED; The dictionary Values are not valid.
        ///                 The dictionary Values being updated from the ARCL Server are delayed.
        /// State= UPDATING; The dictionary Values are being updated.
        /// State= OK; The dictionary is up to date.
        /// </summary>
        public SyncStateEventArgs SyncState { get; private set; } = new SyncStateEventArgs();
        public bool IsSynced => SyncState.State == SyncStates.OK;
        /// <summary>
        /// A reference to the connection to the ARCL Server.
        /// </summary>
        private ARCLConnection Connection { get; set; }
        /// <summary>
        /// Start the manager.
        /// This will load the dictionary.
        /// </summary>
        /// <param name="updateRate">How often to send a request to update the dictionary's Values.</param>
        /// <returns>False: Connection issue.</returns>
        public bool Start(int updateRate)
        {
            UpdateRate = updateRate;

            if(Connection == null || !Connection.IsConnected)
                return false;

            Start_();

            return true;
        }
        /// <summary>
        /// Start the manager.
        /// This will load the dictionary.
        /// </summary>
        /// <param name="updateRate">How often to send a request to update the dictionary's Values.</param>
        /// <param name="connection">A connected ARCLConnection.</param>
        /// <returns>False: Connection issue.</returns>
        public bool Start(int updateRate, ARCLConnection connection)
        {
            Connection = connection;

            return Start(updateRate);
        }
        /// <summary>
        /// Stop the manager.
        /// </summary>
        public void Stop()
        {
            if(SyncState.State != SyncStates.WAIT)
            {
                SyncState.State = SyncStates.WAIT;
                SyncState.Message = "Stop";
                SyncStateChange?.Invoke(this, SyncState);
            }

            Stop_();
        }
        /// <summary>
        /// Wait for the dictionary to be in sync with the ARCL server data.
        /// After calling Start(), you can either call this method or wait for the SyncStateChanged event. 
        /// </summary>
        /// <param name="timeout">Wait for SyncState.State.OK for milliseconds.</param>
        /// <returns>False: Timeout waiting for SyncState.State.OK.</returns>
        public bool WaitForSync(long timeout = 30000)
        {
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            while(SyncState.State != SyncStates.OK & sw.ElapsedMilliseconds < timeout) { Thread.Sleep(1); }

            return SyncState.State == SyncStates.OK;
        }
        /// <summary>
        /// Is the update thread running?
        /// </summary>
        public bool IsRunning { get; private set; } = false;
        /// <summary>
        /// Time between updates of the dictionary Values.
        /// If this is consistently greater than the UpdateRate passed to Start(), consider lowering the update rate.
        /// </summary>
        public long TTL { get; private set; } = 0;
        /// <summary>
        /// Stores the provided update rate for the Update_Thread. (ms)
        /// </summary>
        private int UpdateRate { get; set; } = 500;
        /// <summary>
        /// Used to time the message response. (TTL)
        /// </summary>
        private Stopwatch Stopwatch { get; } = new Stopwatch();
        /// <summary>
        /// Used to track if a message response was received before sending the next request.
        /// </summary>
        private bool Heartbeat { get; set; } = false;

        private object updateLock = new object();

        public bool GetLock()
        {
            bool __lockWasTaken = false;

            Monitor.Enter(updateLock, ref __lockWasTaken);
            return __lockWasTaken;
        }
        public void ReleaseLock() => Monitor.Exit(updateLock);

        private void Start_()
        {
            Connection.RangeDeviceUpdate += Connection_RangeDeviceUpdate;

            Devices.Clear();

            Connection.Send("rangeDeviceList");

            SyncState.State = SyncStates.WAIT;
            SyncState.Message = "RangeDeviceList";
            SyncStateChange?.Invoke(this, SyncState);
        }
        private bool _stopped = false;
        private void Stop_()
        {
            if(Connection != null)
                Connection.RangeDeviceUpdate -= Connection_RangeDeviceUpdate;

            if (!IsRunning) return;

            while (!_stopped)
                IsRunning = false;

            _stopped = false;
        }

        public RangeDeviceManager() { }
        public RangeDeviceManager(ARCLConnection connection) => Connection = connection;

        private void RangeDeviceUpdate_Thread(object sender)
        {
            IsRunning = true;
            Stopwatch.Reset();

            Connection.RangeDeviceCurrentUpdate += Connection_RangeDeviceCurrentUpdate;
            Connection.RangeDeviceCumulativeUpdate += Connection_RangeDeviceCumulativeUpdate;

            try
            {
                while(IsRunning)
                {
                    if(SyncState.State == SyncStates.OK)
                        Stopwatch.Reset();

                    foreach(KeyValuePair<string, RangeDevice> l in Devices)
                        Connection.Send("rangeDeviceGetCurrent " + l.Key);

                    foreach(KeyValuePair<string, RangeDevice> l in Devices)
                        Connection.Send("rangeDeviceGetCumulative " + l.Key);

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if(Heartbeat)
                    {
                        if(SyncState.State != SyncStates.OK)
                        {
                            SyncState.State = SyncStates.OK;
                            SyncStateChange?.Invoke(this, SyncState);
                        }
                    }
                    else
                    {
                        if(SyncState.State != SyncStates.DELAYED)
                        {
                            SyncState.State = SyncStates.DELAYED;
                            SyncStateChange?.Invoke(this, SyncState);
                        }
                    }
                }
            }
            finally
            {
                _stopped = true;
                IsRunning = false;
                Connection.RangeDeviceCurrentUpdate -= Connection_RangeDeviceCurrentUpdate;
                Connection.RangeDeviceCumulativeUpdate -= Connection_RangeDeviceCumulativeUpdate;
            }
        }
        private void Connection_RangeDeviceUpdate(object sender, RangeDeviceEventArgs device)
        {
            if(device.IsEnd)
            {
                Connection.RangeDeviceUpdate -= Connection_RangeDeviceUpdate;
                ThreadPool.QueueUserWorkItem(new WaitCallback(RangeDeviceUpdate_Thread));

                if(SyncState.State != SyncStates.OK)
                {
                    SyncState.State = SyncStates.OK;
                    SyncState.Message = "EndRangeDeviceList";
                    SyncStateChange?.Invoke(this, SyncState);
                }
                return;
            }


            int i = 0;
            if(!string.IsNullOrEmpty(device.RangeDevice.Name) && device.RangeDevice.Name.Contains("Laser"))
                while(!Devices.TryAdd(device.RangeDevice.Name, device.RangeDevice)) { Devices.Locked = false; if (i++ > 1000) break; }
        }


        public ReadOnlyConcurrentDictionary<string, RangeDevice> Devices { get; private set; } = new ReadOnlyConcurrentDictionary<string, RangeDevice>(10, 100);

        public delegate void RangeDeviceCurrentReceivedEventHandler(RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCurrentReceivedEventHandler RangeDeviceCurrentUpdate;

        public delegate void RangeDeviceCumulativeUpdateEventHandler(RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCumulativeUpdateEventHandler RangeDeviceCumulativeUpdate;

        private void Connection_RangeDeviceCurrentUpdate(object sender, RangeDeviceReadingUpdateEventArgs data)
        {
            if(Devices.ContainsKey(data.Name))
            {
                lock (updateLock)
                    Devices[data.Name].CurrentReadings = data;
                Devices[data.Name].CurrentReadingsInSync = true;
            }

            Heartbeat = true;
            TTL = Stopwatch.ElapsedMilliseconds;

            RangeDeviceCurrentUpdate?.Invoke(data);
        }
        private void Connection_RangeDeviceCumulativeUpdate(object sender, RangeDeviceReadingUpdateEventArgs data)
        {
            if(Devices.ContainsKey(data.Name))
            {
                lock(updateLock)
                    Devices[data.Name].CumulativeReadings = data;
                Devices[data.Name].CumulativeReadingsInSync = true;
            }

            Heartbeat = true;
            TTL = Stopwatch.ElapsedMilliseconds;

            RangeDeviceCumulativeUpdate?.Invoke(data);
        }



    }
}
