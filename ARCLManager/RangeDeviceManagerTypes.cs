using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARCLTypes
{
    //public interface IRangeDevice
    //{
    //    string Name { get; }
    //    int LaserNumber { get; }
    //    RangeDeviceUpdateEventArgs CurrentReadings { get; set; }
    //    RangeDeviceUpdateEventArgs CumulativeReadings { get; set; }
    //}
    public enum RangeDeviceLocationType
    {
        NONE,
        LOCATION_DEPENDENT,
        LASER
    }
    public enum RangeDeviceDrawType
    {
        polyArrows,
        polyDots
    }
    public class RangeDeviceDrawingData
    {
        public string Name { get; }
        public RangeDeviceDrawType DrawType { get; }
        public Color Color1 { get; }
        public Color Color2 { get; }

        public int Value1 { get; }
        public int Value2 { get; }
        public bool DefaultState { get; }

        //RangeDeviceCurrentDrawingData: Laser_1 polyDots 0x0000ff 0x000000 80 75 DefaultOn
        public RangeDeviceDrawingData(string message)
        {
            string[] spl = message.Trim('\r', '\n').Split(' ');

            if(spl.Count() == 8)
            {

            }

        }
    }

    public class RangeDevice
    {
        public string Name { get; }
        public int LaserNumber { get; }

        public RangeDeviceLocationType LocationType { get; }

        public RangeDeviceDrawingData CurrentReadingsDrawingData { get; }
        public RangeDeviceDrawingData CumulativeReadingsDrawingData { get; }

        
        public bool CurrentReadingsInSync { get; set; }
        public RangeDeviceReadingUpdateEventArgs CurrentReadings { get; set; }
        public bool CumulativeReadingsInSync { get; set; }
        public RangeDeviceReadingUpdateEventArgs CumulativeReadings { get; set; }

        public RangeDevice(string message)
        {
            if (message.StartsWith("RangeDevice:"))
            {
                string[] spl = message.Trim('\r', '\n').Split(' ');

                Name = spl[1];
                if(Enum.TryParse(spl[2], out RangeDeviceLocationType result))
                    LocationType = result;

                if(LocationType == RangeDeviceLocationType.LASER)
                    LaserNumber = int.Parse(Name.Replace("Laser_", ""));

                return;
            }

            if (message.StartsWith("RangeDeviceCurrentDrawingData:"))
            {
                CurrentReadingsDrawingData = new RangeDeviceDrawingData(message);
                return;
            }

            if (message.StartsWith("RangeDeviceCumulativeDrawingData:"))
            {
                CumulativeReadingsDrawingData = new RangeDeviceDrawingData(message);
                return;
            }
        }
    }

    public class RangeDeviceEventArgs : EventArgs
    {
        public string Message { get; }
        public bool IsEnd { get; } = false;
        public bool IsNew { get; } = false;
        public bool IsReplay { get; }
        public RangeDevice RangeDevice { get; }


        public RangeDeviceEventArgs(string message, bool isReplay = false)
        {
            Message = message;
            IsReplay = isReplay;

            if(message.StartsWith("EndOfRangeDeviceList", StringComparison.CurrentCultureIgnoreCase))
            {
                IsEnd = true;
                return;
            }

            if (message.StartsWith("RangeDevice:"))
                IsNew = true;

            RangeDevice = new RangeDevice(message);
        }
    }

    public class RangeDeviceReadingUpdateEventArgs : EventArgs
    {
        public bool IsCurrent { get; set; } = false;
        public string Name { get; set; } = string.Empty;
        public string Message { get; set; }
        public List<float[]> Data { get; set; } = new List<float[]>();

        public float Timestamp = 0;

        public RangeDeviceReadingUpdateEventArgs(string msg, bool isReplay = false)
        {
            Message = msg;
            string[] rawData;

            if (isReplay)
            {
                string[] spl = msg.Split(',');
                rawData = spl[2].Split();
                Name = spl[1];

                IsCurrent = true;

                if (float.TryParse(spl[0], out float res))
                    Timestamp = res;
            }
            else
            {
                rawData = msg.Split();
                Name = rawData[1];
            }

            IsCurrent = rawData[0].Contains("RangeDeviceGetCurrent");

            int i = 3;
            for (; i < rawData.Length - 3; i += 3)
            {
                float[] fl = new float[2];
                fl[0] = float.Parse(rawData[i]);
                fl[1] = float.Parse(rawData[i + 1]);
                Data.Add(fl);
            }
        }
    }
}
