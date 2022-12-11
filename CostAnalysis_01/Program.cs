using GraphLibYN_2019;
using PyReporting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UtilsYN;

namespace CostAnalysis_01
{
    class Program
    {
        /* YN 7/30/21 - Would like to do something of a "full" cost analysis here. A lot will be hardcoded..
         * 
         * Will start with just one type of BA graph (5000, 8) and use a whole bunch of methods to sample unique edges
         * and track the Cv, Cn, and Cs to get x pct of the top edges for different values of x. This will generate a 
         * table as output but also will hopefully use the rows in these tables to generate heatmaps for different values
         * of Cv and Cn.
         * 
         * Methods will be:
         * 
         * 1. RV
         * 2. RN
         * 3. RN-RV (switching after ~410 per our experimental results)
         * 4. RVN
         * 5. RVN-RV (same switching point)
         * 6. kRN k=2
         * 7. kRN k=3
         * 8. kRN k=mu
         * 9. kRN k=inf
         */

        const bool debug = false;
        const int EXPERIMENTS = debug ? 1 : 1000;
        const int GRAPHS = debug ? 1 : 60;
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        const string OUTPUT_FILE = "BA_1000_8_Results.tsv";
        static Graph[] graphs;

        // Hardcoding these as global values. If we come back to make this program more versatile, history has shown this can get messy...
        const int N = 1000;
        const int M = 8;
        const int BA_SWITCH_POINT = 420; // From our experiments, should be a good time to switch to RV (a little conservative, highest was 412)

        static double[] PCTS = Enumerable.Range(1, 5000).Select(i => 1.0 / 5000.0 * i).ToArray();

        struct Cost
        {
            public double Cv { get; set; }
            public double Cn { get; set; }
            public double Cs { get; set; }
            public Cost(double Cv, double Cn, double Cs)
            {
                this.Cv = Cv;
                this.Cn = Cn;
                this.Cs = Cs;
            }

            public static Cost NewCost()
            {
                return new Cost(0, 0, 0);
            }

            public static Cost NewCost(double Cv, double Cn, double Cs)
            {
                return new Cost(Cv, Cn, Cs);
            }

            public static Cost operator+(Cost c1, Cost c2)
            {
                return new Cost(c1.Cv + c2.Cv, c1.Cn + c2.Cn, c1.Cs + c2.Cs);
            }

            public static Cost operator+(Cost c1, (double Cv, double Cn, double Cs) c2)
            {
                return new Cost(c1.Cv + c2.Cv, c1.Cn + c2.Cn, c1.Cs + c2.Cs);
            }

            public static Cost operator +(Cost c1, (int Cv, int Cn, int Cs) c2)
            {
                return new Cost(c1.Cv + c2.Cv, c1.Cn + c2.Cn, c1.Cs + c2.Cs);
            }

            public void AddCosts(double Cv = 0, double Cn = 0, double Cs = 0)
            {
                this.Cv += Cv;
                this.Cn += Cn;
                this.Cs += Cs;
            }

        }

        public enum SamplingMethod
        {
            RV,
            RN,
            RN_RV,
            RVN,
            RVN_RV,
            RkN
        }

        static void Main(string[] args)
        {
            if (!File.Exists(OUTPUT_FILE))
                File.WriteAllText(OUTPUT_FILE, "Method\tPct\tCv\tCn\tCs\n");


            Console.WriteLine($"Starting individual experiments {DTS}");
            
           // RunExperiments();
            /*PlotRkNVals();*/
            PlotMethodVals();
            


            //CvByCnHeatMap();

            Console.WriteLine($"Done {DTS}");
            Console.ReadKey();
        }

        // This one will include only RkN with k = infinity, PlotRkNVals() below will do the head-to-head comparison for the 
        // different values of k in RkN sampling.

        // When plotting, we'll loop through 4 possibilities, and use the method below
        // to know the correct items of the tuple to use, and the correct description
        enum PlotExpenseType
        {
            Cv,
            Cn,
            Alpha,
            Cs
        }

        static double tCost((double, double, double) tup, PlotExpenseType plotExpenseType)
        {
            switch (plotExpenseType)
            {
                case PlotExpenseType.Cv: return tup.Item1;
                case PlotExpenseType.Cn: return tup.Item2;
                case PlotExpenseType.Alpha: return tup.Item1 + tup.Item2;
                case PlotExpenseType.Cs: return tup.Item3;
                default: throw new Exception($"Unknown PlotExpenseType {plotExpenseType}");
            }
        }    
        
