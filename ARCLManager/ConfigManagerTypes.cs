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
        public static Dictionary<string, float> Variants = new Dictionary<string, float>()
        {
            { "HD-1500" , 201.0f },
            { "LD-60" , 201.0f },
            { "LD-90" , 201.0f },
            { "LD-250-os32c" , 201.0f }
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

                if (!string.IsNullOrEmpty(Model))
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
                            //if (cs.Value.Equals("LD-60") || cs.Value.Equals("LD-90"))
                            //    Variant = "LD-60-90";
                            //else if (cs.Value.StartsWith("LD-250"))
                            //    Variant = "LD-250";
                            //else
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
        public float MaxSpeed { get; set; }
        public float MaxRotSpeed { get; set; }
        public float SlowSpeed { get; set; }
        public float FastSpeed { get; set; }

        public float FrontClearance { get; set; }
        public float FrontPaddingAtSlowSpeed { get; set; }
        public float FrontPaddingAtFastSpeed { get; set; }

        public float SideClearanceAtSlowSpeed { get; set; }
        public float SideClearanceAtFastSpeed { get; set; }

        public float PlanFreeSpace { get; set; }

        public float GoalDistanceTolerance { get; set; }
        public float GoalAngleTolerance { get; set; }

        public PathPlanningSettings() { }
        public PathPlanningSettings(List<ConfigSection> sections)
        {
            foreach (ConfigSection cs in sections)
            {
                if (cs.Name.Equals("MaxSpeed")) MaxSpeed = float.Parse(cs.Value);
                if (cs.Name.Equals("MaxRotSpeed")) MaxSpeed = float.Parse(cs.Value);

                if (cs.Name.Equals("SlowSpeed")) SlowSpeed = float.Parse(cs.Value);
                if (cs.Name.Equals("FastSpeed")) FastSpeed = float.Parse(cs.Value);

                if (cs.Name.Equals("FrontClearance")) FrontClearance = float.Parse(cs.Value);

                if (cs.Name.Equals("FrontPaddingAtSlowSpeed")) FrontPaddingAtSlowSpeed = float.Parse(cs.Value);
                if (cs.Name.Equals("FrontPaddingAtFastSpeed")) FrontPaddingAtFastSpeed = float.Parse(cs.Value);

                if (cs.Name.Equals("SideClearanceAtSlowSpeed")) SideClearanceAtSlowSpeed = float.Parse(cs.Value);
                if (cs.Name.Equals("SideClearanceAtFastSpeed")) SideClearanceAtFastSpeed = float.Parse(cs.Value);

                if (cs.Name.Equals("PlanFreeSpace")) PlanFreeSpace = float.Parse(cs.Value);

                if (cs.Name.Equals("GoalDistanceTol")) GoalDistanceTolerance = float.Parse(cs.Value);
                if (cs.Name.Equals("GoalAngleTol")) GoalAngleTolerance = float.Parse(cs.Value);
            }
        }
    }
}
