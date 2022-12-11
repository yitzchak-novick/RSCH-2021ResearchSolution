using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UtilsYN;

namespace TotalDegreesOverCostBySamplesTaken_01
{
    class Program
    {
        /* YN 2/16/21
        * For this experiment, we are assuming "total degrees" is the measure of success, so if we collect k vertices, their
        * total degrees added up is the measure of success. This part was already run and both methods gains are linear 
        * and precisely equal to the prediction you'd get from RN/RV.
        * 
        * The new part we will add here is we will compare the results when we don't collect the same vertex twice.
        * 
        * We will completely ignore cost for this code, the result files will be changed into plots in Python so we'll
        * simply allow the Python code to take these results and divide them by whatever costs we'll define there. The only
        * place where this will fall short is when we'll consider costs that vary by the degree of the vertex being examined,
        * maybe we'll define a cost for learning a neighbor and for some reason decide this cost is different based on the
        * degree of the vertex.
        */
        
        private static readonly string KOBLENZ_BASE_DIR = @"D:\Graphs\Koblenz\AllOrigFiles\konect.cc\files\"; // location of the files on my workstation computer

        // will be set by the program, default values just for testing
        static int N = 3000;
        static int M = 8;
        static readonly int THREADS = 50;
        static int MAX_SAMPLES => N; // Not sure how many samples to take, probably some significant fraction of N, but we'll start with N for now

        enum GraphType { ER, BA, RW };
        static GraphType graphType = GraphType.BA;
        static string graphName = ""; // for real world graphs
        static string currGraphDir = ""; // the directory where the graph files are found, we'll loop through and use this global variable to know what we're up to
        // Bit of a "retrohack", for synthetic graphs we have to evaluate them to get their metadata, for RWs we get the meta data as we read them from the files, so
        // we'll have a global string that will be populated with the metadata from the files and the code that determines RV/RN/etc. will write this string to the preamble
        static string rwGraphMetaInformation = "";
        static string FILENAME => graphType == GraphType.RW ? $"Results\\{graphType}_{graphName.Replace(" ", "-")}_results.tsv" : $"Results\\{graphType}_N_{N}_M_{M}_results.tsv";

        // The individual experiments will make use of these variables, but making them global:
        static double RV;
        static double RN;
        static double RE;
        static double IRN;
        static double IRE;

        static double[] predictedRv = new double[MAX_SAMPLES + 1];
        static double[] predictedRn = new double[MAX_SAMPLES + 1];
        static double[] predictedRe = new double[MAX_SAMPLES + 1];
        static double[] predictedIrn = new double[MAX_SAMPLES + 1];
        static double[] predictedIre = new double[MAX_SAMPLES + 1];
        static double[] predictedRvRn = new double[MAX_SAMPLES + 1];
        static double[] predictedFullEdge = new double[MAX_SAMPLES + 1]; // Really not different than RE, we add the endpoints one at a time so we predict an addition of RE every time

        static double[] actualRvAverages = new double[MAX_SAMPLES + 1];
        static double[] actualRnAverages = new double[MAX_SAMPLES + 1];
        static double[] actualReAverages = new double[MAX_SAMPLES + 1];
        static double[] actualIrnAverages = new double[MAX_SAMPLES + 1];
        static double[] actualIreAverages = new double[MAX_SAMPLES + 1];
        static double[] actualRvRnAverages = new double[MAX_SAMPLES + 1];
        static double[] actualFullEdgeAverages = new double[MAX_SAMPLES + 1];
        // Values when duplicate vertices are excluded:
        static double[] actualUnqRvAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqRnAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqReAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqIrnAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqIreAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqRvRnAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqFullEdgeAverages = new double[MAX_SAMPLES + 1];
        // Track how many samples were KEPT to know how many times C_s was paid
        static double[] actualUnqRvSamplesKeptAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqRnSamplesKeptAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqReSamplesKeptAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqIrnSamplesKeptAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqIreSamplesKeptAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqRvRnSamplesKeptAverages = new double[MAX_SAMPLES + 1];
        static double[] actualUnqFullEdgeSamplesKeptAverages = new double[MAX_SAMPLES + 1];
        // Also track the max degree found over the max of the graph
        static double[] actualRvMaxAverages = new double[MAX_SAMPLES + 1];
        static double[] actualRnMaxAverages = new double[MAX_SAMPLES + 1];
        static double[] actualReMaxAverages = new double[MAX_SAMPLES + 1];
        static double[] actualIrnMaxAverages = new double[MAX_SAMPLES + 1];
        static double[] actualIreMaxAverages = new double[MAX_SAMPLES + 1];
        static double[] actualRvRnMaxAverages = new double[MAX_SAMPLES + 1];
        static double[] actualFullEdgeMaxAverages = new double[MAX_SAMPLES + 1];
       
