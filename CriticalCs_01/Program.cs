using MathNet.Numerics.Statistics;
using PyReporting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CriticalCs_01
{
    class Program
    {
        /* YN 7/1/21 - This program will be similar to CriticalCn_01. If we fix Cv=Cn=1, what Cs is the "break even" point for RN vs. RVRN,
         * in other words, keeping the first vertex or discarding it.
         * 
         * As with Critical Cn, we will do this in two ways. Comparing x samples of RN to x samples of RVRN and determining the CCs that
         * makes x samples give the same return for both methods when scaled by cost. The other is to take each return of RVRN, find the
         * number of samples required with RN to get that same return, and calculate the CCs that makes the two methods equal to achieve
         * the same return.
         * 
         * We will consider total accumulated edges, total unique edge endpoints, and percent of max degree. For total unique edges, you can
         * assume a predicted value based on expectations will be correct, this value should be 2RV/(RN-RV).
         * 
         * The results will be based on the result files from the TotalDegrees..._02 project in this solution.
         */

        static readonly string RESULTS_DIR = @"C:\Users\Yitzchak\Desktop\Visual Studio 2021\2021Research_Sol01\TotalDegreesOverCostBySamplesTaken_02\bin\Debug\Results";
        static string GetFileName(String type, int n, int m) => $"{RESULTS_DIR}\\{type.ToUpper()}_N_{n}_M_{m}_results.tsv";

        const int RnTotalEdgesIndex = 54;
        const int RvRnTotalEdgesIndex = 58;
        const int RnTotalUniqueEdgesIndex = 61;
        const int RvRnTotalUniqueEdgesIndex = 65;
        const int RnMaxDegreeIndex = 75;
        const int RvRnMaxDegreeIndex = 79;
        const int RnTotalSelectionsIndex = 68;
        const int RvRnTotalSelectionsIndex = 72;
        const int MetaFileLineIndex = 44;

        static string DTS => UtilsYN.Utils.DTS;

        static void Main(string[] args)
        {
            GetAllCriticalCsBySamples();
            GetAllCriticalCsByResults();
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        static void GetAllCriticalCsBySamples()
        {
            Console.WriteLine($"Starting Total Edges by Samples {DTS}");
            string preamble =
                "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                "Here we are accumulating total edges, in other words the sum of all degrees of all vertices we sample. Using the expected values, " +
                "we can predict CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                "Because we are sampling with replacement, the assumption is that the results will match the prediction " +
                "perfectly as more samples are taken. The x axes are the number of samples taken, the y axes are the CCs values.";
            GetCriticalCsBySamples(preamble, RnTotalEdgesIndex, RvRnTotalEdgesIndex, false, "CriticalCS_TotalEdgesBySamples");

            Console.WriteLine($"Starting Total Unique Edges by Samples {DTS}");
            preamble =
                "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                "Here we are accumulating total UNIQUE edges, we sum the degrees of the vertices we sample, but we do not increase the sum if we " +
                "sample a vertex we have seen already. Using the expected values, " +
                "we calculate CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                "As we continue to sample, both methods should have less return and CCs should decrease. " +
                "The x axes are the number of samples taken, the y axes are the CCs values.";
            GetCriticalCsBySamples(preamble, RnTotalUniqueEdgesIndex, RvRnTotalUniqueEdgesIndex, true, "CriticalCS_TotalUniqueEdgesBySamples");

            Console.WriteLine($"Starting Max Degree by Samples {DTS}");
            preamble =
                "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                "Here we are collecting samples and tracking the largest degree vertex, what percent is that degree of the max degree of the graph." +
                "Using the expected values, " +
                "we calculate CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                "As we continue to sample, both methods should have less return and CCs should decrease. " +
                "The x axes are the number of samples taken, the y axes are the CCs values.";
            GetCriticalCsBySamples(preamble, RnMaxDegreeIndex, RvRnMaxDegreeIndex, true, "CriticalCS_MaxDegreeBySamples");

        }

        static void GetAllCriticalCsByResults()
        {
            Console.WriteLine($"Starting Total Edges by Results {DTS}");
            string preamble =
                "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                "We are looking at total accumulated edges, in other words the sum of all degrees of all vertices we sample. " +
                "The plots are based on results, so it is as if we are asking, 'If our goal is to obtain x total edges, what is CCn?' " +
                "Using the expected values, " +
                "we can calculate CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                "Because we are sampling with replacement, the assumption is that the results will match the prediction " +
                "perfectly as more samples are taken. The x axes are the number of accumulated edges, the y axes are the CCs values.";
            GetCriticalCsByResults(preamble, RnTotalEdgesIndex, RvRnTotalEdgesIndex, false, false, "CriticalCS_TotalEdgesByResults");

            Console.WriteLine($"Starting Total Unique Edges by Results {DTS}");
            preamble =
                "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                "Here we are looking at total UNIQUE edges, we sum the degrees of the vertices we sample, but we do not increase the sum if we " +
                "sample a vertex we have seen already. " +
                "The plots are based on results, so it is as if we are asking, 'If our goal is to obtain p percent of the total edges, what is CCn?' " +
                "Using the expected values, " +
                "we calculate CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                "As we continue to sample, both methods should have less return and CCs should decrease. " +
                "The x axes are the percent of all edges accumulated, the y axes are the CCs values.";
            GetCriticalCsByResults(preamble, RnTotalUniqueEdgesIndex, RvRnTotalUniqueEdgesIndex, true, true, "CriticalCS_TotalUniqueEdgesByResults");

            Console.WriteLine($"Starting Max Degree by Results {DTS}");
            preamble =
                "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                "Here we are collecting samples and tracking the largest degree vertex, what percent is that degree of the max degree of the graph." +
                "The plots are based on results, so it is as if we are asking, 'If our goal is to obtain p percent of the max degree, what is CCn?' " +
                "Using the expected values, " +
                "we calculate CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                "As we continue to sample, both methods should have less return and CCs should decrease. " +
                "The x axes are the number of samples taken, the y axes are the CCs values.";
            GetCriticalCsByResults(preamble, RnMaxDegreeIndex, RvRnMaxDegreeIndex, false, true, "CriticalCS_MaxDegreeByResults");

        }

        static void GetCriticalCsBySamples(string preamble, int rnLineIndex, int rvrnLineIndex, bool onlySelections, string outputdir)
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
                                try
                                {
                                    var imgName = $"{type}_N_{nVal}_M_{mVal}_({d:0.0#}samples).png";
                                    var text = CreateCCsBySamplesCell(fileName, rvrnLineIndex, rnLineIndex, $"{outputdir}\\{imgName}", $"{type} N={nVal} M={mVal}", onlySelections, d);
                                    html.AddCell(text, imgName);
                                }
                                catch (Exception ex)
                                {
                                    html.AddCell($"An exception occurred for {type}, n={nVal}, m={mVal}", "");
                                }
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

        static void GetCriticalCsByResults(string preamble, int rnLineIndex, int rvrnLineIndex, bool usePercentOfMaxOfLine, bool onlySelections, string outputdir)
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
                tableHtml += "\n<h1>" + type + " Graphs</h1>\n";
                HtmlTableReporter html = new HtmlTableReporter("", colHeaders, rowHeaders);
                foreach (var nVal in nVals)
                    foreach (var mVal in mVals)
                    {
                        var fileName = GetFileName(type, nVal, mVal);
                        if (File.Exists(fileName))
                        {
                            try
                            {
                                var imgName = $"{type}_N_{nVal}_M_{mVal}.png";
                                var text = CreateCCsByResultsCell(fileName, rvrnLineIndex, rnLineIndex, $"{outputdir}\\{imgName}", $"{type} N={nVal} M={mVal}", onlySelections, usePercentOfMaxOfLine);
                                html.AddCell(text, imgName); 
                            }
                            catch (Exception ex)
                            {
                                html.AddCell($"An exception occurred for {type}, n={nVal}, m={mVal}", "");
                            }
                        }
                        else
                            html.AddCell($"No file found for {type}, n={nVal}, m={mVal}", "");
                    }
                tableHtml += html.GetTableHtml();

            }
            File.WriteAllText($"{outputdir}\\Results.html", "<html>\n<body>\n" + tableHtml + "\n</body>\n</html>\n");
        }

        public static String[] CreateCCsBySamplesCell(String graphFileFullName, int rvrnLineIndex, int rnLineIndex, String imgOutputFileFullName, String graphDesc, bool onlySelections, double pctSamples = 1.0)
        {
            var fileLines = File.ReadAllLines(graphFileFullName);
            var meta = fileLines[MetaFileLineIndex].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]);
            var rv = double.Parse(meta["RV"]);
            var rn = double.Parse(meta["RN"]);
            var prediction = 2 * rv / (rn - rv);
            var rvrnVals = Regex.Split(fileLines[rvrnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rnVals = Regex.Split(fileLines[rnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            rvrnVals = rvrnVals.Take((int)(rvrnVals.Count * pctSamples)).ToList();
            rnVals = rnVals.Take((int)(rnVals.Count * pctSamples)).ToList();
            var rvrnSelections = Regex.Split(fileLines[RvRnTotalSelectionsIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rnSelections = Regex.Split(fileLines[RnTotalSelectionsIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();

            List<double> criticalCsSamplesTaken = new List<double>();
            List<double> undefinedCCsSamplesTaken = new List<double>();

            List<double> criticalCsValues = new List<double>();

            for (int i = 0; i < rvrnVals.Count; i++)
            {
                var criticalCsValue = (i + 1) * (2 * rvrnVals[i] - rnVals[i]) /
                        (rnVals[i] * (onlySelections ? rvrnSelections[i] : (i + 1)) - rvrnVals[i] * (onlySelections ? rnSelections[i] : (i + 1)));
                if (Double.IsInfinity(criticalCsValue))
                    undefinedCCsSamplesTaken.Add(i + 1);
                else
                {
                    criticalCsSamplesTaken.Add(i + 1);
                    criticalCsValues.Add(criticalCsValue);
                }
            }

            var midRange = (criticalCsValues.Max() - criticalCsValues.Min()) / 2.0 + criticalCsValues.Min();

            Py.CreatePyPlot(
                new[] { Py.PlotType.plot, Py.PlotType.scatter, Py.PlotType.scatter },
                new[] { Enumerable.Range(1, rnVals.Count).Select(i => (double)i).ToArray(), criticalCsSamplesTaken.ToArray(), undefinedCCsSamplesTaken.ToArray() },
                new[] { Enumerable.Range(1, rnVals.Count).Select(i => prediction).ToArray(), criticalCsValues.ToArray(), undefinedCCsSamplesTaken.Select(i => midRange).ToArray() },
                new[] { "2RV/(RN-RV)", "Critical Cs", "Undefined CCs" }, new[] { "r", "b", "g" },
                $"{graphDesc} - Critical Cs by Samples", "Samples", "Critical Cs", imgOutputFileFullName);
            return new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
        }

        public static string[] CreateCCsByResultsCell(String graphFileFullName, int rvrnLineIndex, int rnLineIndex, String imgOutputFileFullName, String graphDesc, bool onlySelections, bool usePercentOfMaxOfLine = false)
        {
            var fileLines = File.ReadAllLines(graphFileFullName);
            var meta = fileLines[MetaFileLineIndex].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]);
            var rv = double.Parse(meta["RV"]);
            var rn = double.Parse(meta["RN"]);
            var prediction = 2 * rv / (rn - rv);

            var rnVals = Regex.Split(fileLines[rnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rvrnVals = Regex.Split(fileLines[rvrnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            if (usePercentOfMaxOfLine)
            {
                var maxRnLineVal = rnVals.Max();
                var maxRvrnLineVal = rvrnVals.Max();

                rnVals = rnVals.Select(v => v / maxRnLineVal).ToList();
                rvrnVals = rvrnVals.Select(v => v / maxRvrnLineVal).ToList();
            }
            var rnSelections = Regex.Split(fileLines[RnTotalSelectionsIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rvrnSelections = Regex.Split(fileLines[RvRnTotalSelectionsIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();

            List<double> definedResultValues = new List<double>();
            List<double> undefinedResultValues = new List<double>();
            List<double> allResultValues = new List<double>();

            List<double> definedCCsValues = new List<double>();

            var currRvrnIndex = 0;
            var maxRvrnVal = rvrnVals.Max();
            for (int currRnIndex = 0; currRnIndex < rnVals.Count; currRnIndex++)
            {
                var currRnVal = rnVals[currRnIndex];
                if (currRnVal > maxRvrnVal)
                    break;
                var currRvrnVal = rvrnVals[currRvrnIndex];
                while (currRvrnVal < currRnVal)
                    currRvrnVal = rvrnVals[++currRvrnIndex];

                var currCCs =
                    (currRvrnIndex + 1 - 2 * (currRnIndex + 1)) /
                    ((onlySelections ? rnSelections[currRnIndex] : (currRnIndex + 1)) - (onlySelections ? rvrnSelections[currRvrnIndex] : (currRvrnIndex + 1)));

                allResultValues.Add(currRnVal);

                if (Double.IsInfinity(currCCs))
                    undefinedResultValues.Add(currRnVal);
                else
                {
                    definedResultValues.Add(currRnVal);
                    definedCCsValues.Add(currCCs);
                }

            }
            var midRange = (definedCCsValues.Max() - definedCCsValues.Min()) / 2.0 + definedCCsValues.Min();

            Py.CreatePyPlot(
                new[] { Py.PlotType.plot, Py.PlotType.scatter, Py.PlotType.scatter },
                new[] { allResultValues.ToArray(), definedResultValues.ToArray(), undefinedResultValues.ToArray() },
                new[] { allResultValues.Select(i => prediction).ToArray(), definedCCsValues.ToArray(), undefinedResultValues.Select(d => midRange).ToArray() },
                new[] { "2RV/(RN-RV)", "Critical Cs", "Undefined CCs" }, new[] { "r", "b", "g" },
                $"{graphDesc} - Critical Cs by Results", "Result", "Critical Cs", imgOutputFileFullName);
            return new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
        }
    }
}
