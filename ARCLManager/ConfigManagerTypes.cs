using System;
using System.Collections.Generic;

namespace ARCLTypes
{
    public class ConfigSection
    {
        public string Name { get; private set; }
        public string Value { get; set; } = string.Empty;
        public bool IsBeginList { get; private set; } = false;
        public bool IsEndList { get; private set; } = false;

        public ConfigSection(string name, string value)
        {
            Name = name;
            Value = value;
        }
        public ConfigSection(string name, string value, bool isBeginList = false, bool isEndList = false)
        {
            Name = name;
            Value = value;

            IsBeginList = isBeginList;
            IsEndList = isEndList;
        }
    }

    public class ConfigSectionUpdateEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public ConfigSection Section { get; private set; }
        public SyncStates State { get; set; }
        public bool IsSectionName { get; private set; }
        public string SectionName { get; private set; }
        public ConfigSectionUpdateEventArgs(string msg)
        {
            Message = msg;
            if (msg.StartsWith("Configuration changed", StringComparison.CurrentCultureIgnoreCase))
            {
                State = SyncStates.CHANGED;
                return;
            }

            if (msg.StartsWith("endof", StringComparison.CurrentCultureIgnoreCase))
            {
                State = SyncStates.END;
                return;
            }

            if (msg.StartsWith("CommandError", StringComparison.CurrentCultureIgnoreCase))
            {
                SectionName = msg.Replace("CommandError: getconfigsectionvalues ", "");
                State = SyncStates.ERROR;
                return;
            }

            if (msg.StartsWith("GetConfigSectionList", StringComparison.CurrentCultureIgnoreCase))
            {
                State = SyncStates.SECTION_START;

                SectionName = msg.Replace("GetConfigSectionList: ", "").TrimStart();
                return;
            }

            msg = msg.Replace("GetConfigSectionValue: ", "").TrimStart();

            string[] spl = msg.Split(' ');

            if (msg.Contains("_beginList"))
            {
                Section = new ConfigSection(spl[0], spl[1], true);
                State = SyncStates.LIST_START;
            }
            else if (msg.Contains("_endList"))
            {
                Section = new ConfigSection(spl[0], spl[1], false, true);
                State = SyncStates.LIST_END;
            }
            else
            {
                State = SyncStates.VALUE;
                if (spl.Length == 1)
                    Section = new ConfigSection(spl[0], "");
                else
                    Section = new ConfigSection(spl[0], spl[1]);
            }

            
        }
        //public void Update(string msg)
        //{
        //    if (msg.StartsWith("endof", StringComparison.CurrentCultureIgnoreCase))
        //    {
        //        IsEnd = true;
        //        return;
        //    }

        //    string[] spl = msg.Split(' ');

        //    string other = "";
        //    for (int i = 3; i < spl.Length; i++)
        //        other += " " + spl[i];