        static Graph[] graphs = new Graph[THREADS];
        static Random[] rands = TSRandom.ArrayOfRandoms(THREADS);
        static string DTS => UtilsYN.Utils.DTS;

        static void Main(string[] args)
        {
            if (!Directory.Exists("Results"))
                Directory.CreateDirectory("Results");
            if (File.Exists(ERRFILENAME))
                File.Delete(ERRFILENAME);

            Console.WriteLine($"Starting program at {DTS}");

            List<Action> RwActions = new List<Action>();
            List<Action> SyntheticGraphActions = new List<Action>();

            graphType = GraphType.RW;
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
                RwActions.Add(() =>
                {
                    graphType = GraphType.RW;
                    currGraphDir = graphDir.Name;
                    if (!File.Exists(FILENAME))
                    {

                        Run();
                    }
                });
            }

            graphName = "";

            foreach (var n in new[] { 100, 500, 1000, 2000, 5000, 7500, 10000 })
                foreach (var m in new[] { 2, 5, 8, 15, 25, 50, 100 })
                {
                    if (m >= n / 2) continue;
                    SyntheticGraphActions.Add(() =>
                    {
                        foreach (var gType in new[] { GraphType.ER, GraphType.BA })
                        {
                            N = n;
                            M = m;
                            graphType = gType;
                            graphName = "";
                            if (!File.Exists(FILENAME) || graphType == GraphType.ER) // redoing all ER, have to take this out...
                            {

                                Run();
                            }
                        }
                    });
                }

            for (int i = 0; i < Math.Max(RwActions.Count, SyntheticGraphActions.Count); i++)
            {
                if (i < RwActions.Count)
                    RwActions[i]();
                if (i < SyntheticGraphActions.Count)
                    SyntheticGraphActions[i]();
            }

            Console.WriteLine($"Finished program at {DTS}, press any key to exit");
            Console.ReadKey();
        }

        static int totalRuns = 1;
        static void Run()
        {
            Console.WriteLine($"Starting run {totalRuns}, Type: {graphType}, {(String.IsNullOrWhiteSpace(graphName) ? string.Empty : graphName + ", ")}N: {N}, M: {M}, Max Samples: {MAX_SAMPLES}, at {DTS}");

            InitializeGraphs();
            if (graphs[0] == null)
                return;
            WriteFilePreamble();
            GetExpectedValuesForGraphs();
            CollectSamplesAndWriteResults();

            Console.WriteLine($"Finished run {totalRuns++}, at {DTS}");
        }

        /// <summary>
        /// A preamble for the file that explains the experiment, hopefully we will be able to read this in the file itself to 
        /// avoid having to look back at the code to understand what the results are. Lines will start with % so we can write
        /// the Python code to ignore them.
        /// </summary>
        static string Preamble =
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
        static void WriteFilePreamble()
        {
            File.WriteAllText(FILENAME, Preamble);
        }

