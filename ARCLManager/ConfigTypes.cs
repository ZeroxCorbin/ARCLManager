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
        public bool IsEnd { get; private set; }
        public bool IsSectionName { get; private set; }
        public string SectionName { get; private set; }
        public ConfigSectionUpdateEventArgs(string msg)
        {
            Message = msg;

            if (msg.StartsWith("endof", StringComparison.CurrentCultureIgnoreCase))
            {
                IsEnd = true;
                return;
            }

            if (msg.StartsWith("GetConfigSectionList", StringComparison.CurrentCultureIgnoreCase))
            {
                SectionName = msg.Replace("GetConfigSectionList: ", "").TrimStart();
                IsSectionName = true;
                return;
            }

            msg = msg.Replace("GetConfigSectionValue: ", "").TrimStart();

            string[] spl = msg.Split(' ');

            if(msg.Contains("_beginList"))
                Section = new ConfigSection(spl[0], spl[1], true);
            else if (msg.Contains("_endList"))
                Section = new ConfigSection(spl[0], spl[1], false, true);
            else
            {
                if(spl.Length == 1)
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

    public class RobotShapeData
    {
        public float Radius { get; set; }
        public float Width { get; set; }
        public float LengthFront { get; set; }
        public float LengthRear { get; set; }
        public int MaxNumberOfLasers { get; set; }
        public float HeightToCenter { get; set; } = 203.272f;

        public RobotShapeData() { }

        public RobotShapeData(List<ConfigSection> sections)
        {
            foreach (ConfigSection cs in sections)
            {
                if (cs == null)
                    continue;
                if (cs.Name.Contains("Radius")) Radius = float.Parse(cs.Value);
                if (cs.Name.Contains("Width")) Width = float.Parse(cs.Value);
                if (cs.Name.Contains("LengthFront")) LengthFront = float.Parse(cs.Value);
                if (cs.Name.Contains("LengthRear")) LengthRear = float.Parse(cs.Value);
                if (cs.Name.Contains("MaxNumberOfLasers")) MaxNumberOfLasers = int.Parse(cs.Value);
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
