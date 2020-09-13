using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
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

        public Dictionary<string, List<ConfigSection>> Sections { get; set; } = new Dictionary<string, List<ConfigSection>>();
        private object SectionsLockObject { get; set; } = new object();

        private string InProcessSectionName { get; set; } = null;

        public ConfigManager(ARCLConnection connection) => Connection = connection;

        public void Start()
        {
            if (!Connection.IsReceivingAsync)
                Connection.StartReceiveAsync();

            Connection.ConfigSectionUpdate += Connection_ConfigSectionUpdate;
        }
        public void Stop()
        {
            Connection.ConfigSectionUpdate -= Connection_ConfigSectionUpdate;
            Connection?.StopReceiveAsync();
        }

        public string SectionAsText(string sectionName)
        {
            if (!Sections.ContainsKey(sectionName)) return string.Empty;

            StringBuilder sb = new StringBuilder();

            sb.Append($"{sectionName}\r\n");
            foreach (ConfigSection cs in Sections[sectionName])
            {
                if (!string.IsNullOrEmpty(cs.Value))
                    sb.Append($"{cs.Name} {cs.Value}\r\n");
                else
                    sb.Append($"{cs.Name}\r\n");
            }

            return sb.ToString();
        }

        public bool TextAsSection(string sectionText)
        {
            string[] spl = sectionText.Split('\r', '\n');

            if (spl.Length < 2) return false;

            if (Sections.ContainsKey(spl[0]))
                Sections[spl[0]].Clear();
            else
                Sections.Add(spl[0], new List<ConfigSection>());

            for(int i = 1; i < spl.Length; i++)
            {
                if (string.IsNullOrEmpty(spl[i])) continue;
                Sections[spl[0]].Add(new ConfigSectionUpdateEventArgs(spl[i]).Section);
            }
                

            return true;
        }

        public bool ReadConfigSection(string sectionName)
        {
            IsSynced = false;

            if (Sections.ContainsKey(sectionName))
                Sections[sectionName].Clear();
            else
                Sections.Add(sectionName, new List<ConfigSection>());

            InProcessSectionName = sectionName;

            return Connection.Write($"getconfigsectionvalues {sectionName}\r\n");
        }

        public void WriteConfigSection(string sectionName)
        {
            if (!Sections.ContainsKey(sectionName)) return;

            Connection.Write($"configStart\r\n");
            Connection.Write($"configAdd Section {sectionName}\r\n");

            foreach (ConfigSection cs in Sections[sectionName])
            {
                if (!string.IsNullOrEmpty(cs.Value))
                {
                    if (cs.IsBeginList || cs.IsEndList)
                        Connection.Write($"configAdd {cs.Value} {cs.Name}\r\n");
                    else
                        Connection.Write($"configAdd {cs.Name} {cs.Value}\r\n");
                }
                else
                {
                    Connection.Write($"configAdd {cs.Name}\r\n");
                }
            }
            Connection.Write($"configParse\r\n");
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
                Sections[InProcessSectionName].Add(data.Section);

        }
    }
}
