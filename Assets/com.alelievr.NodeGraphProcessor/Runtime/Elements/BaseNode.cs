using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using Unity.Jobs;
using System.Linq;

namespace GraphProcessor
{
	public delegate IEnumerable<PortData> CustomPortBehaviorDelegate(List<SerializableEdge> edges);
	public delegate IEnumerable< PortData > CustomPortTypeBehaviorDelegate(string fieldName, string displayName, object value);

	[Serializable]
	public abstract class BaseNode
	{
		public virtual string category => null;

		public virtual string name => GetType().Name;

		public virtual string layoutStyle => string.Empty;

		public virtual bool unlockable => true;

		public virtual bool isLocked => nodeLock;

		public virtual string headerClass => "";
		
		public virtual string nodeClass => "";

		//id
		public string GUID;

		public int  computeOrder = -1;
		
		/// <summary>Tell wether or not the node can be processed. Do not check anything from inputs because this step happens before inputs are sent to the node</summary>
		public virtual bool canProcess => true;
		
		/// <summary>Show the node controlContainer only when the mouse is over the node</summary>
		public virtual bool showControlsOnHover => false;


		[NonSerialized]
		public readonly NodeInputPortContainer inputPorts;

		[NonSerialized]
		public readonly NodeOutputPortContainer outputPorts;

		//Node view datas
		public Rect position;
		public bool expanded;
		public bool debug;
		public bool nodeLock;

		public delegate void ProcessDelegate();

		public event ProcessDelegate                 onProcessed;
		public event Action<string, NodeMessageType> onMessageAdded;
		public event Action<string>                  onMessageRemoved;
		public event Action<SerializableEdge>        onAfterEdgeConnected;
		public event Action<SerializableEdge>        onAfterEdgeDisconnected;
		/// <summary>
		/// Triggered after a single/list of port(s) is updated, the parameter is the field name
		/// </summary>
		public event Action< string > onPortsUpdated;

		[NonSerialized]
		internal Dictionary<string, NodeFieldInformation> nodeFields = new Dictionary<string, NodeFieldInformation>();
		
		[NonSerialized]
		internal Dictionary< Type, CustomPortTypeBehaviorDelegate> customPortTypeBehaviorMap = new Dictionary<Type, CustomPortTypeBehaviorDelegate>();
		
		[NonSerialized]
		List<string> messages = new List<string>();

		[NonSerialized]
		protected BaseGraph graph;

		internal class NodeFieldInformation
		{
			public string                     name;
			public string                     fieldName;
			public FieldInfo                  info;
			public bool                       input;
			public bool                       isMultiple;
			public CustomPortBehaviorDelegate behavior;

			public NodeFieldInformation(FieldInfo info, string name, bool input, bool isMultiple, CustomPortBehaviorDelegate behavior)
			{
				this.input      = input;
				this.isMultiple = isMultiple;
				this.info       = info;
				this.name       = name;
				this.fieldName  = info.Name;
				this.behavior   = behavior;
			}
		}
		
		struct PortUpdate
		{
			public List<string> fieldNames;
			public BaseNode     node;

			public void Deconstruct(out List<string> fieldNames, out BaseNode node)
			{
				fieldNames = this.fieldNames;
				node       = this.node;
			}
		}

		// Used in port update algorithm
		Stack<PortUpdate>   fieldsToUpdate = new Stack<PortUpdate>();
		HashSet<PortUpdate> updatedFields  = new HashSet<PortUpdate>();

		public static T CreateFromType<T>(Vector2 position) where T : BaseNode
		{
			return CreateFromType(typeof(T), position) as T;
		}

		public static BaseNode CreateFromType(Type nodeType, Vector2 position)
		{
			if(!nodeType.IsSubclassOf(typeof(BaseNode)))
				return null;

			var node = Activator.CreateInstance(nodeType) as BaseNode;

			node.position = new Rect(position, new Vector2(100, 100));

			ExceptionToLog.Call(() => node.OnNodeCreated());

			return node;
		}

		#region Initialization

		// called by the BaseGraph when the node is added to the graph
		public void Initialize(BaseGraph graph)
		{
			this.graph = graph;
			
			ExceptionToLog.Call(() => Enable());
			
			InitializePorts();
		}