        //    Sections.Add(new ConfigSection(spl[1], spl[2], other));
        //}
    }

    public class RobotGeneral
    {
        public float Radius { get; set; }
        public float Width { get; set; }
        public float LengthFront { get; set; }
        public float LengthRear { get; set; }
        public int MaxNumberOfLasers { get; set; }
        public string DockType { get; set; }


        public RobotGeneral() { }

        public RobotGeneral(List<ConfigSection> sections)
        {
            foreach (ConfigSection cs in sections)
            {
                if (cs == null)
                    continue;
                if (cs.Name.StartsWith("Radius")) Radius = float.Parse(cs.Value);
                if (cs.Name.StartsWith("Width")) Width = float.Parse(cs.Value);
                if (cs.Name.StartsWith("LengthFront")) LengthFront = float.Parse(cs.Value);
                if (cs.Name.StartsWith("LengthRear")) LengthRear = float.Parse(cs.Value);
                if (cs.Name.StartsWith("MaxNumberOfLasers")) MaxNumberOfLasers = int.Parse(cs.Value);
                if (cs.Name.StartsWith("DockType")) DockType = cs.Value;
            }
        }
    }

    public class RobotType
    {
        public Dictionary<string, float> Variants = new Dictionary<string, float>()
        {
            { "HD-1500" , 80.0f },
            { "LD-60" , 203.272f },
            { "LD-90" , 203.272f }
        };

        public string Model { get; set; }
        public string Variant { get; set; }
        public float HeightToCenter { get => Variants[Variant]; }

        public RobotType() { }

        public RobotType(List<ConfigSection> sections)
        {
            bool foundVariant = false;
            foreach (ConfigSection cs in sections)
            {
                if (cs == null)
                    continue;
                
                if (cs.Name.Contains("Model")) Model = cs.Value;

                if(!string.IsNullOrEmpty(Model))
                {
                    if (cs.IsBeginList)
                        if (cs.Name.Equals(Model))
                        {
                            foundVariant = true;
                            continue;
                        }


                    if (foundVariant)
                        if (cs.Name.Contains("Variant"))
                        {
                            Variant = cs.Value;
                            return;
                        }
                }
            }
        }
    }

    public class LaserSettings
    {
        public bool LaserAutoConnect { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Theta { get; set; }
        public LaserSettings() { }

        public LaserSettings(List<ConfigSection> sections)
        {
            foreach (ConfigSection cs in sections)
            {
                if (cs == null)
                    continue;
                if (cs.Name.StartsWith("LaserAutoConnect")) LaserAutoConnect = bool.Parse(cs.Value);
                if (cs.Name.StartsWith("LaserX")) X = float.Parse(cs.Value);
                if (cs.Name.StartsWith("LaserY")) Y = float.Parse(cs.Value);
                if (cs.Name.StartsWith("LaserZ")) Z = float.Parse(cs.Value);
                if (cs.Name.StartsWith("LaserTh")) Theta = float.Parse(cs.Value);
            }
        }
    }
    public class PathPlanningSettings
    {
        public float FrontClearance { get; set; }
        public float SlowSpeed { get; set; }
        public float SideClearanceAtSlowSpeed { get; set; }
        public float FrontPaddingAtSlowSpeed { get; set; }
        public float FrontPaddingAtFastSpeed { get; set; }
        public float FastSpeed { get; set; }
        public float SideClearanceAtFastSpeed { get; set; }

        public float PlanFreeSpace { get; set; }

        public float GoalDistanceTolerance { get; set; }
        public float GoalAngleTolerance { get; set; }

        public PathPlanningSettings() { }
        public PathPlanningSettings(List<ConfigSection> sections)
        {
            foreach (ConfigSection cs in sections)
            {
                if (cs.Name.StartsWith("FrontClearance")) FrontClearance = float.Parse(cs.Value);
                if (cs.Name.StartsWith("SlowSpeed")) SlowSpeed = float.Parse(cs.Value);
                if (cs.Name.StartsWith("SideClearanceAtSlowSpeed")) SideClearanceAtSlowSpeed = float.Parse(cs.Value);
                if (cs.Name.StartsWith("FrontPaddingAtSlowSpeed")) FrontPaddingAtSlowSpeed = float.Parse(cs.Value);
                if (cs.Name.StartsWith("FastSpeed")) FastSpeed = float.Parse(cs.Value);
                if (cs.Name.StartsWith("SideClearanceAtFastSpeed")) SideClearanceAtFastSpeed = float.Parse(cs.Value);
                if (cs.Name.StartsWith("FrontPaddingAtFastSpeed")) FrontPaddingAtFastSpeed = float.Parse(cs.Value);
                if (cs.Name.StartsWith("PlanFreeSpace")) PlanFreeSpace = float.Parse(cs.Value);

                if (cs.Name.StartsWith("GoalDistanceTol")) GoalDistanceTolerance = float.Parse(cs.Value);
                if (cs.Name.StartsWith("GoalAngleTol")) GoalAngleTolerance = float.Parse(cs.Value);
            }
        }
    }
}
