﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System.Reflection;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditorInternal;

using Status = UnityEngine.UIElements.DropdownMenuAction.Status;
using NodeView = UnityEditor.Experimental.GraphView.Node;

namespace GraphProcessor
{
	[NodeCustomEditor(typeof(BaseNode))]
	public class BaseNodeView : NodeView
	{
		public BaseNode							nodeTarget;

		public List< PortView >					inputPortViews = new List< PortView >();
		public List< PortView >					outputPortViews = new List< PortView >();

		public BaseGraphView					owner { private set; get; }

		protected Dictionary< string, List< PortView > > portsPerFieldName = new Dictionary< string, List< PortView > >();

        protected VisualElement 				controlsContainer;
		protected VisualElement					debugContainer;

		VisualElement							settings;
		VisualElement							settingButton;

		Label									computeOrderLabel = new Label();

		public event Action< PortView >			onPortConnected;
		public event Action< PortView >			onPortDisconnected;

		protected virtual bool					hasSettings => false;

        public bool								initializing = false; //Used for applying SetPosition on locked node at init.

        readonly string							baseNodeStyle = "GraphProcessorStyles/BaseNodeView";

		bool									settingsExpanded = false;

		[System.NonSerialized]
		List< IconBadge >						badges = new List< IconBadge >();
		
		private List<Node> selectedNodes = new List<Node>();
		private float selectedNodesFarLeft;
		private float selectedNodesNearLeft;
		private float selectedNodesFarRight;
		private float selectedNodesNearRight;
		private float selectedNodesFarTop;
		private float selectedNodesNearTop;
		private float selectedNodesFarBottom;
		private float selectedNodesNearBottom;
		private float selectedNodesAvgHorizontal;
		private float selectedNodesAvgVertical;
		private float selectedNodesLongestWidth;
		private float selectedNodesShortestWidth;
		private float selectedNodesAvgWidth;
		private float selectedNodesLongestHeight;
		private float selectedNodesShortestHeight;
		private float selectedNodesAvgHeight;
		

			#region  Initialization

		public void Initialize(BaseGraphView owner, BaseNode node)
		{
			nodeTarget = node;
			this.owner = owner;

			owner.computeOrderUpdated += ComputeOrderUpdatedCallback;
			node.onMessageAdded += AddMessageView;
			node.onMessageRemoved += RemoveMessageView;

            styleSheets.Add(Resources.Load<StyleSheet>(baseNodeStyle));

            if (!string.IsNullOrEmpty(node.layoutStyle))
                styleSheets.Add(Resources.Load<StyleSheet>(node.layoutStyle));

			InitializePorts();
			InitializeView();
			InitializeDebug();

			Enable();

			InitializeSettings();

			RefreshExpandedState();

			this.RefreshPorts();
		}

		void InitializePorts()
		{
			var listener = owner.connectorListener;

			foreach (var inputPort in nodeTarget.inputPorts)
			{
				AddPort(inputPort.fieldInfo, Direction.Input, listener, inputPort.portData);
			}

			foreach (var outputPort in nodeTarget.outputPorts)
			{
				AddPort(outputPort.fieldInfo, Direction.Output, listener, outputPort.portData);
			}
		}

		void InitializeView()
		{
            controlsContainer = new VisualElement{ name = "controls" };
			mainContainer.Add(controlsContainer);

			debugContainer = new VisualElement{ name = "debug" };
			if (nodeTarget.debug)
				mainContainer.Add(debugContainer);

			title = (string.IsNullOrEmpty(nodeTarget.name)) ? nodeTarget.GetType().Name : nodeTarget.name;

            initializing = true;

            SetPosition(nodeTarget.position);
		}

		void InitializeSettings()
		{
			// Initialize settings button:
			if (hasSettings)
				CreateSettingButton();
		}
		
		void CreateSettingButton()
		{
			settingButton = new VisualElement {name = "settings-button"};
			settingButton.Add(new VisualElement { name = "icon" });
			settings = new VisualElement();

			// Add Node type specific settings
			settings.Add(CreateSettingsView());

			// Add manipulators
			settingButton.AddManipulator(new Clickable(ToggleSettings));

			var buttonContainer = new VisualElement { name = "button-container" };
			buttonContainer.style.flexDirection = FlexDirection.Row;
			buttonContainer.Add(settingButton);
			titleContainer.Add(buttonContainer);
		}

		void ToggleSettings()
		{
			settingsExpanded = !settingsExpanded;
			if (settingsExpanded)
			{
				topContainer.parent.Insert(0, settings);
				owner.ClearSelection();
				owner.AddToSelection(this);

				settingButton.AddToClassList("clicked");
			}
			else
			{
				settings.RemoveFromHierarchy();

				settingButton.RemoveFromClassList("clicked");
			}
		}

