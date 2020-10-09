using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphProcessor
{
	public class ProcessOrderView : PinnedElementView
	{
		private BaseGraph     graph;
		private BaseGraphView graphView;
		private ListView      listView;

		public ProcessOrderView()
		{
			title = "Process Order";
		}

		protected override void Initialize(BaseGraphView baseGraphView)
		{
			if(pinnedElement.position.x == 5 && pinnedElement.position.y == 5)
			{
				pinnedElement.position = new Rect(new Vector2(5, 410), PinnedElement.defaultSize);
			}

			graphView                         =  baseGraphView;
			graph                             =  graphView.graph;
			baseGraphView.computeOrderUpdated += UpdateOrderList;

			UpdateOrderList();
		}

		private void RefreshOrderList()
		{
			listView.Refresh();
		}

		private void UpdateOrderList()
		{
			content.Clear();
			var orderedNodes = graph.nodes.OrderBy(n => n.computeOrder).ToList();

			Func<VisualElement> makeItem = () => new Label();
			Action<VisualElement, int> bindItem = (e, i) =>
			{
				var label = e as Label;
				if(orderedNodes[i].computeOrder == -2)
				{
					label.AddToClassList("infinite-loop");
					label.tooltip = "This node won't work if you don't fix the recursive loop.";
					label.text    = "! x " + (orderedNodes[i].category != null ? orderedNodes[i].category + "/" : "") + orderedNodes[i].name;
				}
				else
				{
					label.text = orderedNodes[i].computeOrder + 1 + " -> " + (orderedNodes[i].category != null ? orderedNodes[i].category + "/" : "") +
								 orderedNodes[i].name;
					if(!orderedNodes[i].GetInputNodes().Any() && !orderedNodes[i].GetOutputNodes().Any())
					{
						label.AddToClassList("no-connection");
						label.text    += " (e)";
						label.tooltip =  "This node doesn't connected to any other nodes. It is recommended to remove it from the graph.";
					}
				}

				var nodeView = graphView.Query(name: orderedNodes[i].GUID).Build().First();

				label.RegisterCallback<MouseOverEvent>(mo => Highlight(nodeView));
				label.RegisterCallback<MouseOutEvent>(mo => UnHighlight(nodeView));
			};

			const int itemHeight = 20;

			listView                = new ListView(orderedNodes, itemHeight, makeItem, bindItem);
			listView.style.flexGrow = 1.0f;

			listView.onItemsChosen += obj =>
			{
				var selectedNode = obj.ToList()[0] as BaseNode;

				graph.position = new Vector3(-selectedNode.position.x + graphView.viewport.contentRect.width / 2f - selectedNode.position.width / 2f,
											 -selectedNode.position.y + graphView.viewport.contentRect.height / 2f - selectedNode.position.height / 2f, 0f);
				graph.scale = Vector3.one;

				graphView.UpdateViewTransform(graph.position, graph.scale);
			};
			onResized                                              += RefreshOrderList;
			listView.Q<ScrollView>().verticalScroller.valueChanged += RefreshOrderList;

			content.Add(listView);
		}

		private void RefreshOrderList(float obj)
		{
			graphView.Query(className: "Highlight").Build().ForEach(view => view.RemoveFromClassList("Highlight"));
			RefreshOrderList();
		}

		private void Highlight(VisualElement nodeView)
		{
			nodeView.AddToClassList("Highlight");
		}

		private void UnHighlight(VisualElement nodeView)
		{
			nodeView.RemoveFromClassList("Highlight");
		}
	}
}