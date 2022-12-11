using BestSwitchForRNRV_02;
using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UtilsYN;

namespace CostsForPctTotalDegressAndPctRank
{
    /* YN 1/24/22 - This experiment will compare all of our one-phase sampling methods (RV, RN, RkN {k = 2, 3, mu, infty}, RVN, and RVkN {k = 2, 3, mu, infty},
     * to see how they perform in terms of Cv, Cn, and Cs for accumulating unique degrees. We will see what costs are incurred for getting .1 of the total degrees,
     * .2 of the total degrees, etc.
     * 
     * UPDATE: 1/31/22 - Made a hacky change. First of all, many more values of k. Second, it will now also give costs for the top .01 pct of hubs, .02 pct of hubs
     * etc. Code may be a bit confusing, will try to comment it a bit
     */


    /// <summary>
    /// Data structure that tracks Sampling Costs, Cv, Cn.
    /// </summary>
    public class SamplingCost
    {
        public int Cv { get; set; }
        public int Cn { get; set; }

        public SamplingCost(int Cv, int Cn)
        {
            this.Cv = Cv;
            this.Cn = Cn;
        }
    }

    // For readability, a sampling method that can be called to sample iteratively and give back a list of 
    // tuples where the first item is the vertex selected, the second is the cost. Often it will give back
    // just one item, but some methods take a vertex and a neighbor (or more) so give back as a list.
    delegate List<Tuple<Vertex, SamplingCost>> SampleForCost(Graph graph, Random rand);

    class Program
    {
        const bool debug = false;
        const int EXPERIMENTS = debug ? 1 : 400;
        const int GRAPHS = debug ? 1 : EXPERIMENTS;
        const int THREADS = debug ? 1 : 54;
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        const String TOTDEG_RESULTS_FILE = "TotDegResults_7000-5.csv";
        const String RANK_RESULTS_FILE = "RankResults_7000-5.csv";

        const int N = 1000;
        const int M = 8;
        const double INCRMNT = 0.01;
        static Graph[] graphs;

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

        static void Main(string[] args)
        {
            CreateCsv();
            //CreateCharts_AllCostsForWholeGraphByMethod(new[] { "RV", "RN", "RVN", "RkN k=2", "RkN k=9(mu)", "RkN k=infty", "RVkN k=2", "RVkN k=9(mu)", "RVkN k=infty"  });
            //CreateCharts_HeatPlots(new[] { "RV", "RN", "RVN" });
            /*new List<string[]>
            {
                new[] {"RVN", "RVkN k=2" },
                new[] {"RVkN k=2", "RVkN k=3" },
                new[] {"RVkN k=3", "RVkN k=4" },
                new[] {"RVkN k=4", "RVkN k=5" },
                new[] {"RVkN k=5", "RVkN k=9(mu)" },
                new[] {"RVkN k=9(mu)", "RVkN k=10" },
                new[] {"RVkN k=10", "RVkN k=11" },
                new[] {"RVkN k=11", "RVkN k=12" },
                new[] {"RVkN k=12", "RVkN k=infty" },
            }.ForEach(CreateCharts);*/
        }

