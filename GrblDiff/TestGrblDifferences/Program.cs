using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestGrblDifferences
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: TestGrblDifferences [com1] [baud] [com7] [baud]");
                Console.WriteLine(" - The first COM port is the port that should be correct. Dumped as 'C: '.");
                Console.WriteLine(" - The second COM port is the port that is validated. Dumped as 'V: '.");
            }
            else
            {
                Console.WriteLine("Press 'q' to quit.");
                SerialPort port1 = new SerialPort(args[0], int.Parse(args[1]));
                SerialPort port2 = new SerialPort(args[2], int.Parse(args[3]));

                port1.Open();
                port2.Open();
                try
                {
                    Thread t1 = new Thread(() =>
                    {
                        while (true)
                        {
                            string s = port1.ReadLine();
                            Console.WriteLine($"C: {s}");
                        }
                    })
                    { IsBackground = true, Name = "Original" };

                    Thread t2 = new Thread(() =>
                    {
                        while (true)
                        {
                            string s = port2.ReadLine();
                            Console.WriteLine($"V: {s}");
                        }
                    })
                    { IsBackground = true, Name = "Validate" };

                    t1.Start();
                    t2.Start();

                    while (true)
                    {
                        string input = Console.ReadLine();
                        if (input == "q" || input == "Q")
                        {
                            port1.Close();
                            port2.Close();
                            return;
                        }

                        port1.WriteLine(input);
                        port2.WriteLine(input);
                    }
                }
                finally
                {
                    port1.Close();
                    port2.Close();
                }
            }
        }
    }
}