        static void PlotMethodVals()
        {
            // choose from  { "RV", "RN", "RVN", "RN_RV", "RVN_RV", "RkN(k=2)", "RkN(k=3)", "RkN(k=16)", "RkN(k=5000)" }
            var includedMethods = new[] { "RV", "RN", /*"RVN",*/ /*"RN_RV", "RVN_RV", "RkN(k=5000)"*/ };

            var data = File.ReadAllLines(OUTPUT_FILE).Where(l => includedMethods.Any(m => Regex.Split(l, @"\s+")[0] == m)).Select(l => Regex.Split(l, @"\s+"))
                .GroupBy(arr => arr[0].Replace("_", "-").Replace("5000", "Inf")) // Now have groups of arrays, key is the method type
                .ToDictionary(
                    g => g.Key, 
                    g => g.ToDictionary(arr => double.Parse(arr[1]), arr => (double.Parse(arr[2]), double.Parse(arr[3]), double.Parse(arr[4])) // method points to dict<pct, tuple of costs>
                )
            );

            var xVals = data.Values.First().Keys.OrderBy(d => d).ToArray();

            foreach(PlotExpenseType expenseType in Enum.GetValues(typeof(PlotExpenseType)))
            {

                Py.CreatePyPlot(
                    Py.PlotType.plot,
                    xVals,
                    data.Keys.Select(mthd => data[mthd].Keys.OrderBy(pct => pct).Select(pct => data[mthd][pct]).Select(t => tCost(t, expenseType)).ToArray()).ToArray(),
                    data.Keys.ToArray(),
                    title: expenseType + " Costs for Sampling Top x Pct of High-Degree Vertices",
                    xlabel: "Top x Pct of High-Degree Vertices",
                    ylabel: expenseType + " Costs",
                    fileName: expenseType + "CostToGetTopXPctOfHdVertices.png"
                );
            }
        }

        static void PlotRkNVals()
        {
            var data = File.ReadAllLines(OUTPUT_FILE).Where(l => l.StartsWith("RkN") || l.StartsWith("RN")).Select(l => Regex.Split(l, @"\s+"))
                .GroupBy(arr => arr[0].Replace("_", "-").Replace("5000", "Infinity")) // Now have groups of arrays, key is the method type
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(arr => double.Parse(arr[1]), arr => (double.Parse(arr[2]), double.Parse(arr[3]), double.Parse(arr[4])) // method points to dict<pct, tuple of costs>
                )
            );


            var xVals = data.Values.First().Keys.OrderBy(d => d).ToArray();

            //var xVals = data.Select(d => double.Parse(d.pct)).Distinct().OrderBy(d => d).ToArray();
            var k1Vals = data["RN"].OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray();
            var k2Vals = data["RkN(k=2)"].OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray();
            var k3Vals = data["RkN(k=3)"].OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray();
            var kMuVals = data["RkN(k=16)"].OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray();
            var kInfVals = data["RkN(k=Infinity)"].OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray();


            foreach (PlotExpenseType expenseType in Enum.GetValues(typeof(PlotExpenseType)))
            {
                Py.CreatePyPlot(
                    Py.PlotType.plot,
                    xVals,
                    new[]
                    {
                        k1Vals.Select(t => tCost(t, expenseType)).ToArray(),
                        k2Vals.Select(t => tCost(t, expenseType)).ToArray(),
                        k3Vals.Select(t => tCost(t, expenseType)).ToArray(),
                        kMuVals.Select(t => tCost(t, expenseType)).ToArray(),
                        kInfVals.Select(t => tCost(t, expenseType)).ToArray()
                    },
                    new[] { "k=1 (RN)", "k=2", "k=3", "k=AvgDeg", "k=Infinity" },
                    title: expenseType + " for RkN Sampling to Get Top x Pct Of High Degree Vertices",
                    xlabel: "Pct of High-Degree Vertices",
                    ylabel: expenseType + " Costs",
                    fileName: expenseType + "ForRkNSampling.png"
                );
            }
        }

        static void CvByCnHeatMap()
        {
            var data = File.ReadAllLines(OUTPUT_FILE).Skip(1).Select(l => Regex.Split(l, @"\s+"))
                .GroupBy(arr => arr[0].Replace("_", "-").Replace("5000", "Inf")) // Now have groups of arrays, key is the method type
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(arr => double.Parse(arr[1]), arr => (double.Parse(arr[2]), double.Parse(arr[3]), double.Parse(arr[4])) // method points to dict<pct, tuple of costs>
                )
            );

            var desiredPcts = Range(1, 5000).Select(i => Math.Round(1.0 / 5000.0 * i, 4)).ToArray();
            var methods = data.Keys.ToArray();

