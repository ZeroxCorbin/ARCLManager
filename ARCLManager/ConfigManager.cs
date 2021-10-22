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
    /// When you call Start(), the Sections dictionary will be loaded with all of the available configuration sections.
    /// Call WaitForSync() to wait for the Sections (dictionary) to be loaded.
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
        /// <returns>False: Connection issue.</returns>
        public bool Start()
        {
            if(Connection == null || !Connection.IsConnected)
                return false;

            Connection.ConfigSectionUpdate += Connection_ConfigSectionUpdate;

            if (!Connection.StartReceiveAsync())
                return false;

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
                SyncStateChange?.Invoke(this, SyncState);
            }
            Connection?.StopReceiveAsync();

            if(Connection != null)
                Connection.ConfigSectionUpdate -= Connection_ConfigSectionUpdate;
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

            while(SyncState.State != SyncStates.END && SyncState.State != SyncStates.ERROR && sw.ElapsedMilliseconds < timeout)
            { Thread.Sleep(10); }

            return SyncState.State == SyncStates.OK;
        }

        public ConfigManager() { }
        public ConfigManager(ARCLConnection connection) => Connection = connection;
        private void GetSections()
        {
            Sections.Clear();

            SyncState.State = SyncStates.WAIT;
            SyncState.Message = "GetConfigSectionList";
           SyncStateChange?.Invoke(this, SyncState);

            Connection.Write($"getconfigsectionlist");
        }

        private void Connection_ConfigSectionUpdate(object sender, ConfigSectionUpdateEventArgs data)
        {
            if(data.State == SyncStates.CHANGED)
            {
                if(SectionChangeExpected)
                {
                    SectionChangeExpected = false;
                    return;
                }

                if(SyncState.State != SyncStates.UPDATING)
                {
                    SyncState.State = SyncStates.UPDATING;
                    SyncState.Message = "Configuration Changed";
                    SyncStateChange?.Invoke(this, SyncState);
                }

                return;
            }

            if(data.State == SyncStates.END || data.State == SyncStates.ERROR)
            {
                if(data.State == SyncStates.ERROR)
                    while (!Sections.TryRemove(data.SectionName, out List<ConfigSection> config)) { Sections.Locked = false; }

                InProcessSectionName = null;

                SyncState.State = data.State;
                SyncState.Message = "EndGetConfigSection";
                SyncStateChange?.Invoke(this, SyncState);

                return;
            }

            if(data.State == SyncStates.SECTION_START)
            {
                if(Sections.ContainsKey(data.SectionName))
                    return;

                while(!Sections.TryAdd(data.SectionName, new List<ConfigSection>())) { Sections.Locked = false; }

                return;
            }
            if(InProcessSectionName == null)
                return;

            Sections[InProcessSectionName].Add(data.Section);
        }

        /// <summary>
        /// 
        /// </summary>
        public ReadOnlyConcurrentDictionary<string, List<ConfigSection>> Sections { get; set; } = new ReadOnlyConcurrentDictionary<string, List<ConfigSection>>(10, 100);
        private object __InProcessLock { get; } = new object();

        private string InProcessSectionName { get; set; } = null;
        private bool SectionChangeExpected { get; set; } = false;

        public bool ReadSectionValues(string sectionName)
        {
            lock(__InProcessLock)
            {
                InProcessSectionName = sectionName;

                SyncState.State = SyncStates.UPDATING;
                SyncState.Message = $"GetConfigSectionValues {sectionName}";
                SyncStateChange?.Invoke(this, SyncState);

                if(Sections.ContainsKey(sectionName))
                    Sections[sectionName].Clear();
                else
                    while(!Sections.TryAdd(sectionName, new List<ConfigSection>())) { Sections.Locked = false; }

                Connection.Write($"getconfigsectionvalues {sectionName}");

                while(InProcessSectionName != null)
                    Thread.Sleep(1);

                return Sections.ContainsKey(sectionName);
            }
        }
        public bool ReadAllSectionsValues()
        {
            bool @ret = false;
            foreach(var kv in Sections)
                if(!ReadSectionValues(kv.Key))
                {
                    @ret = true;
                    break;
                }
            return @ret;
        }

        public void WriteSectionValues(string sectionName)
        {
            if(!Sections.ContainsKey(sectionName)) return;

            WaitForSync();

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
            SectionChangeExpected = true;
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
