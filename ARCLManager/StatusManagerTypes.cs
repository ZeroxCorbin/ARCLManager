using System;
using System.Collections.Generic;
using System.Text;

namespace ARCLTypes
{
    public class StatusUpdateEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DockingState { get; set; } = string.Empty;
        public string ForcedState { get; set; } = string.Empty;
        public float ChargeState { get; set; }
        public float StateOfCharge { get; set; }
        public string Location => $"{X},{Y},{Heading}";
        public float X { get; set; }
        public float Y { get; set; }
        public float Heading { get; set; }
        public float Temperature { get; set; }

        public float Timestamp { get; set; }

        public StatusUpdateEventArgs(string msg, bool isReplay = false)
        {
            //if(isReplay)
            //{
            //    //0.361,encoderTransform,82849.2 -33808.8 140.02
            //    Message = msg;
            //    string[] spl = msg.Split(',');
            //    string[] loc = spl[2].Split();

            //    X = float.Parse(loc[0]);
            //    Y = float.Parse(loc[1]);
            //    Heading = float.Parse(loc[2]);

            //    if(float.TryParse(spl[0], out float res))
            //    {
            //        Timestamp = res;
            //    }
            //    else
            //    {
            //        if(!spl[0].Equals("starting"))
            //        {

            //        }
            //    }

            //}
            //else
            //{
            Message = msg;
            string[] spl = msg.Split();
            int i = 0;
            float val;
            if(spl.Length < 10)
                return;

            while(true)
            {
                switch(spl[i])
                {
                    case "Status:":
                        while(true)
                        {
                            if(spl[i + 1].Contains(":") & !spl[i + 1].Contains("Error")) break;
                            Status += spl[++i] + ' ';
                        }
                        break;

                    case "DockingState:":
                        if(!spl[i + 1].Contains(":"))
                            DockingState = spl[++i];
                        break;

                    case "ForcedState:":
                        if(!spl[i + 1].Contains(":"))
                            ForcedState = spl[++i];
                        break;

                    case "ChargeState:":
                        if(!spl[i + 1].Contains(":"))
                            if(float.TryParse(spl[++i], out val))
                                ChargeState = val;
                        break;

                    case "StateOfCharge:":
                        if(!spl[i + 1].Contains(":"))
                            if(float.TryParse(spl[++i], out val))
                                StateOfCharge = val;
                        break;

                    case "Location:":
                        if(!spl[i + 1].Contains(":"))
                        {
                            X = float.Parse(spl[++i]);
                            Y = float.Parse(spl[++i]);
                            Heading = float.Parse(spl[++i]);
                        }

                        break;
                    case "Temperature:":
                        if(!spl[i + 1].Contains(":"))
                            if(float.TryParse(spl[++i], out val))
                                Temperature = val;
                        break;

                    default:
                        break;
                }

                i++;
                if(spl.Length == i) break;
            }


        }
    }
}
