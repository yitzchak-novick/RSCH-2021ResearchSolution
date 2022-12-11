using GraphLibYN_2019;
using PyReporting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilsYN;

namespace BestSwitchForRNRV_02
{

    /* YN 7/13/21 - Taking a different approach here than in the 01 version of this project. Here I will just 
     * look to determine what percent of samples is required to find the top x vertices of a graph for both RN
     * and RV. Hopefully this will give some insight into the question of when to switch from RN to RV in RNRV.
     */
    class Program
    {
        const bool debug = false;
        const int EXPERIMENTS = debug ? 1 : 400;
        const int GRAPHS = debug ? 1 :  100;
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        static string SYN_GRAPH_NAME(string type, int n, int m) => $"{type.ToUpper()}_N_{n}_M_{m}.png";
        const string BEST_SWTICH_FILE = "Results\\BestSwitches.tsv";

        public enum SamplingMethod
        {
            RV,
            NaiveRN,
            CostCorrectRN,
            RVN
        }

        static void Main(string[] args)
        {
            while (!File.Exists(@"C:\Users\Yitzchak\Desktop\Visual Studio 2021\2021Research_Sol01\OptimalKForKRNSampling_01\bin\Debug\Done.txt"))
                Thread.Sleep(TimeSpan.FromMinutes(1));
            Console.WriteLine($"Starting Program {DTS}");
            SaveSyntheticGraphImages();
            //PlotForGraphCollectionPctBySamples();
            Console.WriteLine($"Done {DTS}");
            Console.ReadKey();
        }

