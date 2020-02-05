﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GraphProcessor;

[NodeCustomEditor(typeof(IfNode))]
public class IfNodeView : ConditionalNodeView
{
	public override void Enable()
	{
		base.Enable();
		var node = nodeTarget as IfNode;

        // Create your fields using node's variables and add them to the controlsContainer

		controlsContainer.Add(new Label($"Last Evaluation: {node.condition}"));
	}
}