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

namespace TotalDegreesOverCostBySamplesTaken_02
{
    class Program
    {
        /* YN 4/15/21 - Trying to rewrite version 1 of this program where the code is an absolute mess. Needs to be
         * more modular (of course) with fewer globals, etc., and most specifically we need a cleaner way to
         * include RW graphs to keep them as similar as possible to the synthetic ones.
         */

        // Hack to let two programs take turns
        static string ThisProgId = "B";
        static string OtherProgId = "A";
        static string SyncFilePath = @"C:\temp\RunTwoProgramsSync.txt";

        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int n) => Enumerable.Range(0, n);
        static readonly int THREADS = 50;
        static Random[] rands = TSRandom.ArrayOfRandoms(THREADS);
        private static readonly string KOBLENZ_BASE_DIR = @"D:\Graphs\Koblenz\AllOrigFiles\konect.cc\files\"; // location of the files on my workstation computer

        // The number of samples we take. We want to eventually get every vertex which should require
        // n log n samplings. For now doing 3n log n, to make sure we get each one WHP.
        static int SamplesToTake(int n) => (int)(3 * n * Math.Log(n));

        enum GraphType { ER, BA, RW };
        static string FILENAME_SYNTH(GraphType graphType, int n, int m) => $"Results\\{graphType}_N_{n}_M_{m}_results.tsv";
        static string FILENAME_RW(string graphName) => $"Results\\RW_{graphName.Replace(" ", "-")}_results.tsv";

        // For the RW graphs, errors may come up in parsing, so save them to a file so we can examine them by hand.
        // In case we decide to parse them in parallel or something like that, we will lock when we write an error.
        static object WriteErrLock = new object();
        const string ERRFILENAME = "Results\\ErrLog.txt";
        static void WriteErr(string message)
        {
            Console.WriteLine($"Error: {message}, ({DTS})");
            lock (WriteErrLock)
            {
                if (!File.Exists(ERRFILENAME))
                    File.WriteAllText(ERRFILENAME, "");
                File.AppendAllText(ERRFILENAME, message + $" ({DTS})\n");
            }
        }

        /// <summary>
        /// A preamble for the file that explains the experiment, hopefully we will be able to read this in the file itself to 
        /// avoid having to look back at the code to understand what the results are. Lines will start with % so we can write
        /// the Python code to ignore them.
        /// </summary>
        static string PREAMBLE =
                "% We are testing what happens when you collect vertices, what are the total degrees of all vertices collected.\n" +
                $"% For every x_value (sample size) we will have {THREADS} collections from {THREADS} separate graphs\n" +
                $"% and report the average result over the whole collection.\n" +
                $"% Note that we are sampling WITHOUT replacement.\n" +
                $"% Also note that for efficiency we are NOT resampling for every x_val, we are just adding a new sample every time.\n" +
                $"% x_vals are the values of the x-axis, the numbers of samples in each collection\n" +
                $"% PredictedRv is what we would predict with our knowledge of RV\n" +
                $"% PredictedRn is what we would predict with our knowledge of RN\n" +
                $"% PredictedRe is what we would predict with our knowledge of RE\n" +
                $"% PredictedIrn is what we would predict for IRN with our knowledge of IRN\n" +
                $"% PredictedIre is what we would predict for IRE with our knowledge of IRE\n" +
                $"% PredictedRvRn is what we would predict for RN when we keep the first vertex also with our knowledge of RV and RN (note we will add in two steps, v then n, so the size is still x)\n" +
                $"% PredictedFullEdge is what we would predict if we take an edge and include both vertices, again including one at a time so the collection size is still x\n" +
                $"% ActualRv is the actual total degrees (with duplicate vertices) for RV for the given number of samples\n" +
                $"% ActualRn is the actual total degrees (with duplicate vertices) for RN for the given number of samples\n" +
                $"% ActualRe is the actual total degrees (with duplicate vertices) for RE for the given number of samples\n" +
                $"% ActualIrn is the actual total degrees (with duplicate vertices) for IRN for the given number of samples\n" +
                $"% ActualIre is the actual total degrees (with duplicate vertices) for IRE for the given number of samples\n" +
                $"% ActualRvRn is the actual total degrees (with duplicate vertices) for RV+RN for the given number of samples\n" +
                $"% ActualFullEdge is the actual total degrees (with duplicate vertices) for RE when we keep both endpoints (one at a time)\n" +
                $"% *** The next methods are UNIQUE versions, if our sampling gives a vertex we've already collected we ignore it ***\n" +
                $"% ActualUnqRv is the actual total degrees (WITHOUT duplicate vertices) for RV for the given number of samples\n" +
                $"% ActualUnqRn is the actual total degrees (WITHOUT duplicate vertices) for RN for the given number of samples\n" +
                $"% ActualUnqRe is the actual total degrees (WITHOUT duplicate vertices) for RE for the given number of samples\n" +
                $"% ActualUnqIrn is the actual total degrees (WITHOUT duplicate vertices) for IRN for the given number of samples\n" +
                $"% ActualUnqIre is the actual total degrees (WITHOUT duplicate vertices) for IRE for the given number of samples\n" +
                $"% ActualUnqRvRn is the actual total degrees (WITHOUT duplicate vertices) for RV+RN for the given number of samples\n" +
                $"% ActualUnqFullEdge is the actual total degrees (WITHOUT duplicate vertices) for RE when we keep both endpoints (one at a time)\n" +
                $"% ActualUnqRvSamples is the total size of the collection of distinct vertices, in other words how many times was C_s paid for number of samplings\n" +
                $"% ActualUnqRnSamples is the total size of the collection of distinct neighbors, in other words how many times was C_s paid for number of samplings\n" +
                $"% ActualUnqReSamples is the total size of the collection of distinct edge endpoints, in other words how many times was C_s paid for number of samplings\n" +
                $"% ActualUnqIrnSamples is the total size of the collection of distinct IRN vertices, in other words how many times was C_s paid for number of samplings\n" +
                $"% ActualUnqIreSamples is the total size of the collection of distinct IRE vertices, in other words how many times was C_s paid for number of samplings\n" +
                $"% ActualUnqRvRnSamples is the total size of the collection of distinct RV+RV vertices, in other words how many times was C_s paid for number of samplings\n" +
                $"% ActualUnqFullEdgeSamples is the total size of the collection of distinct full edge vertices, in other words how many times was C_s paid for number of samplings\n" +
                $"% ActualRvMax is the degree of the max degree vertex collected over the max degree for the graph\n" +
                $"% ActualRnMax is the degree of the max degree vertex collected over the max degree for the graph\n" +
                $"% ActualReMax is the degree of the max degree vertex collected over the max degree for the graph\n" +
                $"% ActualIrnMax is the degree of the max degree vertex collected over the max degree for the graph\n" +
                $"% ActualIreMax is the degree of the max degree vertex collected over the max degree for the graph\n" +
                $"% ActualRvRnMax is the degree of the max degree vertex collected over the max degree for the graph\n" +
                $"% ActualFullEdgeMax is the degree of the max degree vertex collected over the max degree for the graph\n" +
                $"% When examining these results in Python, you can define any costs you want and divide the results by them to get\n" +
                $"% menaningful figures based on cost.\n";

