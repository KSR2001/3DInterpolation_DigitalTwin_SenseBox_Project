using System;
using System.Collections.Generic;

public class DFSGraph
{
    private int V;
    private List<int>[] adj;

    public DFSGraph(int v)
    {
        V = v;
        adj = new List<int>[v];
        for (int i = 0; i < v; ++i)
            adj[i] = new List<int>();
    }

    public void AddEdge(int v, int w)
    {
        adj[v].Add(w);
    }

    public List<int> GetLargestComponent()
    {
        bool[] visited = new bool[V];
        List<int> largestComponent = new List<int>();
        List<int> currentComponent = new List<int>();

        for (int i = 0; i < V; i++)
        {
            if (!visited[i])
            {
                currentComponent = DFSIterative(i, visited);

                if (currentComponent.Count > largestComponent.Count)
                {
                    largestComponent = new List<int>(currentComponent);
                }
            }
        }

        return largestComponent;
    }

    private List<int> DFSIterative(int startVertex, bool[] visited)
    {
        Stack<int> stack = new Stack<int>();
        List<int> component = new List<int>();
        stack.Push(startVertex);

        while (stack.Count > 0)
        {
            int vertex = stack.Pop();

            if (!visited[vertex])
            {
                visited[vertex] = true;
                component.Add(vertex);

                foreach (var neighbor in adj[vertex])
                {
                    if (!visited[neighbor])
                    {
                        stack.Push(neighbor);
                    }
                }
            }
        }

        return component;
    }
}