		void InitializeDebug()
		{
			ComputeOrderUpdatedCallback();
			debugContainer.Add(computeOrderLabel);
		}

		#endregion

		#region API

		public List< PortView > GetPortViewsFromFieldName(string fieldName)
		{
			List< PortView >	ret;

			portsPerFieldName.TryGetValue(fieldName, out ret);

			return ret;
		}

		public PortView GetFirstPortViewFromFieldName(string fieldName)
		{
			return GetPortViewsFromFieldName(fieldName)?.First();
		}

		public PortView GetPortViewFromFieldName(string fieldName, string identifier)
		{
			return GetPortViewsFromFieldName(fieldName)?.FirstOrDefault(pv => {
				return (pv.portData.identifier == identifier) || (String.IsNullOrEmpty(pv.portData.identifier) && String.IsNullOrEmpty(identifier));
			});
		}

		public PortView AddPort(FieldInfo fieldInfo, Direction direction, EdgeConnectorListener listener, PortData portData)
		{
			// TODO: hardcoded value
			PortView p = new PortView(Orientation.Horizontal, direction, fieldInfo, portData, listener);

			if (p.direction == Direction.Input)
			{
				inputPortViews.Add(p);
				inputContainer.Add(p);
			}
			else
			{
				outputPortViews.Add(p);
				outputContainer.Add(p);
			}

			p.Initialize(this, portData?.displayName);

			List< PortView > ports;
			portsPerFieldName.TryGetValue(p.fieldName, out ports);
			if (ports == null)
			{
				ports = new List< PortView >();
				portsPerFieldName[p.fieldName] = ports;
			}
			ports.Add(p);

			return p;
		}

		public void RemovePort(PortView p)
		{
			// Remove all connected edges:
			var edgesCopy = p.GetEdges().ToList();
			foreach (var e in edgesCopy)
				owner.Disconnect(e, refreshPorts: false);

			if (p.direction == Direction.Input)
			{
				inputPortViews.Remove(p);
				inputContainer.Remove(p);
			}
			else
			{
				outputPortViews.Remove(p);
				outputContainer.Remove(p);
			}

			List< PortView > ports;
			portsPerFieldName.TryGetValue(p.fieldName, out ports);
			ports.Remove(p);
		}

		private void SetSelectedNodeVariables()
		{
			owner.nodes.ForEach(node =>
			{
				if(node.selected) selectedNodes.Add(node);
			});

			selectedNodesFarLeft = int.MinValue;
			selectedNodesFarRight = int.MinValue;
			selectedNodesFarTop = int.MinValue;
			selectedNodesFarBottom = int.MinValue;
			selectedNodesLongestWidth = 0;
			selectedNodesLongestHeight = 0;

			selectedNodesNearLeft = int.MaxValue;
			selectedNodesNearRight = int.MaxValue;
			selectedNodesNearTop = int.MaxValue;
			selectedNodesNearBottom = int.MaxValue;
			selectedNodesShortestWidth = 1000;
			selectedNodesShortestHeight = 1000;
			
			foreach(var selectedNode in selectedNodes)
			{
				var nStyle = selectedNode.style;
				var wd = selectedNode.localBound.size.x;
				var hg = selectedNode.localBound.size.y;
				
				if(nStyle.left.value.value > selectedNodesFarLeft) selectedNodesFarLeft = nStyle.left.value.value;
				if(nStyle.left.value.value + wd > selectedNodesFarRight) selectedNodesFarRight = nStyle.left.value.value + wd;
				if(nStyle.top.value.value > selectedNodesFarTop) selectedNodesFarTop = nStyle.top.value.value;
				if(nStyle.top.value.value + hg > selectedNodesFarBottom) selectedNodesFarBottom = nStyle.top.value.value + hg;
				if(wd > selectedNodesLongestWidth) selectedNodesLongestWidth = wd;
				if(hg > selectedNodesLongestHeight) selectedNodesLongestHeight = hg;
				
				if(nStyle.left.value.value < selectedNodesNearLeft) selectedNodesNearLeft = nStyle.left.value.value;
				if(nStyle.left.value.value + wd < selectedNodesNearRight) selectedNodesNearRight = nStyle.left.value.value + wd;
				if(nStyle.top.value.value < selectedNodesNearTop) selectedNodesNearTop = nStyle.top.value.value;
				if(nStyle.top.value.value + hg < selectedNodesNearBottom) selectedNodesNearBottom = nStyle.top.value.value + hg;
				if(wd < selectedNodesShortestWidth) selectedNodesShortestWidth = wd;
				if(hg < selectedNodesShortestHeight) selectedNodesShortestHeight = hg;
			}
			
			selectedNodesAvgHorizontal   = (selectedNodesNearLeft + selectedNodesFarRight) / 2f;
			selectedNodesAvgVertical    = (selectedNodesNearTop + selectedNodesFarBottom) / 2f;
			selectedNodesAvgWidth  = (selectedNodesLongestWidth + selectedNodesShortestWidth) / 2f;
			selectedNodesAvgHeight = (selectedNodesLongestHeight + selectedNodesShortestHeight) / 2f;
		}

