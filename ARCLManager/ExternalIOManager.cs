﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class ExternalIOManager
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
        private bool IsIOUpdate { get; set; } = false;

        public ConcurrentDictionary<string, ExtIOSet> ActiveSets { get; } = new ConcurrentDictionary<string, ExtIOSet>(10, 100);
        //public ReadOnlyDictionary<string, ExtIOSet> ActiveSets
        //{
        //    get
        //    {
        //        Monitor.Enter(ActiveSetsLock);
        //        return new ReadOnlyDictionary<string, ExtIOSet>(_ActiveSets);
        //    }
        //}
        //private object ActiveSetsLock { get; } = new object();
        //public void ReleaseActiveSetLock() => Monitor.Exit(ActiveSetsLock);

        public ReadOnlyDictionary<string, ExtIOSet> DesiredSets { get; }
        private Dictionary<string, ExtIOSet> InProcessSets { get; set; } = new Dictionary<string, ExtIOSet>();

        public ExternalIOManager(ARCLConnection connection, Dictionary<string, ExtIOSet> desiredSets)
        {
            Connection = connection;

            if (desiredSets == null) DesiredSets = new ReadOnlyDictionary<string, ExtIOSet>(new Dictionary<string, ExtIOSet>());
            else DesiredSets = new ReadOnlyDictionary<string, ExtIOSet>(desiredSets);
        }

        public void Start()
        {
            Connection.ReceiveAsync();
            Connection.ExternalIOUpdate += Connection_ExternalIOUpdate;

            //Initiate the the load of the current ExtIO
            Dump();
        }
        public void Stop()
        {
            Connection.ExternalIOUpdate -= Connection_ExternalIOUpdate;
            Connection?.StopReceiveAsync();

            if (IsSynced)
            {
                IsSynced = false;
                Connection.Queue(false, new Action(() => InSync?.Invoke(this, false)));
            }
        }

        public bool ReadActiveSets() => Dump();
        public bool WriteAllInputs(List<byte> inputs)
        {
            bool res = false;
            if (!IsSynced) return false;

            //try
            //{
                //ReadOnlyDictionary<string, ExtIOSet> temp = ActiveSets;

                if (inputs.Count() < ActiveSets.Count())
                    return false;

                int i = 0;
                foreach (KeyValuePair<string, ExtIOSet> set in ActiveSets)
                {
                    set.Value.Inputs = new List<byte> { inputs[i++] };
                    set.Value.AddedForPendingUpdate = true;
                    res &= Connection.Write(set.Value.WriteInputCommand);
                }
            //}
            //finally
            //{
            //    ReleaseActiveSetLock();
            //}

            return res ^= true;
        }
        public bool ReadAllOutputs() => Dump();

        private bool Dump()
        {
            //lock (ActiveSetsLock)
                foreach (KeyValuePair<string, ExtIOSet> set in ActiveSets)
                    set.Value.AddedForPendingUpdate = true;

            return Connection.Write("extIODump");
        }
        private bool SyncDesiredSets()
        {
            if (DesiredSets.Count() == 0)
                return true;

            foreach (KeyValuePair<string, ExtIOSet> set in DesiredSets)
            {
                if (ActiveSets.ContainsKey(set.Key))
                {
                    if (InProcessSets.ContainsKey(set.Key))
                        InProcessSets.Remove(set.Key);
                    continue;
                }
                else
                {
                    if (!InProcessSets.ContainsKey(set.Key))
                        InProcessSets.Add(set.Key, set.Value);
                }
            }

            if (InProcessSets.Count() > 0)
                AddSets();
            else
                return true;

            return false;
        }
        private void AddSets()
        {
            foreach (KeyValuePair<string, ExtIOSet> set in InProcessSets)
            {
                if (set.Value.AddedForPendingUpdate) continue;
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

        private void Connection_ExternalIOUpdate(object sender, ExternalIOUpdateEventArgs data)
        {
            if (data.ExtIOSet == null) return;

            if (data.ExtIOSet.IsEnd)
            {
                if (SyncDesiredSets())
                {
                    if (!IsSynced)
                    {
                        IsSynced = true;
                        Connection.Queue(false, new Action(() => InSync?.Invoke(this, IsSynced)));
                    }
                }
                return;
            }

            if (data.ExtIOSet.IsDump)
            {
                IsIOUpdate = false;
                //lock (ActiveSetsLock)
                //{
                    if (ActiveSets.ContainsKey(data.ExtIOSet.Name))
                        ActiveSets[data.ExtIOSet.Name] = data.ExtIOSet;
                    else
                    {
                        while (!ActiveSets.TryAdd(data.ExtIOSet.Name, data.ExtIOSet)) { }
                        IsIOUpdate = true;
                    }

                    foreach (KeyValuePair<string, ExtIOSet> set in ActiveSets)
                        IsIOUpdate |= set.Value.AddedForPendingUpdate;
                //}

                if (IsSynced)
                    if (!IsIOUpdate)
                        Connection.Queue(false, new Action(() => InSync?.Invoke(this, true)));

                return;
            }

            if (data.ExtIOSet.HasInputs)
            {
                IsIOUpdate = false;
                //lock (ActiveSetsLock)
                //{
                    if (ActiveSets.ContainsKey(data.ExtIOSet.Name))
                    {
                        ActiveSets[data.ExtIOSet.Name].Inputs = data.ExtIOSet.Inputs;
                        ActiveSets[data.ExtIOSet.Name].AddedForPendingUpdate = false;
                    }
                    else
                        IsIOUpdate = true;

                    foreach (KeyValuePair<string, ExtIOSet> set in ActiveSets)
                        IsIOUpdate |= set.Value.AddedForPendingUpdate;
                //}

                if (!IsIOUpdate)
                    Connection.Queue(false, new Action(() => InSync?.Invoke(this, true)));

                return;
            }
        }
    }
}
