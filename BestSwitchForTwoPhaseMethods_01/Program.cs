using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UtilsYN;


/* YN 2/4/22 - Completely rewriting this in a very early phase so there is a bit of code from an old idea which I will leave in
 * because it can be a little useful. 
 * 
 * In order to test two-phase methods there are MANY parameters. Where to switch (what we are seeking), what level of coverage 
 * we want, etc. But I think we can start with a somewhat more straightforward experiment.
 * 
 * 1) Partition the graph into hubs and leaves. Rank them in descending order of degree, and choose a partition. For this experiment
 *    I think we will fix this and not make it a variable at all, we'll just use the "80/20" rule and say that the top 20% are our
 *    hubs, the remaining 80% are our leaves.
 *    
 * 2) What amount of coverage do we want in each partition? To keep this simpler, we will assume the same amount in each, but we will
 *    consider different values for this amount. Start small with .75, .85, and .95, but if it runs reasonably fast enough we will
 *    of course want more values for this.
 *    
 * 3) We will NOT experiment with DIFFERENT switching points (this is the big breakthrough). We will simply run each phase until it
 *    has selected the desired number of vertices from its partition and report the costs. Admittedly figuring out how different 
 *    switching points do is an interesting experiment in its own right, but I think this should really be enough to have something
 *    meaningful to report.
 */

namespace BestSwitchForTwoPhaseMethods_01
{
    class Program
    {
        const int N = 7000; // Size of the graph. We can test with small values, but if the program runs fast enough we'll want data for a large N
        const int M = 5; // Same remark, may raise to 5 if N raises
        const int THREADS = 61;
        const int EXPERIMENTS = 400; // Again may want a large value here, we'll see.
        readonly decimal[] ALL_DESIRED_PCTS = new[] { .75m, .85m, .95m }; // think ideally .7, .75, .8, .85, .9, .95, 1
        const double PARTITION = .05; // the top 5% will be considered "hubs", we can make this adjustable if we want

        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static Graph[] graphs = new Graph[EXPERIMENTS];

        /// <summary>
        /// Data structure that tracks Sampling Costs, Cv, Cn.
        /// </summary>
        class SamplingCost
        {
            public int Cv { get; set; }
            public int Cn { get; set; }

            public SamplingCost(int Cv, int Cn)
            {
                this.Cv = Cv;
                this.Cn = Cn;
            }
        }

        // For readability (always love that readability) we will summarize the result of a single experiment in this struct
        class ExperimentResult
        {
            public int Phase1Cv { get; set; }
            public int Phase1Cn { get; set; }
            public int Phase1Cs { get; set; }
            public int Phase2Cv { get; set; }
            public int Phase2Cn { get; set; }
            public int Phase2Cs { get; set; }

        }

        // For readability, a sampling method that can be called to sample iteratively and give back a list of 
        // tuples where the first item is the vertex selected, the second is the cost. Often it will give back
        // just one item, but some methods take a vertex and a neighbor (or more) so give back as a list.
        delegate List<Tuple<Vertex, SamplingCost>> SampleForCost(Graph graph, Random rand);

        /* Doing a type of binary search, so this class is written to give all of the midpoints of the previous set of number
        * 0, .5, 1, then .25 and .75, then .125, .375, .625, .875, etc.
        * UPDATE EXPLANATION: When I thought we would try to use different switching points, I wanted some sort of way to
        * do a kind of "exhaustive binary search", so that instead of going in order from least to greatest we would always
        * be filling in midpoints instead. Every subsequent iteration gives more values, but not in the same range, rather
        * each one fills in a value in between two existing values. There are likely better ways to do this, and this one
        * repeats values and omits them which is wasteful, but it's a pretty effective hack so I'm leaving it in in case
        *there is some other use for it at some point.
        */
        class CoefficientGenerator
        {
            decimal bottom = 0.01m;
            decimal top = 2.0m;

            int currAmtToReturn = 2;
            HashSet<decimal> returnedCoefficients = new HashSet<decimal>();

            public List<decimal> getNextCoefficients()
            {
                decimal intervalSize = (top - bottom) / currAmtToReturn;
                List<decimal> list = new List<decimal>();
                for (decimal val = bottom; val <= top; val += intervalSize)
                {
                    if (returnedCoefficients.Add(val))
                        list.Add(val);
                }
                currAmtToReturn *= 2;
                return list;
            }
        }


        #region SAMPLING_METHODS
        static SampleForCost RV = (g, r) =>
        {
            var vertex = g.Vertices.ChooseRandomElement(r);
            return new List<Tuple<Vertex, SamplingCost>> { new Tuple<Vertex, SamplingCost>(vertex, new SamplingCost(1, 0)) };
        };

        static SampleForCost RN = (g, r) =>
        {
            var vertex = g.Vertices.ChooseRandomElement(r);
            var neighbor = vertex.Neighbors.ChooseRandomElement(r);
            return new List<Tuple<Vertex, SamplingCost>>
            {
                new Tuple<Vertex, SamplingCost>(neighbor, new SamplingCost(1, 1))
            };
        };

