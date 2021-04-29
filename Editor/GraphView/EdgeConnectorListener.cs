using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    class EdgeConnectorListener : IEdgeConnectorListener
    {
        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            var graphView = edge.GetFirstAncestorOfType<GraphView>();
            if (graphView != null)
            {
                var context = new NodeCreationContext();
                context.screenMousePosition = position;
                context.target = edge;
                graphView.nodeCreationRequest(context);
            }
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            var edgesToDelete = new List<GraphElement>();
            if (edge.input.capacity == Port.Capacity.Single)
                edgesToDelete.AddRange(edge.input.connections.Where(e => e != edge));
            if (edge.output.capacity == Port.Capacity.Single)
                edgesToDelete.AddRange(edge.output.connections.Where(e => e != edge));
            if (edgesToDelete.Count > 0)
                graphView.DeleteElements(edgesToDelete);

            var edgesToCreate = new List<Edge>() { edge };
            if (graphView.graphViewChanged != null)
            {
                var graphViewChange = new GraphViewChange();
                graphViewChange.edgesToCreate = edgesToCreate;
                edgesToCreate = graphView.graphViewChanged(graphViewChange).edgesToCreate;
            }

            foreach (var e in edgesToCreate)
            {
                graphView.AddElement(e);
                edge.input.Connect(e);
                edge.output.Connect(e);
            }
        }
    }
}