        static string GetMetaInfoString(Dictionary<string, string> meta) =>
            $"% Meta Information: " + String.Join(";", meta.Select(kvp => kvp.Key.ToString() + "\t" + kvp.Value.ToString())) + "\n";

        static Dictionary<string, string> GetBasicMetaInfo(String type, IEnumerable<Graph> graphs)
        {
            Dictionary<string, string> metaInfo = new Dictionary<string, string>();
            metaInfo["Experiments"] = THREADS.ToString();
            metaInfo["GraphType"] = type;
            metaInfo["N"] = graphs.Average(g => g.Vertices.Count()).ToString();
            metaInfo["M"] = graphs.Average(g => g.Edges.Count()).ToString();
            metaInfo["RV"] = graphs.Average(g => g.RV).ToString();
            metaInfo["RN"] = graphs.Average(g => g.RN).ToString();
            metaInfo["RN/RV"] = graphs.Average(g => g.RN / g.RV).ToString();
            metaInfo["RE"] = graphs.Average(g => g.RE).ToString();
            metaInfo["RE/RV"] = graphs.Average(g => g.RE / g.RV).ToString();
            metaInfo["IRN"] = graphs.Average(g => g.IRN).ToString();
            metaInfo["IRE"] = graphs.Average(g => g.IRE).ToString();
            metaInfo["Assort"] = graphs.Average(g => g.Assortativity()).ToString();
            metaInfo["AFI"] = graphs.Average(g => g.PositiveDegreeVertices.Average(v => v.Neighbors.Average(n => n.Degree) / v.Degree)).ToString();
            metaInfo["GFI"] = graphs.Average(g => g.PositiveDegreeVertices.Average(v => Math.Log(v.Neighbors.Average(n => n.Degree) / v.Degree))).ToString();
            metaInfo["Avg Max-Degree Vertices"] = graphs.AsParallel().Average(g =>
            {
                var maxDeg = g.Vertices.Max(v => v.Degree);
                return g.Vertices.Count(v => v.Degree == maxDeg);
            }).ToString();

            return metaInfo;
        }

