using GraphLibYN_2019;
using UtilsYN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace NetworkDisintegration_02
{
    class Program2
    {
        /* YN 5/6/21
         * 
         * Just changing to use fewere M values, more N values, write to Results3
         */

        // Hack to let two programs take turns
        static string ThisProgId = "A";
        static string OtherProgId = "B";
        static string SyncFilePath = @"C:\temp\RunTwoProgramsSync.txt";

        static string DTS => Utils.DTS;
        static IEnumerable<int> Range(int n) => Enumerable.Range(0, n);
        static readonly int THREADS = 50;
        static Random[] rands = TSRandom.ArrayOfRandoms(THREADS);
        private static readonly string KOBLENZ_BASE_DIR = @"D:\Graphs\Koblenz\AllOrigFiles\konect.cc\files\"; // location of the files on my workstation computer

        // The number of samples we take. We want to eventually get every vertex which should require
        // n log n samplings. For now doing 3n log n, to make sure we get each one WHP.
        static int SamplesToTake(int n) => (int)(2 * n * Math.Log(n));
        enum GraphType { ER, BA, RW };
        static string FILENAME_SYNTH(GraphType graphType, int n, int m) => $"Results3\\{graphType}_N_{n}_M_{m}_results.tsv";
        static string FILENAME_RW(string graphName) => $"Results3\\RW_{graphName.Replace(" ", "-")}_results.tsv";

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

        static string PREAMBLE = "% This file tracks the disintegration of a network. Four methods are used:\n" +
                                "% 1) Descending Order - This is our presumably optimal case, we always select the vertex of highest degree (dynamically, max even after adjustments) and remove its edges.\n" +
                                "% 2) RN - Take a random vertex\n" +
                                "% 3) RN - Take a random vertex, then take a neighbor of it at random\n" +
                                "% 4) RVRN - First take a random vertex, remove its edges, then take one its neighbors at random and remove its edges\n" +
                                "% All of these are without replacement, we will repeat vertices. We will typically select more than n vertices so the network will eventually be fully disintegrated and\n" +
                                "% a large part of the vectors may be unnecessaary.\n" +
                                "% For each method, we will track seven vectors:\n" +
                                "% 1) Max Component - At each step, what is the size of the largest component. This descending curve can be plotted to show the disintegration visually.\n" +
                                "% 2) Second Component - At each step, what is the size of the second largest component. It is generally accepted that the maximal value for this vector is the point of disintegration.\n" +
                                "% 3) CvAll - Assuming a cost of 1 for each vertex (not neighbor) collected, how many times did we pay this cost to get to each step. Multiply by the actual CV cost being used.\n" +
                                "% 4) CvUnq - Same as CvAll but we only charge a cost when the vertex has not been seen before (whether as a vertex or neighbor).\n" +
                                "% 5) CnAll - Assuming a cost of 1 for each neighbor collected, how many times did we pay this cost to get to each step. Multiply by the actual CV cost being used.\n" +
                                "% 6) CnUnq - Same as CnAll but we only charge a cost when the vertex has not been seen before (whether as a vertex or neighbor).\n" +
                                "% 7) Cs - Assuming a cost of 1 for actually taking a vertex (i.e. removing its edges), how many times did we pay this cost to get to this step. The cost is not charged unless there are edges,\n" +
                                "%    even if it is the first time seeing the vertex.\n" +
                                "% The experiments are repeated multiple times and the average of all vectors are reported here.\n";

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

        enum ResultVector
        {
            MaxComponent, SecondMaxComponent, CvAll, CvUnq, CnAll, CnUnq, Cs
        }

        enum SamplingMethod
        {
            DescendingOrder, RV, RN, RVRN
        }

        private static void GetSyntheticGraphs(GraphType graphType, int n, int m, out Graph[] graphs, out Dictionary<string, string> metaInfo)
        {
            graphs = Enumerable.Range(0, THREADS).AsParallel().Select(i => graphType == GraphType.BA ? Graph.NewBaGraph(n, m, random: rands[i]) : Graph.NewErGraphFromBaM(n, m, random: rands[i])).ToArray();
            metaInfo = GetBasicMetaInfo(graphType.ToString(), graphs);
        }

        static void Main(string[] args)
        {
            Console.WriteLine($"Staring program {(DTS)}");

            if (!Directory.Exists("Results"))
                Directory.CreateDirectory("Results");

            List<Action> SyntheticGraphActions = new List<Action>();
            List<Action> RwActions = new List<Action>();

            foreach (var n in new[] { 100, 500, 1000, 2000, 5000, 7500, 10000, 15000, 25000 })
                foreach (var m in new[] { 2, 5, 15, 50, 100 })
                {
                    if (m >= n / 2)
                        continue;

                    foreach (var grphType in new[] { GraphType.ER, GraphType.BA })
                    {
                        var fileName = FILENAME_SYNTH(grphType, n, m);
                        if (File.Exists($"{fileName}"))
                            continue;

                        SyntheticGraphActions.Add(() =>
                        {
                            Console.WriteLine($"Starting: {grphType}, N: {n}, M: {m}, getting graphs ({DTS})");
                            Graph[] graphs;
                            Dictionary<string, string> metaInfo;
                            GetSyntheticGraphs(grphType, n, m, out graphs, out metaInfo);
                            Console.WriteLine($"Getting results {DTS}");
                            var results = GetResultsForGraphs(graphs, metaInfo);
                            File.WriteAllText(fileName, results);

                            Console.WriteLine($"Finished: {grphType}, N: {n}, M: {m}. ({DTS})");
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
                        var results = GetResultsForGraphs(graphs, metaInfo);
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
                /*while (File.ReadAllText(SyncFilePath) != ThisProgId)
                    Thread.Sleep(TimeSpan.FromSeconds(10));*/

                if (i < SyntheticGraphActions.Count)
                    SyntheticGraphActions[i]();
                /*if (i < RwActions.Count)
                    RwActions[i]();*/

                File.WriteAllText(SyncFilePath, OtherProgId);
            }

            Console.WriteLine($"Program Finished at {DTS}, press any key to exit");
            Console.ReadKey();
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


        static string GetResultsForGraphs(Graph[] graphs, Dictionary<string, string> metaInfo)
        {
            Dictionary<SamplingMethod, Dictionary<ResultVector, int[]>>[] allResults =
                Range(THREADS).Select(i => new Dictionary<SamplingMethod, Dictionary<ResultVector, int[]>>()).ToArray();
            var samples = SamplesToTake(graphs[0].Vertices.Count());

            Parallel.For(0, THREADS, i =>
            {
                var graph = graphs[i].Clone();
                allResults[i][SamplingMethod.DescendingOrder] = DisintegrateInDescendingOrder(graph, samples, rands[i]);
            });

            Parallel.For(0, THREADS, i =>
            {
                var graph = graphs[i].Clone();
                allResults[i][SamplingMethod.RV] = DisintegrateByRv(graph, samples, rands[i]);
            });

            Parallel.For(0, THREADS, i =>
            {
                var graph = graphs[i].Clone();
                allResults[i][SamplingMethod.RN] = DisintegrateByRn(graph, samples, rands[i]);
            });

            Parallel.For(0, THREADS, i =>
            {
                var graph = graphs[i].Clone();
                allResults[i][SamplingMethod.RVRN] = DisintegrateByRvRn(graph, samples, rands[i]);
            });

            Dictionary<String, double[]> collapsedResults = new Dictionary<string, double[]>();
            foreach (var samplingmethod in new[] { SamplingMethod.DescendingOrder, SamplingMethod.RN, SamplingMethod.RV, SamplingMethod.RVRN })
                foreach (var resultvector in new[] { ResultVector.MaxComponent, ResultVector.SecondMaxComponent, ResultVector.CvAll, ResultVector.CvUnq, ResultVector.CnAll, ResultVector.CnUnq, ResultVector.Cs })
                    collapsedResults[samplingmethod.ToString() + "_" + resultvector.ToString()] =
                        Range(samples).Select(j =>
                        Range(THREADS).Select(i => allResults[i][samplingmethod][resultvector]).Average(arr => arr[j])).ToArray();


            StringBuilder results = new StringBuilder();
            foreach (var line in collapsedResults)
            {
                results.AppendLine(line.Key + "\t" + String.Join("\t", line.Value));
            }

            results.Insert(0, GetMetaInfoString(metaInfo));
            results.Insert(0, PREAMBLE);

            return results.ToString();
        }


        #region DISINTEGRATION_METHODS
        /* YN 3/12/21 - For now writing a separate method for each sampling method, if there's overlap maybe we can refactor but
         * typically it's been hard to combine these.
         */


        // Disintegrating in order is our theoretically optimal case. It is probably fine to just use the order
        // based on the initial state of a graph but we'll go the extra mile here and actually dynamically choose
        // the max-degree vertex, hopefully adds minimal complexity. Will need to see how this goes.
        static Dictionary<ResultVector, int[]> DisintegrateInDescendingOrder(Graph graph, int totalSamplesToTake, Random rand)
        {
            Dictionary<ResultVector, int[]> results = new Dictionary<ResultVector, int[]>();

            foreach (ResultVector rv in Enum.GetValues(typeof(ResultVector)))
                results[rv] = new int[totalSamplesToTake];

            // No Cn costs
            results[ResultVector.CnAll] = Enumerable.Range(0, totalSamplesToTake).Select(i => 0).ToArray();
            results[ResultVector.CnUnq] = Enumerable.Range(0, totalSamplesToTake).Select(i => 0).ToArray();

            int totalCvAll = 0;
            int totalCvUnq = 0;
            int totalCs = 0;

            DynamicPriorityQueue<Vertex, int> pQueue = new DynamicPriorityQueue<Vertex, int>(graph.Vertices.Count());
            graph.Vertices.ToList().ForEach(v => pQueue.AddItem(v, v.Degree));

            List<Tuple<string, string>>[] removedEdges = new List<Tuple<string, string>>[totalSamplesToTake];

            HashSet<Vertex> sampledVertices = new HashSet<Vertex>();
            for (int i = 0; i < totalSamplesToTake; i++)
            {
                var initialVertex = pQueue.PeekTopItem();
                //Console.WriteLine(initialVertex.Id + " deg: " + initialVertex.Degree); // Line for testing, take out
                results[ResultVector.CvAll][i] = ++totalCvAll;
                if (sampledVertices.Add(initialVertex))
                    totalCvUnq++;
                results[ResultVector.CvUnq][i] = totalCvUnq;
                removedEdges[i] = new List<Tuple<string, string>>();
                if (initialVertex.Edges.Any())
                {
                    foreach (var edge in initialVertex.Edges.ToList())
                    {
                        removedEdges[i].Add(new Tuple<string, string>(edge.v1.Id, edge.v2.Id));
                        graph.RemoveEdge(edge.v1.Id, edge.v2.Id);
                        pQueue.UpdateItem(edge.v1, edge.v1.Degree);
                        pQueue.UpdateItem(edge.v2, edge.v2.Degree);
                    }
                    totalCs++;
                }
                results[ResultVector.Cs][i] = totalCs;
            }

            results[ResultVector.MaxComponent] = rebuildMaxComponents(graph, removedEdges, UnionFindRank.Max);
            results[ResultVector.SecondMaxComponent] = rebuildMaxComponents(graph, removedEdges, UnionFindRank.SecondLargest);

            return results;
        }

        static Dictionary<ResultVector, int[]> DisintegrateByRv(Graph graph, int totalSamplesToTake, Random rand)
        {
            Dictionary<ResultVector, int[]> results = new Dictionary<ResultVector, int[]>();

            foreach (ResultVector rv in Enum.GetValues(typeof(ResultVector)))
                results[rv] = new int[totalSamplesToTake];

            // No Cn costs for RV
            results[ResultVector.CnAll] = Enumerable.Range(0, totalSamplesToTake).Select(i => 0).ToArray();
            results[ResultVector.CnUnq] = Enumerable.Range(0, totalSamplesToTake).Select(i => 0).ToArray();

            int totalCvAll = 0;
            int totalCvUnq = 0;
            int totalCs = 0;

            List<Tuple<string, string>>[] removedEdges = new List<Tuple<string, string>>[totalSamplesToTake];

            HashSet<Vertex> sampledVertices = new HashSet<Vertex>();
            for (int i = 0; i < totalSamplesToTake; i++)
            {
                var initialVertex = graph.Vertices.ChooseRandomElement(rand);
                //Console.WriteLine(initialVertex.Id + ", "); // Line for testing, take out
                results[ResultVector.CvAll][i] = ++totalCvAll;
                if (sampledVertices.Add(initialVertex))
                    totalCvUnq++;
                results[ResultVector.CvUnq][i] = totalCvUnq;
                removedEdges[i] = new List<Tuple<string, string>>();
                if (initialVertex.Edges.Any())
                {
                    foreach (var edge in initialVertex.Edges.ToList())
                    {
                        removedEdges[i].Add(new Tuple<string, string>(edge.v1.Id, edge.v2.Id));
                        graph.RemoveEdge(edge.v1.Id, edge.v2.Id);
                    }
                    totalCs++;
                }
                results[ResultVector.Cs][i] = totalCs;
            }

            results[ResultVector.MaxComponent] = rebuildMaxComponents(graph, removedEdges, UnionFindRank.Max);
            results[ResultVector.SecondMaxComponent] = rebuildMaxComponents(graph, removedEdges, UnionFindRank.SecondLargest);

            return results;
        }


        static Dictionary<ResultVector, int[]> DisintegrateByRn(Graph graph, int totalSamplesToTake, Random rand)
        {
            Dictionary<ResultVector, int[]> results = new Dictionary<ResultVector, int[]>();

            foreach (ResultVector rv in Enum.GetValues(typeof(ResultVector)))
                results[rv] = new int[totalSamplesToTake];

            int totalCvAll = 0;
            int totalCvUnq = 0;
            int totalCnAll = 0;
            int totalCnUnq = 0;
            int totalCs = 0;

            List<Tuple<string, string>>[] removedEdges = new List<Tuple<string, string>>[totalSamplesToTake];

            // One set for both vertices and neighbors, assuming if we are counting unique it doesn't matter
            // if we encountered it as a vertex or a neighbor.
            HashSet<Vertex> sampledVertices = new HashSet<Vertex>();
            for (int i = 0; i < totalSamplesToTake; i++)
            {
                var initialVertex = graph.Vertices.ChooseRandomElement(rand);
                results[ResultVector.CvAll][i] = ++totalCvAll;
                if (sampledVertices.Add(initialVertex))
                    totalCvUnq++;
                results[ResultVector.CvUnq][i] = totalCvUnq;
                removedEdges[i] = new List<Tuple<string, string>>();
                if (initialVertex.Neighbors.Any())
                {
                    var neighbor = initialVertex.Neighbors.ChooseRandomElement(rand);
                    totalCnAll++;
                    if (sampledVertices.Add(neighbor))
                        totalCnUnq++;
                    foreach (var edge in neighbor.Edges.ToList())
                    {
                        removedEdges[i].Add(new Tuple<string, string>(edge.v1.Id, edge.v2.Id));
                        graph.RemoveEdge(edge.v1.Id, edge.v2.Id);
                    }
                    totalCs++;
                }
                results[ResultVector.CnAll][i] = totalCnAll;
                results[ResultVector.CnUnq][i] = totalCnUnq;
                results[ResultVector.Cs][i] = totalCs;
            }

            results[ResultVector.MaxComponent] = rebuildMaxComponents(graph, removedEdges, UnionFindRank.Max);
            results[ResultVector.SecondMaxComponent] = rebuildMaxComponents(graph, removedEdges, UnionFindRank.SecondLargest);

            return results;
        }


        static Dictionary<ResultVector, int[]> DisintegrateByRvRn(Graph graph, int totalSamplesToTake, Random rand)
        {
            Dictionary<ResultVector, int[]> results = new Dictionary<ResultVector, int[]>();

            foreach (ResultVector rv in Enum.GetValues(typeof(ResultVector)))
                results[rv] = new int[totalSamplesToTake];

            int totalCvAll = 0;
            int totalCvUnq = 0;
            int totalCnAll = 0;
            int totalCnUnq = 0;
            int totalCs = 0;

            List<Tuple<string, string>>[] removedEdges = new List<Tuple<string, string>>[totalSamplesToTake];

            // One set for both vertices and neighbors, assuming if we are counting unique it doesn't matter
            // if we encountered it as a vertex or a neighbor.
            HashSet<Vertex> sampledVertices = new HashSet<Vertex>();
            for (int i = 0; i < totalSamplesToTake; i++)
            {
                var initialVertex = graph.Vertices.ChooseRandomElement(rand);
                results[ResultVector.CvAll][i] = ++totalCvAll;
                if (sampledVertices.Add(initialVertex))
                    totalCvUnq++;
                results[ResultVector.CvUnq][i] = totalCvUnq;


                // Choose the neighbor before innoculating the vertex
                var neighbor = initialVertex.Neighbors.Any() ? initialVertex.Neighbors.ChooseRandomElement(rand) : null;

                // innoculate the vertex:
                removedEdges[i] = new List<Tuple<string, string>>();
                if (initialVertex.Degree >= 1)
                {
                    foreach (var edge in initialVertex.Edges.ToList())
                    {
                        removedEdges[i].Add(new Tuple<string, string>(edge.v1.Id, edge.v2.Id));
                        graph.RemoveEdge(edge.v1.Id, edge.v2.Id);
                    }
                    totalCs++;
                }
                results[ResultVector.CnAll][i] = totalCnAll;
                results[ResultVector.CnUnq][i] = totalCnUnq;
                results[ResultVector.Cs][i] = totalCs;

                // A second step in this iteration to innoculate the neighbor
                i++;
                if (i >= totalSamplesToTake)
                    break;

                removedEdges[i] = new List<Tuple<string, string>>();
                if (neighbor != null)
                {
                    totalCnAll++;
                    if (sampledVertices.Add(neighbor))
                        totalCnUnq++;
                    if (neighbor.Neighbors.Any()) // if the vertex was the only neighbor of this neighbor it will now be degree-0
                    {
                        foreach (var edge in neighbor.Edges.ToList())
                        {
                            removedEdges[i].Add(new Tuple<string, string>(edge.v1.Id, edge.v2.Id));
                            graph.RemoveEdge(edge.v1.Id, edge.v2.Id);
                        }
                        totalCs++;
                    }
                }
                results[ResultVector.CvAll][i] = totalCvAll;
                results[ResultVector.CvUnq][i] = totalCvUnq;
                results[ResultVector.CnAll][i] = totalCnAll;
                results[ResultVector.CnUnq][i] = totalCnUnq;
                results[ResultVector.Cs][i] = totalCs;

            }

            results[ResultVector.MaxComponent] = rebuildMaxComponents(graph, removedEdges, UnionFindRank.Max);
            results[ResultVector.SecondMaxComponent] = rebuildMaxComponents(graph, removedEdges, UnionFindRank.SecondLargest);

            return results;
        }
        #endregion

        enum UnionFindRank { Max, SecondLargest }
        /// <summary>
        /// Pass in the final state of a graph after disintegration, and a collection of the edges removed in order, and it
        /// will return an array with the size of the max/second componenet at each step.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="removedEdges"></param>
        /// <param name="rank"></param>
        /// <returns></returns>
        static int[] rebuildMaxComponents(Graph graph, List<Tuple<String, String>>[] removedEdges, UnionFindRank rank)
        {
            int[] maxComponents = new int[removedEdges.Length];

            UnionFind<String> rebuildUnionFind = new UnionFind<string>();
            rebuildUnionFind.AddElements(graph.Vertices.Select(v => v.Id));
            graph.Edges.ToList().ForEach(e => rebuildUnionFind.Union(e.v1.Id, e.v2.Id));
            // Final state of the graph is the last entry
            maxComponents[removedEdges.Length - 1] = rank == UnionFindRank.Max ? rebuildUnionFind.GetMaxSetCount() : rebuildUnionFind.GetSecondLargestSetCount();
            // Loop backwards through removedEdges. Adding a set of removed edges gives the state of the PREVIOUS graph, last entry is irrelevant it just gives
            // the original graph.
            for (int i = removedEdges.Length - 1; i > 0; i--)
            {
                foreach (var edge in removedEdges[i])
                {
                    rebuildUnionFind.Union(edge.Item1, edge.Item2);
                }
                maxComponents[i - 1] = rank == UnionFindRank.Max ? rebuildUnionFind.GetMaxSetCount() : rebuildUnionFind.GetSecondLargestSetCount();
            }
            return maxComponents;
        }

    }
}
