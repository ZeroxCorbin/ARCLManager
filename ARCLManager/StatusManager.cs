using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class StatusManager
    {
        public delegate void SyncStateChangeEventHandler(object sender, SyncStateEventArgs syncState);
        public event SyncStateChangeEventHandler SyncStateChange;
        public SyncStateEventArgs SyncState { get; private set; } = new SyncStateEventArgs();

        public bool IsRunning { get; private set; } = false;
        public long TTL { get; private set; } = 0;

        private int UpdateRate { get; set; } = 500;
        private Stopwatch Stopwatch { get; } = new Stopwatch();
        private bool Heartbeat { get; set; } = false;

        private ARCLConnection Connection { get; set; }
        public StatusManager(ARCLConnection connection) => Connection = connection;

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
        public bool Start(int updateRate, ARCLConnection connection)
        {
            UpdateRate = updateRate;
            Connection = connection;

            return Start(updateRate);
        }
        public void Stop()
        {
            if(SyncState.State != SyncStates.FALSE)
            {
                SyncState.State = SyncStates.FALSE;
                SyncState.Message = "Stop";
                Connection?.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
            }
            Connection?.StopReceiveAsync();

            Stop_();
        }
        public bool WaitForSync(long timeout = 30000)
        {
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            while(SyncState.State != SyncStates.TRUE & sw.ElapsedMilliseconds < timeout) { Thread.Sleep(1); }

            return SyncState.State == SyncStates.TRUE;
        }

        private void Start_()
        {
            Status = null;

            ThreadPool.QueueUserWorkItem(new WaitCallback(StatusUpdate_Thread));

            SyncState.State = SyncStates.FALSE;
            SyncState.Message = "OneLineStatus";
            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
        }
        private void Stop_()
        {
            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);
        }

        private void StatusUpdate_Thread(object sender)
        {
            IsRunning = true;
            Stopwatch.Reset();

            Connection.StatusUpdate += Connection_StatusUpdate;

            try
            {
                while(IsRunning)
                {
                    if(SyncState.State == SyncStates.TRUE)
                        Stopwatch.Reset();

                    Connection.Write("onelinestatus");

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if(Heartbeat)
                    {
                        if(SyncState.State == SyncStates.DELAYED)
                        {
                            SyncState.State = SyncStates.TRUE;
                            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                        }
                    }
                    else
                    {
                        if(SyncState.State != SyncStates.DELAYED)
                        {
                            SyncState.State = SyncStates.DELAYED;
                            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
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

            if(SyncState.State != SyncStates.TRUE)
            {
                SyncState.State = SyncStates.TRUE;
                SyncState.Message = "EndOneLineStatus";
                Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
            }


            Connection.QueueTask(false, new Action(() => StatusUpdate?.Invoke(sender, data)));
        }


        public delegate void StatusUpdateEventHandler(object sender, StatusUpdateEventArgs data);
        public event StatusUpdateEventHandler StatusUpdate;

        public StatusUpdateEventArgs Status { get; private set; } = null;
    }
}