        static void Main(string[] args)
        {
            Console.WriteLine($"Starting program at {DTS}");

            if (!Directory.Exists("Results"))
                Directory.CreateDirectory("Results");
            if (File.Exists(ERRFILENAME))
                File.Delete(ERRFILENAME);

            List<Action> RwActions = new List<Action>();
            List<Action> SyntheticGraphActions = new List<Action>();

            foreach (var n in new[] { 100, 500, 1000, 2000, 5000, 7500, 10000 })
                foreach (var m in new[] { 2, 5, 8, 15, 25, 50, 100 })
                {
                    if (m >= n / 2)
                        continue;

                    foreach (var grphType in new[] { GraphType.ER, GraphType.BA })
                    {
                        var fileName = FILENAME_SYNTH(grphType, n, m);
                        if (File.Exists(fileName))
                            continue;
                        SyntheticGraphActions.Add(() =>
                        {
                            Console.WriteLine($"Starting {grphType}, N: {n}, M: {m}, getting graphs {DTS}");
                            Graph[] graphs;
                            Dictionary<string, string> metaInfo;
                            GetSyntheticGraphs(grphType, n, m, out graphs, out metaInfo);
                            Console.WriteLine($"Getting results {DTS}");
                            var results = CollectSamplesAndGetResults(graphs, metaInfo);
                            File.WriteAllText(fileName, results);
                            Console.WriteLine($"Finished {grphType}, N: {n}, M: {m} {DTS}");
                        });
                    }
                }

            var graphDirs = new DirectoryInfo(KOBLENZ_BASE_DIR).GetDirectories().Select(di =>
            {
                long fileSize = -1;
                try
                {
                    var f = di.GetFiles().FirstOrDefault(fi => fi.Name.ToLower().StartsWith("out"));
                    if (f == null)
                        WriteErr($"No 'out' file in: {di.Name}");
                    else
                        fileSize = f.Length;
                }
                catch (Exception ex)
                {
                    WriteErr($"Error determining line count in {di.Name}, {ex.Message}");
                    fileSize = -1;
                }
                return new { di, fileSize };
            }).Where(o => o.fileSize > 0).OrderBy(o => o.fileSize).Select(o => o.di).ToList();
            foreach (var graphDir in graphDirs)
            {
                var fileName = FILENAME_RW(graphDir.Name);
                if (File.Exists(fileName))
                    continue;
                RwActions.Add(() =>
                {
                    Console.WriteLine($"Starting RW {graphDir.Name}, getting graph {DTS}");
                    Graph[] graphs;
                    Dictionary<string, string> metaInfo;
                    if (GetRWGraph(graphDir.FullName, out graphs, out metaInfo))
                    {
                        Console.WriteLine($"Getting results {DTS}");
                        var results = CollectSamplesAndGetResults(graphs, metaInfo);
                        File.WriteAllText(fileName, results);
                    }
                    else
                    {
                        Console.WriteLine($"Failure in RW Graph {graphDir.Name} {DTS}");
                        WriteErr($"Failed to process RW Graph {graphDir.Name}");
                    }
                    Console.WriteLine($"Finished RW {graphDir.Name} {DTS}");
                });
            }
            for (int i = 0; i < Math.Max(SyntheticGraphActions.Count, RwActions.Count); i++)
            {
                // When syncing with another program use this code:
                /*while (File.ReadAllText(SyncFilePath) != ThisProgId)
                    Thread.Sleep(TimeSpan.FromSeconds(10));*/

                //if (i < SyntheticGraphActions.Count)
                //    SyntheticGraphActions[i]();
                if (i < RwActions.Count)
                    for (int j = 0; j < 65; j++)
                        RwActions[i]();

                File.WriteAllText(SyncFilePath, OtherProgId);
            }

            Console.WriteLine($"Program Finished at {DTS}, press any key to exit");
            Console.ReadKey();
        }

        private static void GetSyntheticGraphs(GraphType graphType, int n, int m, out Graph[] graphs, out Dictionary<string, string> metaInfo)
        {
            graphs = Enumerable.Range(0, THREADS).AsParallel().Select(i => graphType == GraphType.BA ? Graph.NewBaGraph(n, m, random: rands[i]) : Graph.NewErGraphFromBaM(n, m, random: rands[i])).ToArray();
            metaInfo = GetBasicMetaInfo(graphType.ToString(), graphs);
        }