            foreach (var desiredMethod in methods)
            {
                Console.WriteLine($"Starting heat maps for {desiredMethod}, {DTS}");
                
                // When comparaing Cvn to Cs will scale by their respective averages over the entire sampling
                var medCvCn = data[desiredMethod].Values.YNMedian(t => t.Item1 + t.Item2);
                var medCs = data[desiredMethod].Values.Average(t => t.Item3);

                foreach (var desiredPct in desiredPcts.Where(p => new[] {.1, .9}.Contains(p)))
                {
                    var CvByCnFileName = $"{desiredMethod.Replace("=", "_")}_CvByCn_CostHeatMap_Pct{desiredPct.ToString("0.0000")}.png";

                    var line = data[desiredMethod][desiredPct];
                    var Cv = line.Item1;
                    var Cn = line.Item2;
                    var Cs = line.Item3;

                    var matrix = Range(100).Select(i => new double[100]).ToArray();

                    if (!File.Exists(CvByCnFileName))
                    {
                        for (int rowIndex = 99; rowIndex >= 0; rowIndex--)
                        {
                            for (int colIndex = 0; colIndex < 100; colIndex++)
                            {
                                var TotalCvSpent = ((colIndex + 1) / 100.0) * Cv;
                                var TotalCnSpent = ((100 - rowIndex) / 50.0) * Cn;
                                matrix[rowIndex][colIndex] = TotalCvSpent + TotalCnSpent;
                            }
                        }

                        Py.CreatePyHeatmap(
                            matrix,
                            Range(1, 100).Select(i => (1.0 / 100 * i).ToString()).ToArray(),
                            Range(1, 100).Select(i => (1.0 / 100 * i).ToString()).Reverse().ToArray(),
                            $"{desiredMethod.Replace("_", "-").Replace("5000", "Inf")} - Total cost for \\n{desiredPct} of high-degree vertices",
                            "Cv", "Cn",
                            CvByCnFileName
                            );
                    }

                    var CsByCvFileName = $"{desiredMethod.Replace("=", "_")}_CsByCv_CostHeatMap_Pct{desiredPct.ToString("0.0000")}.png";

                    if (!File.Exists(CsByCvFileName))
                    {
                        // Now do Cs By Cv(=Cn)
                        for (int rowIndex = 99; rowIndex >= 0; rowIndex--)
                        {
                            for (int colIndex = 0; colIndex < 100; colIndex++)
                            {
                                var TotalCsSpent = (((colIndex + 1) / 100.0) * Cs)/medCs;
                                var TotalCvSpent = (((100 - rowIndex) / 100.0) * (Cv+Cn))/medCvCn;
                                matrix[rowIndex][colIndex] = TotalCvSpent + TotalCsSpent;

                            }
                        }

                        Py.CreatePyHeatmap(
                            matrix,
                            Range(1, 100).Select(i => (1.0 / 100 * i).ToString()).ToArray(),
                            Range(1, 100).Select(i => (1.0 / 100 * i).ToString()).Reverse().ToArray(),
                            $"{desiredMethod.Replace("_", "-").Replace("5000", "Inf")} - Total cost for \\n{desiredPct} of high-degree vertices",
                            "Cs", "Cv=Cn",
                            CsByCvFileName
                            );
                    }
                }
            }
        }

        static void RunExperiments()
        {
            Console.WriteLine($"Generating Graphs {DTS}");
            graphs = Range(GRAPHS).AsParallel().Select(i => Graph.NewErGraphFromBaM(N, M, random: rands[i])).ToArray();
            Console.WriteLine($"Done generating graphs {DTS}");
            foreach (SamplingMethod method in Enum.GetValues(typeof(SamplingMethod)))
            {
                var kVals = method == SamplingMethod.RkN ? new[] { 2, 3, M * 2, N } : new[] { 0 };

                foreach (var kVal in kVals)
                {
                    var methodName = method.ToString() + (method == SamplingMethod.RkN ? $"(k={kVal})" : "");
                    Console.WriteLine($"Starting method: {methodName} {DTS}");
                    Dictionary<double, Cost[]> AllCostResultsByPct = PCTS.ToDictionary(d => d, d => new Cost[EXPERIMENTS]);

                    Parallel.For(0, EXPERIMENTS, exp =>
                    {
                        var rand = rands[exp];
                        var graph = graphs[exp % GRAPHS];
                        var currPctIndex = 0;
                        VertexDegreeRanker ranker = new VertexDegreeRanker(graph);
                        foreach (var (V, C) in SampleAndReturnVertexAndCost(graph.Vertices, method, rand, BA_SWITCH_POINT, kVal))
                        {
                            var currPct = ranker.GetCurrRankFoundAsPct(V);
                            var currCost = C;

                            while (currPctIndex < PCTS.Length && currPct >= PCTS[currPctIndex])
                                AllCostResultsByPct[PCTS[currPctIndex++]][exp] = currCost;

                        }
                    });

                    foreach (var kvp in AllCostResultsByPct)
                        File.AppendAllText(OUTPUT_FILE, $"{methodName}\t{kvp.Key}\t{kvp.Value.Average(c => c.Cv)}\t{kvp.Value.Average(c => c.Cn)}\t{kvp.Value.Average(c => c.Cs)}\n");
                }
            }
        }

        static IEnumerable<(Vertex V, Cost C)> SampleAndReturnVertexAndCost(IEnumerable<Vertex> vertices, SamplingMethod method, Random rand, int switchingPoint = 0, int kForRkN = 0)
        {
            var allVertices = vertices.ToList();
            HashSet<Vertex> sampledVertices = new HashSet<Vertex>();
            var totalVertices = vertices.Count();
            var currCost = new Cost();
            var currentSamplesPerformed = 0; // for Hybrid Methods, to know when to switch

            // apparently need to share these names in all cases??
            Vertex vertex = null;
            Vertex neighbor = null;

            // Define inline methods for sampling and returning a vertex/neighbor

            (Vertex V, Cost C) SampleVertex()
            {
                vertex = allVertices.ChooseRandomElement(rand);
                currCost.AddCosts(Cv: 1);
                if (sampledVertices.Add(vertex))
                    currCost.AddCosts(Cs: 1);
                return (V: vertex, C: currCost);
            }

            (Vertex V, Cost C) SampleNeighbor(bool usingLastVertex)
            {
                if (usingLastVertex)
                {
                    neighbor = vertex.Neighbors.ChooseRandomElement(rand);
                }
                else
                {
                    neighbor = allVertices.ChooseRandomElement(rand).Neighbors.ChooseRandomElement(rand);
                    currCost.AddCosts(Cv: 1);
                }
                currCost.AddCosts(Cn: 1); // Pay for the neighbor itself

                if (sampledVertices.Add(neighbor))
                    currCost.AddCosts(Cs: 1);

                return (V: neighbor, C: currCost);
            }

            (Vertex V, Cost C) SampleNeighborOfLastVertex() => SampleNeighbor(true);
            (Vertex V, Cost C) SampleNeighborOnly() => SampleNeighbor(false);

            while (sampledVertices.Count < totalVertices)
            {
                switch (method)
                {
                    case SamplingMethod.RV:
                        yield return SampleVertex();
                        break;
                    case SamplingMethod.RN:
                        yield return SampleNeighborOnly();
                        break;
                    case SamplingMethod.RVN:                        
                        yield return SampleVertex();
                        if (sampledVertices.Count < totalVertices) // while loop should continue to sample
                            yield return SampleNeighborOfLastVertex();
                        break;
                    case SamplingMethod.RN_RV:
                        if (switchingPoint <= 0)
                            throw new Exception($"Must specify a positive switchingPoint param to use a hybrid method (switchingPoint value: {switchingPoint})");
                        if (currentSamplesPerformed < switchingPoint)
                        {
                            yield return SampleNeighborOnly();
                            currentSamplesPerformed++;
                        }
                        else
                            yield return SampleVertex();
                        break;
                    case SamplingMethod.RVN_RV:
                        if (switchingPoint <= 0)
                            throw new Exception($"Must specify a positive switchingPoint param to use a hybrid method (switchingPoint value: {switchingPoint})");
                        if (currentSamplesPerformed < switchingPoint)
                        {
                            yield return SampleVertex();
                            if (sampledVertices.Count < totalVertices) // while loop should continue to sample
                                yield return SampleNeighborOfLastVertex();
                            currentSamplesPerformed++;
                        }
                        else
                            yield return SampleVertex();
                        break;
                    case SamplingMethod.RkN:
                        if (kForRkN < 2)
                            throw new Exception($"Must specify a value greater than 1 for kForRkN to use RkN (kForRkN value: {kForRkN}");
                        var neighbors = allVertices.ChooseRandomElement(rand).Neighbors.ChooseRandomSubset(kForRkN, random: rand);
                        currCost.AddCosts(Cv: 1); // for the initial vertex
                        foreach (var currNeighbor in neighbors)
                        {
                            currCost.AddCosts(Cn: 1);
                            if (sampledVertices.Add(currNeighbor))
                                currCost.AddCosts(Cs: 1);
                            yield return (V: currNeighbor, C: currCost);
                            if (sampledVertices.Count == totalVertices)
                                break;
                        }                        
                        break;
                    default:
                        throw new Exception($"Undefined SamplingMethod enum value: {method}");
                }
            }
        }


    }
}