        static void SaveSyntheticGraphImages()
        {
            // In addition to creating a chart that shows the results, we'll save a text file with the experimentally calculated "best
            // point to switch from RN to RV".
            if (!File.Exists(BEST_SWTICH_FILE))
                File.WriteAllText(BEST_SWTICH_FILE, "Type\tN\tM\tSamples\tRV_TopPctFound\tRN_TopPctFound\n");

            var nVals = debug ? new[] { 200, 500 } : new[] { 200, 500, 1000, 2000, 5000, 7500, 10000 };
            var mVals = debug ? new[] { 2, 5 } : new[] { 2, 5, 8, 15, 25, 50, 100 };

            // For special run(s):
            nVals = new[] { 500, 1000, 2000, 5000 };
            mVals = new[] { 2, 5, 8, 15 };

            nVals = new[] { 1000 };
            mVals = new[] {  8 };

            HtmlTableReporter htmlBA = new HtmlTableReporter("Collecting vertices in BA graphs with RV and RN untill all vertices are collected. We are plotting " +
                "how long it takes to get the top x percent of vertices in descending order of degree. RV should get all in n log n (coupon collector)" +
                " so we will scale the results by n log n.", new[] { "nVals" }.Union(nVals.Select(n => $"n={n}")).ToArray(), mVals.Select(m => $"m={m}").ToArray());

            HtmlTableReporter htmlER = new HtmlTableReporter("Collecting vertices in ER graphs with RV and RN untill all vertices are collected. We are plotting " +
                "how long it takes to get the top x percent of vertices in descending order of degree. RV should get all in n log n (coupon collector)" +
                " so we will scale the results by n log n.", new[] { "nVals" }.Union(nVals.Select(n => $"n={n}")).ToArray(), mVals.Select(m => $"m={m}").ToArray());

            var tuples = Range(mVals.Length).SelectMany(mIndx => Range(nVals.Length).Select(nIndx => new Tuple<int, int>(mIndx, nIndx)))
                .OrderBy(t => Math.Max(t.Item1, t.Item2)).ThenBy(t => Math.Min(t.Item1, t.Item2));

            //foreach (var m in mVals)
            //    foreach (var n in nVals)
            foreach (var tuple in tuples)
            {
                var m = mVals[tuple.Item1];
                var n = nVals[tuple.Item2];
                for (int graphType = 0; graphType < 2; graphType++)
                {

                    Console.WriteLine($"Starting n={n}, m={m}, ({DTS})");
                    var graphs = Range(Math.Min(GRAPHS, EXPERIMENTS)).AsParallel().Select(
                        i => graphType == 0 ? Graph.NewBaGraph(n, m, random: rands[i]) : Graph.NewErGraphFromBaM(n, m, random: rands[i])).ToArray();
                    var fileName = "Results\\" + SYN_GRAPH_NAME(graphType == 0 ? "BA" : "ER", n, m);
                    if (!File.Exists(fileName))
                    {
                        try
                        {
                            var graphDesc = graphType == 0 ? $"BA Graph n={n} m={m}" : $"ER Graph n={n} Avg Deg={m * 2}";
                            File.AppendAllText(BEST_SWTICH_FILE, $"{(graphType == 0 ? "BA" : "ER")}\t{n}\t{m}\t");
                            PlotForGraphCollection(graphs, graphDesc, fileName);
                            (graphType == 0 ? htmlBA : htmlER).AddCell("", SYN_GRAPH_NAME(graphType == 0 ? "BA" : "ER", n, m));
                        }
                        catch (Exception ex)
                        {
                            (graphType == 0 ? htmlBA : htmlER).AddCell($"An error occurred for n={n}, m={m}", "");
                        }
                    }
                    var pctBySamplesFileName = fileName.Replace(".png", "PctBySampes.png"); // hack to create second version of charts
                    if (!File.Exists(pctBySamplesFileName))
                    {
                        try
                        {
                            PlotForGraphCollectionPctBySamples(graphs, graphType == 0 ? $"BA Graph n={n} m={m}" : $"ER Graph n={n} Avg Deg={m * 2}", pctBySamplesFileName);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
            }
            File.WriteAllText("Results\\BAResults.html", htmlBA.GetTableHtml());
            File.WriteAllText("Results\\ERResults.html", htmlER.GetTableHtml());

        }

        static void PlotForGraphCollection(Graph[] graphs, string graphDesc, string outputfilename)
        {
            var rvResults = new double[EXPERIMENTS][];
            var naiveRnResults = new double[EXPERIMENTS][];
            var costCorrectRnResults = new double[EXPERIMENTS][];
            var rvnResults = new double[EXPERIMENTS][];

            Parallel.For(0, EXPERIMENTS, i =>
            {
                var results = GetSamplesRequiredToFindTopVerticesInGraphForRvRn(graphs[i % graphs.Length], rands[i]);
                rvResults[i] = results[SamplingMethod.RV];
                naiveRnResults[i] = results[SamplingMethod.NaiveRN];
                costCorrectRnResults[i] = results[SamplingMethod.CostCorrectRN];
                rvnResults[i] = results[SamplingMethod.RVN];
            });
            var RvAverageResults = Range(rvResults[0].Length).Select(i => rvResults.Average(arr => arr[i])).ToArray();
            var NaiveRnAverageResults = Range(naiveRnResults[0].Length).Select(i => naiveRnResults.Average(arr => arr[i])).ToArray();
            var CostCorrectRnAverageResults = Range(costCorrectRnResults[0].Length).Select(i => costCorrectRnResults.Average(arr => arr[i])).ToArray();
            var RvnAverageResults = Range(rvnResults[0].Length).Select(i => rvnResults.Average(arr => arr[i])).ToArray();

            var currIndex = 0;

            while (RvAverageResults[currIndex] > NaiveRnAverageResults[currIndex])
                currIndex++;
            File.AppendAllText(BEST_SWTICH_FILE, $"{currIndex}\t{RvAverageResults[currIndex]}\t{NaiveRnAverageResults[currIndex]}\n");

            Py.CreatePyPlot(
                Py.PlotType.plot,
                Range(1, RvAverageResults.Length).Select(i => (double)i / RvAverageResults.Length).ToArray(),
                new[] { RvAverageResults, NaiveRnAverageResults, RvnAverageResults },
                new[] { "RV", "RN", "RVN" },
                new[] { "r", "b", "k" },
                "Samples Required to Find Top Vertices\\n" + graphDesc,
                "Percent of Top Vertices Found",
                "Samples", // (Scaled by n log n)",
                outputfilename
                );

        }

        static void PlotForGraphCollectionPctBySamples(Graph[] graphs, string graphDesc, string outputfilename)
        {
            var rvResults = new double[EXPERIMENTS][];
            var naiveRnResults = new double[EXPERIMENTS][];
            var rvnResults = new double[EXPERIMENTS][];

            Parallel.For(0, EXPERIMENTS, i =>
            {
                var results = GetPctOfTopVerticesFoundBySamples(graphs[i % graphs.Length], rands[i]);
                rvResults[i] = results[SamplingMethod.RV];
                naiveRnResults[i] = results[SamplingMethod.NaiveRN];
                rvnResults[i] = results[SamplingMethod.RVN];
            });



            var RvAverageResults = Range(rvResults[0].Length).Select(i => rvResults.Average(arr => i < arr.Length ? arr[i] : arr.Last())).ToArray();
            var NaiveRnAverageResults = Range(naiveRnResults[0].Length).Select(i => naiveRnResults.Average(arr => i < arr.Length ? arr[i] : arr.Last())).ToArray();
            var RvnAverageResults = Range(rvnResults[0].Length).Select(i => rvnResults.Average(arr => i < arr.Length ? arr[i] : arr.Last())).ToArray();


            Py.CreatePyPlot(
                Py.PlotType.plot,
                Range(1, RvAverageResults.Length).Select(i => (double)i).ToArray(),
                new[] { RvAverageResults, NaiveRnAverageResults, RvnAverageResults },
                new[] { "RV", "RN", "RVN" },
                new[] { "r", "b", "k" },
                "Samples Required to Find Top Vertices\\n" + graphDesc,
                "Samples",
                "Pct of Top Vertices", // (Scaled by n log n)",
                outputfilename
                );
        }

        // Will return two double-arrays, first one is for RV, second is for RN. Each value i represents the number of
        // samples required to find the top i elements as ranked by degree.
        static Dictionary<SamplingMethod, double[]> GetSamplesRequiredToFindTopVerticesInGraphForRvRn(Graph graph, Random rand)
        {
            VertexDegreeRanker RvRanker = new VertexDegreeRanker(graph);
            VertexDegreeRanker RnRanker = new VertexDegreeRanker(graph);
            VertexDegreeRanker RvnRanker = new VertexDegreeRanker(graph);

            //var verticesCollection = graph.Vertices.OrderByDescending(v => v.Degree).ToArray();
            //var vertexRanks = new Dictionary<Vertex, int>();
            //Range(verticesCollection.Length).ToList().ForEach(i => vertexRanks[verticesCollection[i]] = i);
            //var foundVerticesRv = new bool[graph.Vertices.Count()];
            //var foundVerticesRn = new bool[graph.Vertices.Count()];

            // Getting the very last vertex takes an incredibly long time with RN, we'll only examine up to 98% of the graph
            var arraySize = (int)(graph.Vertices.Count() * 1);
            var samplesRequiredToFindRv = new int[arraySize];
            var samplesRequiredToFindRn = new int[arraySize];
            var samplesRequiredToFindRvn = new int[arraySize];

            var currUnfoundRv = 0;
            var currUnfoundRn = 0;
            var currUnfoundRvn = 0;

            int samplesTaken = 0;
            while (currUnfoundRv < samplesRequiredToFindRv.Length || currUnfoundRn < samplesRequiredToFindRn.Length || currUnfoundRvn < samplesRequiredToFindRvn.Length)
            {
                samplesTaken++;
                var vertex = graph.Vertices.ChooseRandomElement(rand);
                var neighbor = vertex.Neighbors.ChooseRandomElement(rand);

                var currRvRank = RvRanker.GetCurrRankFound(vertex);
                var currRnRank = RnRanker.GetCurrRankFound(neighbor);

                while (currUnfoundRv < currRvRank && currUnfoundRv < samplesRequiredToFindRv.Length)
                    samplesRequiredToFindRv[currUnfoundRv++] = samplesTaken;
                while (currUnfoundRn < currRnRank && currUnfoundRn < samplesRequiredToFindRn.Length)
                    samplesRequiredToFindRn[currUnfoundRn++] = samplesTaken;

                var currRvnRank = RvnRanker.GetCurrRankFound(vertex);
                while (currUnfoundRvn < currRvnRank && currUnfoundRvn < samplesRequiredToFindRvn.Length)
                    samplesRequiredToFindRvn[currUnfoundRvn++] = 2 * samplesTaken - 1;
                currRvnRank = RvnRanker.GetCurrRankFound(neighbor);
                while (currUnfoundRvn < currRvnRank && currUnfoundRvn < samplesRequiredToFindRvn.Length)
                    samplesRequiredToFindRvn[currUnfoundRvn++] = 2 * samplesTaken;
            }
            var N = graph.Vertices.Count();

            return new Dictionary<SamplingMethod, double[]>
            {
                { SamplingMethod.RV, samplesRequiredToFindRv.Select(i => (double)i).ToArray() },
                { SamplingMethod.NaiveRN,  samplesRequiredToFindRn.Select(i => (double)i).ToArray() },
                { SamplingMethod.CostCorrectRN,  samplesRequiredToFindRn.Select(i => 2.0 * i).ToArray() },
                { SamplingMethod.RVN, samplesRequiredToFindRvn.Select(i => (double)i).ToArray() }
            };

            /*
            return new Dictionary<SamplingMethod, double[]>
            {
                { SamplingMethod.RV, samplesRequiredToFindRv.Select(i => (double)i / (N * Math.Log(N))).ToArray() },
                { SamplingMethod.NaiveRN,  samplesRequiredToFindRn.Select(i => (double)i / (N * Math.Log(N))).ToArray() },
                { SamplingMethod.CostCorrectRN,  samplesRequiredToFindRn.Select(i => 2.0 * i/ (N * Math.Log(N))).ToArray() },
                { SamplingMethod.RVN, samplesRequiredToFindRvn.Select(i => (double)i / (N*Math.Log(N))).ToArray() }
            };
            */
        }

        static Dictionary<SamplingMethod, double[]> GetPctOfTopVerticesFoundBySamples(Graph graph, Random rand)
        {
            var MAX_PCT = .98;

            VertexDegreeRanker RvRanker = new VertexDegreeRanker(graph);
            VertexDegreeRanker RnRanker = new VertexDegreeRanker(graph);
            VertexDegreeRanker RvnRanker = new VertexDegreeRanker(graph);

            var pctsFoundByRv = new List<double>();
            var pctsFoundByRn = new List<double>();
            var pctsFoundByRvn = new List<double>();

            do
            {
                var vertex = graph.Vertices.ChooseRandomElement(rand);
                var neighbor = vertex.Neighbors.ChooseRandomElement(rand);

                var currRvRank = RvRanker.GetCurrRankFoundAsPct(vertex);
                var currRnRank = RnRanker.GetCurrRankFoundAsPct(neighbor);

                pctsFoundByRv.Add(currRvRank);
                pctsFoundByRn.Add(currRnRank);

                var currRvnRank = RvnRanker.GetCurrRankFoundAsPct(vertex);
                pctsFoundByRvn.Add(currRvnRank);
                currRvnRank = RvnRanker.GetCurrRankFoundAsPct(neighbor);
                pctsFoundByRvn.Add(currRvnRank);
            } while (pctsFoundByRv[pctsFoundByRv.Count() - 1] < MAX_PCT || pctsFoundByRn[pctsFoundByRn.Count() - 1] < MAX_PCT || pctsFoundByRvn[pctsFoundByRvn.Count() - 1] < MAX_PCT);

            var lastIndex = 0;
            while (pctsFoundByRv[lastIndex] < MAX_PCT || pctsFoundByRn[lastIndex] < MAX_PCT || pctsFoundByRvn[lastIndex] < MAX_PCT)
            {
                lastIndex++;

                if (pctsFoundByRv.Count == lastIndex)
                    pctsFoundByRv.Add(pctsFoundByRv[lastIndex - 1]);
                if (pctsFoundByRn.Count == lastIndex)
                    pctsFoundByRn.Add(pctsFoundByRn[lastIndex - 1]);
                if (pctsFoundByRvn.Count == lastIndex)
                    pctsFoundByRvn.Add(pctsFoundByRvn[lastIndex - 1]);
            }



            return new Dictionary<SamplingMethod, double[]>
            {
                { SamplingMethod.RV, pctsFoundByRv.Take(lastIndex + 1).ToArray() },
                { SamplingMethod.NaiveRN, pctsFoundByRn.Take(lastIndex + 1).ToArray() },
                { SamplingMethod.RVN, pctsFoundByRvn.Take(lastIndex + 1).ToArray() }
            };
        }
    }
}