        private static bool GetRWGraph(string graphDirName, out Graph[] graphs, out Dictionary<string, string> metaInfo)
        {
            Graph graph = new Graph();
            string meta = "";
            try
            {
                int selfLoops = 0;
                // The following are fields that (hopefully) will be found in the metainformation
                // and/or comments of the files
                string url = "N/A"; // the "url" from the metafile
                string longdescr = "N/A"; // the "long-description" from the metafile
                string category = "N/A"; // "category" in the metafile
                bool? bipPerMeta = null; // does the word "bipartite" appear in the metafile
                bool bipPerComments = false; // does the first comment(s) in the file have the text "bip"
                int columnNumbers = -1; // How many columns in the graph file, more than 2 usually indicates a weighted graph
                // NOTE: Also might be of interest to know if the graph is directed, for now will not try to parse this
                // in code, just rely on the inclusion of description and comment
                string graphComment = "N/A"; // the first line of the graph file if it is a comment

                var dirfiles = new DirectoryInfo(graphDirName).GetFiles();
                var metafile = dirfiles.FirstOrDefault(fi => fi.Name.ToLower().StartsWith("meta"));
                if (metafile == null)
                {
                    WriteErr($"Couldn't find meta file for {graphDirName}");
                    meta = $"Couldn't find meta file for {graphDirName}\t";
                }
                else
                {
                    var fileMetaInfoLines = File.ReadAllLines(metafile.FullName).Select(l => Regex.Split(l.Replace("\t", "$TAB$").Replace(";", "$SEMICOLON$"), @"\:\s+"));
                    longdescr = fileMetaInfoLines.FirstOrDefault(mi => mi[0].ToLower() == "long-description")?[1] ?? "*** NOT FOUND ***";
                    url = fileMetaInfoLines.FirstOrDefault(mi => mi[0].ToLower() == "url")?[1] ?? "*** NOT FOUND ***";
                    category = fileMetaInfoLines.FirstOrDefault(mi => mi[0].ToLower() == "category")?[1] ?? "*** NOT FOUND ***";
                    bipPerMeta = File.ReadAllText(metafile.FullName).ToLower().Contains("bipartite");
                }
                var graphfile = dirfiles.FirstOrDefault(fi => fi.Name.ToLower().StartsWith("out"));
                if (graphfile == null)
                    throw new Exception($"Couldn't find graph file for {graphDirName}");

                var edges = File.ReadLines(graphfile.FullName);
                columnNumbers = Regex
                    .Split(edges.First(e => !String.IsNullOrWhiteSpace(e) && !e.StartsWith("%") && !e.StartsWith("#")),
                        @"\s+").Length;
                graph = new Graph();
                foreach (var edgeline in edges)
                {
                    if (String.IsNullOrWhiteSpace(edgeline))
                        continue;
                    if (edgeline.StartsWith("%") || edgeline.StartsWith("#"))
                    {
                        if (edgeline.ToLower().Contains("bip"))
                            bipPerComments = true;
                        graphComment += $"* {edgeline} * ";
                        continue;
                    }

                    var parts = Regex.Split(edgeline, @"\s+");
                    if (parts[0] == parts[1])
                    {
                        selfLoops++;
                        continue;
                    }
                    graph.AddEdge(parts[0], parts[1]);
                }

                metaInfo = GetBasicMetaInfo("RW-" + graphDirName.Substring(graphDirName.LastIndexOf('\\') + 1), new[] { graph });
                metaInfo["Url"] = url;
                metaInfo["Long Descr"] = longdescr;
                metaInfo["Category"] = category;
                metaInfo["BipPerMeta"] = bipPerMeta.ToString();
                metaInfo["BipPerComments"] = bipPerComments.ToString();
                metaInfo["Column Numbers"] = columnNumbers.ToString();
                metaInfo["Comments"] = graphComment;
                metaInfo["Self Loops"] = selfLoops.ToString();

                graphs = Enumerable.Range(0, THREADS).AsParallel().Select(i => graph.Clone()).ToArray();

                return true;
            }
            catch (Exception ex)
            {
                WriteErr($"Fatal Exception in graph {graphDirName}: {ex}");
                graphs = null;
                metaInfo = null;
                return false;
            }
        }

