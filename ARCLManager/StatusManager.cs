using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class StatusManager
    {
        public delegate void IsSyncedEventHandler(bool state);
        public event IsSyncedEventHandler IsSyncedEvent;

        public delegate void IsDelayedEventHandler(bool state);
        public event IsDelayedEventHandler IsDelayedEvent;
        //Public
        public delegate void StatusUpdateEventHandler(object sender, StatusUpdateEventArgs data);
        public event StatusUpdateEventHandler StatusUpdate;

        //Public Read-only
        public bool IsSynced { get; private set; } = false;
        public bool IsDelayed { get; private set; } = false;
        public bool IsRunning { get; private set; } = false;

        public long TTL { get; private set; }
        //Private
        private int UpdateRate { get; set; }
        private Stopwatch Stopwatch { get; } = new Stopwatch();
        private bool Heartbeat { get; set; } = false;

        public StatusUpdateEventArgs Status { get; private set; }

        private ARCLConnection Connection { get; }
        public StatusManager(ARCLConnection connection) => Connection = connection;

        public void Start(int updateRate)
        {
            UpdateRate = updateRate;

            if (!Connection.IsReceivingAsync)
                Connection.StartReceiveAsync();

            ThreadPool.QueueUserWorkItem(new WaitCallback(StatusUpdate_Thread));
        }
        public void Stop()
        {
            if(IsSynced)
            {
                IsSynced = false;
                Connection.QueueTask(false, new Action(() => IsSyncedEvent?.Invoke(IsSynced)));
            }

            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);
        }

        //Private
        private void Connection_StatusUpdate(object sender, StatusUpdateEventArgs data)
        {
            Status = data;

            if(!IsSynced)
            {
                IsSynced = true;
                Connection.QueueTask(false, new Action(() => IsSyncedEvent?.Invoke(IsSynced)));
            }

            Heartbeat = true;
            TTL = Stopwatch.ElapsedMilliseconds;

            Connection.QueueTask(false, new Action(() => StatusUpdate?.Invoke(sender, data)));
        }

        private void StatusUpdate_Thread(object sender)
        {
            IsRunning = true;
            Connection.StatusUpdate += Connection_StatusUpdate;

            try
            {
                while (IsRunning)
                {
                    if (!IsDelayed)
                        Stopwatch.Reset();

                    Connection.Write("onelinestatus");

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if (Heartbeat)
                    {
                        if (IsDelayed)
                        {
                            IsDelayed = false;
                            Connection.QueueTask(false, new Action(() => IsDelayedEvent?.Invoke(false)));
                        }
                    }
                    else
                    {
                        if (!IsDelayed)
                        {
                            IsDelayed = true;
                            Connection.QueueTask(false, new Action(() => IsDelayedEvent?.Invoke(true)));
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
    }
}