		public Rect GetNodeRect(Node node, float left = int.MaxValue, float top = int.MaxValue)
		{
			return new Rect(
				new Vector2(left != int.MaxValue ? left : node.style.left.value.value, top != int.MaxValue ? top : node.style.top.value.value),
				new Vector2(node.style.width.value.value, node.style.height.value.value)
			);
		}

		public void AlignToLeft()
		{
			SetSelectedNodeVariables();
			if(selectedNodes.Count < 2) return;

			foreach(var selectedNode in selectedNodes)
			{
				selectedNode.SetPosition(GetNodeRect(selectedNode, selectedNodesNearLeft));
			}
		}

		public void AlignToCenter()
		{
			SetSelectedNodeVariables();
			if(selectedNodes.Count < 2) return;

			foreach(var selectedNode in selectedNodes)
			{
				selectedNode.SetPosition(GetNodeRect(selectedNode, selectedNodesAvgHorizontal - selectedNode.localBound.size.x / 2f));
			}
		}

		public void AlignToRight()
		{
			SetSelectedNodeVariables();
			if(selectedNodes.Count < 2) return;
			
			foreach(var selectedNode in selectedNodes)
			{
				selectedNode.SetPosition(GetNodeRect(selectedNode, selectedNodesFarRight - selectedNode.localBound.size.x));
			}
		}

		public void AlignToTop()
		{
			SetSelectedNodeVariables();
			if(selectedNodes.Count < 2) return;

			foreach(var selectedNode in selectedNodes)
			{
				selectedNode.SetPosition(GetNodeRect(selectedNode, top: selectedNodesNearTop));
			}
		}

		public void AlignToMiddle()
		{
			SetSelectedNodeVariables();
			if(selectedNodes.Count < 2) return;

			foreach(var selectedNode in selectedNodes)
			{
				selectedNode.SetPosition(GetNodeRect(selectedNode, top: selectedNodesAvgVertical - selectedNode.localBound.size.y / 2f));
			}
		}

		public void AlignToBottom()
		{
			SetSelectedNodeVariables();
			if(selectedNodes.Count < 2) return;
			
			foreach(var selectedNode in selectedNodes)
			{
				selectedNode.SetPosition(GetNodeRect(selectedNode, top: selectedNodesFarBottom - selectedNode.localBound.size.y));
			}
		}
		
		public void OpenNodeViewScript()
		{
			var scriptPath = NodeProvider.GetNodeViewScript(GetType());

#pragma warning disable CS0618 // Deprecated function but no alternative :(
			if (scriptPath != null)
				InternalEditorUtility.OpenFileAtLineExternal(scriptPath, 0);
#pragma warning restore CS0618
		}

		public void OpenNodeScript()
		{
			var scriptPath = NodeProvider.GetNodeScript(nodeTarget.GetType());

#pragma warning disable CS0618 // Deprecated function but no alternative :(
			if (scriptPath != null)
				InternalEditorUtility.OpenFileAtLineExternal(scriptPath, 0);
#pragma warning restore CS0618
		}

		public void ToggleDebug()
		{
			nodeTarget.debug = !nodeTarget.debug;
			UpdateDebugView();
		}

		public void UpdateDebugView()
		{
			if (nodeTarget.debug)
				mainContainer.Add(debugContainer);
			else
				mainContainer.Remove(debugContainer);
		}

		public void AddMessageView(string message, Texture icon, Color color)
			=> AddBadge(new NodeBadgeView(message, icon, color));

		public void AddMessageView(string message, NodeMessageType messageType)
		{
			IconBadge	badge = null;
			switch (messageType)
			{
				case NodeMessageType.Warning:
					badge = new NodeBadgeView(message, EditorGUIUtility.IconContent("Collab.Warning").image, Color.yellow);
					break ;
				case NodeMessageType.Error:	
					badge = IconBadge.CreateError(message);
					break ;
				case NodeMessageType.Info:
					badge = IconBadge.CreateComment(message);
					break ;
				default:
				case NodeMessageType.None:
					badge = new NodeBadgeView(message, null, Color.grey);
					break ;
			}
			
			AddBadge(badge);
		}