        static void InitializeGraphs()
        {
            switch (graphType)
            {
                case GraphType.BA:
                    Parallel.For(0, THREADS, i =>
                    {
                        int count = 0;
                        //do
                        {
                            //Console.WriteLine($"{++count} attempt(s) for BA graph N: {N}, M: {M}");
                            graphs[i] = Graph.NewBaGraph(N, M, random: rands[i]);
                        }// while (graphs[i].PositiveDegreeVertices.Count(v1 => v1.Degree == graphs[i].PositiveDegreeVertices.Max(v2 => v2.Degree)) > 1);
                    });
                    break;
                case GraphType.ER:
                    Parallel.For(0, THREADS, i =>
                    {
                        int count = 0;
                        //do
                        {
                            //Console.WriteLine($"{++count} attempt(s) for BA graph N: {N}, M: {M}");
                            graphs[i] = Graph.NewErGraphFromBaM(N, M, random: rands[i]);
                        } //while (graphs[i].PositiveDegreeVertices.Count(v1 => v1.Degree == graphs[i].PositiveDegreeVertices.Max(v2 => v2.Degree)) > 1);
                    });
                    break;
                case GraphType.RW:
                    Graph koblenzGraph = getKoblenzGraphFromDir(currGraphDir);
                    if (koblenzGraph == null) // couldn't parse for some reason
                    {
                        Console.WriteLine($"{currGraphDir} failed to parse, skipping {DTS}");
                        graphs[0] = null;
                        return;
                    }
                    Parallel.For(0, THREADS, i => graphs[i] = koblenzGraph.Clone());
                    break;
                default:
                    // eventually will probably want to run this for at least real-world graphs also...
                    throw new Exception($"No code for graph type: {graphType}");
            }

        }

        static Graph getKoblenzGraphFromDir(string graphdir)
        {
            Graph graph = null;
            string meta = "";
            try
            {
                int selfLoops = 0;
                // The following are fields that (hopefully) will be found in the metainformation
                // and/or comments of the files
                string url; // the "url" from the metafile
                string longdescr; // the "long-description" from the metafile
                string category; // "category" in the metafile
                bool bipPerMeta; // does the word "bipartite" appear in the metafile
                bool bipPerComments = false; // does the first comment(s) in the file have the text "bip"
                int columnNumbers; // How many columns in the graph file, more than 2 usually indicates a weighted graph
                // NOTE: Also might be of interest to know if the graph is directed, for now will not try to parse this
                // in code, just rely on the inclusion of description and comment
                string graphComment = ""; // the first line of the graph file if it is a comment

                var dirfiles = new DirectoryInfo(KOBLENZ_BASE_DIR + graphdir).GetFiles();

                var metafile = dirfiles.FirstOrDefault(fi => fi.Name.ToLower().StartsWith("meta"));
                if (metafile == null)
                {
                    WriteErr($"Couldn't find meta file for {graphdir}");
                    meta = $"Couldn't find meta file for {graphdir}\t";
                }

                var metainfo = File.ReadAllLines(metafile.FullName).Select(l => Regex.Split(l, @"\:\s+"));
                longdescr = metainfo.FirstOrDefault(mi => mi[0].ToLower() == "long-description")?[1] ?? "*** NOT FOUND ***";
                url = metainfo.FirstOrDefault(mi => mi[0].ToLower() == "url")?[1] ?? "*** NOT FOUND ***";
                category = metainfo.FirstOrDefault(mi => mi[0].ToLower() == "category")?[1] ?? "*** NOT FOUND ***";
                bipPerMeta = File.ReadAllText(metafile.FullName).ToLower().Contains("bipartite");

                var graphfile = dirfiles.FirstOrDefault(fi => fi.Name.ToLower().StartsWith("out"));
                if (graphfile == null)
                    throw new Exception($"Couldn't find graph file for {graphdir}");

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

                // Need to set these global values so that the number of samples taken will be correct.
                N = graph.Vertices.Count();
                M = graph.Edges.Count();
                double assort = graph.Assortativity();
                double afi = graph.Vertices.Average(v => v.Neighbors.Average(n => n.Degree) / v.Degree);
                double gfi = graph.Vertices.Average(v => Math.Log(v.Neighbors.Average(n => n.Degree) / v.Degree));
                var rv = graph.Vertices.Average(v => v.Degree);
                var rn = graph.Vertices.Average(v => v.Neighbors.Average(n => n.Degree));
                var re = graph.Edges.Average(e => (e.v1.Degree + e.v2.Degree) / 2.0);
                var irn = graph.Vertices.Average(v => v.Neighbors.Average(n => Math.Max(v.Degree, n.Degree)));
                var ire = graph.Edges.Average(e => Math.Max(e.v1.Degree, e.v2.Degree));

                string results = $"{graphdir}\t" +
                                 $"{url}\t" +
                                 $"{longdescr}\t" +
                                 $"{category}\t" +
                                 $"BipPerMeta:\t{bipPerMeta}\t" +
                                 $"BipPerComments\t{bipPerComments}\t" +
                                 $"ColNumbers:\t{columnNumbers}\t" +
                                 $"Comment:\t{graphComment}\t" +
                                 $"N:\t{N}\t" +
                                 $"E:\t{M}\t" +
                                 $"SelfLoops:\t{selfLoops}\t" +
                                 $"Assort:\t{Math.Round(assort, 3)}\t" +
                                 $"Afi:\t{Math.Round(afi, 3)}\t" +
                                 $"Gfi:\t{Math.Round(gfi, 3)}\t" +
                                 $"RV:\t{Math.Round(rv, 3)}\t" +
                                 $"RN:\t{Math.Round(rn, 3)}\t" +
                                 $"RE:\t{Math.Round(re, 3)}\t" +
                                 $"IRN:\t{Math.Round(irn, 3)}\t" +
                                 $"IRE:\t{Math.Round(ire, 3)}\t" +
                                 $"Avg Max-Degree Vertices: {graph.PositiveDegreeVertices.Count(v1 => v1.Degree == graph.PositiveDegreeVertices.Max(v2 => v2.Degree))}\n";

                meta += results;
                rwGraphMetaInformation = meta;
            }
            catch (Exception ex)
            {
                WriteErr("Fatal exception in graph " + ex);
                return null;
            }
            graphName = graphdir;
            return graph;
        }

