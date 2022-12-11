using PyReporting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CriticalCn_01
{
    class Program
    {
        /* YN 6/29/21 - Wrote a version of this before, trying to get it a little more organized now. Will try to define 'critical Cn', if 
         * we fix Cv=1, for what Cn do we "break even"? We will consider three metrics of results, total accumulated edges, total unique
         * edges, and max degree. At some point, we'll want to do network disintegration points also. Here the numbers can be calculated
         * ignoring whether or not duplicates are taken, we are only considering Cv and Cn and we pay those for duplicates also
         * 
         * We'll try two forms of this. One is to iterate through the actual results and, using the cost of each result, calculate the cost of a 
         * unit of result, and calculate CCn based on that. The second is to loop through possible results, 1, 2, 3, etc., and for each see
         * what the actual cost was to get that result (or higher) in the actual result vectors.
         * 
         * For total accumulated edges, you would assume the result will be a flatline, perfectly in line with the theoretical calculation
         * (which is (RN-RV)/RV). For the others the results may be more interesting.
         * 
         * The results will be based on the result files from the TotalDegrees..._02 project in this solution.
         */

        static readonly string RESULTS_DIR = @"C:\Users\Yitzchak\Desktop\Visual Studio 2021\2021Research_Sol01\TotalDegreesOverCostBySamplesTaken_02\bin\Debug\Results";
        static string GetFileName(String type, int n, int m) => $"{RESULTS_DIR}\\{type.ToUpper()}_N_{n}_M_{m}_results.tsv";

        const int RvTotalEdgesIndex = 53;
        const int RnTotalEdgesIndex = 54;
        const int RvTotalUniqueEdges = 60;
        const int RnTotalUniqueEdges = 61;
        const int RvMaxDegreeIndex = 74;
        const int RnMaxDegreeIndex = 75;
        const int MetaFileLineIndex = 44;

        static string DTS => UtilsYN.Utils.DTS;


        static void Main(string[] args)
        {
            GetCriticalCnBySamplesForGraphs_AllMeasures();
            GetCriticalCnByResultsForErBaGraphs();
            Console.WriteLine("Done");
            Console.ReadKey();
        }
        static void GetCriticalCnBySamplesForGraphs_AllMeasures()
        {
            Console.WriteLine($"Starting Total Edges by Samples {DTS}");
            string preamble =
                "Finding 'Critical Cn' for 'Total Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                "Here we are accumulating total edges, in other words the sum of all degrees of all vertices we sample. Using the expected values, " +
                "we can predict CCn=(RN-RV)/RV. Because we are sampling with replacement, the assumption is that the results will match the prediction " +
                "perfectly as more samples are taken. The x axes are the number of samples taken, the y axes are the CCn values.";
            GetCriticalCnBySamplesForErBaGraphs(preamble, RvTotalEdgesIndex, RnTotalEdgesIndex, "CriticalCN_TotalEdgesBySamples");

            Console.WriteLine($"Starting Total Unique Edges by Samples {DTS}");
            preamble =
                "Finding 'Critical Cn' for 'Total UNIQUE Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                "Here we are accumulating total unique edges, we sum the degrees of the vertices we sample, but we do not increase the sum if we " +
                "sample a vertex we have seen already. Using the expected values, " +
                "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we repeat vertices and the methods give less return. " +
                "The x axes are the number of samples taken, the y axes are the CCn values. ";
            GetCriticalCnBySamplesForErBaGraphs(preamble, RvTotalUniqueEdges, RnTotalUniqueEdges, "CriticalCN_TotalUniqueEdgesBySamples");

            Console.WriteLine($"Starting Max Degree by Samples {DTS}");
            preamble =
                "Finding 'Critical Cn' for 'Max Degree Vertex' collected. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                "Here we are collecting samples and tracking the largest degree vertex, what percent is that degree of the max degree of the graph." +
                "Using the expected values, " +
                "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we find larger degree vertices and most of our sampling doesn't change the result. " +
                "The x axes are the number of samples taken, the y axes are the CCn values. ";
            GetCriticalCnBySamplesForErBaGraphs(preamble, RvMaxDegreeIndex, RnMaxDegreeIndex, "CriticalCN_MaxDegreeBySamples");

        }

        static void GetCriticalCnBySamplesForErBaGraphs(string preamble, int rvLineIndex, int rnLineIndex, string outputdir)
        {
            /* YN 6/29/21 - Here we will iterate through the actual results, and calculate how much is paid for each unit of the result, and then
             * determine the Cn that makes RV and RN cost the same amount for their respective result totals.
             */

            if (!Directory.Exists(outputdir))
                Directory.CreateDirectory(outputdir);

            var colHeaders = new[] { "n vals", "m=2", "m=5", "m=8", "m=15", "m=25", "m=50", "m=100" };
            var rowHeaders = new[] { "n=500", "n=1000", "n=2000", "n=5000", "n=7500" };

            var nVals = new[] { 500, 1000, 2000, 5000, 7500 };
            var mVals = new[] { 2, 5, 8, 15, 25, 50, 100 };

            String tableHtml = "\n<h1>" + preamble + "</h1>\n";
            foreach (var type in new[] { "ER", "BA" })
            {
                tableHtml += "\n<h2>" + type + " Graphs</h2>\n";

                foreach (var d in new[] { 1.0, 0.33 })
                {
                    tableHtml += "\n<p>Taking " + d.ToString("0.0#") + " of the samples</p>\n";
                    HtmlTableReporter html = new HtmlTableReporter("", colHeaders, rowHeaders);

                    foreach (var nVal in nVals)
                        foreach (var mVal in mVals)
                        {
                            var fileName = GetFileName(type, nVal, mVal);
                            if (File.Exists(fileName))
                            {
                                var imgName = $"{type}_N_{nVal}_M_{mVal}_({d:0.0#}samples).png";
                                var text = CreateCCnBySamplesCell(fileName, rvLineIndex, rnLineIndex, $"{outputdir}\\{imgName}", $"{type} N={nVal} M={mVal}", d);
                                html.AddCell(text, imgName);
                            }
                            else
                            {
                                html.AddCell($"No file found for {type}, n={nVal}, m={mVal}", "");
                            }
                        }
                    tableHtml += html.GetTableHtml();
                }

            }
            File.WriteAllText($"{outputdir}\\Results.html", "<html>\n<body>\n" + tableHtml + "\n</body>\n</html>\n");
        }

        static void GetCriticalCnByResultsForErBaGraphs()
        {
            Console.WriteLine($"Starting Total Edges by Results {DTS}");
            string preamble =
                "Finding 'Critical Cn' for 'Total Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                "We are accumulating total edges, in other words the sum of all degrees of all vertices we sample. The plots are looking at the " +
                "results, so we are asking, 'With the goal of accumulating x edges, what is CCn?' " + 
                "Using the expected values, " +
                "we can predict CCn=(RN-RV)/RV. Because we are sampling with replacement, the assumption is that the results will match the prediction " +
                "perfectly with enough samples taken. The x axes are the number of total edges, the y axes are the CCn values.";
            GetCriticalCnByResultsForErBaGraphs(preamble, RvTotalEdgesIndex, RnTotalEdgesIndex, false, "CriticalCN_TotalEdgesByResults");

            Console.WriteLine($"Starting Total Unique Edges by Results {DTS}");
            preamble = 
                "Finding 'Critical Cn' for 'Total UNIQUE Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                "We are accumulating total unique edges, we sum the degrees of the vertices we sample, but we do not increase the sum if we " +
                "sample a vertex we have seen already. The plots are looking at the " +
                "results, so we are asking, 'With the goal of accumulating x unique edges, what is CCn?' " +
                "Using the expected values, " +
                "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we repeat vertices and the methods give less return. " +
                "The x axes are the percent of total edges accumulated, the y axes are the CCn values.";
            GetCriticalCnByResultsForErBaGraphs(preamble, RvTotalUniqueEdges, RnTotalUniqueEdges, true, "CriticalCN_TotalUniqueEdgesByResults");

            Console.WriteLine($"Starting Max Degree by Results {DTS}");
            preamble = 
                "Finding 'Critical Cn' for 'Max Degree Vertex' collected. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                "We are collecting samples and tracking the largest degree vertex, what percent is that degree of the max degree of the graph." +
                "The plots are looking at the " +
                "results, so we are asking, 'With the goal of obtaining a vertex with degree d of the max degree, what is CCn?' " +
                "Using the expected values, " +
                "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we find larger degree vertices and most of our sampling doesn't change the result. " +
                "The x axes are the percent of max degree, the y axes are the CCn values. ";
            GetCriticalCnByResultsForErBaGraphs(preamble, RvMaxDegreeIndex, RnMaxDegreeIndex, false, "CriticalCN_MaxDegreeByResults");

        }

        static void GetCriticalCnByResultsForErBaGraphs(string preamble, int rvLineIndex, int rnLineIndex, bool usePercentOfMaxOfLine, string outputdir)
        {
            /* YN 6/29/21 - Here we will iterate through possible results, basically iterating through RN's actual results
             * and finding the index where RV gives that result or more, and determining the cost that allows RV to achieve what RN
             * achieved.
             */

            if (!Directory.Exists(outputdir))
                Directory.CreateDirectory(outputdir);

            var colHeaders = new[] { "n vals", "m=2", "m=5", "m=8", "m=15", "m=25", "m=50", "m=100" };
            var rowHeaders = new[] { "n=500", "n=1000", "n=2000", "n=5000", "n=7500" };

            var nVals = new[] { 500, 1000, 2000, 5000, 7500 };
            var mVals = new[] { 2, 5, 8, 15, 25, 50, 100 };

            String tableHtml = "\n<h1>" + preamble + "</h1>\n";
            foreach (var type in new[] { "ER", "BA" })
            {
                tableHtml += "\n<h2>" + type + " Graphs</h2>\n";
                HtmlTableReporter html = new HtmlTableReporter("", colHeaders, rowHeaders);
                foreach (var nVal in nVals)
                    foreach (var mVal in mVals)
                    {
                        var fileName = GetFileName(type, nVal, mVal);
                        if (File.Exists(fileName))
                        {
                            var imgName = $"{type}_N_{nVal}_M_{mVal}.png";
                            var text = CreateCCnByResultsCell(fileName, rvLineIndex, rnLineIndex, $"{outputdir}\\{imgName}", $"{type} N={nVal} M={mVal}", usePercentOfMaxOfLine);
                            html.AddCell(text, imgName);
                        }
                        else
                        {
                            html.AddCell($"No file found for {type}, n={nVal}, m={mVal}", "");
                        }
                    }
                tableHtml += html.GetTableHtml();

            }
            File.WriteAllText($"{outputdir}\\Results.html", "<html>\n<body>\n" + tableHtml + "\n</body>\n</html>\n");
        }

        public static string[] CreateCCnBySamplesCell(String graphFileFullName, int rvLineIndex, int rnLineIndex, String imgOutputFileFullName, String graphDesc, double pctSamples = 1.0)
        {
            var fileLines = File.ReadAllLines(graphFileFullName);
            var meta = fileLines[MetaFileLineIndex].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]);
            var rv = double.Parse(meta["RV"]);
            var rn = double.Parse(meta["RN"]);
            var prediction = (rn - rv) / rv;
            var rvVals = Regex.Split(fileLines[rvLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rnVals = Regex.Split(fileLines[rnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            rvVals = rvVals.Take((int)(rvVals.Count * pctSamples)).ToList();
            rnVals = rnVals.Take((int)(rnVals.Count * pctSamples)).ToList();
            var criticalVals = Enumerable.Range(0, rvVals.Count).Select(i => (rnVals[i] - rvVals[i]) / rvVals[i]).ToArray();
            Py.CreatePyPlot(Py.PlotType.plot, Enumerable.Range(1, rvVals.Count).Select(i => (double)i).ToArray(),
                new[] { criticalVals.Select(i => prediction).ToArray(), criticalVals }, new[] { "(RN-RV)/RV", "Critical Cn" }, new[] { "r", "b" },
                $"{graphDesc} - Critical Cn by Samples", "Samples", "Critical Cn", imgOutputFileFullName);
            var text = new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };

            return text;
        }

        public static string[] CreateCCnByResultsCell(String graphFileFullName, int rvLineIndex, int rnLineIndex, String imgOutputFileFullName, String graphDesc, bool usePercentOfMaxOfLine = false)
        {
            var fileLines = File.ReadAllLines(graphFileFullName);
            var meta = fileLines[MetaFileLineIndex].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]);
            var rv = double.Parse(meta["RV"]);
            var rn = double.Parse(meta["RN"]);
            var prediction = (rn - rv) / rv;

            var rvVals = Regex.Split(fileLines[rvLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rnVals = Regex.Split(fileLines[rnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();

            if (usePercentOfMaxOfLine)
            {
                var maxOfRvLine = rvVals.Max();
                var maxOfRnLine = rnVals.Max();
                rvVals = rvVals.Select(v => v / maxOfRvLine).ToList();
                rnVals = rnVals.Select(v => v / maxOfRnLine).ToList();
            }

            List<double> rnResultVals = new List<double>();
            List<double> criticalCnVals = new List<double>();

            var currRvIndex = 0;
            var maxRvVal = rvVals.Max();
            for (int currRnIndex = 0; currRnIndex < rnVals.Count; currRnIndex++)
            {
                var currRnVal = rnVals[currRnIndex];
                if (currRnVal > maxRvVal)
                    break;
                var currRvVal = rvVals[currRvIndex];
                while (currRvVal < currRnVal)
                    currRvVal = rvVals[++currRvIndex];
                var currCCn = (currRnVal * (currRvIndex + 1)) / (currRnVal * (currRnIndex + 1)) - 1;
                rnResultVals.Add(currRnVal);
                criticalCnVals.Add(currCCn);
            }

            Py.CreatePyPlot(Py.PlotType.plot, rnResultVals.ToArray(),
                new[] { rnResultVals.Select(i => prediction).ToArray(), criticalCnVals.ToArray() }, new[] { "(RN-RV)/RV", "Critical Cn" }, new[] { "r", "b" },
                $"{graphDesc} - Critical Cn by Results", "Result", "Critical Cn", imgOutputFileFullName);
            return new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
        }


    }
}
