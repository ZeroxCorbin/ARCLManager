using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    /// <summary>
    /// This class is used to manage the configuration of a Mobile robot or Enterprise Manager.
    /// When you call Start() the Sections list will be loaded with all of the available configuration sections.
    /// Call WaitForSync() to wait for the configuration sections to be loaded.
    /// You must call ReadSectionValues(string sectionName) for each section to get it's values.
    /// You can serialize the Sections dictionary to and from a string to store the values.
    /// </summary>
    public class ConfigManager
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
        /// The state of the dictionary.
        /// State= WAIT; Wait to access the dictionary.
        ///              Calling Start() or Stop() sets this state.
        /// State= DELAYED; The dictionary Values are not valid.
        ///                 This indicates the Values of the dictionary are being updated
        ///                 or the Values from the ARCL Server are delayed.
        /// State= OK; The dictionary is up to date.
        /// </summary>
        public SyncStateEventArgs SyncState { get; private set; } = new SyncStateEventArgs();
        /// <summary>
        /// A reference to the connection to the ARCL Server.
        /// </summary>
        private ARCLConnection Connection { get; set; }
        /// <summary>
        /// Start the manager.
        /// This will load the dictionary.
        /// </summary>
        /// <returns>False: Connection issue.</returns>
        public bool Start()
        {
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
        /// <param name="connection">A connected ARCLConnection.</param>
        /// <returns>False: Connection issue.</returns>
        public bool Start(ARCLConnection connection)
        {
            Connection = connection;
            return Start();
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
                Connection?.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
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

            while(SyncState.State != SyncStates.OK & sw.ElapsedMilliseconds < timeout)
            { Thread.Sleep(10); }

            return SyncState.State == SyncStates.OK;
        }

        public ConfigManager() { }
        public ConfigManager(ARCLConnection connection) => Connection = connection
        private void Start_()
        {
            Connection.ConfigSectionUpdate += Connection_ConfigSectionUpdate;

            Sections.Clear();

            Connection.Write($"getconfigsectionlist");

            SyncState.State = SyncStates.WAIT;
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
                if(SyncState.State != SyncStates.OK)
                {
                    SyncState.State = SyncStates.OK;
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

        /// <summary>
        /// 
        /// </summary>
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