        // Our predictions are based on RV and RN so they will be calculated once for each experiment
        static void GetExpectedValuesForGraphs()
        {
            var RVs = new double[THREADS];
            var RNs = new double[THREADS];
            var REs = new double[THREADS];
            var IRNs = new double[THREADS];
            var IREs = new double[THREADS];

            Parallel.For(0, THREADS, i => RVs[i] = graphs[i].RV);
            Parallel.For(0, THREADS, i => RNs[i] = graphs[i].RN);
            Parallel.For(0, THREADS, i => REs[i] = graphs[i].RE);
            Parallel.For(0, THREADS, i => IRNs[i] = graphs[i].IRN);
            Parallel.For(0, THREADS, i => IREs[i] = graphs[i].IRE);

            RV = RVs.Average();
            RN = RNs.Average();
            RE = REs.Average();
            IRN = IRNs.Average();
            IRE = IREs.Average();

            // Note the average number of vertices in each graph are of max-degree, may be relevant to consider this in the results..
            var maxDegreeVerticesPerGraph = graphs.Average(g => g.PositiveDegreeVertices.Count(v1 => v1.Degree == g.PositiveDegreeVertices.Max(v2 => v2.Degree)));

            Console.WriteLine($"{THREADS} {graphType} Graphs N: {N} , M:{M} , RV: {RV} , RN: {RN} , RN/RV: {RN / RV} , RE: {RE} , RE/RV: {RE / RV} , IRN: {IRN} , IRE: {IRE} , Avg Max-Degree Vertices: {maxDegreeVerticesPerGraph} , at {DTS}");

            File.AppendAllText(FILENAME, $"% Averages over {THREADS} {graphType} Graphs N: {N} , M: {M} , RV: {RV} , RN: {RN} , RN/RV: {RN / RV} , RE: {RE} , RE/RV: {RE / RV} , IRN: {IRN} , IRE: {IRE} , Avg Max-Degree Vertices: {maxDegreeVerticesPerGraph}\n");
            if (graphType == GraphType.RW) // RW graphs have completely other metadata
            {
                File.AppendAllText(FILENAME, $"% {rwGraphMetaInformation}");
            }
        }

