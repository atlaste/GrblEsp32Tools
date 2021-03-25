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
    class Program
    {
        static void Main(string[] args)
        {
            string stacktrace = null;// @"0x400dee4d:0x3ffbe800 0x400dea4c:0x3ffbe8f0 0x400d53ac:0x3ffbe920 0x40081f17:0x3ffbe940 0x400821f9:0x3ffbe960 0x40085c29:0x3ffbe980 0x40090ba2:0x3ffbcba0 0x40090bab:0x3ffbcbc0 0x4000be96:0x3ffbcbe0 0x4000bec2:0x3ffbcc00 0x40203525:0x3ffbcc20 0x402038ad:0x3ffbcc40 0x400ed17d:0x3ffbcc60 0x400f4d69:0x3ffbcca0 0x400e1c31:0x3ffbccd0 0x400ead62:0x3ffbcd10 0x400eaa24:0x3ffbcd30 0x400da234:0x3ffbcd50 0x4008fd35:0x3ffbcd70";

            if (args.Length != 2)
            {
                Console.WriteLine("Usage: Test [com port, e.g. COM7] \"[firmware file.elf]\"");
            }

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

            var firmware = args[1];
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

            /*
            // Run job:
            var lines = File.ReadAllLines(@"C:\Tmp\esp32fail\xTestBlockM4fast.nc").ToList();
            SerialPort port = new SerialPort(args[0], 115200);
            port.Open();

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
                            int len = s.Length + 2;
                            if (len + total < 255)
                            {
                                ++n;
                                port.WriteLine(s);
                                emitted = true;

                                total += len;
                                todo.Enqueue(len);
                            }
                            else
                            {
                                Thread.Sleep(1);
                            }
                            if (port.BytesToRead != 0)
                            {
                                string response = port.ReadLine().Trim();
                                // Console.WriteLine(response);

                                var processed = todo.Dequeue();
                                total -= processed;

                                // if (response != "ok")
                                // {
                                Console.WriteLine("> {0}", response);
                                // }
                                // else
                                // {
                                //     Console.Write('.');
                                // }

                                if ((n % 100) == 0)
                                {
                                    port.WriteLine("?\r\n");
                                    string response2 = port.ReadLine().Trim();
                                    Console.WriteLine(response2);
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("Done.");
            */

            bool closed = false;

            SerialPort port = new SerialPort(args[0], 115200);
            port.Open();
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
