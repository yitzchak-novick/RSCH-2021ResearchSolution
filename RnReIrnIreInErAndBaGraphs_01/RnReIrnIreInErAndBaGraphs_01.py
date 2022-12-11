# YN 1/26/21 - This is a redo of the experiments we cite in the Inclusive Sampling conference paper, where we check the 
# RN, RE, IRN, and IRE of ER and BA graphs. May include WS also, we'll see.

from graphyn import graphyn as graph
import networkx as nx
import threading
import random
import numpy as np
from datetime import datetime
import csv

THREADS = 30
seeds = [random.randint(0, 2**256) for i in range(THREADS)]
graphs = [''] * THREADS

results = [['Type', 'n_val', 'm_val', 'RV', 'RN', 'RE', 'IRN', 'IRE']]

def get_graph(type, indx, n, m):
    #print('start')
    if type == 'er':
        graphs[indx] = graph.fixed_er_graph(n, m*(n-m), seeds[indx])
    elif type == 'ba':
        graphs[indx] = graph(nx.barabasi_albert_graph(n, m, seeds[indx]))
    #print('done')

def main():
    global seeds
    global graphs
    n_vals = [50, 75, 100, 500, 1000, 5000, 10000]
    m_vals = [2, 3, 5, 10, 25, 60, 80, 200, 400, 800, 1000, 1500, 2000, 3000, 4000, 6000, 9000]

    with open('results.tsv', 'w') as f:
        f.write('Type,\tn_val,\tm_val,\tRV,\tRN,\tRE,\tIRN,\tIRE\n')

    print('Type,\tn_val,\tm_val,\tRV,\tRN,\tRE,\tIRN,\tIRE')

    for n_val in n_vals:
        for m_val in m_vals:
            if m_val >= n_val/3:
                continue
            for type in ['er', 'ba']:
                graphs = [''] * THREADS
                threads = [''] * THREADS
                seeds = [random.randint(0, 2**256) for i in range(THREADS)]
                for i in range(THREADS):
                    threads[i] = threading.Thread(target = get_graph, args = (type, i, n_val, m_val))
                    threads[i].start()
                for t in threads: t.join()
                g_rv = np.mean([g.RV() for g in graphs])
                g_rn = np.mean([g.RN() for g in graphs])
                g_re = np.mean([g.RE() for g in graphs])
                g_irn = np.mean([g.IRN() for g in graphs])
                g_ire = np.mean([g.IRE() for g in graphs])
                row = [type, n_val, m_val, g_rv, g_rn, g_re, g_irn, g_ire]
                results.append(row)
                with open('results.tsv', 'a') as f:
                    f.write("\t".join(str(v) for v in [row]) + "\n")
                print("Finished" , type, n_val, m_val, datetime.now().strftime('%H:%M:%S'), sep=',\t')
                #input()
    print(results)

    print("DONE", datetime.now().strftime('%H:%M:%S'))


main()