		void AddBadge(IconBadge badge)
		{
			Add(badge);
			badges.Add(badge);
			badge.AttachTo(topContainer, SpriteAlignment.TopRight);
		}

		public void RemoveMessageView(string message)
		{
			badges.RemoveAll(b => {
				if (b.badgeText == message)
				{
					b.Detach();
					b.RemoveFromHierarchy();
					return true;
				}
				return false;
			});
		}

		#endregion

		#region Callbacks & Overrides

		void ComputeOrderUpdatedCallback()
		{
			//Update debug compute order
			computeOrderLabel.text = "Compute order: " + nodeTarget.computeOrder;
		}

		public virtual void Enable()
		{
			DrawDefaultInspector();
		}

		protected virtual void DrawDefaultInspector()
		{
			var fields = nodeTarget.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

			foreach (var field in fields)
			{
				//skip if the field is not serializable
				if (!field.IsPublic && field.GetCustomAttribute(typeof(SerializeField)) == null)
					continue ;

				//skip if the field is an input/output and not marked as SerializedField
				if (field.GetCustomAttribute(typeof(SerializeField)) == null && (field.GetCustomAttribute(typeof(InputAttribute)) != null || field.GetCustomAttribute(typeof(OutputAttribute)) != null))
					continue ;

                //skip if marked with NonSerialized or HideInInspector
                if (field.GetCustomAttribute(typeof(System.NonSerializedAttribute)) != null || field.GetCustomAttribute(typeof(HideInInspector)) != null)
                    continue ;

				var fieldName = Regex.Replace( 
					Regex.Replace( 
						field.Name, 
						@"(\P{Ll})(\P{Ll}\p{Ll})", 
						"$1 $2" 
					), 
					@"(\p{Ll})(\P{Ll})", 
					"$1 $2" 
				);

				fieldName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fieldName);
				
				AddControlField(field, fieldName);
			}
		}