        // This method is the heart of the program, and is mostly working well from the previous version. Have
        // to modify it somewhat, but I think the length of it is necessary, the different sampling methods
        // have to all be run together for efficiency.
        static string CollectSamplesAndGetResults(Graph[] graphs, Dictionary<string, string> metaInfo)
        {
            double RV = double.Parse(metaInfo["RV"]);
            double RN = double.Parse(metaInfo["RN"]);
            double RE = double.Parse(metaInfo["RE"]);
            double IRN = double.Parse(metaInfo["IRN"]);
            double IRE = double.Parse(metaInfo["IRE"]);

            var max_samples = SamplesToTake(graphs.First().Vertices.Count());

            // Use max_samples + 1 so that each index matches the sample it actually is
            double[] predictedRv = new double[max_samples + 1];
            double[] predictedRn = new double[max_samples + 1];
            double[] predictedRe = new double[max_samples + 1];
            double[] predictedIrn = new double[max_samples + 1];
            double[] predictedIre = new double[max_samples + 1];
            double[] predictedRvRn = new double[max_samples + 1];
            double[] predictedFullEdge = new double[max_samples + 1];

            double[] actualRvAverages = new double[max_samples + 1];
            double[] actualRnAverages = new double[max_samples + 1];
            double[] actualReAverages = new double[max_samples + 1];
            double[] actualIrnAverages = new double[max_samples + 1];
            double[] actualIreAverages = new double[max_samples + 1];
            double[] actualRvRnAverages = new double[max_samples + 1];
            double[] actualFullEdgeAverages = new double[max_samples + 1];
            // Values when duplicate vertices are excluded:
            double[] actualUnqRvAverages = new double[max_samples + 1];
            double[] actualUnqRnAverages = new double[max_samples + 1];
            double[] actualUnqReAverages = new double[max_samples + 1];
            double[] actualUnqIrnAverages = new double[max_samples + 1];
            double[] actualUnqIreAverages = new double[max_samples + 1];
            double[] actualUnqRvRnAverages = new double[max_samples + 1];
            double[] actualUnqFullEdgeAverages = new double[max_samples + 1];
            double[] actualUnqRvSamplesKeptAverages = new double[max_samples + 1];
            double[] actualUnqRnSamplesKeptAverages = new double[max_samples + 1];
            double[] actualUnqReSamplesKeptAverages = new double[max_samples + 1];
            double[] actualUnqIrnSamplesKeptAverages = new double[max_samples + 1];
            double[] actualUnqIreSamplesKeptAverages = new double[max_samples + 1];
            double[] actualUnqRvRnSamplesKeptAverages = new double[max_samples + 1];
            double[] actualUnqFullEdgeSamplesKeptAverages = new double[max_samples + 1];
            double[] actualRvMaxAverages = new double[max_samples + 1];
            double[] actualRnMaxAverages = new double[max_samples + 1];
            double[] actualReMaxAverages = new double[max_samples + 1];
            double[] actualIrnMaxAverages = new double[max_samples + 1];
            double[] actualIreMaxAverages = new double[max_samples + 1];
            double[] actualRvRnMaxAverages = new double[max_samples + 1];
            double[] actualFullEdgeMaxAverages = new double[max_samples + 1];

            // Fill in the predictions
            for (int i = 1; i <= max_samples; i++)
            {
                predictedRv[i] = predictedRv[i - 1] + RV;
                predictedRn[i] = predictedRn[i - 1] + RN;
                predictedRe[i] = predictedRe[i - 1] + RE;
                predictedIrn[i] = predictedIrn[i - 1] + IRN;
                predictedIre[i] = predictedIre[i - 1] + IRE;

                // For predicted RvRn we have to first add RV, then RN
                if (i % 2 == 1) // odd number, add the first vertex
                {
                    predictedRvRn[i] = predictedRvRn[i - 1] + RV;
                }
                else // even, add the neighbor
                {
                    predictedRvRn[i] = predictedRvRn[i - 1] + RN;
                }
                // For predicted full edge it's really the same as predicted RE
                predictedFullEdge[i] = predictedFullEdge[i - 1] + RE;
            }

            // Collect actual samples from all of the graphs:
            var rvVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var rnVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var reVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var irnVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var ireVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var rvRnVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var fullEdgeVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqRvVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqRnVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqReVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqIrnVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqIreVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqRvRnVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqFullEdgeVals = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqRvSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqRnSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqReSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqIrnSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqIreSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqRvRnSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var unqFullEdgeSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[max_samples + 1]).ToArray();
            var rvMaxVals = Enumerable.Range(0, THREADS).Select(i => new double[max_samples + 1]).ToArray();
            var rnMaxVals = Enumerable.Range(0, THREADS).Select(i => new double[max_samples + 1]).ToArray();
            var reMaxVals = Enumerable.Range(0, THREADS).Select(i => new double[max_samples + 1]).ToArray();
            var irnMaxVals = Enumerable.Range(0, THREADS).Select(i => new double[max_samples + 1]).ToArray();
            var ireMaxVals = Enumerable.Range(0, THREADS).Select(i => new double[max_samples + 1]).ToArray();
            var rvRnMaxVals = Enumerable.Range(0, THREADS).Select(i => new double[max_samples + 1]).ToArray();
            var fullEdgeMaxVals = Enumerable.Range(0, THREADS).Select(i => new double[max_samples + 1]).ToArray();

