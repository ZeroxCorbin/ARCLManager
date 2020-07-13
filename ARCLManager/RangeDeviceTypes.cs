using System;
using System.Collections.Generic;
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


    public class RangeDevice
    {
        public string Name { get; }
        public int LaserNumber { get; }
        public RangeDeviceUpdateEventArgs CurrentReadings { get; set; }
        public RangeDeviceUpdateEventArgs CumulativeReadings { get; set; }

        public RangeDevice(string name)
        {
            Name = name;

            int idx = Name.IndexOf('_') + 1;
            LaserNumber = int.Parse(Name.Substring(idx));
        }
    }

    public class RangeDeviceUpdateEventArgs : EventArgs
    {
        public bool IsCurrent { get; set; } = false;
        public string Name { get; set; } = string.Empty;
        public string Message { get; set; }
        public List<float[]> Data { get; set; } = new List<float[]>();

        public float Timestamp = 0;

        public RangeDeviceUpdateEventArgs(string msg, bool isReplay = false)
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