		void InitializeCustomPortTypeMethods()
		{
			MethodInfo[] methods = new MethodInfo[0];
			Type baseType = GetType();
			while (true)
			{
				methods = baseType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
				foreach (var method in methods)
				{
					var typeBehaviors = method.GetCustomAttributes<CustomPortTypeBehavior>().ToArray();

					if (typeBehaviors.Length == 0)
						continue;

					CustomPortTypeBehaviorDelegate deleg = null;
					try
					{
						deleg = Delegate.CreateDelegate(typeof(CustomPortTypeBehaviorDelegate), this, method) as CustomPortTypeBehaviorDelegate;
					} catch (Exception e)
					{
						Debug.LogError(e);
						Debug.LogError($"Cannot convert method {method} to a delegate of type {typeof(CustomPortTypeBehaviorDelegate)}");
					}

					foreach (var typeBehavior in typeBehaviors)
						customPortTypeBehaviorMap[typeBehavior.type] = deleg;
				}

				// Try to also find private methods in the base class
				baseType = baseType.BaseType;
				if (baseType == null)
					break;
			}
		}
		
		internal void InitializePorts()
		{
			InitializeCustomPortTypeMethods();
			
			foreach (var nodeFieldKP in nodeFields.ToList().OrderByDescending(kp => kp.Value.info.MetadataToken))
			{
				var nodeField = nodeFieldKP.Value;

				if (HasCustomBehavior(nodeField))
				{
					UpdatePortsForField(nodeField.fieldName);
				}
				else
				{
					// If we don't have a custom behavor on the node, we just have to create a simple port
					AddPort(nodeField.input, nodeField.fieldName, new PortData {acceptMultipleEdges = nodeField.isMultiple, displayName = nodeField.name});
				}
			}
		}

		protected BaseNode()
		{
			inputPorts  = new NodeInputPortContainer(this);
			outputPorts = new NodeOutputPortContainer(this);

			InitializeInOutDatas();
		}