            Parallel.For(0, THREADS, i =>
            {
                var graph = graphs[i];
                var rand = rands[i];

                Vertex lastSelectedVertex = null;
                Vertex lastSelectedNeighbor = null;
                Edge lastSelectedEdge = null;

                int currRvTotal = 0;
                int currRnTotal = 0;
                int currReTotal = 0;
                int currIrnTotal = 0;
                int currIreTotal = 0;
                int currRvRnTotal = 0;
                int currFullEdgeTotal = 0;
                int currUnqRvTotal = 0;
                int currUnqRnTotal = 0;
                int currUnqReTotal = 0;
                int currUnqIrnTotal = 0;
                int currUnqIreTotal = 0;
                int currUnqRvRnTotal = 0;
                int currUnqFullEdgeTotal = 0;
                double currRvMax = 0;
                double currRnMax = 0;
                double currReMax = 0;
                double currIrnMax = 0;
                double currIreMax = 0;
                double currRvRnMax = 0;
                double currFullEdgeMax = 0;

                // each method will keep a set to know what it already collected for the unique versions:
                HashSet<Vertex> rvVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> rnVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> reVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> irnVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> ireVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> rvRnVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> fullEdgeVerticesCollected = new HashSet<Vertex>();


                var maxDegreeOfGraph = graph.Vertices.Max(v => v.Degree);



                for (int currSample = 1; currSample <= max_samples; currSample++)
                {
                    // We will need a new vertex, neighbor, and edge every iteration:
                    var selectedVertex = graph.PositiveDegreeVertices.ChooseRandomElement(random: rand);
                    var selectedNeighbor = selectedVertex.Neighbors.ChooseRandomElement(random: rand);
                    var selectedEdge = graph.Edges.ChooseRandomElement(random: rand);

                    currRvTotal += selectedVertex.Degree;
                    currRvMax = Math.Max(currRvMax, (double)selectedVertex.Degree / maxDegreeOfGraph);
                    if (rvVerticesCollected.Add(selectedVertex))
                        currUnqRvTotal += selectedVertex.Degree;
                    currRnTotal += selectedNeighbor.Degree;
                    currRnMax = Math.Max(currRnMax, (double)selectedNeighbor.Degree / maxDegreeOfGraph);
                    if (rnVerticesCollected.Add(selectedNeighbor))
                        currUnqRnTotal += selectedNeighbor.Degree;
                    var selectedEdgeEndpoint = new Vertex[] { selectedEdge.v1, selectedEdge.v2 }.ChooseRandomElement(rand);
                    currReTotal += selectedEdgeEndpoint.Degree;
                    currReMax = Math.Max(currReMax, (double)selectedEdgeEndpoint.Degree / maxDegreeOfGraph);
                    if (reVerticesCollected.Add(selectedEdgeEndpoint))
                        currUnqReTotal += selectedEdgeEndpoint.Degree;
                    var maxDegreeVertex = selectedVertex.Degree > selectedNeighbor.Degree ? selectedVertex : selectedNeighbor;
                    currIrnTotal += maxDegreeVertex.Degree;
                    currIrnMax = Math.Max(currIrnMax, (double)maxDegreeVertex.Degree / maxDegreeOfGraph);
                    if (irnVerticesCollected.Add(maxDegreeVertex))
                        currUnqIrnTotal += maxDegreeVertex.Degree;
                    var maxEdgeEndpoint = selectedEdge.v1.Degree > selectedEdge.v2.Degree ? selectedEdge.v1 : selectedEdge.v2;
                    currIreTotal += maxEdgeEndpoint.Degree;
                    currIreMax = Math.Max(currIreMax, (double)maxEdgeEndpoint.Degree / maxDegreeOfGraph);
                    if (ireVerticesCollected.Add(maxEdgeEndpoint))
                        currUnqIreTotal += maxEdgeEndpoint.Degree;
                    // Now the ones where we add two vertices in two steps:
                    if (currSample % 2 == 1)
                    {
                        lastSelectedVertex = selectedVertex;
                        lastSelectedNeighbor = selectedNeighbor;
                        lastSelectedEdge = selectedEdge;

                        currRvRnTotal += lastSelectedVertex.Degree;
                        currRvRnMax = Math.Max(currRvRnMax, (double)lastSelectedVertex.Degree / maxDegreeOfGraph);
                        if (rvRnVerticesCollected.Add(lastSelectedVertex))
                            currUnqRvRnTotal += lastSelectedVertex.Degree;
                        currFullEdgeTotal += lastSelectedEdge.v1.Degree;
                        currFullEdgeMax = Math.Max(currFullEdgeMax, (double)lastSelectedEdge.v1.Degree / maxDegreeOfGraph);
                        if (fullEdgeVerticesCollected.Add(lastSelectedEdge.v1))
                            currUnqFullEdgeTotal += lastSelectedEdge.v1.Degree;
                    }
                    else
                    {
                        currRvRnTotal += lastSelectedNeighbor.Degree;
                        currRvRnMax = Math.Max(currRvRnMax, (double)lastSelectedNeighbor.Degree / maxDegreeOfGraph);
                        if (rvRnVerticesCollected.Add(lastSelectedNeighbor))
                            currUnqRvRnTotal += lastSelectedNeighbor.Degree;
                        currFullEdgeTotal += lastSelectedEdge.v2.Degree;
                        currFullEdgeMax = Math.Max(currFullEdgeMax, (double)lastSelectedEdge.v2.Degree / maxDegreeOfGraph);
                        if (fullEdgeVerticesCollected.Add(lastSelectedEdge.v2))
                            currUnqFullEdgeTotal += lastSelectedEdge.v2.Degree;
                    }
                    rvVals[i][currSample] = currRvTotal;
                    rnVals[i][currSample] = currRnTotal;
                    reVals[i][currSample] = currReTotal;
                    irnVals[i][currSample] = currIrnTotal;
                    ireVals[i][currSample] = currIreTotal;
                    rvRnVals[i][currSample] = currRvRnTotal;
                    fullEdgeVals[i][currSample] = currFullEdgeTotal;
                    unqRvVals[i][currSample] = currUnqRvTotal;
                    unqRnVals[i][currSample] = currUnqRnTotal;
                    unqReVals[i][currSample] = currUnqReTotal;
                    unqIrnVals[i][currSample] = currUnqIrnTotal; ;
                    unqIreVals[i][currSample] = currUnqIreTotal; ;
                    unqRvRnVals[i][currSample] = currUnqRvRnTotal; ;
                    unqFullEdgeVals[i][currSample] = currUnqFullEdgeTotal;
                    unqRvSamplesKept[i][currSample] = rvVerticesCollected.Count;
                    unqRnSamplesKept[i][currSample] = rnVerticesCollected.Count;
                    unqReSamplesKept[i][currSample] = reVerticesCollected.Count;
                    unqIrnSamplesKept[i][currSample] = irnVerticesCollected.Count;
                    unqIreSamplesKept[i][currSample] = ireVerticesCollected.Count;
                    unqRvRnSamplesKept[i][currSample] = rvRnVerticesCollected.Count;
                    unqFullEdgeSamplesKept[i][currSample] = fullEdgeVerticesCollected.Count;
                    rvMaxVals[i][currSample] = currRvMax;
                    rnMaxVals[i][currSample] = currRnMax;
                    reMaxVals[i][currSample] = currReMax;
                    irnMaxVals[i][currSample] = currIrnMax;
                    ireMaxVals[i][currSample] = currIreMax;
                    rvRnMaxVals[i][currSample] = currRvRnMax;
                    fullEdgeMaxVals[i][currSample] = currFullEdgeMax;
                }

            });

