using System.Collections.Generic;
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
		// protected VisualElement					debugContainer;

		VisualElement							settings;
		NodeSettingsView settingsContainer;
		VisualElement							settingButton;

		// Label									computeOrderLabel = new Label();

		public event Action< PortView >			onPortConnected;
		public event Action< PortView >			onPortDisconnected;

		protected virtual bool					hasSettings { get; set; }

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
		private float selectedNodesLongestHeight;
		private float selectedNodesShortestHeight;

		private VisualElement inputContainerElement;
		

		#region  Initialization

		public void Initialize(BaseGraphView owner, BaseNode node)
		{
			nodeTarget = node;
			this.owner = owner;

			owner.computeOrderUpdated += ComputeOrderUpdatedCallback;
			node.onMessageAdded       += AddMessageView;
			node.onMessageRemoved     += RemoveMessageView;
			node.onPortsUpdated       += UpdatePortsForField;

            styleSheets.Add(Resources.Load<StyleSheet>(baseNodeStyle));

            if (!string.IsNullOrEmpty(node.layoutStyle))
                styleSheets.Add(Resources.Load<StyleSheet>(node.layoutStyle));

			InitializePorts();
			InitializeView();
			// InitializeDebug();
			ComputeOrderUpdatedCallback();

			ExceptionToLog.Call(() => Enable());

			InitializeSettings();

			RefreshExpandedState();

			this.RefreshPorts();
			
			RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
			OnGeometryChanged(null);
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
            controlsContainer         = new VisualElement{ name = "controls" };
			mainContainer.parent.name = nodeTarget.GUID;
			mainContainer.Add(controlsContainer);
			
			if (nodeTarget.showControlsOnHover)
			{
				bool mouseOverControls = false;
				controlsContainer.style.display = DisplayStyle.None;
				RegisterCallback<MouseOverEvent>(e => {
					controlsContainer.style.display = DisplayStyle.Flex;
					mouseOverControls               = true;
				});
				RegisterCallback<MouseOutEvent>(e => {
					var rect               = GetPosition();
					var graphMousePosition = owner.contentViewContainer.WorldToLocal(e.mousePosition);
					if (rect.Contains(graphMousePosition) || !nodeTarget.showControlsOnHover)
						return;
					mouseOverControls = false;
					schedule.Execute(_ => {
						if (!mouseOverControls)
							controlsContainer.style.display = DisplayStyle.None;
					}).ExecuteLater(500);
				});
			}
			
			Undo.undoRedoPerformed += UpdateFieldValues;
			//
			// debugContainer = new VisualElement{ name = "debug" };
			// if (nodeTarget.debug)
			// 	mainContainer.Add(debugContainer);

			title                  = (string.IsNullOrEmpty(nodeTarget.name)) ? nodeTarget.GetType().Name : nodeTarget.name;
			titleContainer.tooltip = title;

			if(!string.IsNullOrEmpty(nodeTarget.category))
			{
				var categoryLabel = new Label(nodeTarget.category) {name = "category"};
				mainContainer.parent.Add(categoryLabel);
				categoryLabel.SendToBack();
				categoryLabel.pickingMode = PickingMode.Ignore;
			}

            initializing = true;

            SetPosition(nodeTarget.position);
			
			if(nodeTarget is IConditionalNode)
			{
				mainContainer.parent.AddToClassList("conditional-node");
				
				var previousLink = contentContainer.Q(className: "executed");
				if(previousLink != null)
				{
					titleContainer.Add(previousLink);
					previousLink.SendToBack();
				}

				if(nodeTarget is LinearConditionalNode)
				{
					mainContainer.parent.AddToClassList("executes-next");
					var nextLink = contentContainer.Q(className: "executes");
					titleContainer.Add(nextLink);
				}
			}
			
			if(nodeTarget.headerClass != "")
				titleContainer.AddToClassList(nodeTarget.headerClass);
			
			if(nodeTarget.nodeClass != "")
				contentContainer.AddToClassList(nodeTarget.nodeClass);
		}

		void InitializeSettings()
		{
			// Initialize settings button:
			if(hasSettings)
			{
				CreateSettingButton();
				
				settingsContainer         = new NodeSettingsView();
				settingsContainer.visible = false;
				settingsContainer.Add(settings);
				Add(settingsContainer);
				var fields = nodeTarget.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

				foreach(var field in fields)
					if(field.GetCustomAttribute(typeof(SettingAttribute)) != null) 
						AddSettingField(field);

				PrepareSettingsView();
			}
		}

		void OnGeometryChanged(GeometryChangedEvent evt)
		{
			if(settingButton != null)
			{
				var settingsButtonLayout = settingButton.ChangeCoordinatesTo(settingsContainer.parent, settingButton.layout);
				settingsContainer.style.top  = settingsButtonLayout.yMax - 18f;
				settingsContainer.style.left = settingsButtonLayout.xMin - 26f;
			}
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
			
			// Check and reorder if has a output port in the title
			var outPutPort = titleContainer.Q<PortView>(className: "output");
			if(outPutPort != null) buttonContainer.PlaceBehind(outPutPort);
		}

		void ToggleSettings()
		{
			settingsExpanded = !settingsExpanded;
			if (settingsExpanded)
				OpenSettings();
			else
				CloseSettings();
		}

		public void OpenSettings()
		{
			if (settingsContainer != null)
			{
				owner.ClearSelection();
				owner.AddToSelection(this);

				settingButton.AddToClassList("clicked");
				settingsContainer.visible = true;
				settingsExpanded          = true;
			}
		}

		public void CloseSettings()
		{
			if (settingsContainer != null)
			{
				settingButton.RemoveFromClassList("clicked");
				settingsContainer.visible = false;
				settingsExpanded          = false;
			}
		}
		//
		// void InitializeDebug()
		// {
		// 	ComputeOrderUpdatedCallback();
		// 	debugContainer.Add(computeOrderLabel);
		// }

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

		public PortView AddPort(FieldInfo fieldInfo, Direction direction, BaseEdgeConnectorListener listener, PortData portData)
		{
			// TODO: hardcoded value
			PortView p = PortView.CreatePV(Orientation.Horizontal, direction, fieldInfo, portData, listener);

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
		
		public void InsertPort(PortView portView, int index)
		{
			if (portView.direction == Direction.Input)
				inputContainer.Insert(index, portView);
			else
				outputContainer.Insert(index, portView);
		}

		public void RemovePort(PortView p)
		{
			// Remove all connected edges:
			var edgesCopy = p.GetEdges().ToList();
			foreach (var e in edgesCopy)
				owner.Disconnect(e, refreshPorts: false);

			if (p.direction == Direction.Input)
			{
				if (inputPortViews.Remove(p))
					inputContainer.Remove(p);
			}
			else
			{
				if (outputPortViews.Remove(p))
					outputContainer.Remove(p);
			}

			List< PortView > ports;
			portsPerFieldName.TryGetValue(p.fieldName, out ports);
			ports.Remove(p);
		}

		private void SetSelectedNodeVariables()
		{
			selectedNodes = new List<Node>();
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
		}

		private static Rect GetNodeRect(Node node, float left = int.MaxValue, float top = int.MaxValue)
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
			var script = NodeProvider.GetNodeViewScript(GetType());

			if (script != null)
				AssetDatabase.OpenAsset(script.GetInstanceID(), 0, 0);
		}

		public void OpenNodeScript()
		{
			var script = NodeProvider.GetNodeScript(nodeTarget.GetType());

			if (script != null)
				AssetDatabase.OpenAsset(script.GetInstanceID(), 0, 0);
		}
		//
		// public void ToggleDebug()
		// {
		// 	nodeTarget.debug = !nodeTarget.debug;
		// 	UpdateDebugView();
		// }
		//
		// public void UpdateDebugView()
		// {
		// 	if (nodeTarget.debug)
		// 		mainContainer.Add(debugContainer);
		// 	else
		// 		mainContainer.Remove(debugContainer);
		// }

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

		void RemoveBadge(Func<IconBadge, bool> callback)
		{
			badges.RemoveAll(b => {
				if (callback(b))
				{
					b.Detach();
					b.RemoveFromHierarchy();
					return true;
				}
				return false;
			});
		}
		
		public void RemoveMessageViewContains(string message) => RemoveBadge(b => b.badgeText.Contains(message));

		public void RemoveMessageView(string message) => RemoveBadge(b => b.badgeText == message);

		public void Highlight()
		{
			AddToClassList("Highlight");
		}

		public void UnHighlight()
		{
			RemoveFromClassList("Highlight");
		}

		#endregion

		#region Callbacks & Overrides

		readonly VisualElement infiniteLoopContainer = new VisualElement{ name = "infinite-loop-error" };
		readonly VisualElement infiniteLoopLabel     = new Label("Infinite Loop Error!");
		
		void ComputeOrderUpdatedCallback()
		{
			//Update debug compute order
			// computeOrderLabel.text = "Compute order: " + nodeTarget.computeOrder;
			if(nodeTarget.computeOrder == BaseGraph.loopComputeOrder)
			{
				AddToClassList("infinite-loop-error");
				if(mainContainer.Q("infinite-loop-error") == null)
				{
					infiniteLoopLabel.tooltip              = "This node won't work if you don't fix the recursive loop.";
					infiniteLoopLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
					infiniteLoopContainer.Add(infiniteLoopLabel);
					mainContainer.Add(infiniteLoopContainer);
					infiniteLoopContainer.SendToBack();
				}
			}
			else
			{
				RemoveFromClassList("infinite-loop-error");
				if(mainContainer.Q("infinite-loop-error") != null)
					mainContainer.Remove(infiniteLoopContainer);
			}
		}

		public virtual void Enable()
		{
			DrawDefaultInspector();
		}

		protected void AddInputContainer()
		{
			inputContainerElement = new VisualElement {name = "input-container"};
			mainContainer.parent.Add(inputContainerElement);
			inputContainerElement.SendToBack();
			inputContainerElement.pickingMode = PickingMode.Ignore;
		}
		
		Dictionary<string, List<(object value, VisualElement target)>> visibleConditions = new Dictionary<string, List<(object value, VisualElement target)>>();
		Dictionary<FieldInfo, List<VisualElement>>                     fieldControlsMap  = new Dictionary<FieldInfo, List<VisualElement>>();

		protected virtual void DrawDefaultInspector()
		{
			AddInputContainer();
			
			var fields = nodeTarget.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			
			visibleConditions.Clear();
			foreach(var field in fields)
			{
				var isSerializedInput = false;
				var isEmpty           = false;

				//skip if the field is a node setting
				if(field.GetCustomAttribute(typeof(SettingAttribute)) != null)
				{
					isEmpty = true;
					hasSettings = true;
				}

				//skip if the field is not serializable
				else if(!field.IsPublic && field.GetCustomAttribute(typeof(SerializeField)) == null)
					isEmpty = true;

				//skip if the field is an input/output and not marked as SerializedField
				else if(field.GetCustomAttribute(typeof(SerializeField)) == null &&
				   (field.GetCustomAttribute(typeof(InputAttribute)) != null || field.GetCustomAttribute(typeof(OutputAttribute)) != null))
					isEmpty = true;

				//skip if marked with NonSerialized or HideInInspector
				else if(field.GetCustomAttribute(typeof(System.NonSerializedAttribute)) != null || field.GetCustomAttribute(typeof(HideInInspector)) != null)
					isEmpty = true;

				if(isEmpty)
				{
					if(field.GetCustomAttribute(typeof(InputAttribute)) != null)
					{
						var box = new VisualElement {name = field.Name};
						box.AddToClassList("port-input-element");
						box.AddToClassList("empty");
						inputContainerElement.Add(box);
					}
					continue;
				}

				var fieldName = GetFormattedFieldName(field.Name);

				if(field.GetCustomAttribute(typeof(InputAttribute)) != null && field.GetCustomAttribute(typeof(SerializeField)) != null)
				{
					isSerializedInput = true;
				}

				AddControlField(field, fieldName, isSerializedInput);
			}
		}

		protected string GetFormattedFieldName(string fieldName)
		{
			var returnedName = Regex.Replace( 
				Regex.Replace( 
					fieldName, 
					@"(\P{Ll})(\P{Ll}\p{Ll})", 
					"$1 $2" 
				), 
				@"(\p{Ll})(\P{Ll})", 
				"$1 $2" 
			);

			return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(returnedName);
		}
		
		void UpdateFieldVisibility(string fieldName, object newValue)
		{
			if (visibleConditions.TryGetValue(fieldName, out var list))
			{
				foreach (var elem in list)
				{
					if (newValue.Equals(elem.value))
						elem.target.style.display = DisplayStyle.Flex;
					else
						elem.target.style.display = DisplayStyle.None;
				}
			}
		}
		
		void UpdateOtherFieldValueSpecific<T>(FieldInfo field, object newValue)
		{
			foreach(var inputField in fieldControlsMap[field])
			{
				if (inputField is INotifyValueChanged<T> notify)
					notify.SetValueWithoutNotify((T)newValue);
			}
		}

		static MethodInfo specificUpdateOtherFieldValue = typeof(BaseNodeView).GetMethod(nameof(UpdateOtherFieldValueSpecific), BindingFlags.NonPublic | BindingFlags.Instance);
		void UpdateOtherFieldValue(FieldInfo info, object newValue)
		{
			// Warning: Keep in sync with FieldFactory CreateField
			var fieldType     = info.FieldType.IsSubclassOf(typeof(UnityEngine.Object)) ? typeof(UnityEngine.Object) : info.FieldType;
			var genericUpdate = specificUpdateOtherFieldValue.MakeGenericMethod(fieldType);

			genericUpdate.Invoke(this, new object[]{info, newValue});
		}
		
		object GetInputFieldValueSpecific<T>(FieldInfo field)
		{
			if (fieldControlsMap.TryGetValue(field, out var list))
			{
				foreach (var inputField in list)
				{
					if (inputField is INotifyValueChanged<T> notify)
						return notify.value;
				}
			}
			return null;
		}

		static MethodInfo specificGetValue = typeof(BaseNodeView).GetMethod(nameof(GetInputFieldValueSpecific), BindingFlags.NonPublic | BindingFlags.Instance);
		
		object GetInputFieldValue(FieldInfo info)
		{
			// Warning: Keep in sync with FieldFactory CreateField
			var fieldType     = info.FieldType.IsSubclassOf(typeof(UnityEngine.Object)) ? typeof(UnityEngine.Object) : info.FieldType;
			var genericUpdate = specificGetValue.MakeGenericMethod(fieldType);

			return genericUpdate.Invoke(this, new object[]{info});
		}
		
		protected VisualElement AddControlField(string fieldName, string label = null, bool isSerializedInput = false, Action valueChangedCallback = null)
			=> AddControlField(nodeTarget.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), label, isSerializedInput, valueChangedCallback);

		protected VisualElement AddControlField(FieldInfo field, string label = null, bool isSerializedInput = false, Action valueChangedCallback = null)
		{
			if (field == null)
				return null;
	
			var element = FieldFactory.CreateField(field.FieldType, field.GetValue(nodeTarget), (newValue) => {
				owner.RegisterCompleteObjectUndo("Updated " + newValue);
				field.SetValue(nodeTarget, newValue);
				NotifyNodeChanged();
				valueChangedCallback?.Invoke();
				UpdateFieldVisibility(field.Name, newValue);
				// When you have the node inspector, it's possible to have multiple input fields pointing to the same
				// property. We need to update those manually otherwise they still have the old value in the inspector.
				UpdateOtherFieldValue(field, newValue);
			}, isSerializedInput ? "" : label);
			
			if (!fieldControlsMap.TryGetValue(field, out var inputFieldList))
				inputFieldList = fieldControlsMap[field] = new List<VisualElement>();
			inputFieldList.Add(element);

			if(element != null)
			{
				if(isSerializedInput)
				{
					var box = new VisualElement {name = field.Name};
					box.AddToClassList("port-input-element");
					box.Add(element);
					inputContainerElement.Add(box);
				}
				else
				{
					controlsContainer.AddToClassList("has-control");
					controlsContainer.Add(element);
				}
			}
			
			var visibleCondition = field.GetCustomAttribute(typeof(VisibleIf)) as VisibleIf;
			if (visibleCondition != null)
			{
				// Check if target field exists:
				var conditionField = nodeTarget.GetType().GetField(visibleCondition.fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				if (conditionField == null)
					Debug.LogError($"[VisibleIf] Field {visibleCondition.fieldName} does not exists in node {nodeTarget.GetType()}");
				else
				{
					visibleConditions.TryGetValue(visibleCondition.fieldName, out var list);
					if (list == null)
						list = visibleConditions[visibleCondition.fieldName] = new List<(object value, VisualElement target)>();
					list.Add((visibleCondition.value, element));
					// TODO
					UpdateFieldVisibility(visibleCondition.fieldName, conditionField.GetValue(nodeTarget));
				}
			}

			return element;
		}
		protected void AddSettingField(FieldInfo field)
		{
			if (field == null)
				return;

			var label = field.GetCustomAttribute<SettingAttribute>().name;
	
			var element = FieldFactory.CreateField(field.FieldType, field.GetValue(nodeTarget), (newValue) => {
				owner.RegisterCompleteObjectUndo("Updated " + newValue);
				field.SetValue(nodeTarget, newValue);
			}, label);

			if(element != null)
			{
				settingsContainer.Add(element);
				element.name = field.Name;
			}
		}
		
		void UpdateFieldValues()
		{
			if(!(EditorWindow.focusedWindow is BaseGraphWindow)) return;
			foreach (var kp in fieldControlsMap)
				UpdateOtherFieldValue(kp.Key, kp.Key.GetValue(nodeTarget));
		}

		internal void OnPortConnected(PortView port)
		{
			if(port.direction == Direction.Input && inputContainerElement?.Q(port.fieldName) != null)
				inputContainerElement.Q(port.fieldName).AddToClassList("empty");

			onPortConnected?.Invoke(port);
		}

		internal void OnPortDisconnected(PortView port)
		{
			if(port.direction == Direction.Input && inputContainerElement?.Q(port.fieldName) != null)
			{
				inputContainerElement.Q(port.fieldName).RemoveFromClassList("empty");

				if (nodeTarget.nodeFields.TryGetValue(port.fieldName, out var fieldInfo))
				{
					var valueBeforeConnection = GetInputFieldValue(fieldInfo.info);

					if (valueBeforeConnection != null)
					{
						fieldInfo.info.SetValue(nodeTarget, valueBeforeConnection);
					}
				}
			}

			onPortDisconnected?.Invoke(port);
		}

		// TODO: a function to force to reload the custom behavior ports (if we want to do a button to add ports for example)

		public virtual void OnRemoved()
		{
			Undo.undoRedoPerformed -= UpdateFieldValues;
		}
		public virtual void OnCreated() {}

		public override void SetPosition(Rect newPos)
		{
            if (initializing || !nodeTarget.isLocked)
            {
                initializing = false;
                base.SetPosition(newPos);

				owner.RegisterCompleteObjectUndo("Moved graph node");
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
			if(nodeTarget.nodeLock)
			{
				AddToClassList("locked-node");
			}
			else
			{
				RemoveFromClassList("locked-node");
			}
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
			// evt.menu.AppendAction("Debug", (e) => ToggleDebug(), DebugStatus);
            if (nodeTarget.unlockable)
                evt.menu.AppendAction((nodeTarget.isLocked ? "Unlock" : "Lock"), (e) => ChangeLockStatus(), LockStatus);
        }

        Status LockStatus(DropdownMenuAction action)
        {
            return Status.Normal;
        }
  //
  //       Status DebugStatus(DropdownMenuAction action)
		// {
		// 	if (nodeTarget.debug)
		// 		return Status.Checked;
		// 	return Status.Normal;
		// }

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

		IEnumerable<PortView> SyncPortCounts(IEnumerable< NodePort > ports, IEnumerable< PortView > portViews)
		{
			var listener     = owner.connectorListener;
			var portViewList = portViews.ToList();

			// Maybe not good to remove ports as edges are still connected :/
			foreach (var pv in portViews.ToList())
			{
				// If the port have disappeared from the node data, we remove the view:
				// We can use the identifier here because this function will only be called when there is a custom port behavior
				if (!ports.Any(p => p.portData.identifier == pv.portData.identifier))
				{
					RemovePort(pv);
					portViewList.Remove(pv);
				}
			}

			foreach (var p in ports)
			{
				// Add missing port views
				if (!portViews.Any(pv => p.portData.identifier == pv.portData.identifier))
				{
					Direction portDirection = nodeTarget.IsFieldInput(p.fieldName) ? Direction.Input : Direction.Output;
					var       pv            = AddPort(p.fieldInfo, portDirection, listener, p.portData);
					portViewList.Add(pv);
				}
			}
			
			return portViewList;
		}

		void SyncPortOrder(IEnumerable< NodePort > ports, IEnumerable< PortView > portViews)
		{
			var portViewList = portViews.ToList();
			var portsList    = ports.ToList();

			// Re-order the port views to match the ports order in case a custom behavior re-ordered the ports
			for (int i = 0; i < portsList.Count; i++)
			{
				if(portsList[i].portData.displayType == typeof(ConditionalLink))
					continue;

				var id = portsList[i].portData.identifier;

				var pv = portViewList.FirstOrDefault(p => p.portData.identifier == id);
				if (pv != null)
					InsertPort(pv, i);
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
						IEnumerable< PortView > portViewsList = portViewPerFieldName;
						if (portPerFieldName.Count() != portViewPerFieldName.Count())
							portViewsList = SyncPortCounts(portPerFieldName, portViewPerFieldName);
						SyncPortOrder(portPerFieldName, portViewsList);
						// We don't care about the result, we just iterate over port and portView
						return "";
					}).ToList();
				}

				// Here we're sure that we have the same amount of port and portView
				// so we can update the view with the new port data (if the name of a port have been changed for example)

				for (int i = 0; i < portViews.Count; i++)
					if(ports[i] != null) portViews[i].UpdatePortView(ports[i].portData);
			}

			return base.RefreshPorts();
		}

		protected void ForceUpdatePorts()
		{
			nodeTarget.UpdateAllPorts();

			RefreshPorts();
		}
		
		void UpdatePortsForField(string fieldName)
		{
			// TODO: actual code
			RefreshPorts();
		}

		protected virtual VisualElement CreateSettingsView() => new Label("Settings") {name = "header"};
		protected virtual VisualElement PrepareSettingsView() => null;
		
		/// <summary>
		/// Send an event to the graph telling that the content of this node have changed
		/// </summary>
		public void NotifyNodeChanged() => owner.graph.NotifyNodeChanged(nodeTarget);

		#endregion
	}
}