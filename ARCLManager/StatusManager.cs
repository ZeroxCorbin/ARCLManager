using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class StatusManager
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
            if(!Connection.StartReceiveAsync())
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

            Connection?.StopReceiveAsync();

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

        private void Start_()
        {
            Status = null;

            ThreadPool.QueueUserWorkItem(new WaitCallback(StatusUpdate_Thread));

            SyncState.State = SyncStates.WAIT;
            SyncState.Message = "OneLineStatus";
            SyncStateChange?.Invoke(this, SyncState);
        }
        private void Stop_()
        {
            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);
        }

        public StatusManager() { }
        public StatusManager(ARCLConnection connection) => Connection = connection;

        private void StatusUpdate_Thread(object sender)
        {
            IsRunning = true;
            Stopwatch.Reset();

            Connection.StatusUpdate += Connection_StatusUpdate;

            try
            {
                while(IsRunning)
                {
                    if(SyncState.State == SyncStates.OK)
                        Stopwatch.Reset();

                    Connection.Write("onelinestatus");

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if(Heartbeat)
                    {
                        if(SyncState.State == SyncStates.DELAYED)
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
                IsRunning = false;
                Connection.StatusUpdate -= Connection_StatusUpdate;
            }
        }
        private void Connection_StatusUpdate(object sender, StatusUpdateEventArgs data)
        {
            Heartbeat = true;
            TTL = Stopwatch.ElapsedMilliseconds;

            Status = data;

            if(SyncState.State != SyncStates.OK)
            {
                SyncState.State = SyncStates.OK;
                SyncState.Message = "EndOneLineStatus";
                SyncStateChange?.Invoke(this, SyncState);
            }


            StatusUpdate?.Invoke(sender, data);
        }


        public delegate void StatusUpdateEventHandler(object sender, StatusUpdateEventArgs data);
        public event StatusUpdateEventHandler StatusUpdate;

        public StatusUpdateEventArgs Status { get; private set; } = null;
    }
}

