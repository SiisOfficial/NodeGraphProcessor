using UnityEditor.UIElements;
using UnityEngine.UIElements;
using GraphProcessor;

[NodeCustomEditor(typeof(StartNode))]
public class StartNodeView : ConditionalNodeView
{
	public override void Enable()
	{
		this.Q("title").Q("collapse-button").Q("icon").RemoveFromHierarchy();
	}
}