        static SampleForCost getRkN(int k)
        {
            return (g, r) =>
            {
                var vertex = g.Vertices.ChooseRandomElement(r);
                var neighbors = vertex.Neighbors.ChooseRandomSubset(k, random: r).ToArray();
                return new List<Tuple<Vertex, SamplingCost>> { new Tuple<Vertex, SamplingCost>(neighbors.First(), new SamplingCost(1, 1)) }.Union(
                    neighbors.Skip(1).Select(n => new Tuple<Vertex, SamplingCost>(n, new SamplingCost(0, 1)))).ToList();
            };
        }

        static SampleForCost RVN = (g, r) =>
        {
            var vertex = g.Vertices.ChooseRandomElement(r);
            var neighbor = vertex.Neighbors.ChooseRandomElement(r);
            return new List<Tuple<Vertex, SamplingCost>>
            {
                new Tuple<Vertex, SamplingCost>(vertex, new SamplingCost(1, 0)),
                new Tuple<Vertex, SamplingCost>(neighbor, new SamplingCost(0,1))
            };
        };

        static SampleForCost getRVkN(int k)
        {
            return (g, r) =>
            {
                var vertex = g.Vertices.ChooseRandomElement(r);
                var neighbors = vertex.Neighbors.ChooseRandomSubset(k, random: r);
                return new List<Tuple<Vertex, SamplingCost>> { new Tuple<Vertex, SamplingCost>(vertex, new SamplingCost(1, 0)) }.Union(
                    neighbors.Select(n => new Tuple<Vertex, SamplingCost>(n, new SamplingCost(0, 1)))).ToList();
            };
        }
        #endregion

        // Sample a graph in two phases, specify the initial one from the ones we are considering, and the second is RV. The method
        // will return the cost of getting the desired coverage in each phase. Keeping the method open to allow different desired 
        // coverage pct for each partition though in the first writing we will just use the same value for both.
        static ExperimentResult GetSamplingResult(Graph graph, SampleForCost phaseOneMethod, double partition, double hubsCoveragePct, double leafCoveragePct, Random rand)
        {
            ExperimentResult result = new ExperimentResult();
            HashSet<Vertex> hubs = new HashSet<Vertex>(), leaves = new HashSet<Vertex>();
            var vertices = graph.Vertices.OrderByDescending(v => v.Degree).ToList();
            // put each vertex in its correct collection
            for (int i = 0; i < vertices.Count; i++)
            {
                if (i < vertices.Count * partition)
                    hubs.Add(vertices[i]);
                else
                    leaves.Add(vertices[i]);
            }
            // Calculate how many vertices should remain in each partition when we conisder the job done.
            var remainingHubCount = hubs.Count - ((int)(hubs.Count * hubsCoveragePct));
            var remainingLeavesCount = leaves.Count - ((int)(leaves.Count * leafCoveragePct));

            while (hubs.Count > remainingHubCount)
            {
                var nextVerticesAndCosts = phaseOneMethod(graph, rand);
                foreach (var nextVertexCost in nextVerticesAndCosts)
                {
                    var vertex = nextVertexCost.Item1;
                    var cost = nextVertexCost.Item2;
                    result.Phase1Cv += cost.Cv;
                    result.Phase1Cn += cost.Cn;

                    if (hubs.Remove(vertex) || leaves.Remove(vertex))
                        result.Phase1Cs++;
                }
            }

            while (leaves.Count > remainingLeavesCount)
            {
                var nextVerticesAndCosts = RV(graph, rand);
                foreach (var nextVertexCost in nextVerticesAndCosts)
                {
                    var vertex = nextVertexCost.Item1;
                    var cost = nextVertexCost.Item2;
                    result.Phase2Cv += cost.Cv;
                    result.Phase2Cn += cost.Cn;

                    if (leaves.Remove(vertex))
                        result.Phase2Cs++;
                }
            }

            return result;
        }


        /*Old code, probably take out..
         * Dictionary<String, Dictionary<string, double>> GetSamplingResult2(Graph graph, SampleForCost initialMethod, decimal switchingCoefficient, double pctGoal, Random rand)
        {
            HashSet<Vertex> selectedVertices = new HashSet<Vertex>();
            int vertexCountGoal = (int)(N * pctGoal);
            int phaseOneSelections = (int)(N * switchingCoefficient * (decimal)Math.Log(N));
            bool inPhaseOne = true;
            int Cv = 0, Cn = 0, Cs = 0;
            while (selectedVertices.Count < vertexCountGoal)
            {
                var currSamplingMethod = inPhaseOne ? initialMethod : RV;

                var nextVerticesAndCosts = currSamplingMethod(graph, rand);
                foreach(var nextVertexAndCosts in nextVerticesAndCosts)
                {
                    var vertex = nextVertexAndCosts.Item1;
                    Cv += nextVertexAndCosts.Item2.Cv;
                    Cn += nextVertexAndCosts.Item2.Cn;
                    if (selectedVertices.Add(vertex))
                    {
                        Cs++;

                    }
                }

                if (selectedVertices.Count >= phaseOneSelections)
                    inPhaseOne = false;
            }

        } */