        static void CreateCharts_HeatPlots(string[] includedMethods)
        {
            /* YN 2-1-22 - Over here I am trying to create a heat plot where the x-axis is 0..1 of Cs, and y-axis is 0..1 of Cv+Cn (in others words, total samples), and the heat represents 
             * the total cost, so we will see how sampling and selecting contribute to total cost for each method. We will again do both total degrees and rank, and create a separate heat-plot
             * for each percent in which we are interested (maybe every .1 or every .5), and generate an HTML table that 
             */

            var amount = 20; // How many heatplots to generate, we will divide the total results by this number
            StringBuilder html = new StringBuilder();
            html.AppendLine($"<h1>X-axis is Cs, Y-Axis is Cv+Cn (total samples), 'Heat' is total cost. Included Methods: {String.Join(" ", includedMethods)}</h1>");
            var methodsForFileName = String.Join("-", includedMethods.Select(m => m.Replace(" ", "-").Replace("=", "--")));
            var reportDir = $"HeatMapReport_{methodsForFileName}";
            if (Directory.Exists(reportDir))
                Directory.Delete(reportDir);
            Directory.CreateDirectory(reportDir);

            foreach (var metric in new[] { "Total Degrees", "Ranks" })
            {
                html.AppendLine($"<h2>By {metric}</h2>");
                var FileLines = File.ReadAllLines(metric == "Total Degrees" ? TOTDEG_RESULTS_FILE : RANK_RESULTS_FILE).Select(l => Regex.Split(l, ",")).ToList();
                var pctVector = FileLines.First().Skip(1).Where((l, i) => (i - 2) % 3 == 0).Select(s => decimal.Parse(s)).ToArray();
                var CvDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => i % 3 == 0).Select(s => decimal.Parse(s)).ToArray());
                var CnDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => (i - 1) % 3 == 0).Select(s => decimal.Parse(s)).ToArray());
                var CsDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => (i - 2) % 3 == 0).Select(s => decimal.Parse(s)).ToArray());

                var ResultCostDictionaries = new[] { CvDictionary, CnDictionary, CsDictionary };


                foreach (var method in includedMethods)
                {
                    html.AppendLine($"<h3>Sampling Method: {method}</h3>");
                    html.AppendLine($"<table>");
                    html.AppendLine($"\t<tr>");
                    for (int rangeNum = 0; rangeNum < amount; rangeNum++)
                    {
                        decimal SamplingCost = CvDictionary[method][rangeNum * (pctVector.Length / amount)] + CnDictionary[method][rangeNum * (pctVector.Length / amount)];
                        decimal SelectingCost = CsDictionary[method][rangeNum * (pctVector.Length / amount)];

                        double[][] totalCosts = new double[100][];
                        for (int smpl = 0; smpl < 100; smpl++)
                        {
                            totalCosts[99 - smpl] = new double[100];
                            for (int slct = 0; slct < 100; slct++)
                            {
                                totalCosts[99 - smpl][slct] = Convert.ToDouble(SamplingCost * (smpl / 100m) + SelectingCost * (slct / 100m));
                            }
                        }

                        String fileName = $"HeatMap{metric.Replace(" ", "")}_{method.Replace("=", "--").Replace(" ", "-")}_{rangeNum.ToString("0#")}.png";

                        PyReporting.Py.CreatePyHeatmap(
                            totalCosts,
                            Range(100).Select(i => "").ToArray(),
                            Range(100).Select(i => "").ToArray(),
                            $"Sampling vs Selection Costs {method}, {(rangeNum * (pctVector.Length / amount))} Pct of {metric}",
                            "Selection Costs",
                            "Sampling Costs",
                            reportDir + "\\" + fileName
                            );
                        html.AppendLine($"\t\t<td><img src='{fileName}' /></td>");
                    }
                    html.AppendLine($"\t</tr>");
                    html.AppendLine($"</table>");
                }
            }
            File.WriteAllText($"{reportDir}\\Results_{methodsForFileName}.html", html.ToString());

        }

        #region CREATE_CHARTS

        static void CreateCharts_AllCostsForWholeGraphByMethod(string[] includedMethods)
        {
            /* YN 2-1-22 - This method will create one chart for each sampling method (RV, RN, etc.) where the x-axis is the pct of the goal
             * (either total degrees or rank) and the y-axis is the cost, with a separate line for Cv, Cn, (Cv+Cn?), and Cs. If we see that these
             * don't fit together on the same scale will have to readjust.
             */
            StringBuilder html = new StringBuilder();
           
            var methodsForFileName = String.Join("-", includedMethods.Select(m => m.Replace(" ", "-").Replace("=", "--")));
            var reportDir = $"AllCostsByMethod_Report";
            if (Directory.Exists(reportDir))
                Directory.Delete(reportDir, true);
            Directory.CreateDirectory(reportDir);

            foreach (var metric in new[] { "Total Degrees", "Ranks" })
            {
                var amount = 20;

                html.AppendLine($"<h2>By {metric}</h2>");
                var FileLines = File.ReadAllLines(metric == "Total Degrees" ? TOTDEG_RESULTS_FILE : RANK_RESULTS_FILE).Select(l => Regex.Split(l, ",")).ToList();
                var pctVector = FileLines.First().Skip(1).Where((l, i) => (i - 2) % 3 == 0).Select(s => double.Parse(s)).ToArray();
                var CvDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => i % 3 == 0).Select(s => double.Parse(s)).ToArray());
                var CnDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => (i - 1) % 3 == 0).Select(s => double.Parse(s)).ToArray());
                var CsDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => (i - 2) % 3 == 0).Select(s => double.Parse(s)).ToArray());

                foreach(var method in includedMethods)
                {
                    html.AppendLine("<table>");
                    html.AppendLine("\t<tr>");
                    var yValsOrig = new[] 
                    {
                        CvDictionary[method], 
                        CnDictionary[method], 
                        Range(pctVector.Length).Select(i => CvDictionary[method][i] + CnDictionary[method][i]).ToArray(), 
                        CsDictionary[method]
                    };

                    var fileName = $"{metric.Replace(" ", "").Replace("=", "--")}_{method}_FULL.png";
                    PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot,
                            pctVector.ToArray(),
                            yValsOrig,
                            new[] { "Cv", "Cn", "Cv+Cn", "Cs" },
                            //new string[] { "r", "b", "g", "k", "m" },
                            title: $"{metric} Cost Values for {method}",
                            xlabel: metric == "Total Degrees" ? "Percent of Total Degrees Selected" : "Percent of Highest-Degree Vertices Selected",
                            ylabel: $"Total Cost",
                            fileName: reportDir + "\\" + fileName);
                    html.AppendLine($"\t\t<td><img src=\"{fileName}\" /></td>");

                    for (int rangeNum = 0; rangeNum < amount; rangeNum++)
                    {
                        var currYVals = yValsOrig.Select(arr => arr.Skip(rangeNum * (pctVector.Length / amount)).Take(arr.Length / amount).ToArray()).ToArray();
                        fileName = $"{metric.Replace(" ", "")}_{method}_{rangeNum.ToString("#0")}.png";

                        PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot,
                               pctVector.Skip(rangeNum * (pctVector.Length / amount)).Take(pctVector.Length / amount).ToArray(),
                               currYVals,
                               new[] { "Cv", "Cn", "Cv+Cn", "Cs" },
                                //new string[] { "r", "b", "g", "k", "m" },
                                title: $"{metric} Cost Values for {method} Range: {rangeNum}",
                                xlabel: metric == "Total Degrees" ? "Percent of Total Degrees Selected" : "Percent of Highest-Degree Vertices Selected",
                                ylabel: $"Total Cost",
                                fileName: reportDir + "\\" + fileName);
                        html.AppendLine($"\t\t<td><img src=\"{fileName}\" /></td>");
                    }
                    html.AppendLine("\t</tr>");
                    html.AppendLine("</table>");

                }
            }
            File.WriteAllText($"{reportDir}\\Results.html", html.ToString());
        }

        static void CreateCharts_LinePlots(string[] includedMethods)
        {
            /* YN 2-1-2022 This method creates line charts where the x-axis is some pct of the goal, either pct of total degrees found, or pct of high-degree
             * vetices found, and the y axis is the total cost. It creates three separate sets of charts, Cv, Cn, and Cs. The charts are broken down, first is
             * a full x-axis, after that the x-axis is broken up into how ever many pieces (variable below) to "zoom in". The method creates an HTML file
             * that puts all images for a cost-metric combination on a row.
             */

            var amount = 10;


            StringBuilder html = new StringBuilder();
            html.AppendLine($"<h1>{String.Join(" ", includedMethods)} Comparisons</h1>");
            var methodsForFileName = String.Join("-", includedMethods.Select(m => m.Replace(" ", "-").Replace("=", "--")));
            var reportDir = $"Report{methodsForFileName}";
            if (Directory.Exists(reportDir))
                Directory.Delete(reportDir, true);
            Directory.CreateDirectory(reportDir);

            foreach (var metric in new[] { "Total Degrees", "Ranks" })
            {
                html.AppendLine($"<h2>By {metric}</h2>");
                var FileLines = File.ReadAllLines(metric == "Total Degrees" ? TOTDEG_RESULTS_FILE : RANK_RESULTS_FILE).Select(l => Regex.Split(l, ",")).ToList();
                var pctVector = FileLines.First().Skip(1).Where((l, i) => (i - 2) % 3 == 0).Select(s => double.Parse(s)).ToArray();
                var CvDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => i % 3 == 0).Select(s => double.Parse(s)).ToArray());
                var CnDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => (i - 1) % 3 == 0).Select(s => double.Parse(s)).ToArray());
                var CsDictionary = FileLines.Skip(2).ToDictionary(arr => arr[0], arr => arr.Skip(1).Where((s, i) => (i - 2) % 3 == 0).Select(s => double.Parse(s)).ToArray());

                var ResultCostDictionaries = new[] { CvDictionary, CnDictionary, CsDictionary };

                for (int i = 0; i < ResultCostDictionaries.Length; i++)
                {
                    html.AppendLine($"<h3>{(i == 0 ? "Cv" : i == 1 ? "Cn" : "Cs")} Costs</h3>");
                    html.AppendLine("<table>");
                    html.AppendLine("\t<tr>");

                    // Append the full chart:
                    var currResults = ResultCostDictionaries[i];
                    var Cost = i == 0 ? "Cv" : i == 1 ? "Cn" : "Cs";
                    var yVals = includedMethods.Select(mthd => currResults[mthd]).ToArray().ToArray();
                    var fileName = $"{metric.Replace(" ", "")}_{Cost}_{methodsForFileName}_FULL.png";
                    PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot,
                            pctVector.ToArray(),
                            yVals,
                            includedMethods,
                            //new string[] { "r", "b", "g", "k", "m" },
                            title: $"{metric} Cost Values for Sampling Methods",
                            xlabel: metric == "Total Degrees" ? "Percent of Total Degrees Selected" : "Percent of Highest-Degree Vertices Selected",
                            ylabel: $"Total {Cost} Cost",
                            fileName: reportDir + "\\" + fileName);
                    html.AppendLine($"\t\t<td><img src=\"{fileName}\" /></td>");

                    for (int rangeNum = 0; rangeNum < amount; rangeNum++)
                    {
                        yVals = includedMethods.Select(mthd => currResults[mthd].Skip(rangeNum * amount).Take(amount).ToArray()).ToArray();
                        fileName = $"{metric.Replace(" ", "")}_{Cost}_{methodsForFileName}_{rangeNum.ToString("0#")}.png";
                        PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot,
                               pctVector.Skip(rangeNum * amount).Take(amount).ToArray(),
                               yVals,
                               includedMethods,
                               //new string[] { "r", "b", "g", "k", "m" },
                               title: $"{metric} Cost Values for Sampling Methods",
                               xlabel: metric == "Total Degrees" ? "Percent of Total Degrees Selected" : "Percent of Highest-Degree Vertices Selected",
                               ylabel: $"Total {Cost} Cost",
                               fileName: reportDir + "\\" + fileName);
                        html.AppendLine($"\t\t<td><img src=\"{fileName}\" /></td>");
                    }
                    html.AppendLine("\t</tr>");
                    html.AppendLine("</table>");
                }
            }
            File.WriteAllText($"{reportDir}\\Results_{methodsForFileName}.html", html.ToString());
            
        }
        #endregion

        static void CreateCsv()
        {
            Console.WriteLine($"Starting program {DTS}");
            if (!File.Exists("C:\\Graphs-7000-5\\graphs000.txt"))
            {
                graphs = Range(0, EXPERIMENTS).AsParallel().Select(i => Graph.NewBaGraph(N, M, random: rands[i])).ToArray();
                for (int i = 0; i < graphs.Length; i++)
                {
                    File.WriteAllLines($"C:\\Graphs-7000-5\\graphs{i.ToString("00#")}.txt", graphs[i].Edges.Select(e => e.v1.Id + "\t" + e.v2.Id));
                }
            }
            else
            {
                graphs = new Graph[EXPERIMENTS];
                for (int i = 0; i < graphs.Length; i++)
                {
                    graphs[i] = new Graph();
                    File.ReadAllLines($"C:\\Graphs-7000-5\\graphs{i.ToString("00#")}.txt").ToList().ForEach(l => graphs[i].AddEdge(Regex.Split(l, @"\s+")[0], Regex.Split(l, @"\s+")[1]));
                }
            }    
               


            String preamble = "";
            for (double d = INCRMNT; d <= 1.0; d = Math.Round(d + INCRMNT, 2))
                preamble += ",,," + Math.Round(d, 2);
            preamble += "\n";
            for (double d = INCRMNT; d <= 1.0; d = Math.Round(d + INCRMNT, 2))
                preamble += ",Cv,Cn,Cs";
            preamble += "\n";

            //if (!File.Exists(RESULTS_FILE))
            File.WriteAllText(TOTDEG_RESULTS_FILE, preamble);
            File.WriteAllText(RANK_RESULTS_FILE, preamble);

            StringBuilder CSV_TOTDEG = new StringBuilder();
            CSV_TOTDEG.Append(preamble);
            StringBuilder CSV_RANK = new StringBuilder();
            CSV_RANK.Append(preamble);

            int mu = (int)graphs[0].Vertices.Average(v => v.Degree);
            int max = graphs.Max(g => g.Vertices.Max(v => v.Degree));
            Dictionary<String, SampleForCost> methods = new Dictionary<string, SampleForCost>
            {
                /*{ "RV", RV },
                { "RN", RN },
                { "RkN k=2", getRkN(2) },
                { "RkN k=3", getRkN(3) },
                { "RkN k=4", getRkN(4) },
                { "RkN k=5", getRkN(5) },
                { "RkN k=5", getRkN(6) },
                { "RkN k=5", getRkN(7) },
                { "RkN k=5", getRkN(8) },
                { "RkN k=5", getRkN(9) },
                { "RkN k=4", getRkN(4) },
                { "RkN k=5", getRkN(5) },
                { "RkN k=5", getRkN(6) },
                { "RkN k=5", getRkN(7) },
                { "RkN k=5", getRkN(8) },
                { "RkN k=5", getRkN(9) },
                { "RkN k=infty", getRkN(int.MaxValue) },
                { "RVN", RVN },
                { "RVkN k=2", getRVkN(2) },
                { "RVkN k=3", getRVkN(3) },
                { "RVkN k=4", getRVkN(4) },
                { "RVkN k=5", getRVkN(5) },
                { $"RVkN k={mu}(mu)", getRVkN(mu) },
                { $"RVkN k={mu+1}", getRVkN(mu+1) },
                { $"RVkN k={mu+2}", getRVkN(mu+2) },
                { $"RVkN k={mu+3}", getRVkN(mu+3) },*/
                { "RVkN k=infty", getRVkN(int.MaxValue) },
            };
            methods = Range(2, max).SelectMany(i =>
                new[] {
                    new KeyValuePair<String, SampleForCost>($"RkN k={i}", getRkN(i)),
                    new KeyValuePair<string, SampleForCost>($"RVkN k={i}", getRVkN(i)) })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            methods = new Dictionary<string, SampleForCost>();

            methods["RVkN k=0"] = RV;
            methods["RkN k=1"] = RN;
            methods["RVkN k=1"] = RVN;

            decimal currK = 2;
            HashSet<int> completedK = new HashSet<int>();
            decimal currInc = 1.1m;
            while (currK <= max)
            {
                if (!completedK.Contains((int)currK))
                {
                    completedK.Add((int)currK);
                    methods[$"RkN k={(int)currK}"] = getRkN((int)currK);
                    methods[$"RVkN k={(int)currK}"] = getRVkN((int)currK);
                }
                currK += currInc;
                currInc += .05m;
            }

            for (int i = 2; i <= max; i++)
            {
                if (!completedK.Contains(i))
                {
                    methods[$"RkN k={i}"] = getRkN(i);
                    methods[$"RVkN k={i}"] = getRVkN(i);
                }
            }

            foreach (var kvp in methods)
            {
                Console.WriteLine($"Starting method {kvp.Key}, at {DTS}");
                int[][] totDegResultArrays = new int[EXPERIMENTS][];
                int[][] rankResultArrays = new int[EXPERIMENTS][];
                Parallel.For(0, EXPERIMENTS, new ParallelOptions() { MaxDegreeOfParallelism = THREADS } , i =>
                {
                    var results = SampleAndGetDegrees(graphs[i], kvp.Value, rands[i]);
                    totDegResultArrays[i] = results.Item1.ToArray();
                    rankResultArrays[i] = results.Item2.ToArray();
                });
                string degresult = kvp.Key + "," + String.Join(",", Range(0, totDegResultArrays[0].Length).Select(i => totDegResultArrays.Average(row => row[i]))) + "\n";
                string rankresult = kvp.Key + "," + String.Join(",", Range(0, rankResultArrays[0].Length).Select(i => rankResultArrays.Average(row => row[i]))) + "\n";
                File.AppendAllText(TOTDEG_RESULTS_FILE, degresult);
                File.AppendAllText(RANK_RESULTS_FILE, rankresult);
                CSV_TOTDEG.Append(degresult);
                CSV_RANK.Append(rankresult);
            }
           
        }

        // give a graph, and a function that samples and gives back pairs of vertices and the Cv and Cn cost to get them (may
        // will return just one vertex, but some return multiple), it will repeatedly sample and give back a list of ints which are
        // Cv, Cn, Cs, to get .1 of all degrees, Cv, Cn, Cs to get .2, etc. 30 values total.
        static Tuple<List<int>, List<int>> SampleAndGetDegrees(Graph g, SampleForCost Sampler, Random rand)
        {
            List<int> allCostsForDegrees = new List<int>(); // for .01 of total degrees, .02 of total degress..
            List<int> allCostsForRanks = new List<int>(); // for .01 of top ranked vertices, .02 of top ranked vertices..
            int n = g.Vertices.Count();
            int totalDegreesSought = g.Vertices.Sum(v => v.Degree);
            // start both initial goals at the minimum value
            double currPctSought = INCRMNT;
            double currPctRankSought = INCRMNT;
            int currDegreesSought = (int)(currPctSought * totalDegreesSought); // an exact degree count being sought
            HashSet<Vertex> obtainedVertices = new HashSet<Vertex>(); // only pay Cs and add to results when we find a new vertex
            VertexDegreeRanker ranker = new VertexDegreeRanker(g);
            int currDegreesObtained = 0;
            int currCvTotal = 0;
            int currCnTotal = 0;
            int currCsTotal = 0;

            while (currDegreesObtained < totalDegreesSought) // Till we get all degrees
            {
                var nexVerticesAndCosts = Sampler(g, rand);
                foreach (var nextVertexAndCost in nexVerticesAndCosts)
                {
                    var vertex = nextVertexAndCost.Item1;
                    var costs = nextVertexAndCost.Item2;
                    currCvTotal += costs.Cv;
                    currCnTotal += costs.Cn;
                    if (obtainedVertices.Add(vertex))
                    {
                        currCsTotal += 1;
                        // deal with the goal of total degrees:
                        currDegreesObtained += vertex.Degree;
                        while (currDegreesObtained >= currDegreesSought) // finding this vertex may surpass more than one goal
                        {
                            if (currPctSought > 1.0)
                                break;     
                            allCostsForDegrees.Add(currCvTotal);
                            allCostsForDegrees.Add(currCnTotal);
                            allCostsForDegrees.Add(currCsTotal);
                            currPctSought = Math.Round(currPctSought + INCRMNT, 2);
                            currDegreesSought = (int)(currPctSought * totalDegreesSought);
                        }
                        // Now deal with the goal of finding the top degree vertices:
                        double currRankFound = ranker.GetCurrRankFoundAsPct(vertex);
                        while (currRankFound >= currPctRankSought) // finding this vertex may surpass more than one goal
                        {
                            if (currPctRankSought > 1.0)
                                break; 
                            allCostsForRanks.Add(currCvTotal);
                            allCostsForRanks.Add(currCnTotal);
                            allCostsForRanks.Add(currCsTotal);
                            currPctRankSought = Math.Round(currPctRankSought + INCRMNT, 2);
                        }
                    }
                }
            }

            return new Tuple<List<int>, List<int>>(allCostsForDegrees, allCostsForRanks);
        }

    }
}