            // Collapse the values into averages:
            for (int i = 1; i <= max_samples; i++)
            {
                actualRvAverages[i] = rvVals.Select(arr => arr[i]).Average();
                actualRnAverages[i] = rnVals.Select(arr => arr[i]).Average();
                actualReAverages[i] = reVals.Select(arr => arr[i]).Average();
                actualIrnAverages[i] = irnVals.Select(arr => arr[i]).Average();
                actualIreAverages[i] = ireVals.Select(arr => arr[i]).Average();
                actualRvRnAverages[i] = rvRnVals.Select(arr => arr[i]).Average();
                actualFullEdgeAverages[i] = fullEdgeVals.Select(arr => arr[i]).Average();
                // Values when duplicate vertices are excluded:
                actualUnqRvAverages[i] = unqRvVals.Select(arr => arr[i]).Average();
                actualUnqRnAverages[i] = unqRnVals.Select(arr => arr[i]).Average();
                actualUnqReAverages[i] = unqReVals.Select(arr => arr[i]).Average();
                actualUnqIrnAverages[i] = unqIrnVals.Select(arr => arr[i]).Average();
                actualUnqIreAverages[i] = unqIreVals.Select(arr => arr[i]).Average();
                actualUnqRvRnAverages[i] = unqRvRnVals.Select(arr => arr[i]).Average();
                actualUnqFullEdgeAverages[i] = unqFullEdgeVals.Select(arr => arr[i]).Average();
                actualUnqRvSamplesKeptAverages[i] = unqRvSamplesKept.Select(arr => arr[i]).Average();
                actualUnqRnSamplesKeptAverages[i] = unqRnSamplesKept.Select(arr => arr[i]).Average();
                actualUnqReSamplesKeptAverages[i] = unqReSamplesKept.Select(arr => arr[i]).Average();
                actualUnqIrnSamplesKeptAverages[i] = unqIrnSamplesKept.Select(arr => arr[i]).Average();
                actualUnqIreSamplesKeptAverages[i] = unqIreSamplesKept.Select(arr => arr[i]).Average();
                actualUnqRvRnSamplesKeptAverages[i] = unqRvRnSamplesKept.Select(arr => arr[i]).Average();
                actualUnqFullEdgeSamplesKeptAverages[i] = unqFullEdgeSamplesKept.Select(arr => arr[i]).Average();
                actualRvMaxAverages[i] = rvMaxVals.Select(arr => arr[i]).Average();
                actualRnMaxAverages[i] = rnMaxVals.Select(arr => arr[i]).Average();
                actualReMaxAverages[i] = reMaxVals.Select(arr => arr[i]).Average();
                actualIrnMaxAverages[i] = irnMaxVals.Select(arr => arr[i]).Average();
                actualIreMaxAverages[i] = ireMaxVals.Select(arr => arr[i]).Average();
                actualRvRnMaxAverages[i] = rvRnMaxVals.Select(arr => arr[i]).Average();
                actualFullEdgeMaxAverages[i] = fullEdgeMaxVals.Select(arr => arr[i]).Average();
            }

