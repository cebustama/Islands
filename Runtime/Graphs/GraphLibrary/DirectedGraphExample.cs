using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphLibrary;

// TODO: REDO

public class DirectedGraphExample : MonoBehaviour
{
    /*
    public class StringNode : IVertex
    {
        public string name;

        public string GetID() => name;
    }

    // Start is called before the first frame update
    void Start()
    {
        DirectedGraph<StringNode, string> graph = new DirectedGraph<StringNode, string>();
        graph.AddVertex(new[] { "A", "B", "C", "D" });
        graph.AddEdge("A", "B", "Label1");
        graph.AddEdge("A", "C", "Label2");

        foreach(string v in graph.GetVertexSet())
            Debug.Log(v);

        foreach (var p in graph.GetEdgeSet())
        {
            string v1 = p.GetFirst();
            string v2 = p.GetSecond();
            string w = graph.GetWeight(v1, v2);

            if (w != null)
                Debug.Log(p.GetFirst() + " - " + graph.GetWeight(p.GetFirst(), p.GetSecond()) + " -> " + p.GetSecond());
        }
            
    }   
    */
}
