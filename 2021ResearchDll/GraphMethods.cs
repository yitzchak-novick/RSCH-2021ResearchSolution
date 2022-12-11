using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UtilsYN;

namespace _2021ResearchDll
{
    public class GraphMethods
    {
        /* YN 5/12/21 - This is a collection of methods taken from other previous projects and it looks like they may have other uses
         * in othe projects, so creating this class for them.
         */

        public static readonly string KOBLENZ_BASE_DIR = @"D:\Graphs\Koblenz\AllOrigFiles\konect.cc\files\"; // location of the files on my workstation computer
        public enum GraphType { ER, BA, RW };

        // Follow these filename conventions:
        public static string FILENAME_SYNTH(GraphType graphType, int n, int m) => $"{graphType}_N_{n}_M_{m}_results.tsv";
        public static string FILENAME_RW(string graphName) => $"RW_{graphName.Replace(" ", "-")}_results.tsv";


        // Private method that gives back the basic meta data I see us caring about. The only thing it needs is the number of
        // experiments, for a collection of synthetic graphs it is the count of the collection, but for RW we would just take
        // one. So we will use "Experiments" as the flag.
        private static Dictionary<string, string> GetBasicMetaInfo(String type, IEnumerable<Graph> graphs, int Experiments)
        {
            Dictionary<string, string> metaInfo = new Dictionary<string, string>();
            metaInfo["Experiments"] = Experiments.ToString();
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

        public static Dictionary<string, string> GetBasicMetaInfoForSyntheticGraphs(GraphType graphType, IEnumerable<Graph> graphs) => 
            GetBasicMetaInfo(graphType.ToString(), graphs, graphs.Count());

        public static Dictionary<string, string> GetBasicMetaInfoForRwGraph(String graphName, Graph graph, int Experiments) =>
            GetBasicMetaInfo("RW-" + graphName, new Graph[] { graph }, Experiments);

        private static void GetSyntheticGraphs(GraphType graphType, int n, int m, int count, out Graph[] graphs, out Dictionary<string, string> metaInfo)
        {
            var rands = TSRandom.ArrayOfRandoms(count);
            graphs = Enumerable.Range(0, count).AsParallel().Select(i => graphType == GraphType.BA ? Graph.NewBaGraph(n, m, random: rands[i]) : Graph.NewErGraphFromBaM(n, m, random: rands[i])).ToArray();
            metaInfo = GetBasicMetaInfoForSyntheticGraphs(graphType, graphs);
        }
                
        private static bool GetRWGraph(string graphDirName, int count, out Graph[] graphs, out Dictionary<string, string> metaInfo)
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
                    throw new Exception($"Couldn't find meta file for {graphDirName}");

                    var fileMetaInfoLines = File.ReadAllLines(metafile.FullName).Select(l => Regex.Split(l.Replace("\t", "$TAB$").Replace(";", "$SEMICOLON$"), @"\:\s+"));
                    longdescr = fileMetaInfoLines.FirstOrDefault(mi => mi[0].ToLower() == "long-description")?[1] ?? "*** NOT FOUND ***";
                    url = fileMetaInfoLines.FirstOrDefault(mi => mi[0].ToLower() == "url")?[1] ?? "*** NOT FOUND ***";
                    category = fileMetaInfoLines.FirstOrDefault(mi => mi[0].ToLower() == "category")?[1] ?? "*** NOT FOUND ***";
                    bipPerMeta = File.ReadAllText(metafile.FullName).ToLower().Contains("bipartite");

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

                metaInfo = GetBasicMetaInfoForRwGraph(graphDirName.Substring(graphDirName.LastIndexOf('\\') + 1), graph, count);
                metaInfo["Url"] = url;
                metaInfo["Long Descr"] = longdescr;
                metaInfo["Category"] = category;
                metaInfo["BipPerMeta"] = bipPerMeta.ToString();
                metaInfo["BipPerComments"] = bipPerComments.ToString();
                metaInfo["Column Numbers"] = columnNumbers.ToString();
                metaInfo["Comments"] = graphComment;
                metaInfo["Self Loops"] = selfLoops.ToString();

                graphs = Enumerable.Range(0, count).AsParallel().Select(i => graph.Clone()).ToArray();

                return true;
            }
            catch (Exception ex)
            {
                graphs = null;
                metaInfo = null;
                return false;
                throw new Exception($"Exception Parsing Graph {graphDirName}, Message: {ex.Message}, See Inner Exception for More Details", ex);
            }
        }
    }
}