            StringBuilder results = new StringBuilder();

            // Write the results to the file:
            results.AppendLine($"x_vals\t{String.Join("\t", Enumerable.Range(1, max_samples))}");
            results.AppendLine($"PredictedRv\t{String.Join("\t", predictedRv.Skip(1))}");
            results.AppendLine($"PredictedRn\t{String.Join("\t", predictedRn.Skip(1))}");
            results.AppendLine($"PredictedRe\t{String.Join("\t", predictedRe.Skip(1))}");
            results.AppendLine($"PredictedIrn\t{String.Join("\t", predictedIrn.Skip(1))}");
            results.AppendLine($"PredictedIre\t{String.Join("\t", predictedIre.Skip(1))}");
            results.AppendLine($"PredictedRvRn\t{String.Join("\t", predictedRvRn.Skip(1))}");
            results.AppendLine($"PredictedFullEdge\t{String.Join("\t", predictedFullEdge.Skip(1))}");
            results.AppendLine($"ActualRv\t{String.Join("\t", actualRvAverages.Skip(1))}");
            results.AppendLine($"ActualRn\t{String.Join("\t", actualRnAverages.Skip(1))}");
            results.AppendLine($"ActualRe\t{String.Join("\t", actualReAverages.Skip(1))}");
            results.AppendLine($"ActualIrn\t{String.Join("\t", actualIrnAverages.Skip(1))}");
            results.AppendLine($"ActualIre\t{String.Join("\t", actualIreAverages.Skip(1))}");
            results.AppendLine($"ActualRvRn\t{String.Join("\t", actualRvRnAverages.Skip(1))}");
            results.AppendLine($"ActualFullEdge\t{String.Join("\t", actualFullEdgeAverages.Skip(1))}");
            results.AppendLine($"ActualUnqRv\t{String.Join("\t", actualUnqRvAverages.Skip(1))}");
            results.AppendLine($"ActualUnqRn\t{String.Join("\t", actualUnqRnAverages.Skip(1))}");
            results.AppendLine($"ActualUnqRe\t{String.Join("\t", actualUnqReAverages.Skip(1))}");
            results.AppendLine($"ActualUnqIrn\t{String.Join("\t", actualUnqIrnAverages.Skip(1))}");
            results.AppendLine($"ActualUnqIre\t{String.Join("\t", actualUnqIreAverages.Skip(1))}");
            results.AppendLine($"ActualUnqRvRn\t{String.Join("\t", actualUnqRvRnAverages.Skip(1))}");
            results.AppendLine($"ActualUnqFullEdge\t{String.Join("\t", actualUnqFullEdgeAverages.Skip(1))}");
            results.AppendLine($"ActualUnqRvSamples\t{String.Join("\t", actualUnqRvSamplesKeptAverages.Skip(1))}");
            results.AppendLine($"ActualUnqRnSamples\t{String.Join("\t", actualUnqRnSamplesKeptAverages.Skip(1))}");
            results.AppendLine($"ActualUnqReSamples\t{String.Join("\t", actualUnqReSamplesKeptAverages.Skip(1))}");
            results.AppendLine($"ActualUnqIrnSamples\t{String.Join("\t", actualUnqIrnSamplesKeptAverages.Skip(1))}");
            results.AppendLine($"ActualUnqIreSamples\t{String.Join("\t", actualUnqIreSamplesKeptAverages.Skip(1))}");
            results.AppendLine($"ActualUnqRvRnSamples\t{String.Join("\t", actualUnqRvRnSamplesKeptAverages.Skip(1))}");
            results.AppendLine($"ActualUnqFullEdgeSamples\t{String.Join("\t", actualUnqFullEdgeSamplesKeptAverages.Skip(1))}");

            results.AppendLine($"ActualRvMax\t{String.Join("\t", actualRvMaxAverages.Skip(1))}");
            results.AppendLine($"ActualRnMax\t{String.Join("\t", actualRnMaxAverages.Skip(1))}");
            results.AppendLine($"ActualReMax\t{String.Join("\t", actualReMaxAverages.Skip(1))}");
            results.AppendLine($"ActualIrnMax\t{String.Join("\t", actualIrnMaxAverages.Skip(1))}");
            results.AppendLine($"ActualIreMax\t{String.Join("\t", actualIreMaxAverages.Skip(1))}");
            results.AppendLine($"ActualRvRnMax\t{String.Join("\t", actualRvRnMaxAverages.Skip(1))}");
            results.AppendLine($"ActualFullEdgeMax\t{String.Join("\t", actualFullEdgeMaxAverages.Skip(1))}");

            results.Insert(0, GetMetaInfoString(metaInfo));
            results.Insert(0, PREAMBLE);

            return results.ToString();
        }

    }
}
