using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace AffinityWatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            int i;
            int nodes = 0;
            int cpuPerNode = 0;
            int coresPerNode = 0;
            bool HT = false;
            int HTmask = 3;
            long[] affs;
            long affMask;

            if (args.Length == 0 || args.Length == 1 || args.Length > 3)
            {
                Console.WriteLine("Usage: AffinityWatcher Nodes Cores HT");
                return;
            }
            else
            {
                nodes = int.Parse(args[0]);
                coresPerNode = int.Parse(args[1]);
                cpuPerNode = coresPerNode;
                if (args.Length == 3 && (args[2] == "1" || args[2] == "2"))
                {
                    HTmask = int.Parse(args[2]);
                    HT = true;
                    coresPerNode >>= 1;
                }
            }

            /*foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                nodes++;
                cpuPerNode = int.Parse(item["NumberOfLogicalProcessors"].ToString());
                coresPerNode = int.Parse(item["NumberOfCores"].ToString());
                if (coresPerNode != cpuPerNode)
                    HT = true;
            }*/

            Console.WriteLine("Nodes: {0}, Cores: {1}{2}", nodes, coresPerNode, HT ? ", HT" : "");

            affs = new long[nodes];
            affs[0] = HT ? 3 : 1;
            for (i = 1; i < coresPerNode; i++)
            {
                affs[0] <<= HT ? 2 : 1;
                affs[0] += HT ? HTmask : 1;
            }
            for (i = 1; i < nodes; i++)
                affs[i] = affs[i - 1] << cpuPerNode;
            affMask = (1 << cpuPerNode) - 1;

            int fails = 0;
            while (true)
            {
                try
                {
                    List<int> freeNodes = new List<int>();
                    List<Process> unbound = new List<Process>();

                    for (i = 0; i < nodes; i++)
                        freeNodes.Add(i);

                    foreach (Process proc in Process.GetProcesses())
                        if (proc.ProcessName.StartsWith("primegrid_cllr") || proc.ProcessName.StartsWith("cllr") || proc.ProcessName.StartsWith("llr2"))
                        {
                            long a = (long)proc.ProcessorAffinity;
                            int node = -1;
                            for (i = 0; i < nodes; i++, a >>= cpuPerNode)
                                if ((a & affMask) != 0)
                                {
                                    if (node >= 0 || freeNodes.IndexOf(i) < 0)
                                    {
                                        node = -1;
                                        unbound.Add(proc);
                                        break;
                                    }
                                    node = i;
                                }
                            if (node >= 0)
                                freeNodes.Remove(node);
                        }
                    foreach (Process proc in unbound)
                    {
                        if (freeNodes.Count == 0)
                            break;
                        int node = freeNodes[0];
                        freeNodes.RemoveAt(0);
                        Console.WriteLine("Binding process {0} to node {1}.", proc.Id, node);
                        proc.ProcessorAffinity = (IntPtr)affs[node];
                    }

                    fails = 0;
                }
                catch (Exception e)
                {
                    fails++;
                    if (fails >= 3)
                    {
                        Console.WriteLine("{0} consecutive fails. The last one is:", fails);
                        Console.WriteLine(e);
                        fails = 0;
                    }
                }

                Thread.Sleep(10000);
            }
        }
    }
}
