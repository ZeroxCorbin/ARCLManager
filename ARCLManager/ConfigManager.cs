using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ARCLTypes;

namespace ARCL
{
    public class ConfigManager
    {
        //Public
        /// <summary>
        /// Raised when the config Section is sycronized with the EM.
        /// </summary>
        public delegate void InSyncEventHandler(object sender, string sectionName);
        public event InSyncEventHandler InSync;
        /// <summary>
        /// True when the config Section is sycronized with the EM.
        /// </summary>
        public bool IsSynced { get; private set; } = false;

        private ARCLConnection Connection { get; set; }

        private Dictionary<string, List<ConfigSection>> _Sections { get; set; } = new Dictionary<string, List<ConfigSection>>();
        public ReadOnlyDictionary<string, List<ConfigSection>> Sections { get { lock (SectionsLockObject) return new ReadOnlyDictionary<string, List<ConfigSection>>(_Sections); } }
        private object SectionsLockObject { get; set; } = new object();

        private string InProcessSectionName { get; set; } = null;

        public ConfigManager(ARCLConnection connection) => Connection = connection;

        public void Start()
        {
            if (!Connection.IsReceivingAsync)
                Connection.ReceiveAsync();

            Connection.ConfigSectionUpdate += Connection_ConfigSectionUpdate;
        }
        public void Stop()
        {
            Connection.ConfigSectionUpdate -= Connection_ConfigSectionUpdate;
            Connection?.StopReceiveAsync();
        }

        public bool GetConfigSection(string sectionName)
        {
            IsSynced = false;

            if (_Sections.ContainsKey(sectionName))
                _Sections[sectionName].Clear();
            else
                _Sections.Add(sectionName, new List<ConfigSection>());

            InProcessSectionName = sectionName;

            return Connection.Write($"getconfigsectionvalues {sectionName}\r\n");
        }

        private void Connection_ConfigSectionUpdate(object sender, ConfigSectionUpdateEventArgs data)
        {
            if (InProcessSectionName == null) return;

            if (data.IsEnd)
            {
                IsSynced = true;
                Connection.QueueTask(false, new Action(() => InSync?.Invoke(this, InProcessSectionName)));
                InProcessSectionName = null;
                return;
            }

            lock (SectionsLockObject)
                _Sections[InProcessSectionName].Add(data.Section);

        }
    }
}
