using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;

namespace PowerLawExponentAndSampling_01
{
    /*
     * YN 3/8/22
     * This experiment, which may hopefully be the last and perhaps won't even be included... will look for the connection
     * between the power-law exponent and our four ex/inc methods.
     * 
     * It actually should be a very straightforward experiment. For various values of Alpha, we will generate some number of 
     * BA graphs (and, at least to start with, we can use very simple params because we are only comparing it to an existing 
     * experiment that used one set of n,m params) and take the average Assort, AFI, GFI, RN, RE, IRN, and IRE values over
     * all graphs. 
     */

    class Program
    {
        const int N = 2000; // hard to believe the last experiment on this was 2000, 15.. whatever
        const int M = 15; 
        const int THREADS = 54;
        const int EXPERIMENTS = 600; 


        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static Graph[] graphs = new Graph[EXPERIMENTS];
        static decimal[] ALPHAS = Enumerable.Range(0, 500).Select(i => i * .05m).ToArray();
       


        static void Main(string[] args)
        {
            var OUTPUT_FILE = "Results.tsv";
            File.WriteAllText(OUTPUT_FILE, "Alpha\tAssort\tAFI\tGFI\tRN\tRE\tIRN\tIRE\n");
            foreach (var currAlpha in ALPHAS)
            {
                Console.WriteLine($"Starting alpha {currAlpha}, {DTS}");
                Parallel.For(0, EXPERIMENTS, new ParallelOptions() { MaxDegreeOfParallelism = THREADS }, i => graphs[i] = Graph.NewBaGraph(N, M, (double)currAlpha, rands[i]));
                Console.WriteLine($"Graphs generated {DTS}");
                var assort = graphs.Average(g => g.Assortativity());
                var AFI = graphs.Average(g => g.Vertices.Average(v => (decimal)v.Neighbors.Average(n => n.Degree) / v.Degree));
                var GFI = graphs.Average(g => g.Vertices.Average(v => (decimal)Math.Log((double)v.Neighbors.Average(n => n.Degree) / v.Degree)));
                var RN = graphs.Average(g => g.RN);
                var RE = graphs.Average(g => g.RE);
                var IRN = graphs.Average(g => g.IRN);
                var IRE = graphs.Average(g => g.IRE);
                File.AppendAllText(OUTPUT_FILE, $"{currAlpha}\t{assort}\t{AFI}\t{GFI}\t{RN}\t{RE}\t{IRN}\t{IRE}\n");
            }
            Console.WriteLine($"DONE {DTS}");
            Console.ReadKey();
        }
    }
}
