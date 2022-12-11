import networkx as nx
import threading
import random
import numpy as np

class graphyn(object):
    """YN 1/26/21 - A wrapper around a networkx graph to have more intuitive methods for what we need"""

    def __init__(self, g):
        self.g = g
        # we only work with the subgraph that has edges (our research decision, subject to change)
        self.g.remove_nodes_from([n for n in self.g.nodes() if self.g.degree(n) == 0 ])

    def vertices(self):
        return self.g.nodes()

    def edges(self):
        return self.g.edges()

    def N(self):
        return len(self.vertices())

    def M(self):
        return len(self.edges())

    def deg(self, v):
        return self.g.degree(v)

    def RV(self):
        return np.mean([self.deg(v) for v in self.vertices()])

    def RN(self):
        return np.mean([np.mean([self.deg(n) for n in self.g.neighbors(v)]) for v in self.vertices()])

    def RE(self):
        return np.mean([(self.deg(e[0]) + self.deg(e[1]))/2 for e in self.edges()])

    def IRN(self):
        return np.mean([np.mean([max(self.deg(n), self.deg(v)) for n in self.g.neighbors(v)]) for v in self.vertices()])

    def IRE(self):
        return np.mean([max(self.deg(e[0]), self.deg(e[1])) for e in self.edges()])

    def fixed_er_graph(n, m, seed):
        # this is a memory-intensive version of the ER algorithm where we return a graph with an exact 
        # number of edges selected instead of relying on probability, did this in C# copying from there
        # here m is the TOTAL EDGES in the graph
        rand = random.Random(seed)

        g = nx.Graph()
        g.add_nodes_from(list(range(n)))
        possible_edges = [(x, y) for x in range(n - 1) for y in range(x + 1, n)]
        edges = rand.sample(possible_edges, k=m)
        g.add_edges_from(edges)
        return graphyn(g)



