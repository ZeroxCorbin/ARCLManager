using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class ConfigManager
    {
        public delegate void SyncStateChangeEventHandler(object sender, SyncStateEventArgs syncState);
        public event SyncStateChangeEventHandler SyncStateChange;

        public SyncStateEventArgs SyncState { get; private set; } = new SyncStateEventArgs();

        private ARCLConnection Connection { get; set; }
        public ConfigManager() { }
        public ConfigManager(ARCLConnection connection) => Connection = connection;

        public bool Start()
        {
            if(Connection == null || !Connection.IsConnected)
                return false;
            if(!Connection.StartReceiveAsync())
                return false;

            Start_();

            return true;
        }
        public bool Start(ARCLConnection connection)
        {
            Connection = connection;
            return Start();
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
            Connection.ConfigSectionUpdate += Connection_ConfigSectionUpdate;

            Sections.Clear();

            Connection.Write($"getconfigsectionlist");

            SyncState.State = SyncStates.FALSE;
            SyncState.Message = "GetConfigSectionList";
            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
        }
        private void Stop_()
        {
            if(Connection != null)
                Connection.ConfigSectionUpdate -= Connection_ConfigSectionUpdate;
        }

        private void Connection_ConfigSectionUpdate(object sender, ConfigSectionUpdateEventArgs data)
        {
            if(data.IsEnd)
            {
                if(SyncState.State != SyncStates.TRUE)
                {
                    SyncState.State = SyncStates.TRUE;
                    SyncState.Message = "EndGetConfigSectionList";
                    Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                }

                InProcessSectionName = null;
                return;
            }

            if(data.IsSectionName)
            {
                if(Sections.ContainsKey(data.SectionName))
                    return;

                while(!Sections.TryAdd(data.SectionName, new List<ConfigSection>())) { Sections.Locked = false; }

                return;
            }
            if(InProcessSectionName == null) return;

            Sections[InProcessSectionName].Add(data.Section);
        }

        public ReadOnlyConcurrentDictionary<string, List<ConfigSection>> Sections { get; set; } = new ReadOnlyConcurrentDictionary<string, List<ConfigSection>>(10,100);
 
        private string InProcessSectionName { get; set; } = null;
        public bool ReadSectionValues(string sectionName)
        {
            SyncState.State = SyncStates.DELAYED;
            SyncState.Message = $"GetConfigSectionValues {sectionName}";
            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));

            if(Sections.ContainsKey(sectionName))
            {
                Sections[sectionName].Clear();
            }
            else
            {
                 while(!Sections.TryAdd(sectionName, new List<ConfigSection>())) { Sections.Locked = false; }
            }


            Stopwatch sw = new Stopwatch();

            while(InProcessSectionName != null && sw.ElapsedMilliseconds < 1000) { }
            if(InProcessSectionName != null)
                return false;
            InProcessSectionName = sectionName;

            return Connection.Write($"getconfigsectionvalues {sectionName}");
        }
        public void WriteSectionValues(string sectionName)
        {
            if(!Sections.ContainsKey(sectionName)) return;

            Connection.Write($"configStart");
            Connection.Write($"configAdd Section {sectionName}");

            foreach(ConfigSection cs in Sections[sectionName])
            {
                if(!string.IsNullOrEmpty(cs.Value))
                {
                    if(cs.IsBeginList || cs.IsEndList)
                        Connection.Write($"configAdd {cs.Value} {cs.Name}");
                    else
                        Connection.Write($"configAdd {cs.Name} {cs.Value}");
                }
                else
                {
                    Connection.Write($"configAdd {cs.Name}");
                }
            }
            Connection.Write($"configParse");
        }

        public string SectionValuesToText(string sectionName)
        {
            if(!Sections.ContainsKey(sectionName)) return string.Empty;

            StringBuilder sb = new StringBuilder();

            sb.Append($"Section::{sectionName}\r\n");
            foreach(ConfigSection cs in Sections[sectionName])
            {
                if(!string.IsNullOrEmpty(cs.Value))
                    sb.Append($"{cs.Name} {cs.Value}\r\n");
                else
                    sb.Append($"{cs.Name}\r\n");
            }

            return sb.ToString();
        }
        public bool TextToSectionValues(string sectionText)
        {
            string[] spl = sectionText.Split('\r', '\n');

            if(spl.Length < 2) return false;

            string section = "";
            foreach(string value in spl)
            {
                if(string.IsNullOrEmpty(value)) continue;

                if(value.StartsWith($"Section::"))
                {
                    section = value.Replace("Section::", "");

                    if(Sections.ContainsKey(section))
                        Sections[section].Clear();
                    else
                        while(!Sections.TryAdd(section, new List<ConfigSection>())) { Sections.Locked = false; }

                    continue;
                }

                Sections[section].Add(new ConfigSectionUpdateEventArgs(value).Section);
            }

            return true;
        }
    }
}
