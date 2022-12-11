using PyReporting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CriticalCnCs_01
{
    class Program
    {
        /* YN 7/7/21 - Originally did Critical Cn and Cs separately, going to try to combine them here to make
         * more modular code in order to test RW graphs next.
         */

        static readonly string RESULTS_DIR = @"C:\Users\Yitzchak\Desktop\Visual Studio 2021\2021Research_Sol01\TotalDegreesOverCostBySamplesTaken_02\bin\Debug\Results";
        static string GetFileName(String type, int n, int m) => $"{RESULTS_DIR}\\{type.ToUpper()}_N_{n}_M_{m}_results.tsv";

        const int RvTotalEdgesIndex = 53;
        const int RnTotalEdgesIndex = 54;
        const int RvRnTotalEdgesIndex = 58;

        const int RvTotalUniqueEdgesIndex = 60;
        const int RnTotalUniqueEdgesIndex = 61;
        const int RvRnTotalUniqueEdgesIndex = 65;

        const int RvMaxDegreeIndex = 74;
        const int RnMaxDegreeIndex = 75;
        const int RvRnMaxDegreeIndex = 79;

        const int RnTotalSelectionsIndex = 68;
        const int RvRnTotalSelectionsIndex = 72;

        const int MetaFileLineIndex = 44;
        enum ReportType
        {
            CCnBySamples,
            CCnByResults,
            CCsBySamples,
            CCsByResults
        }
        static string DTS => UtilsYN.Utils.DTS;

        static void Main(string[] args)
        {
            Console.WriteLine($"Starting program {DTS}");

            FullProgram_01(args);

            Console.WriteLine($"Done {DTS}");
            Console.ReadKey();
        }

        static void SpecialRunForPaper_01()
        {

        }

        static void FullProgram_01(string[] args)
        {
            var tasks = GetCriticalCnBySamplesForSyntheticGraphs_AllMeasures();
            tasks = tasks.Union(GetCriticalCnByResultsForSyntheticGraphs_AllMeasures()).ToList();
            tasks = tasks.Union(GetCriticalCsBySamplesForSyntheticGraphs_AllMeasures()).ToList();
            tasks = tasks.Union(GetCriticalCsByResultsForSyntheticGraphs_AllMeasures()).ToList();
            tasks.ForEach(t => t.Wait());
            Console.WriteLine($"Starting RW {DTS}");
            //GetCriticalCnCsBySamplesForRwGraphs();
        }

        static void GetCriticalCnCsBySamplesForRwGraphs()
        {
            var allRwGraphs = new DirectoryInfo(RESULTS_DIR).GetFiles().Where(f => f.Name.StartsWith("RW")).Select(fi =>
                new { fi.FullName, fi.Name, meta = File.ReadAllLines(fi.FullName)[MetaFileLineIndex].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]) })
                .Select(gInfo => new
                {
                    gInfo.Name,
                    gInfo.FullName,
                    name = gInfo.meta["GraphType"],
                    n = int.Parse(gInfo.meta["N"]),
                    m = int.Parse(gInfo.meta["M"]),
                    assort = double.Parse(gInfo.meta["Assort"]),
                    Afi = double.Parse(gInfo.meta["AFI"]),
                    Category = gInfo.meta["Category"],
                    BipPerMeta = gInfo.meta["BipPerMeta"],
                    BipPerComments = gInfo.meta["BipPerComments"]
                }).ToList();

            var outputDir = @"RW_Results";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            StringBuilder allHtml = new StringBuilder();
            allHtml.AppendLine("<html>");
            allHtml.AppendLine("<body>");
            allHtml.AppendLine("<h2><center>Contents:</center></h2>");

            allHtml.AppendLine("<p><center><a href='#0-1.0-BySamples'>CCn for Total Edges By Samples (All Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#0-0.3-BySamples'>CCn for Total Edges By Samples (.33 of Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#0-ByResults'>CCn for Total Edges By Results</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#1-1.0-BySamples'>CCn for Total Unique Edges By Samples (All Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#1-0.3-BySamples'>CCn for Total Unique Edges By Samples (.33 of Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#1-ByResults'>CCn for Total Unique Edges By Results</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#2-1.0-BySamples'>CCn for Max Degree By Samples (All Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#2-0.3-BySamples'>CCn for Max Degree By Samples (.33 of Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#2-ByResults'>CCn for Max Degree By Results</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#3-1.0-BySamples'>CCs for Total Edges By Samples (All Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#3-0.3-BySamples'>CCs for Total Edges By Samples (.33 of Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#3-ByResults'>CCs for Total Edges By Results</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#4-1.0-BySamples'>CCs for Total Unique Edges By Samples (All Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#4-0.3-BySamples'>CCs for Total Unique Edges By Samples (.33 of Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#4-ByResults'>CCs for Total Unique Edges By Results</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#5-1.0-BySamples'>CCs for Max Degree By Samples (All Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#5-0.3-BySamples'>CCs for Max Degree By Samples (.33 of Samples Taken)</a></center></p>");
            allHtml.AppendLine("<p><center><a href='#5-ByResults'>CCs for Max Degree By Results</a></center></p>");

            StringBuilder[] reportsHtml = Enumerable.Range(0, 6).Select(i => new StringBuilder()).ToArray();
            reportsHtml[0].AppendLine($"<h1><center>Real World Graph Results - CCn Total Edges</center></h1>");
            reportsHtml[1].AppendLine($"<h1><center>Real World Graph Results - CCn Total Unique Edges</center></h1>");
            reportsHtml[2].AppendLine($"<h1><center>Real World Graph Results - CCn Max Degree</center></h1>");
            reportsHtml[3].AppendLine($"<h1><center>Real World Graph Results - CCs Total Edges</center></h1>");
            reportsHtml[4].AppendLine($"<h1><center>Real World Graph Results - CCs Total Unique Edges</center></h1>");
            reportsHtml[5].AppendLine($"<h1><center>Real World Graph Results - CCs Max Degree</center></h1>");

            Enumerable.Range(0, 6).ToList().ForEach(i => reportsHtml[i].AppendLine($"<h2><center>x Axes are Samples Taken</center></h2>"));

            int graphCounter;
            foreach (var d in new[] { 1.0, .33 })
            {
                Console.WriteLine($"Starting d={d} {DTS}");
                Enumerable.Range(0, 6).ToList().ForEach(i => reportsHtml[i].AppendLine($"<h3 id='{i}-{d:0.0}-BySamples'><center>Taking {d:0.00#} of the Samples</center></h3>"));
                graphCounter = 1;
                foreach (var graph in allRwGraphs)
                {
                    Parallel.For(0, reportsHtml.Length, i =>
                    {
                        var currHtmlReport = reportsHtml[i];
                        try
                        {

                            currHtmlReport.Append("<p><center>");
                            currHtmlReport.AppendLine($"Name: {graph.name} (#{graphCounter} of {allRwGraphs.Count()}), Category: {graph.Category} N={graph.n}, M={graph.m} <br />");
                            currHtmlReport.AppendLine($"Assort: {graph.assort:0.000#}, AFI: {graph.Afi:0.000#}, Bipartite: ({graph.BipPerMeta[0]},{graph.BipPerComments[0]})</center></p>");
                            string imgName = "";
                            switch (i)
                            {
                                case 0:
                                    imgName = $"{graph.name}_CCnTotalEdgesSamp_({d}_Samples).png";
                                    CreateCCnBySamplesCell(graph.FullName, RvTotalEdgesIndex, RnTotalEdgesIndex, $"{outputDir}\\{imgName}", graph.name, d);
                                    break;
                                case 1:
                                    imgName = $"{graph.name}_CCnTotalUnqEdgesSamp_({d}_Samples).png";
                                    CreateCCnBySamplesCell(graph.FullName, RvTotalUniqueEdgesIndex, RnTotalUniqueEdgesIndex, $"{outputDir}\\{imgName}", graph.name, d);
                                    break;
                                case 2:
                                    imgName = $"{graph.name}_CCnMaxDegSamp_({d}_Samples).png";
                                    CreateCCnBySamplesCell(graph.FullName, RvMaxDegreeIndex, RnMaxDegreeIndex, $"{outputDir}\\{imgName}", graph.name, d);
                                    break;
                                case 3:
                                    imgName = $"{graph.name}_CCsTotalEdgesSamp_({d}_Samples).png";
                                    CreateCCsBySamplesCell(graph.FullName, RvRnTotalEdgesIndex, RnTotalEdgesIndex, $"{outputDir}\\{imgName}", graph.Name, false, d);
                                    break;
                                case 4:
                                    imgName = $"{graph.name}_CCsTotalUnqEdgesSamp_({d}_Samples).png";
                                    CreateCCsBySamplesCell(graph.FullName, RvRnTotalUniqueEdgesIndex, RnTotalUniqueEdgesIndex, $"{outputDir}\\{imgName}", graph.name, true, d);
                                    break;
                                case 5:
                                    imgName = $"{graph.name}_CCsMaxDegSamp_({d}_Samples).png";
                                    CreateCCsBySamplesCell(graph.FullName, RvRnMaxDegreeIndex, RnMaxDegreeIndex, $"{outputDir}\\{imgName}", graph.Name, false, d);
                                    break;
                                default:
                                    throw new Exception($"Invalid option: {i}");
                            }
                            currHtmlReport.AppendLine($"<center><img src='{imgName}' /></center>");
                        }
                        catch (Exception ex)
                        {
                            currHtmlReport.AppendLine($"<p><strong><center>An error occurred for graph: {graph.name}<center></strong></p>");
                        }
                    });
                    graphCounter++;
                }
            }

            Enumerable.Range(0, 6).ToList().ForEach(i => reportsHtml[i].AppendLine($"<h2 id='{i}-ByResults'><center>x Axes are Results</center></h2>"));
            Console.WriteLine($"Starting 'By Results' {DTS}");
            graphCounter = 1;
            foreach (var graph in allRwGraphs)
            {
                Parallel.For(0, reportsHtml.Length, i =>
                {
                    var currHtmlReport = reportsHtml[i];
                    try
                    {
                        currHtmlReport.Append("<p><center>");
                        currHtmlReport.AppendLine($"Name: {graph.name} (#{graphCounter} of {allRwGraphs.Count()}), Category: {graph.Category} N={graph.n}, M={graph.m} <br />");
                        currHtmlReport.AppendLine($"Assort: {graph.assort:0.000#}, AFI: {graph.Afi:0.000#}, Bipartite: ({graph.BipPerMeta[0]},{graph.BipPerComments[0]})</center></p>");
                        string imgName = "";
                        switch (i)
                        {
                            case 0:
                                imgName = $"{graph.name}_CCnTotalEdgesResults.png";
                                CreateCCnByResultsCell(graph.FullName, RvTotalEdgesIndex, RnTotalEdgesIndex, $"{outputDir}\\{imgName}", graph.name, false);
                                break;
                            case 1:
                                imgName = $"{graph.name}_CCnTotalUnqEdgesResults.png";
                                CreateCCnByResultsCell(graph.FullName, RvTotalUniqueEdgesIndex, RnTotalUniqueEdgesIndex, $"{outputDir}\\{imgName}", graph.name, true);
                                break;
                            case 2:
                                imgName = $"{graph.name}_CCnMaxDegResults.png";
                                CreateCCnByResultsCell(graph.FullName, RvMaxDegreeIndex, RnMaxDegreeIndex, $"{outputDir}\\{imgName}", graph.name, false);
                                break;
                            case 3:
                                imgName = $"{graph.name}_CCsTotalEdgesResults.png";
                                CreateCCsByResultsCell(graph.FullName, RvRnTotalEdgesIndex, RnTotalEdgesIndex, $"{outputDir}\\{imgName}", graph.Name, false);
                                break;
                            case 4:
                                imgName = $"{graph.name}_CCsTotalUnqEdgesResults.png";
                                CreateCCsByResultsCell(graph.FullName, RvRnTotalUniqueEdgesIndex, RnTotalUniqueEdgesIndex, $"{outputDir}\\{imgName}", graph.name, true);
                                break;
                            case 5:
                                imgName = $"{graph.name}_CCsMaxDegResults.png";
                                CreateCCsByResultsCell(graph.FullName, RvRnMaxDegreeIndex, RnMaxDegreeIndex, $"{outputDir}\\{imgName}", graph.Name, false);
                                break;
                            default:
                                throw new Exception($"Invalid option: {i}");
                        }

                        currHtmlReport.AppendLine($"<center><img src='{imgName}' /></center>");
                    }
                    catch (Exception ex)
                    {
                        currHtmlReport.AppendLine($"<p><strong><center>An error occurred for graph: {graph.name}</center></strong></p>");
                    }
                });
                graphCounter++;
            }

            Enumerable.Range(0, 6).ToList().ForEach(i => allHtml.AppendLine(reportsHtml[i].ToString()));


            allHtml.AppendLine("</body>");
            allHtml.AppendLine("</html>");
            File.WriteAllText(outputDir + "\\RW_Results.html", allHtml.ToString());
            File.WriteAllText(outputDir + "\\RwCCnTotEdg.html",
               "<html><title>RW CCn Tot Edges</title>\n<body>\n" + reportsHtml[0].ToString() + "\n</body></html>");

            File.WriteAllText(outputDir + "\\RwCCnTotUnqEdg.html",
                "<html><title>RW CCn Tot Unq Edges</title>\n<body>\n" + reportsHtml[1].ToString() + "\n</body></html>");

            File.WriteAllText(outputDir + "\\RwCCnMaxDeg.html",
                "<html><title>RW CCn Max Degree</title>\n<body>\n" + reportsHtml[2].ToString() + "\n</body></html>");

            File.WriteAllText(outputDir + "\\RwCCsTotEdg.html",
                "<html><title>RW CCs Tot Edges</title>\n<body>\n" + reportsHtml[3].ToString() + "\n</body></html>");

            File.WriteAllText(outputDir + "\\RwCCsTotUnqEdg.html",
                "<html><title>RW CCs Tot Unq Edges</title>\n<body>\n" + reportsHtml[4].ToString() + "\n</body></html>");

            File.WriteAllText(outputDir + "\\RwCCsMaxDeg.html",
                "<html><title>RW CCs Max Degree</title>\n<body>\n" + reportsHtml[5].ToString() + "\n</body></html>");
        }
         
        static List<Task> GetCriticalCnBySamplesForSyntheticGraphs_AllMeasures()
        {
            var Pcts = new[] { 1.0, .33 };

            List<Task> tasks = new List<Task>();

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Total Edges by Samples {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Total Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "Here we are accumulating total edges, in other words the sum of all degrees of all vertices we sample. Using the expected values, " +
                    "we can predict CCn=(RN-RV)/RV. Because we are sampling with replacement, the assumption is that the results will match the prediction " +
                    "perfectly as more samples are taken. The x axes are the number of samples taken, the y axes are the CCn values.";
                WriteReportForSytheticGraphs(
                    ReportType.CCnBySamples, RvTotalEdgesIndex, RnTotalEdgesIndex, "CriticalCn_TotalEdgesBySamples", preamble, Pcts, false, false, "CriticalCn_TotalEdgesBySamples");
                Console.WriteLine($"Finished Total Edges by Samples {DTS}");
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Total Unique Edges by Samples {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Total UNIQUE Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "Here we are accumulating total unique edges, we sum the degrees of the vertices we sample, but we do not increase the sum if we " +
                    "sample a vertex we have seen already. Using the expected values, " +
                    "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we repeat vertices and the methods give less return. " +
                    "The x axes are the number of samples taken, the y axes are the CCn values. ";
                WriteReportForSytheticGraphs(
                    ReportType.CCnBySamples, RvTotalUniqueEdgesIndex, RnTotalUniqueEdgesIndex, "CriticalCn_TotalUniqueEdgesBySamples", preamble, Pcts, false, true, "CriticalCn_TotalUniqueEdgesBySamples");
                Console.WriteLine($"Finished Total Unique Edges by Samples {DTS}");
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Max Degree by Samples {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Max Degree Vertex' collected. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "Here we are collecting samples and tracking the largest degree vertex, what percent is that degree of the max degree of the graph." +
                    "Using the expected values, " +
                    "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we find larger degree vertices and most of our sampling doesn't change the result. " +
                    "The x axes are the number of samples taken, the y axes are the CCn values. ";
                WriteReportForSytheticGraphs(
                    ReportType.CCnBySamples, RvMaxDegreeIndex, RnMaxDegreeIndex, "CriticalCn_MaxDegreeBySamples", preamble, Pcts, false, false, "CriticalCn_MaxDegreeBySamples");
                Console.WriteLine($"Finished Max Degree by Samples {DTS}");
            }));
            return tasks;
        }

        static List<Task> GetCriticalCnByResultsForSyntheticGraphs_AllMeasures()
        {
            var Pcts = new[] { 1.0 };
            List<Task> tasks = new List<Task>();

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Total Edges by Results {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Total Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "We are accumulating total edges, in other words the sum of all degrees of all vertices we sample. The plots are looking at the " +
                    "results, so we are asking, 'With the goal of accumulating x edges, what is CCn?' " +
                    "Using the expected values, " +
                    "we can predict CCn=(RN-RV)/RV. Because we are sampling with replacement, the assumption is that the results will match the prediction " +
                    "perfectly with enough samples taken. The x axes are the number of total edges, the y axes are the CCn values.";
                WriteReportForSytheticGraphs(
                    ReportType.CCnByResults, RvTotalEdgesIndex, RnTotalEdgesIndex, "CriticalCn_TotalEdgesByResults", preamble, Pcts, false, false, "CriticalCn_TotalEdgesByResults");
                Console.WriteLine($"Finished Total Edges by Results {DTS}");
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {

                Console.WriteLine($"Starting Total Unique Edges by Results {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Total UNIQUE Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "We are accumulating total unique edges, we sum the degrees of the vertices we sample, but we do not increase the sum if we " +
                    "sample a vertex we have seen already. The plots are looking at the " +
                    "results, so we are asking, 'With the goal of accumulating x unique edges, what is CCn?' " +
                    "Using the expected values, " +
                    "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we repeat vertices and the methods give less return. " +
                    "The x axes are the percent of total edges accumulated, the y axes are the CCn values.";
                WriteReportForSytheticGraphs(
                    ReportType.CCnByResults, RvTotalUniqueEdgesIndex, RnTotalUniqueEdgesIndex, "CriticalCn_TotalUniqueEdgesByResults", preamble, Pcts, false, true, "CriticalCn_TotalUniqueEdgesByResults");
                Console.WriteLine($"Finished Total Unique Edges by Results {DTS}");
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Max Degree by Results {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Max Degree Vertex' collected. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "We are collecting samples and tracking the largest degree vertex, what percent is that degree of the max degree of the graph." +
                    "The plots are looking at the " +
                    "results, so we are asking, 'With the goal of obtaining a vertex with degree d of the max degree, what is CCn?' " +
                    "Using the expected values, " +
                    "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we find larger degree vertices and most of our sampling doesn't change the result. " +
                    "The x axes are the percent of max degree, the y axes are the CCn values. ";
                WriteReportForSytheticGraphs(
                        ReportType.CCnByResults, RvMaxDegreeIndex, RnMaxDegreeIndex, "CriticalCn_MaxDegreeByResults", preamble, Pcts, false, false, "CriticalCn_MaxDegreeByResults");
                Console.WriteLine($"Finished Total Unique Edges by Results {DTS}");
            }));
            return tasks;
        }

        static List<Task> GetCriticalCsBySamplesForSyntheticGraphs_AllMeasures()
        {
            var Pcts = new[] { 1.0, .33 };
            List<Task> tasks = new List<Task>();

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Total Edges by Samples {DTS}");
                string preamble =
                    "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                    "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                    "Here we are accumulating total edges, in other words the sum of all degrees of all vertices we sample. Using the expected values, " +
                    "we can predict CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                    "Because we are sampling with replacement, the assumption is that the results will match the prediction " +
                    "perfectly as more samples are taken. The x axes are the number of samples taken, the y axes are the CCs values.";
                WriteReportForSytheticGraphs(
                    ReportType.CCsBySamples, RvRnTotalEdgesIndex, RnTotalEdgesIndex, "CriticalCs_TotalEdgesBySamples", preamble, Pcts, false, false, "CriticalCs_TotalEdgesBySamples");
                Console.WriteLine($"Finished Total Edges by Samples {DTS}");
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Total Unique Edges by Samples {DTS}");
                string preamble =
                    "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                    "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                    "Here we are accumulating total UNIQUE edges, we sum the degrees of the vertices we sample, but we do not increase the sum if we " +
                    "sample a vertex we have seen already. Using the expected values, " +
                    "we calculate CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                    "As we continue to sample, both methods should have less return and CCs should decrease. " +
                    "The x axes are the number of samples taken, the y axes are the CCs values.";
                WriteReportForSytheticGraphs(
                    ReportType.CCsBySamples, RvRnTotalUniqueEdgesIndex, RnTotalUniqueEdgesIndex, "CriticalCs_TotalUniqueEdgesBySamples", preamble, Pcts, true, true, "CriticalCs_TotalUniqueEdgesBySamples");
                Console.WriteLine($"Finished Total Unique Edges by Samples {DTS}");
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Max Degree by Samples {DTS}");
                string preamble =
                    "Calculating 'Critical Cs', if we fix Cv=Cn=1, but we charge Cs to keep a vertex, for what Cs do RVRN (paying Cs to keep the first vertex) " +
                    "and RN (discarding the first vertex and only paying Cs for the neighbor) perform the same. " +
                    "Here we are collecting samples and tracking the largest degree vertex, what percent is that degree of the max degree of the graph." +
                    "Using the expected values, " +
                    "we calculate CCs=2RV/(RN-RV). Note that in any case where RN and RV give the same result, CCs is undefined. " +
                    "As we continue to sample, both methods should have less return and CCs should decrease. " +
                    "The x axes are the number of samples taken, the y axes are the CCs values.";
                WriteReportForSytheticGraphs(
                    ReportType.CCsBySamples, RvRnMaxDegreeIndex, RnMaxDegreeIndex, "CriticalCs_MaxDegreeBySamples", preamble, Pcts, true, false, "CriticalCs_MaxDegreeBySamples");
                Console.WriteLine($"Finished Max Degree by Samples {DTS}");
            }));

            return tasks;
        }

        static List<Task> GetCriticalCsByResultsForSyntheticGraphs_AllMeasures()
        {
            var Pcts = new[] { 1.0 };

            List<Task> tasks = new List<Task>();

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Total Edges by Results {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Total Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "We are accumulating total edges, in other words the sum of all degrees of all vertices we sample. The plots are looking at the " +
                    "results, so we are asking, 'With the goal of accumulating x edges, what is CCn?' " +
                    "Using the expected values, " +
                    "we can predict CCn=(RN-RV)/RV. Because we are sampling with replacement, the assumption is that the results will match the prediction " +
                    "perfectly with enough samples taken. The x axes are the number of total edges, the y axes are the CCn values.";
                WriteReportForSytheticGraphs(
                     ReportType.CCsByResults, RvRnTotalEdgesIndex, RnTotalEdgesIndex, "CriticalCs_TotalEdgesByResults", preamble, Pcts, false, false, "CriticalCs_TotalEdgesByResults");
                Console.WriteLine($"Finished Total Edges by Results {DTS}");
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Total Unique Edges by Results {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Total UNIQUE Edges' accumulated. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "We are accumulating total unique edges, we sum the degrees of the vertices we sample, but we do not increase the sum if we " +
                    "sample a vertex we have seen already. The plots are looking at the " +
                    "results, so we are asking, 'With the goal of accumulating x unique edges, what is CCn?' " +
                    "Using the expected values, " +
                    "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we repeat vertices and the methods give less return. " +
                    "The x axes are the percent of total edges accumulated, the y axes are the CCn values.";
                WriteReportForSytheticGraphs(
                     ReportType.CCsByResults, RvRnTotalUniqueEdgesIndex, RnTotalUniqueEdgesIndex, "CriticalCs_TotalUniqueEdgesByResults", preamble, Pcts, true, true, "CriticalCs_TotalUniqueEdgesByResults");
                Console.WriteLine($"Finished Total Unique Edges by Results {DTS}");
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Starting Max Degree by Results {DTS}");
                string preamble =
                    "Finding 'Critical Cn' for 'Max Degree Vertex' collected. If we fix Cv=1, for what value of Cn do RV and RN peform the same?" +
                    "We are collecting samples and tracking the largest degree vertex, what percent is that degree of the max degree of the graph." +
                    "The plots are looking at the " +
                    "results, so we are asking, 'With the goal of obtaining a vertex with degree d of the max degree, what is CCn?' " +
                    "Using the expected values, " +
                    "we would calculate CCn=(RN-RV)/RV, but here CCn should decrease as we find larger degree vertices and most of our sampling doesn't change the result. " +
                    "The x axes are the percent of max degree, the y axes are the CCn values. ";
                WriteReportForSytheticGraphs(
                     ReportType.CCsByResults, RvRnMaxDegreeIndex, RnMaxDegreeIndex, "CriticalCs_MaxDegreeByResults", preamble, Pcts, true, false, "CriticalCs_MaxDegreeByResults");
                Console.WriteLine($"Finished Max Degree by Results {DTS}");
            }));

            return tasks;
        }

        static void WriteReportForSytheticGraphs(
            ReportType reportType, int weakMethodIndex, int strongMethodIndex, string outputDir, string reportPreamble,
            double[] percentSamplesToTake, bool onlySelections, bool valsAsPercent, string reportName = "Results")
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var colHeaders = new[] { "n vals", "m=2", "m=5", "m=8", "m=15", "m=25", "m=50", "m=100" };
            var rowHeaders = new[] { "n=500", "n=1000", "n=2000", "n=5000", "n=7500" };

            var nVals = new[] { 500, 1000, 2000, 5000, 7500 };
            var mVals = new[] { 2, 5, 8, 15, 25, 50, 100 };

            // Special Run Code
            colHeaders = new[] { "n vals", "m=2", "m=5", "m=8", "m=15" };
            rowHeaders = new[] { "n=500", "n=1000", "n=2000", "n=5000" };

            nVals = new[] { 500, 1000, 2000, 5000 };
            mVals = new[] { 2, 5, 8, 15 };

            colHeaders = new[] { "n vals", "m=8"};
            rowHeaders = new[] { "n=1000" };

            nVals = new[] { 1000 };
            mVals = new[] { 8 };

            String allHtml = "\n<h1>" + reportPreamble + "</h1>\n";
            foreach (var type in new[] { "ER", "BA" })
            {
                allHtml += "\n<h2>" + type + " Graphs</h2>\n";
                foreach (var d in percentSamplesToTake)
                {
                    if (percentSamplesToTake.Length > 1)
                        allHtml += $"\n<p>Taking {d:0.0#} of the samples</p>\n";
                    HtmlTableReporter htmlTable = new HtmlTableReporter("", colHeaders, rowHeaders);
                    foreach (var nVal in nVals)
                        foreach (var mVal in mVals)
                        {
                            var fileName = GetFileName(type, nVal, mVal);
                            if (File.Exists(fileName))
                            {
                                var imgName = $"{type}_N_{nVal}_M_{mVal}({d:0.0#}pct).png";
                                string[] text;
                                switch (reportType)
                                {
                                    case ReportType.CCnBySamples:
                                        text = CreateCCnBySamplesCell(fileName, weakMethodIndex, strongMethodIndex, $"{outputDir}\\\\{imgName}", $"{type} Graph n={nVal} m={mVal}", d);
                                        break;
                                    case ReportType.CCnByResults:
                                        text = CreateCCnByResultsCell(fileName, weakMethodIndex, strongMethodIndex, $"{outputDir}\\\\{imgName}", $"{type} Graph n={nVal} m={mVal}", valsAsPercent);
                                        break;
                                    case ReportType.CCsBySamples:
                                        text = CreateCCsBySamplesCell(fileName, weakMethodIndex, strongMethodIndex, $"{outputDir}\\\\{imgName}", $"{type} Graph n={nVal} m={mVal}", onlySelections, d);
                                        break;
                                    case ReportType.CCsByResults:
                                        text = CreateCCsByResultsCell(fileName, weakMethodIndex, strongMethodIndex, $"{outputDir}\\\\{imgName}", $"{type} Graph n={nVal} m={mVal}", valsAsPercent);
                                        break;
                                    default:
                                        throw new Exception($"Undefined ReportType: {reportType}");
                                }
                                htmlTable.AddCell(text, imgName);
                            }
                            else
                            {
                                htmlTable.AddCell($"No file found for {type}, n={nVal}, m={mVal}", "");
                            }
                        }
                    allHtml += htmlTable.GetTableHtml();

                }
            }
            File.WriteAllText($"{outputDir}\\{reportName}.html", "<html>\n<body>\n" + allHtml + "\n</body>\n</html>\n");
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
            string resultDesc = rvLineIndex == RvTotalEdgesIndex ? "Total Degrees" : rvLineIndex == RvTotalUniqueEdgesIndex ? "Total Unique Degrees" : "Max Degree";
            Py.CreatePyPlot(Py.PlotType.plot, Enumerable.Range(1, rvVals.Count).Select(i => (double)i).ToArray(),
                new[] { criticalVals.Select(i => prediction).ToArray(), criticalVals }, new[] { "E[RN]/E[RV] - 1", "Critical Cn" }, new[] { "r", "b" },
                $"Critical Cn ({resultDesc}) by Samples\\n{graphDesc}", "Samples", $"Critical Cn ({resultDesc})", imgOutputFileFullName);
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
            var maxRnVal = rnVals.Max();
            var maxRvVal = rvVals.Max();
            var prevRnVal = double.MinValue;

            for (int currRnIndex = 0; currRnIndex < rnVals.Count; currRnIndex++)
            {
                var currRnVal = rnVals[currRnIndex];

                if (/*maxRnVal - currRnVal < 0.00001 ||*/ currRnVal > maxRvVal) // Rn is max, or Rv can't improve on this
                    break;
                
                if (currRnVal == prevRnVal) // Sample didn't improve anything, just use previous value
                    continue;

                prevRnVal = currRnVal;
                               
                var currRvVal = rvVals[currRvIndex];
                while (currRvVal < currRnVal)
                    currRvVal = rvVals[++currRvIndex];
                var currCCn = (currRnVal * (currRvIndex + 1)) / (currRnVal * (currRnIndex + 1)) - 1;
                rnResultVals.Add(currRnVal);
                criticalCnVals.Add(currCCn);

                if (rvVals[currRvIndex] == maxRvVal)
                    break;
            }
            string resultDesc = rvLineIndex == RvTotalEdgesIndex ? "Total Degrees" : rvLineIndex == RvTotalUniqueEdgesIndex ? "Total Unique Degrees" : "Max Degree";


            Py.CreatePyPlot(Py.PlotType.plot, rnResultVals.ToArray(),
                new[] { rnResultVals.Select(i => prediction).ToArray(), criticalCnVals.ToArray() }, 
                new[] { "E[RN]/E[RV] - 1", "Critical Cn" }, new[] { "r", "b" },
                $"Critical Cn by {resultDesc}\\n{graphDesc}", resultDesc, "Critical Cn", imgOutputFileFullName);
            return new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
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
                var criticalCsValue = (i + 1) * (rnVals[i] - 2 * rvrnVals[i]) /
                    (rvrnVals[i] * (onlySelections ? rvrnSelections[i] : (i + 1)) - rnVals[i] * (onlySelections ? rnSelections[i] : (i + 1)));

                if (Double.IsInfinity(criticalCsValue))
                    undefinedCCsSamplesTaken.Add(i + 1);
                else
                {
                    criticalCsSamplesTaken.Add(i + 1);
                    criticalCsValues.Add(criticalCsValue);
                }
            }

            var midRange = (criticalCsValues.Max() - criticalCsValues.Min()) / 2.0 + criticalCsValues.Min();

            string resultDesc = rvrnLineIndex == RvTotalEdgesIndex ? "Total Degrees" : rvrnLineIndex == RvTotalUniqueEdgesIndex ? "Total Unique Degrees" : "Max Degree";

            Py.CreatePyPlot(
                new[] { Py.PlotType.plot, Py.PlotType.scatter, Py.PlotType.scatter },
                new[] { Enumerable.Range(1, rnVals.Count).Select(i => (double)i).ToArray(), criticalCsSamplesTaken.ToArray(), undefinedCCsSamplesTaken.ToArray() },
                new[] { Enumerable.Range(1, rnVals.Count).Select(i => prediction).ToArray(), criticalCsValues.ToArray(), undefinedCCsSamplesTaken.Select(i => midRange).ToArray() },
                new[] { "2E[RV]/(E[RN]-E[RV])", "Critical Cs", "Undefined CCs" }, new[] { "r", "b", "g" },
                $"{graphDesc} - Critical Cs ({resultDesc}) by Samples", "Samples", $"Critical Cs ({resultDesc})", imgOutputFileFullName);
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

                var currCCs = (2 * (currRnIndex + 1) - (currRvrnIndex + 1)) /
                    ((onlySelections ? rvrnSelections[currRvrnIndex] : (currRvrnIndex + 1)) - (onlySelections ? rnSelections[currRnIndex] : (currRnIndex + 1)));

                allResultValues.Add(currRnVal);

                if (Double.IsInfinity(currCCs) || Double.IsNaN(currCCs))
                    undefinedResultValues.Add(currRnVal);
                else
                {
                    definedResultValues.Add(currRnVal);
                    definedCCsValues.Add(currCCs);
                }

            }
            var midRange = (definedCCsValues.Max() - definedCCsValues.Min()) / 2.0 + definedCCsValues.Min();

            string resultDesc = rvrnLineIndex == RvTotalEdgesIndex ? "Total Degrees" : rvrnLineIndex == RvTotalUniqueEdgesIndex ? "Total Unique Degrees" : "Max Degree";
            Py.CreatePyPlot(
                new[] { Py.PlotType.plot, Py.PlotType.scatter, Py.PlotType.scatter },
                new[] { allResultValues.ToArray(), definedResultValues.ToArray(), undefinedResultValues.ToArray() },
                new[] { allResultValues.Select(i => prediction).ToArray(), definedCCsValues.ToArray(), undefinedResultValues.Select(d => midRange).ToArray() },
                new[] { "2E[RV]/(E[RN]-E[RV])", "Critical Cs", "Undefined CCs" }, new[] { "r", "b", "g" },
                $"{graphDesc} - Critical Cs by {resultDesc}", resultDesc, "Critical Cs", imgOutputFileFullName);
            return new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
        }

    }
}
