using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        public ConfigManager() { }
        public ConfigManager(ARCLConnection connection) => Connection = connection;
        public bool Start()
        {
            if (Connection == null) return false;

            if(!Connection.IsConnected) return false;

            if (Connection.StartReceiveAsync())
            {
                Connection.ConfigSectionUpdate += Connection_ConfigSectionUpdate;
                return true;
            }
            else
                return false;
           
        }
        public bool Start(ARCLConnection connection)
        {
            Connection = connection;
            return Start();
        }
        public void Stop()
        {
            if (Connection == null) return;

            Connection.ConfigSectionUpdate -= Connection_ConfigSectionUpdate;

            Connection.StopReceiveAsync();
        }

        private string InProcessSectionName { get; set; } = null;

        public string SectionValuesToText(string sectionName)
        {
            if (!Sections.ContainsKey(sectionName)) return string.Empty;

            StringBuilder sb = new StringBuilder();

            sb.Append($"Section::{sectionName}\r\n");
            foreach (ConfigSection cs in Sections[sectionName])
            {
                if (!string.IsNullOrEmpty(cs.Value))
                    sb.Append($"{cs.Name} {cs.Value}\r\n");
                else
                    sb.Append($"{cs.Name}\r\n");
            }

            return sb.ToString();
        }
        public bool TextToSectionValues(string sectionText)
        {
            string[] spl = sectionText.Split('\r', '\n');

            if (spl.Length < 2) return false;

            string section = "";
            foreach(string value in spl)
            {
                if (string.IsNullOrEmpty(value)) continue;

                if (value.StartsWith($"Section::"))
                {
                    section = value.Replace("Section::", "");

                    if (Sections.ContainsKey(section)) 
                        Sections[section].Clear();
                    else
                        Sections.Add(section, new List<ConfigSection>());

                    continue;
                }

                Sections[section].Add(new ConfigSectionUpdateEventArgs(value).Section);
            }

            return true;
        }

        public bool ReadSectionsList()
        {
            IsSynced = false;

            return Connection.Write($"getconfigsectionlist\r\n");
        }
        public bool ReadSectionValues(string sectionName)
        {
            IsSynced = false;

            if (Sections.ContainsKey(sectionName))
            {
                Sections[sectionName].Clear();
                Sections.Remove(sectionName);
            }
            Sections.Add(sectionName, new List<ConfigSection>());

            Stopwatch sw = new Stopwatch();

            while (InProcessSectionName != null && sw.ElapsedMilliseconds < 1000) { }
            if (InProcessSectionName != null)
                return false;
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
            if (data.IsEnd)
            {
                IsSynced = true;
                Connection.QueueTask(false, new Action(() => InSync?.Invoke(this, InProcessSectionName)));
                InProcessSectionName = null;
                return;
            }

            if (data.IsSectionName)
            {
                if (Sections.ContainsKey(data.SectionName)) return;

                Sections.Add(data.SectionName, new List<ConfigSection>());

                return;
            }
            if (InProcessSectionName == null) return;

            Sections[InProcessSectionName].Add(data.Section);
        }
    }
}
