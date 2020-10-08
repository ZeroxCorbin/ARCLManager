using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class ExternalIOManager
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
        public ExternalIOManager(ARCLConnection connection) => Connection = connection;

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

        private void Start_()
        {
            ActiveSets.Clear();
            InProcessSets.Clear();

            ThreadPool.QueueUserWorkItem(new WaitCallback(ExternalIOUpdate_Thread));

            SyncState.State = SyncStates.FALSE;
            SyncState.Message = "ExtIODump";
            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
        }
        private void Stop_()
        {
            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);
        }

        private void ExternalIOUpdate_Thread(object sender)
        {
            IsRunning = true;
            Stopwatch.Reset();

            Connection.ExternalIOUpdate += Connection_ExternalIOUpdate;

            try
            {
                while(IsRunning)
                {
                    if(SyncState.State == SyncStates.TRUE)
                        Stopwatch.Reset();

                    Connection.Write("extIODump");

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if(Heartbeat)
                    {
                        if(SyncState.State == SyncStates.DELAYED)
                        {
                            SyncState.State = SyncStates.TRUE;
                            SyncState.Message = "ExtIODump";
                            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                        }
                    }
                    else
                    {
                        if(SyncState.State != SyncStates.DELAYED)
                        {
                            SyncState.State = SyncStates.DELAYED;
                            SyncState.Message = "ExtIODump";
                            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                        }
                    }
                }
            }
            finally
            {
                IsRunning = false;
                Connection.ExternalIOUpdate -= Connection_ExternalIOUpdate;
            }
        }
        private void Connection_ExternalIOUpdate(object sender, ExternalIOUpdateEventArgs data)
        {
            if(data.ExtIOSet == null) return;

            if(data.ExtIOSet.IsEnd)
            {
                if(SyncDesiredSets())
                {
                    if(SyncState.State != SyncStates.TRUE)
                    {
                        SyncState.State = SyncStates.TRUE;
                        SyncState.Message = "EndExtIODump";
                        Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                    }
                }
                return;
            }

            if(data.ExtIOSet.IsDump)
            {
                IsIOUpdate = false;

                if(ActiveSets.ContainsKey(data.ExtIOSet.Name))
                    ActiveSets[data.ExtIOSet.Name] = data.ExtIOSet;
                else
                {
                    while(!ActiveSets.TryAdd(data.ExtIOSet.Name, data.ExtIOSet)) { ActiveSets.Locked = false; }

                    IsIOUpdate = true;
                }

                foreach(KeyValuePair<string, ExtIOSet> set in ActiveSets)
                    IsIOUpdate |= set.Value.AddedForPendingUpdate;

                if(SyncState.State != SyncStates.TRUE)
                    if(!IsIOUpdate)
                    {
                        SyncState.State = SyncStates.TRUE;
                        Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                    }

                return;
            }

            if(data.ExtIOSet.HasInputs)
            {
                IsIOUpdate = false;

                if(ActiveSets.ContainsKey(data.ExtIOSet.Name))
                {
                    ActiveSets[data.ExtIOSet.Name].Inputs = data.ExtIOSet.Inputs;
                    ActiveSets[data.ExtIOSet.Name].AddedForPendingUpdate = false;
                }
                else
                    IsIOUpdate = true;

                foreach(KeyValuePair<string, ExtIOSet> set in ActiveSets)
                    IsIOUpdate |= set.Value.AddedForPendingUpdate;

                if(!IsIOUpdate)
                {
                    SyncState.State = SyncStates.TRUE;
                    Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                }

                return;
            }
        }


        public ReadOnlyConcurrentDictionary<string, ExtIOSet> ActiveSets { get; } = new ReadOnlyConcurrentDictionary<string, ExtIOSet>(10, 100);
        public ReadOnlyDictionary<string, ExtIOSet> DesiredSets { get; private set; }

        private Dictionary<string, ExtIOSet> InProcessSets { get; set; } = new Dictionary<string, ExtIOSet>();
        private bool IsIOUpdate { get; set; } = false;

        public bool WriteAllInputs()
        {
            if(Connection == null || !Connection.IsConnected)
                return false;
            if(SyncState.State != SyncStates.TRUE)
                return false;
            if(ActiveSets.Count == 0)
                return false;

            bool res = false;
            for(int i = 1; i <= ActiveSets.Count; i++)
            {
                ActiveSets[i.ToString()].AddedForPendingUpdate = true;
                res &= Connection.Write(ActiveSets[i.ToString()].WriteInputCommand);
            }

            return res ^= true;
        }
        public bool ReadAllOutputs()
        {
            if(Connection == null || !Connection.IsConnected)
                return false;
            if(SyncState.State != SyncStates.TRUE)
                return false;
            if(ActiveSets.Count == 0)
                return false;

            return Dump();
        }

        private bool Dump()
        {
            foreach(KeyValuePair<string, ExtIOSet> set in ActiveSets)
                set.Value.AddedForPendingUpdate = true;

            return Connection.Write("extIODump");
        }
        private bool SyncDesiredSets()
        {
            if(DesiredSets.Count == 0)
                return true;

            foreach(KeyValuePair<string, ExtIOSet> set in DesiredSets)
            {
                if(ActiveSets.ContainsKey(set.Key))
                {
                    if(InProcessSets.ContainsKey(set.Key))
                        InProcessSets.Remove(set.Key);
                    continue;
                }
                else
                {
                    if(!InProcessSets.ContainsKey(set.Key))
                        InProcessSets.Add(set.Key, set.Value);
                }
            }

            if(InProcessSets.Count() > 0)
                AddSets();
            else
                return true;

            return false;
        }
        private void AddSets()
        {
            foreach(KeyValuePair<string, ExtIOSet> set in InProcessSets)
            {
                if(set.Value.AddedForPendingUpdate) continue;
                else
                    Connection.Write(set.Value.CreateSetCommand);
            }

            ThreadPool.QueueUserWorkItem(new WaitCallback(CreateIOWaitThread));
        }
        private void CreateIOWaitThread(object sender)
        {
            Thread.Sleep(10000);

            Dump();
        }


    }
}
