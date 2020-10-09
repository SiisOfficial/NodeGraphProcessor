using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;

[System.Serializable, NodeMenuItem("Logic/If")]
public class IfNode : ConditionalNode
{
	public override string category => "Logic";

	public override string name => "If";
	
	[Input(name = "Condition")]
	public bool condition;

	[Output(name = "True")]
	public ConditionalLink @true;

	[Output(name = "False")]
	public ConditionalLink @false;

	// public CompareFunction		compareOperator;

	public override IEnumerable<ConditionalNode> GetExecutedNodes()
	{
		string fieldName = condition ? nameof(@true) : nameof(@false);

		// Return all the nodes connected to either the true or false node
		return outputPorts.FirstOrDefault(n => n.fieldName == fieldName)
						  .GetEdges().Select(e => e.inputNode as ConditionalNode);
	}
}