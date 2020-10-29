using System;
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
                            Console.WriteLine($"V: {s}");

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
    }
}