		protected void AddControlField(string fieldName, string label = null)
			=> AddControlField(nodeTarget.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), label);

		protected void AddControlField(FieldInfo field, string label = null)
		{
			if (field == null)
				return;
	
			var element = FieldFactory.CreateField(field.FieldType, field.GetValue(nodeTarget), (newValue) => {
				owner.RegisterCompleteObjectUndo("Updated " + newValue);
				field.SetValue(nodeTarget, newValue);
			}, label);

			if(element != null)
			{
				controlsContainer.AddToClassList("has-control");
				controlsContainer.Add(element);
			}
		}

		internal void OnPortConnected(PortView port)
		{
			onPortConnected?.Invoke(port);
		}

		internal void OnPortDisconnected(PortView port)
		{
			onPortDisconnected?.Invoke(port);
		}

		// TODO: a function to force to reload the custom behavior ports (if we want to do a button to add ports for example)

		public virtual void OnRemoved() {}
		public virtual void OnCreated() {}

		public override void SetPosition(Rect newPos)
		{
            if (initializing || !nodeTarget.isLocked)
            {
                initializing = false;
                base.SetPosition(newPos);

                Undo.RegisterCompleteObjectUndo(owner.graph, "Moved graph node");
                nodeTarget.position = newPos;
            }
		}

		public override bool	expanded
		{
			get { return base.expanded; }
			set
			{
				base.expanded = value;
				nodeTarget.expanded = value;
			}
		}

        public void ChangeLockStatus()
        {
            nodeTarget.nodeLock ^= true;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
		{
			//	Align
			evt.menu.AppendAction("Align/To Left", (e) => AlignToLeft(), AlignToLeftStatus);
			evt.menu.AppendAction("Align/To Center", (e) => AlignToCenter(), AlignToCenterStatus);
			evt.menu.AppendAction("Align/To Right", (e) => AlignToRight(), AlignToRightStatus);
			evt.menu.AppendSeparator("Align/");
			evt.menu.AppendAction("Align/To Top", (e) => AlignToTop(), AlignToTopStatus);
			evt.menu.AppendAction("Align/To Middle", (e) => AlignToMiddle(), AlignToMiddleStatus);
			evt.menu.AppendAction("Align/To Bottom", (e) => AlignToBottom(), AlignToBottomStatus);
			evt.menu.AppendSeparator();
			//	Align
			
			evt.menu.AppendAction("Open Node Script", (e) => OpenNodeScript(), OpenNodeScriptStatus);
			evt.menu.AppendAction("Open Node View Script", (e) => OpenNodeViewScript(), OpenNodeViewScriptStatus);
			evt.menu.AppendAction("Debug", (e) => ToggleDebug(), DebugStatus);
            if (nodeTarget.unlockable)
                evt.menu.AppendAction((nodeTarget.isLocked ? "Unlock" : "Lock"), (e) => ChangeLockStatus(), LockStatus);
        }

        Status LockStatus(DropdownMenuAction action)
        {
            return Status.Normal;
        }

        Status DebugStatus(DropdownMenuAction action)
		{
			if (nodeTarget.debug)
				return Status.Checked;
			return Status.Normal;
		}

		Status AlignToLeftStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeScript(nodeTarget.GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		Status AlignToCenterStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeScript(nodeTarget.GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		Status AlignToRightStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeScript(nodeTarget.GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		Status AlignToTopStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeScript(nodeTarget.GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		Status AlignToMiddleStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeScript(nodeTarget.GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		Status AlignToBottomStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeScript(nodeTarget.GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		Status OpenNodeScriptStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeScript(nodeTarget.GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		Status OpenNodeViewScriptStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeViewScript(GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		void SyncPortCounts(IEnumerable< NodePort > ports, IEnumerable< PortView > portViews)
		{
			var listener = owner.connectorListener;

			// Maybe not good to remove ports as edges are still connected :/
			foreach (var pv in portViews.ToList())
			{
				// If the port have disappeared from the node data, we remove the view:
				// We can use the identifier here because this function will only be called when there is a custom port behavior
				if (!ports.Any(p => p.portData.identifier == pv.portData.identifier))
					RemovePort(pv);
			}

			foreach (var p in ports)
			{
				// Add missing port views
				if (!portViews.Any(pv => p.portData.identifier == pv.portData.identifier))
				{
					Direction portDirection = nodeTarget.IsFieldInput(p.fieldName) ? Direction.Input : Direction.Output;
					AddPort(p.fieldInfo, portDirection, listener, p.portData);
				}
			}
		}

		// void UpdatePortConnections(List< PortView > portViews)
		// {
		// 	foreach (var pv in portViews)
		// 	{
		// 		Debug.Log("pv: " + pv.portName);
				
		// 		// Go over all connected edges and disconnect them if the serialized edge have been removed
		// 		// This can happens when the new port type is incompatible with the old one.
		// 		foreach (var edge in pv.GetEdges().ToList())
		// 		{
		// 			// TODO: check edge connection compatibility !
		// 			Debug.Log("Edge !");
		// 			if (owner.graph.edges.Contains(edge.serializedEdge))
		// 			{
		// 				owner.Disconnect(edge);
		// 				// owner.RemoveElement(edge);
		// 				// base.RefreshPorts(); // We don't call this.RefreshPorts because it will cause an infinite loop
		// 			}
		// 		}
		// 	}
		// }

		public new bool RefreshPorts()
		{
			// If a port behavior was attached to one port, then
			// the port count might have been updated by the node
			// so we have to refresh the list of port views.
			UpdatePortViewWithPorts(nodeTarget.inputPorts, inputPortViews);
			UpdatePortViewWithPorts(nodeTarget.outputPorts, outputPortViews);

			void UpdatePortViewWithPorts(NodePortContainer ports, List< PortView > portViews)
			{
				// When there is no current portviews, we can't zip the list so we just add all
				if (portViews.Count == 0)
					SyncPortCounts(ports, new PortView[]{});
				else if (ports.Count == 0) // Same when there is no ports
					SyncPortCounts(new NodePort[]{}, portViews);
				else
				{
					var p = ports.GroupBy(n => n.fieldName);
					var pv = portViews.GroupBy(v => v.fieldName);
					p.Zip(pv, (portPerFieldName, portViewPerFieldName) => {
						if (portPerFieldName.Count() != portViewPerFieldName.Count())
							SyncPortCounts(portPerFieldName, portViewPerFieldName);
						// UpdatePortConnections(portViewPerFieldName.ToList());
						// We don't care about the result, we just iterate over port and portView
						return "";
					}).ToList();
				}

				// Here we're sure that we have the same amount of port and portView
				// so we can update the view with the new port data (if the name of a port have been changed for example)

				for (int i = 0; i < portViews.Count; i++)
				{
					var pv = portViews[i];

					pv.UpdatePortView(ports[i].portData.displayName, ports[i].portData.displayType);
				}
			}

			return base.RefreshPorts();
		}

		protected void ForceUpdatePorts()
		{
			nodeTarget.UpdateAllPorts();

			RefreshPorts();
		}
		
		protected virtual VisualElement CreateSettingsView() => new Label("Settings");

		#endregion
    }
}