        static void Main(string[] args)
        {
            Console.WriteLine($"Starting program {DTS}");



            StringBuilder CSV = new StringBuilder();
            CSV.AppendLine("Method,PctCoverage,P1Cv,P1Cn,P1Cs,P2Cv,P2Cn,P2Cs");

            //while (!Range(EXPERIMENTS).All(i => File.Exists($"c:\\Graphs-7000-5\\graphs{i.ToString("00#")}.txt")))
            //    Thread.Sleep(TimeSpan.FromMinutes(2));


            if (!File.Exists("c:\\Graphs\\graphs000.txt"))
            {
                graphs = Range(0, EXPERIMENTS).AsParallel().Select(i => Graph.NewBaGraph(N, M, random: rands[i])).ToArray();
                for (int i = 0; i < graphs.Length; i++)
                {
                    File.WriteAllLines($"C:\\Graphs\\graphs{i.ToString("00#")}.txt", graphs[i].Edges.Select(e => e.v1.Id + "\t" + e.v2.Id));
                }
            }
            else
            {
                graphs = new Graph[EXPERIMENTS];
                Parallel.For(0, graphs.Length, i =>
                    {
                        graphs[i] = new Graph();
                        File.ReadAllLines($"C:\\Graphs\\graphs{i.ToString("00#")}.txt").ToList().ForEach(l => graphs[i].AddEdge(Regex.Split(l, @"\s+")[0], Regex.Split(l, @"\s+")[1]));
                    });
            }

            var MU = (int)(graphs[0].Vertices.Average(v => v.Degree));
            Dictionary<string, SampleForCost> methods = new Dictionary<string, SampleForCost>()
            {
                { "RV", RV },
                { "RN", RN },
                { "RVN", RVN },
                { "RkN k=2", getRkN(2) },
                { "RVkN k=2", getRVkN(2) },
                { "RkN k=3", getRkN(3) },
                { "RVkN k=3", getRVkN(3) },
                { "RkN k=4", getRkN(4) },
                { "RVkN k=4", getRVkN(4) },
                { "RkN k=5", getRkN(5) },
                { "RVkN k=5", getRVkN(5) },
                { "RkN k=6", getRkN(6) },
                { "RVkN k=6", getRVkN(6) },
                { "RkN k=7", getRkN(7) },
                { "RVkN k=7", getRVkN(7) },
                { "RkN k=8", getRkN(8) },
                { "RVkN k=8", getRVkN(8) },
                { "RkN k=9", getRkN(9) },
                { "RVkN k=9", getRVkN(9) },
                { "RkN k=10", getRkN(10) },
                { "RVkN k=10", getRVkN(10) },
                { "RkN k=11", getRkN(11) },
                { "RVkN k=11", getRVkN(11) },
                { $"RkN k=INF", getRkN(int.MaxValue) },
                { $"RVkN k=INF", getRVkN(int.MaxValue) },
            };

            foreach (var partition in new[] { .05, .1, .15, .2, .25, .025, .075, .0125 })
            {
                String fileName = $"G4000-3-results_{partition.ToString(".#00").Replace(".", "")}pctHubs.csv";
                /*if (File.Exists(fileName))
                    File.Delete(fileName);*/

                File.WriteAllText(fileName, "Method,PctCoverage,P1Cv,P1Cn,P1Cs,P2Cv,P2Cn,P2Cs\n");

                var fileLines = File.ReadAllLines(fileName);

                foreach (var pct in new[] { .7, .8, .9, .95, .85, .75, .6, 1.0 })
                {
                    foreach (var kvp in methods)
                    {
                        if (fileLines.Any(l => l.StartsWith($"{kvp.Key},{pct},")))
                            continue;
                        Console.WriteLine($"Starting {kvp.Key}, pct:  {pct} {DTS}");
                        ExperimentResult[] allResults = new ExperimentResult[EXPERIMENTS];
                        Parallel.For(0, EXPERIMENTS, new ParallelOptions() { MaxDegreeOfParallelism = THREADS },
                            i => allResults[i] = GetSamplingResult(graphs[i], kvp.Value, partition, pct, pct, rands[i])
                         );
                        var phase1Cv = allResults.Average(r => r.Phase1Cv);
                        var phase1Cn = allResults.Average(r => r.Phase1Cn);
                        var phase1Cs = allResults.Average(r => r.Phase1Cs);
                        var phase2Cv = allResults.Average(r => r.Phase2Cv);
                        var phase2Cn = allResults.Average(r => r.Phase2Cn);
                        var phase2Cs = allResults.Average(r => r.Phase2Cs);
                        CSV.AppendLine($"{kvp.Key},{pct},{phase1Cv},{phase1Cn},{phase1Cs},{phase2Cv},{phase2Cn},{phase2Cs}");
                        File.AppendAllText(fileName, $"{kvp.Key},{pct},{phase1Cv},{phase1Cn},{phase1Cs},{phase2Cv},{phase2Cn},{phase2Cs}\n");
                    }
                }
            }
            Console.WriteLine("Program finished at {DTS}");




        }
    }
}