        static object resultsFileLock = new object(); // need to lock when individiual threads add a message
        static void CollectSamplesAndWriteResults()
        {
            // Reset the results for the new experiment:
            predictedRv = new double[MAX_SAMPLES + 1];
            predictedRn = new double[MAX_SAMPLES + 1];
            predictedRe = new double[MAX_SAMPLES + 1];
            predictedIrn = new double[MAX_SAMPLES + 1];
            predictedIre = new double[MAX_SAMPLES + 1];
            predictedRvRn = new double[MAX_SAMPLES + 1];
            predictedFullEdge = new double[MAX_SAMPLES + 1];

            actualRvAverages = new double[MAX_SAMPLES + 1];
            actualRnAverages = new double[MAX_SAMPLES + 1];
            actualReAverages = new double[MAX_SAMPLES + 1];
            actualIrnAverages = new double[MAX_SAMPLES + 1];
            actualIreAverages = new double[MAX_SAMPLES + 1];
            actualRvRnAverages = new double[MAX_SAMPLES + 1];
            actualFullEdgeAverages = new double[MAX_SAMPLES + 1];
            // Values when duplicate vertices are excluded:
            actualUnqRvAverages = new double[MAX_SAMPLES + 1];
            actualUnqRnAverages = new double[MAX_SAMPLES + 1];
            actualUnqReAverages = new double[MAX_SAMPLES + 1];
            actualUnqIrnAverages = new double[MAX_SAMPLES + 1];
            actualUnqIreAverages = new double[MAX_SAMPLES + 1];
            actualUnqRvRnAverages = new double[MAX_SAMPLES + 1];
            actualUnqFullEdgeAverages = new double[MAX_SAMPLES + 1];
            actualUnqRvSamplesKeptAverages = new double[MAX_SAMPLES + 1];
            actualUnqRnSamplesKeptAverages = new double[MAX_SAMPLES + 1];
            actualUnqReSamplesKeptAverages = new double[MAX_SAMPLES + 1];
            actualUnqIrnSamplesKeptAverages = new double[MAX_SAMPLES + 1];
            actualUnqIreSamplesKeptAverages = new double[MAX_SAMPLES + 1];
            actualUnqRvRnSamplesKeptAverages = new double[MAX_SAMPLES + 1];
            actualUnqFullEdgeSamplesKeptAverages = new double[MAX_SAMPLES + 1];
            actualRvMaxAverages = new double[MAX_SAMPLES + 1];
            actualRnMaxAverages = new double[MAX_SAMPLES + 1];
            actualReMaxAverages = new double[MAX_SAMPLES + 1];
            actualIrnMaxAverages = new double[MAX_SAMPLES + 1];
            actualIreMaxAverages = new double[MAX_SAMPLES + 1];
            actualRvRnMaxAverages = new double[MAX_SAMPLES + 1];
            actualFullEdgeMaxAverages = new double[MAX_SAMPLES + 1];

            // Fill in the predictions
            for (int i = 1; i <= MAX_SAMPLES; i++)
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
            var rvVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var rnVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var reVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var irnVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var ireVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var rvRnVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var fullEdgeVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqRvVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqRnVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqReVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqIrnVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqIreVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqRvRnVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqFullEdgeVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqRvSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqRnSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqReSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqIrnSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqIreSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqRvRnSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var unqFullEdgeSamplesKept = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var rvMaxVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var rnMaxVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var reMaxVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var irnMaxVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var ireMaxVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var rvRnMaxVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray();
            var fullEdgeMaxVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_SAMPLES + 1]).ToArray(); 
            
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
                int currRvMax = 0;
                int currRnMax = 0;
                int currReMax = 0;
                int currIrnMax = 0;
                int currIreMax = 0;
                int currRvRnMax = 0;
                int currFullEdgeMax = 0;

                // each method will keep a set to know what it already collected for the unique versions:
                HashSet<Vertex> rvVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> rnVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> reVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> irnVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> ireVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> rvRnVerticesCollected = new HashSet<Vertex>();
                HashSet<Vertex> fullEdgeVerticesCollected = new HashSet<Vertex>();

                
                var maxDegreeOfGraph = graph.Vertices.Max(v => v.Degree);
                /*if (graph.Vertices.Count(v => v.Degree == maxDegreeOfGraph) > 1)
                {
                    string errMsg = "";
                    if (graphType == GraphType.RW)
                        errMsg = $"{graphType}, {graphName}, n: {N}, m: {M}, has {graph.Vertices.Count(v => v.Degree == maxDegreeOfGraph)} Max Degree vertices degree: {maxDegreeOfGraph}";
                    else
                        errMsg = $"{graphType} n: {N}, m: {M}, has {graph.Vertices.Count(v => v.Degree == maxDegreeOfGraph)} Max Degree vertices degree: {maxDegreeOfGraph}";

                    if (graphType != GraphType.RW || i == 0)
                    {
                        WriteErr(errMsg);
                        lock (resultsFileLock)
                        {
                            File.AppendAllText(FILENAME, "% *** WARNING *** " + errMsg + "\n");
                        }
                    }
                }*/
                

                for (int currSample = 1; currSample <= MAX_SAMPLES; currSample++)
                {
                    // We will need a new vertex, neighbor, and edge every iteration:
                    var selectedVertex = graph.PositiveDegreeVertices.ChooseRandomElement(random: rand);
                    var selectedNeighbor = selectedVertex.Neighbors.ChooseRandomElement(random: rand);
                    var selectedEdge = graph.Edges.ChooseRandomElement(random: rand);

                    currRvTotal += selectedVertex.Degree;
                    currRvMax = Math.Max(currRvMax, selectedVertex.Degree / maxDegreeOfGraph);
                    if (rvVerticesCollected.Add(selectedVertex))
                        currUnqRvTotal += selectedVertex.Degree;
                    currRnTotal += selectedNeighbor.Degree;
                    currRnMax = Math.Max(currRnMax, selectedNeighbor.Degree / maxDegreeOfGraph);
                    if (rnVerticesCollected.Add(selectedNeighbor))
                        currUnqRnTotal += selectedNeighbor.Degree;
                    var selectedEdgeEndpoint = new Vertex[] { selectedEdge.v1, selectedEdge.v2 }.ChooseRandomElement(rand);
                    currReTotal += selectedEdgeEndpoint.Degree;
                    currReMax = Math.Max(currReMax, selectedEdgeEndpoint.Degree / maxDegreeOfGraph);
                    if (reVerticesCollected.Add(selectedEdgeEndpoint))
                        currUnqReTotal += selectedEdgeEndpoint.Degree;
                    var maxDegreeVertex = selectedVertex.Degree > selectedNeighbor.Degree ? selectedVertex : selectedNeighbor;
                    currIrnTotal += maxDegreeVertex.Degree;
                    currIrnMax = Math.Max(currIrnMax, maxDegreeVertex.Degree / maxDegreeOfGraph);
                    if (irnVerticesCollected.Add(maxDegreeVertex))
                        currUnqIrnTotal += maxDegreeVertex.Degree;
                    var maxEdgeEndpoint = selectedEdge.v1.Degree > selectedEdge.v2.Degree ? selectedEdge.v1 : selectedEdge.v2;
                    currIreTotal += maxEdgeEndpoint.Degree;
                    currIreMax = Math.Max(currIreMax, maxEdgeEndpoint.Degree / maxDegreeOfGraph);
                    if (ireVerticesCollected.Add(maxEdgeEndpoint))
                        currUnqIreTotal += maxEdgeEndpoint.Degree;
                    // Now the ones where we add two vertices in two steps:
                    if (currSample % 2 == 1)
                    {
                        lastSelectedVertex = selectedVertex;
                        lastSelectedNeighbor = selectedNeighbor;
                        lastSelectedEdge = selectedEdge;

                        currRvRnTotal += lastSelectedVertex.Degree;
                        currRvRnMax = Math.Max(currRvRnMax, lastSelectedVertex.Degree / maxDegreeOfGraph);
                        if (rvRnVerticesCollected.Add(lastSelectedVertex))
                            currUnqRvRnTotal += lastSelectedVertex.Degree;
                        currFullEdgeTotal += lastSelectedEdge.v1.Degree;
                        currFullEdgeMax = Math.Max(currFullEdgeMax, lastSelectedEdge.v1.Degree / maxDegreeOfGraph);
                        if (fullEdgeVerticesCollected.Add(lastSelectedEdge.v1))
                            currUnqFullEdgeTotal += lastSelectedEdge.v1.Degree;
                    }
                    else
                    {
                        currRvRnTotal += lastSelectedNeighbor.Degree;
                        currRvRnMax = Math.Max(currRvRnMax, lastSelectedNeighbor.Degree / maxDegreeOfGraph);
                        if (rvRnVerticesCollected.Add(lastSelectedNeighbor))
                            currUnqRvRnTotal += lastSelectedNeighbor.Degree;
                        currFullEdgeTotal += lastSelectedEdge.v2.Degree;
                        currFullEdgeMax = Math.Max(currFullEdgeMax, lastSelectedEdge.v2.Degree / maxDegreeOfGraph);
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
            for (int i = 1; i <= MAX_SAMPLES; i++)
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

            // Write the results to the file:
            File.AppendAllText(FILENAME, $"x_vals\t{String.Join("\t", Enumerable.Range(1, MAX_SAMPLES))}\n");
            File.AppendAllText(FILENAME, $"PredictedRv\t{String.Join("\t", predictedRv.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"PredictedRn\t{String.Join("\t", predictedRn.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"PredictedRe\t{String.Join("\t", predictedRe.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"PredictedIrn\t{String.Join("\t", predictedIrn.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"PredictedIre\t{String.Join("\t", predictedIre.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"PredictedRvRn\t{String.Join("\t", predictedRvRn.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"PredictedFullEdge\t{String.Join("\t", predictedFullEdge.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualRv\t{String.Join("\t", actualRvAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualRn\t{String.Join("\t", actualRnAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualRe\t{String.Join("\t", actualReAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualIrn\t{String.Join("\t", actualIrnAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualIre\t{String.Join("\t", actualIreAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualRvRn\t{String.Join("\t", actualRvRnAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualFullEdge\t{String.Join("\t", actualFullEdgeAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqRv\t{String.Join("\t", actualUnqRvAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqRn\t{String.Join("\t", actualUnqRnAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqRe\t{String.Join("\t", actualUnqReAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqIrn\t{String.Join("\t", actualUnqIrnAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqIre\t{String.Join("\t", actualUnqIreAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqRvRn\t{String.Join("\t", actualUnqRvRnAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqFullEdge\t{String.Join("\t", actualUnqFullEdgeAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqRvSamples\t{String.Join("\t", actualUnqRvSamplesKeptAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqRnSamples\t{String.Join("\t", actualUnqRnSamplesKeptAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqReSamples\t{String.Join("\t", actualUnqReSamplesKeptAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqIrnSamples\t{String.Join("\t", actualUnqIrnSamplesKeptAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqIreSamples\t{String.Join("\t", actualUnqIreSamplesKeptAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqRvRnSamples\t{String.Join("\t", actualUnqRvRnSamplesKeptAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualUnqFullEdgeSamples\t{String.Join("\t", actualUnqFullEdgeSamplesKeptAverages.Skip(1))}\n");

            File.AppendAllText(FILENAME, $"ActualRvMax\t{String.Join("\t", actualRvMaxAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualRnMax\t{String.Join("\t", actualRnMaxAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualReMax\t{String.Join("\t", actualReMaxAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualIrnMax\t{String.Join("\t", actualIrnMaxAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualIreMax\t{String.Join("\t", actualIreMaxAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualRvRnMax\t{String.Join("\t", actualRvRnMaxAverages.Skip(1))}\n");
            File.AppendAllText(FILENAME, $"ActualFullEdgeMax\t{String.Join("\t", actualFullEdgeMaxAverages.Skip(1))}\n");
        }

        static object WriteErrLock = new object();
        const string ERRFILENAME = "Results\\ErrLog.txt";
        static void WriteErr(string message)
        {
            lock (WriteErrLock)
            {
                if (!File.Exists(ERRFILENAME))
                    File.WriteAllText(ERRFILENAME, "");
                File.AppendAllText(ERRFILENAME, message + $" ({DTS})\n");
            }
        }
    }
}