		public bool UpdateAllPorts()
		{
			bool changed = false;
			foreach(var field in nodeFields)
				changed |= UpdatePortsForField(field.Value.fieldName);

			return changed;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fieldName"></param>
		public bool UpdatePortsForFieldLocal(string fieldName)
		{
			bool changed = false;

			if(!nodeFields.ContainsKey(fieldName))
				return false;

			var fieldInfo = nodeFields[fieldName];

			if (!HasCustomBehavior(fieldInfo))
				return false;

			List<string> finalPorts = new List<string>();

			var portCollection = fieldInfo.input ? (NodePortContainer) inputPorts : outputPorts;

			// Gather all fields for this port (before to modify them)
			var nodePorts = portCollection.Where(p => p.fieldName == fieldName);
			// Gather all edges connected to these fields:
			var edges = nodePorts.SelectMany(n => n.GetEdges()).ToList();

			if (fieldInfo.behavior != null)
			{
				foreach (var portData in fieldInfo.behavior(edges))
					AddPortData(portData);
			}
			else
			{
				var customPortTypeBehavior = customPortTypeBehaviorMap[fieldInfo.info.FieldType];

				foreach (var portData in customPortTypeBehavior(fieldName, fieldInfo.name, fieldInfo.info.GetValue(this)))
					AddPortData(portData);
			}

			void AddPortData(PortData portData)
			{
				var port = nodePorts.FirstOrDefault(n => n.portData.identifier == portData.identifier);
				// Guard using the port identifier so we don't duplicate identifiers
				if(port == null)
				{
					AddPort(fieldInfo.input, fieldName, portData);
					changed = true;
				}
				else
				{
					// in case the port type have changed for an incompatible type, we disconnect all the edges attached to this port
					if(!BaseGraph.TypesAreConnectable(port.portData.displayType, portData.displayType))
					{
						foreach(var edge in port.GetEdges().ToList())
							graph.Disconnect(edge.GUID);
					}

					// patch the port data
					if (port.portData != portData)
					{
						port.portData.CopyFrom(portData);
						changed = true;
					}
				}

				finalPorts.Add(portData.identifier);
			}

			// TODO
			// Remove only the ports that are no more in the list
			if(nodePorts != null)
			{
				var currentPortsCopy = nodePorts.ToList();
				foreach(var currentPort in currentPortsCopy)
				{
					// If the current port does not appear in the list of final ports, we remove it
					if(!finalPorts.Any(id => id == currentPort.portData.identifier))
					{
						RemovePort(fieldInfo.input, currentPort);
						changed = true;
					}
				}
			}
			
			// Make sure the port order is correct:
			portCollection.Sort((p1, p2) => {
				int p1Index = finalPorts.FindIndex(id => p1.portData.identifier == id);
				int p2Index = finalPorts.FindIndex(id => p2.portData.identifier == id);

				if (p1Index == -1 || p2Index == -1)
					return 0;

				return p1Index.CompareTo(p2Index);
			});

			onPortsUpdated?.Invoke(fieldName);

			return changed;
		}

		bool HasCustomBehavior(NodeFieldInformation info)
		{
			if (info.behavior != null)
				return true;

			if (customPortTypeBehaviorMap.ContainsKey(info.info.FieldType))
				return true;

			return false;
		}
		
		/// <summary>
		/// Update the ports related to one C# property field and all connected nodes in the graph
		/// </summary>
		/// <param name="fieldName"></param>
		public bool UpdatePortsForField(string fieldName)
		{
			bool changed = false;

			fieldsToUpdate.Clear();
			updatedFields.Clear();

			fieldsToUpdate.Push(new PortUpdate{fieldNames = new List<string>(){fieldName}, node = this});

			// Iterate through all the ports that needs to be updated, following graph connection when the 
			// port is updated. This is required ton have type propagation multiple nodes that changes port types
			// are connected to each other (i.e. the relay node)
			while (fieldsToUpdate.Count != 0)
			{
				var (fields, node) = fieldsToUpdate.Pop();

				// Avoid updating twice a port
				if (updatedFields.Any((t) => t.node == node && fields.SequenceEqual(t.fieldNames)))
					continue;
				updatedFields.Add(new PortUpdate{fieldNames = fields, node = node});

				foreach (var field in fields)
				{
					if (node.UpdatePortsForFieldLocal(field))
					{
						foreach (var port in node.IsFieldInput(field) ? (NodePortContainer)node.inputPorts : node.outputPorts)
						{
							if (port.fieldName != field)
								continue;

							foreach(var edge in port.GetEdges())
							{
								var edgeNode           = (node.IsFieldInput(field)) ? edge.outputNode : edge.inputNode;
								var fieldsWithBehavior = edgeNode.nodeFields.Values.Where(f => HasCustomBehavior(f)).Select(f => f.fieldName).ToList();
								fieldsToUpdate.Push(new PortUpdate{fieldNames = fieldsWithBehavior, node = edgeNode});
							}
						}
						changed = true;
					}
				}
			}

			return changed;
		}
		
		HashSet<BaseNode> portUpdateHashSet = new HashSet<BaseNode>();
		
		internal void DisableInternal() => ExceptionToLog.Call(() => Disable());
		internal void DestroyInternal() => ExceptionToLog.Call(() => Destroy());

		/// <summary>
		/// Called only when the node is created, not when instantiated
		/// </summary>
		public virtual void OnNodeCreated()
		{
			GUID = Guid.NewGuid().ToString();
		}

		public virtual FieldInfo[] GetNodeFields() => GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

		void InitializeInOutDatas()
		{
			var fields  = GetNodeFields();
			var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			foreach(var field in fields)
			{
				var    inputAttribute  = field.GetCustomAttribute<InputAttribute>();
				var    outputAttribute = field.GetCustomAttribute<OutputAttribute>();
				bool   isMultiple      = false;
				bool   input           = false;
				string name            = field.Name;

				if(inputAttribute == null && outputAttribute == null)
					continue;

				//check if field is a collection type
				isMultiple = (inputAttribute != null) ? inputAttribute.allowMultiple : (outputAttribute.allowMultiple);
				input      = inputAttribute != null;

				if(!String.IsNullOrEmpty(inputAttribute?.name))
					name = inputAttribute.name;
				if(!String.IsNullOrEmpty(outputAttribute?.name))
					name = outputAttribute.name;

				// By default we set the behavior to null, if the field have a custom behavior, it will be set in the loop just below
				nodeFields[field.Name] = new NodeFieldInformation(field, name, input, isMultiple, null);
			}

			foreach(var method in methods)
			{
				var                        customPortBehaviorAttribute = method.GetCustomAttribute<CustomPortBehaviorAttribute>();
				CustomPortBehaviorDelegate behavior                    = null;

				if(customPortBehaviorAttribute == null)
					continue;

				// Check if custom port behavior function is valid
				try
				{
					var referenceType = typeof(CustomPortBehaviorDelegate);
					behavior = (CustomPortBehaviorDelegate) Delegate.CreateDelegate(referenceType, this, method, true);
				}
				catch
				{
					Debug.LogError("The function " + method + " cannot be converted to the required delegate format: " + typeof(CustomPortBehaviorDelegate));
				}

				if(nodeFields.ContainsKey(customPortBehaviorAttribute.fieldName))
					nodeFields[customPortBehaviorAttribute.fieldName].behavior = behavior;
				else
					Debug.LogError("Invalid field name for custom port behavior: " + method + ", " + customPortBehaviorAttribute.fieldName);
			}
		}

		#endregion

		#region Events and Processing

		public void OnEdgeConnected(SerializableEdge edge)
		{
			bool              input          = edge.inputNode == this;
			NodePortContainer portCollection = (input) ? (NodePortContainer) inputPorts : outputPorts;

			portCollection.Add(edge);

			// Reset default values of input port:
			bool haveConnectedEdges = edge.inputNode.inputPorts.Where(p => p.fieldName == edge.inputFieldName).Any(p => p.GetEdges().Count != 0);
			if(edge.inputNode == this && !haveConnectedEdges)
				edge.inputPort?.ResetToDefault();

			UpdateAllPorts();

			onAfterEdgeConnected?.Invoke(edge);
		}

		public void OnEdgeDisconnected(SerializableEdge edge)
		{
			if(edge == null)
				return;

			bool              input          = edge.inputNode == this;
			NodePortContainer portCollection = (input) ? (NodePortContainer) inputPorts : outputPorts;

			portCollection.Remove(edge);

			UpdateAllPorts();

			onAfterEdgeDisconnected?.Invoke(edge);
		}

		public void OnProcess()
		{
			inputPorts.PullDatas();

			ExceptionToLog.Call(() => Process());

			InvokeOnProcessed();

			outputPorts.PushDatas();
		}

		public void InvokeOnProcessed() => onProcessed?.Invoke();

		/// <summary>
		/// Called when the node is enabled
		/// </summary>
		protected virtual void Enable() {}
		/// <summary>
		/// Called when the node is disabled
		/// </summary>
		protected virtual void Disable() {}
		/// <summary>
		/// Called when the node is removed
		/// </summary>
		protected virtual void Destroy() {}

		protected virtual void Process() {}

		#endregion

		#region API and utils

		public void AddPort(bool input, string fieldName, PortData portData)
		{
			// Fixup port data info if needed:
			if (portData.displayType == null)
				portData.displayType = nodeFields[fieldName].info.FieldType;
			
			if(input)
				inputPorts.Add(new NodePort(this, fieldName, portData));
			else
				outputPorts.Add(new NodePort(this, fieldName, portData));
		}

		public void RemovePort(bool input, NodePort port)
		{
			if(input)
				inputPorts.Remove(port);
			else
				outputPorts.Remove(port);
		}

		public void RemovePort(bool input, string fieldName)
		{
			if(input)
				inputPorts.RemoveAll(p => p.fieldName == fieldName);
			else
				outputPorts.RemoveAll(p => p.fieldName == fieldName);
		}

		public IEnumerable<BaseNode> GetInputNodes()
		{
			foreach(var port in inputPorts)
			foreach(var edge in port.GetEdges())
				yield return edge.outputNode;
		}

		public IEnumerable<BaseNode> GetOutputNodes()
		{
			foreach(var port in outputPorts)
			foreach(var edge in port.GetEdges())
				yield return edge.inputNode;
		}
		
		/// <summary>
		/// Return a node matching the condition in the dependencies of the node
		/// </summary>
		/// <param name="condition">Condition to choose the node</param>
		/// <returns>Matched node or null</returns>
		public BaseNode FindInDependencies(Func<BaseNode, bool> condition)
		{
			Stack<BaseNode> dependencies = new Stack<BaseNode>();

			dependencies.Push(this);

			int depth = 0;
			while (dependencies.Count > 0)
			{
				var node = dependencies.Pop();

				// Guard for infinite loop (faster than a HashSet based solution)
				depth++;
				if (depth > 2000)
					break;

				if (condition(node))
					return node;

				foreach (var dep in node.GetInputNodes())
					dependencies.Push(dep);
			}
			return null;
		}

		public NodePort GetPort(string fieldName, string identifier)
		{
			return inputPorts.Concat(outputPorts).FirstOrDefault(p =>
			{
				var bothNull = String.IsNullOrEmpty(identifier) && String.IsNullOrEmpty(p.portData.identifier);
				return p.fieldName == fieldName && (bothNull || identifier == p.portData.identifier);
			});
		}

		public bool IsFieldInput(string fieldName) => nodeFields[fieldName].input;

		public void AddMessage(string message, NodeMessageType messageType)
		{
			if(messages.Contains(message))
				return;

			onMessageAdded?.Invoke(message, messageType);
			messages.Add(message);
		}

		public void RemoveMessage(string message)
		{
			onMessageRemoved?.Invoke(message);
			messages.Remove(message);
		}

		public void RemoveMessageContains(string subMessage)
		{
			string toRemove = messages.Find(m => m.Contains(subMessage));
			messages.Remove(toRemove);
			onMessageRemoved?.Invoke(toRemove);
		}

		public void ClearMessages()
		{
			foreach(var message in messages)
				onMessageRemoved?.Invoke(message);
			messages.Clear();
		}

		#endregion
	}
}