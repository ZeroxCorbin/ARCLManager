using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ARCLTypes
{
    public class ExtIOSet
    {
        public string Name { get; private set; }
        public List<byte> Inputs { get; set; } = new List<byte>();
        public List<byte> Outputs { get; set; } = new List<byte>();

        public int InputCount => Inputs.Count() * 8;
        public int OutputCount => Outputs.Count() * 8;
        public bool HasInputs => Inputs.Count() > 0;
        public bool HasOutputs => Outputs.Count() > 0;
        public bool IsDump => Inputs.Count() > 0 & Outputs.Count() > 0;
        public bool IsEnd { get; private set; }
        public bool IsRemove => Inputs.Count() == 0 & Outputs.Count() == 0;

        public bool AddedForPendingUpdate { get; set; }

        public ExtIOSet(string name, List<byte> inputs, List<byte> outputs)
        {
            if (inputs == null) inputs = new List<byte>();
            if (outputs == null) outputs = new List<byte>();

            Name = name;
            Inputs.AddRange(inputs);
            Outputs.AddRange(outputs);
        }
        public ExtIOSet(bool isEnd = false) => IsEnd = isEnd;

        //<name> <valueInHexOrDec>
        public string WriteInputCommand
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("extIOInputUpdate ");
                sb.Append(Name);
                sb.Append(" 0x");

                for (int i = Inputs.Count() - 1; i >= 0; i--)
                    sb.Append(Inputs[i].ToString("X"));

                return sb.ToString();
            }

        }
        public string WriteOutputCommand => $"extIOOutputUpdate {Name} {Inputs[0]:X}";
        public string CreateSetCommand => $"extIOAdd {Name} {InputCount} {OutputCount}";
    }
    public class ExternalIOUpdateEventArgs: EventArgs
    {
        public string Message { get; }
        public ExtIOSet ExtIOSet { get; }

        public ExternalIOUpdateEventArgs(string msg)
        {
            Message = msg;

            string[] spl = msg.Split(' ');

            //ExtIODump: Test with 4 input(s), value = 0x0 and 4 output(s), value = 0x00
            if (spl[0].StartsWith("extiodump", StringComparison.CurrentCultureIgnoreCase))
            {
                if (!spl[4].Contains("input")) throw new ExtIOUpdateParseException();
                if (!spl[10].Contains("output")) throw new ExtIOUpdateParseException();

                if (!int.TryParse(spl[3], out int num_in)) throw new ExtIOUpdateParseException();
                if (!int.TryParse(spl[9], out int num_ot)) throw new ExtIOUpdateParseException();

                List<byte> input = new List<byte>();
                string txt = CleanHexString(spl[7]);
                for (int i = 0; i < num_in / 8; i++)
                {
                    if (txt.Length < 2)
                        txt = txt.PadLeft(2, '0');
                    else
                        txt = txt.Substring(i, 2);
                    input.Add(byte.Parse(txt, System.Globalization.NumberStyles.HexNumber));

                }


                List<byte> output = new List<byte>();
                txt = CleanHexString(spl[7]);
                for (int i = 0; i < num_ot / 8; i++)
                {
                    if (txt.Length < 2)
                        txt = txt.PadLeft(2, '0');
                    else
                        txt = txt.Substring(i, 2);
                    output.Add(byte.Parse(txt, System.Globalization.NumberStyles.HexNumber));
                }

                this.ExtIOSet = new ExtIOSet(spl[1].Trim(), input, output);

                return;
            }

            //extIOInputUpdate: input <name> updated with <IO value in Hex> from <valueInDecOrHex> (asentered in ARCL)
            if (spl[0].StartsWith("extIOInputUpdate", StringComparison.CurrentCultureIgnoreCase))
            {
                if (!spl[1].Contains("input")) throw new ExtIOUpdateParseException();

                int cnt = (spl[7].Count() - 2) / 2;

                List<byte> input = new List<byte>();
                string txt = CleanHexString(spl[5]);
                for (int i = 0; i < cnt; i++)
                    input.Add(byte.Parse(txt.Substring(i, 2), System.Globalization.NumberStyles.HexNumber));

                this.ExtIOSet = new ExtIOSet(spl[2].Trim(), input, null);

                return;
            }

            if (spl[0].StartsWith("extIOOutputUpdate", StringComparison.CurrentCultureIgnoreCase))
            {
                if (!spl[1].Contains("output")) throw new ExtIOUpdateParseException();

                if (!ulong.TryParse(CleanHexString(spl[5]), System.Globalization.NumberStyles.HexNumber, System.Globalization.NumberFormatInfo.InvariantInfo, out ulong val_out)) throw new ExtIOUpdateParseException();

                byte[] val8_out = BitConverter.GetBytes(val_out);

                List<byte> output = new List<byte>();

                foreach (byte b in val8_out)
                    output.Add(b);

                this.ExtIOSet = new ExtIOSet(spl[2].Trim(), null, output);

                return;
            }

            //extIORemove: <name> removed
            if (spl[0].StartsWith("extIORemove", StringComparison.CurrentCultureIgnoreCase))
            {
                if (!spl[2].Contains("removed")) throw new ExtIOUpdateParseException();

                this.ExtIOSet = new ExtIOSet(spl[1].Trim(), null, null);

                return;
            }

            //EndExtIODump
            if (spl[0].StartsWith("EndExtIODump", StringComparison.CurrentCultureIgnoreCase))
            {
                this.ExtIOSet = new ExtIOSet(true);

                return;
            }
        }

        private string CleanHexString(string str)
        {
            int pos = str.IndexOf('x');
            if (pos == -1) return str;

            return str.Remove(0, pos + 1);
        }

    }

}
