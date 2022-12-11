# YN 2/3/2021 - Did an experiment for the conference paper that shows the corelation between
# assortativity (we have inversity and AFI in there too) and the sampling methods. Hoping to
# write a script here that generates a chart for each result so we can make sure it's
# consistent across all of the graphs.

import matplotlib.pyplot as plt
import glob, os
import re

DIR = r"C:\Users\Yitzchak\Desktop\Pre2021\RewiringBaErForAssort"

# indexes of assortativity, inversity, and AFI
assrt_indx = 2
invrst_indx = 3
afi_indx = 4

# function for a single file
def process_file(file):
    pattern = r"(BA|ER)(_)(n)(_)(\d+)(--)(p|m)(_)(\d+\.?\d*)(.*)"
    if not re.search(pattern, file): return
    file_parts = re.split(pattern, file)
    graph_type, param1, param1_val, param2, param2_val = file_parts[1], file_parts[3], file_parts[5], file_parts[7], file_parts[9]
    lines = open(DIR + "\\" + file, 'r').readlines()[2:]
    assrt_xvals = [float(t.split()[assrt_indx]) for t in lines]
    invrst_xvals = [float(t.split()[invrst_indx]) for t in lines]
    afi_xvals = [float(t.split()[afi_indx]) for t in lines]   

    for i in range(3):
        xvals = assrt_xvals if i == 0 else invrst_xvals if i == 1 else afi_xvals
        xlabel = "Assortativity" if i == 0 else "Inversity" if i == 1 else "AFI"
        rvvals = []
        rnvals = []
        revals = []
        irnvals = []
        irevals = [] 
        for line in lines:
            vals = [float(s) for s in line.split()]
            rvvals.append(vals[5])
            rnvals.append(vals[6])
            revals.append(vals[7])
            irnvals.append(vals[8])
            irevals.append(vals[9]) 
        plt.scatter(xvals, irevals, label='IRE')
        plt.scatter(xvals, irnvals, label='IRN')
        plt.scatter(xvals, rnvals, label='RN')
        plt.scatter(xvals, revals, label='RE')
        plt.scatter(xvals, rvvals, label='RV')
        plt.title("Rewired " + graph_type + " Graphs, " + param1 + "=" + param1_val + ", " + param2 + "=" + param2_val)
        plt.xlabel(xlabel)
        plt.ylabel("Expected Degree")
        plt.legend()
        plt.savefig("charts\\" + graph_type + "_" + param1 + "_" + param1_val + "_" + param2 + "_" + param2_val + "_" + xlabel + ".png")
        #plt.show()
        #input()
        plt.close()


os.chdir(DIR)
result_files =  glob.glob("*.tsv")
for file in result_files:
    process_file(file)