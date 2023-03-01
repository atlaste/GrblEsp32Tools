using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace TestGrblDifferences
{
    public class Reader
    {
        private SerialPort port;

        private DateTime startTime = DateTime.MinValue;
        private byte[] buffer1 = new byte[1024];
        private int idx1 = 0;

        public Reader(SerialPort port)
        {
            this.port = port;
        }

        public void Iterate()
        {
            int count = port.BytesToRead;
            if (count > 0)
            {
                if (idx1 == 0)
                {
                    startTime = DateTime.Now;
                }

                var v = Math.Min(1024 - idx1, count);
                int n = port.Read(buffer1, idx1, v);
                idx1 += n;

                for (int i = 0; i < idx1; ++i)
                {
                    if (buffer1[i] == '\n' || buffer1[i] == '\r')
                    {
                        if (i > 2)
                        {
                            Console.WriteLine("{0}: {1}", startTime, Encoding.ASCII.GetString(buffer1, 0, i));
                        }
                        Buffer.BlockCopy(buffer1, i, buffer1, 0, idx1 - i);
                        i = -1;
                        idx1 -= i;
                    }
                }

                if (idx1 == 1024)
                {
                    Console.WriteLine("??: {0}", Encoding.ASCII.GetString(buffer1, 0, 1024));
                    idx1 = 0;
                }
            }
        }
    }



    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(string.Join(", ", SerialPort.GetPortNames()));
            Console.WriteLine("Mito port: ");
            var mituPort = Console.ReadLine().Trim();

            Console.WriteLine(string.Join(", ", SerialPort.GetPortNames()));
            Console.WriteLine("Inclino port: ");
            var inclinoPort = Console.ReadLine().Trim();

            SerialPort mituSerial = new SerialPort(mituPort, 115200, Parity.None, 8, StopBits.One) { DtrEnable = true };
            while (!mituSerial.IsOpen)
            {
                try
                {
                    mituSerial.Open();
                    break;
                }
                catch { }
            }
            Console.WriteLine("Port of mito is open");

            SerialPort inclinoSerial = new SerialPort(inclinoPort, 115200, Parity.None, 8, StopBits.One) { DtrEnable = true };
            while (!inclinoSerial.IsOpen)
            {
                try
                {
                    inclinoSerial.Open();
                    break;
                }
                catch { }
            }
            Console.WriteLine("Port of inclino is open");

            var runner1 = new Reader(mituSerial);
            var runner2 = new Reader(inclinoSerial);

            while (true)
            {
                runner1.Iterate();
                runner2.Iterate();

                Thread.Sleep(1);
            }
        }



        static void GrblThread(string comport, string firmware)
        {

            string stacktrace = null;// @"0x40088e69:0x3ffd31b0 0x00060f2d:0x3ffd3280 0x4008f9bf:0x3ffd32a0 0x400900c6:0x3ffd32c0 0x40083c04:0x3ffd32e0 0x40083c6d:0x3ffd3300 0x400888dd:0x3ffd3320 0x4000bedd:0x3ffd3340 0x4010b153:0x3ffd3360 0x4010b26c:0x3ffd3390 0x4010b6c7:0x3ffd33b0 0x4010b7ed:0x3ffd33d0 0x400efa80:0x3ffd3400 0x401b5761:0x3ffd3420 0x401b6c51:0x3ffd3440 0x400daed4:0x3ffd3460 0x401b6c35:0x3ffd3480 0x40108812:0x3ffd34a0 0x4010abb2:0x3ffd34c0 0x4010abf9:0x3ffd3510 0x4010ac09:0x3ffd3530 0x400d6b8e:0x3ffd3550 0x400d6f5b:0x3ffd3590 0x400d7031:0x3ffd35e0 0x4008c872:0x3ffd3600";

            // if (args.Length != 2)
            // {
            //     Console.WriteLine("Usage: Test [com port, e.g. COM7] \"[firmware file.elf]\"");
            // }

            var environmentVariables = Environment.GetEnvironmentVariables();
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var gdbPath = Path.Combine(path, @"arduino15\packages\esp32\tools\xtensa-esp32-elf-gcc\");
            //1.22.0-80-g6c4433a-5.2.0\bin\xtensa-esp32-elf-gdb.exe");
            if (Directory.Exists(gdbPath))
            {
                gdbPath = Directory.GetFiles(gdbPath, "xtensa-esp32-elf-gdb.exe", SearchOption.AllDirectories).FirstOrDefault();
            }

            if (gdbPath == null || !File.Exists(gdbPath))
            {
                Console.WriteLine("Cannot find gdb.");
                return;
            }

            if (!File.Exists(firmware))
            {
                Console.WriteLine("Cannot find firmware file.");
                return;
            }


            Console.WriteLine("Press 'q' to quit.");
            Console.WriteLine(string.Join(", ", SerialPort.GetPortNames()));

            if (stacktrace != null)
            {
                ParseStackTrace(stacktrace, gdbPath, firmware);
            }

            SerialPort port = new SerialPort(comport, 115200, Parity.None, 8, StopBits.One) { DtrEnable = true };
            // SerialPort port = new SerialPort("com3", 115200);
            while (!port.IsOpen)
            {
                try
                {
                    port.Open();
                    break;
                }
                catch { }
            }
            Console.WriteLine("Port is open");

            // Thread.Sleep(TimeSpan.FromSeconds(10));
            // port.WriteLine("$10=2");

            /*
            // Run job:
            var lines = File.ReadAllLines(@"C:\Users\atlas\Downloads\test.nc").ToList();

            Queue<int> todo = new Queue<int>();
            int total = 0;

            int n = 0;
            foreach (var line in lines)
            {
                List<string> ll = new List<string>();
                int last = 0;
                for (int i = 0; i < line.Length; ++i)
                {
                    if (line[i] == 'G' || line[i] == 'g')
                    {
                        string q = line.Substring(last, i - last);
                        if (!string.IsNullOrEmpty(q)) { ll.Add(q); }
                        last = i;
                    }
                    else if (line[i] == ';')
                    {
                        string q = line.Substring(last, i - last);
                        if (!string.IsNullOrEmpty(q)) { ll.Add(q); }
                        last = line.Length;
                        break;
                    }
                }
                {
                    string q = line.Substring(last);
                    if (!string.IsNullOrEmpty(q)) { ll.Add(q); }
                }


                foreach (var s in ll)
                {
                    if (!s.StartsWith(";") && !string.IsNullOrWhiteSpace(s))
                    {
                        bool emitted = false;

                        while (!emitted && port.IsOpen)
                        {
                            int len = s.Length + 2; // \r\n = 2
                            if (total + len < 150)
                            {
                                Console.WriteLine();
                                Console.WriteLine(s);
                                ++n;
                                port.WriteLine(s);
                                emitted = true;

                                total += len;
                                todo.Enqueue(len);
                            }
                            else
                            {
                                Thread.Sleep(TimeSpan.FromSeconds(0.1));
                                port.Write("?");
                                port.BaseStream.Flush();
                            }

                            if (port.BytesToRead != 0)
                            {
                                string response = port.ReadLine().Trim();

                                Console.Write("> {0}\r", response);
                                if (!response.StartsWith("<"))
                                {
                                    var processed = todo.Dequeue();
                                    total -= processed;
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("Done.");
            */
            bool closed = false;

            try
            {
                Thread t2 = new Thread(() =>
                {
                    Thread.MemoryBarrier();
                    while (!closed)
                    {
                        try
                        {
                            string s = port.ReadLine();
                            Console.WriteLine($"> {s}");

                            ParseStackTrace(s, gdbPath, firmware);
                        }
                        catch { }

                        Thread.MemoryBarrier();
                    }
                })
                { IsBackground = true, Name = "Validate" };

                t2.Start();

                while (true)
                {
                    string input = Console.ReadLine();
                    if (input == "q" || input == "Q")
                    {
                        //port1.Close();
                        port.Close();
                        return;
                    }

                    port.WriteLine(input);
                }
            }
            finally
            {
                port.Close();
                closed = true;
                Thread.MemoryBarrier();
            }
        }

        private static void ParseStackTrace(string s, string gdbPath, string firmware)
        {
            var matches = Regex.Matches(s,
                "0x[A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9]:0x[A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9][A-Fa-f0-9]");
            if (matches.Count != 0)
            {
                foreach (var match in matches)
                {
                    var addr = match.ToString().Split(':');
                    if (addr.Length == 2)
                    {
                        Console.Write(addr[0]);

                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = gdbPath;
                        StringBuilder argBuilder = new StringBuilder();
                        argBuilder.Append("--batch \"");
                        argBuilder.Append(firmware);
                        argBuilder.Append("\" -ex \"set listsize 1\" -ex \"l *");
                        argBuilder.Append(addr[0]);
                        argBuilder.Append("\" -ex \"q\"");
                        psi.Arguments = argBuilder.ToString();
                        psi.UseShellExecute = false;
                        psi.RedirectStandardOutput = true;

                        using (var process = new Process())
                        {
                            process.StartInfo = psi;
                            process.Start();
                            var line = process.StandardOutput.ReadToEnd();
                            Console.WriteLine("> " + line.Trim());
                            process.WaitForExit();
                        }
                    }
                }
            }
        }
    }
}
