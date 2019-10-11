using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GraphProcessor;
using System.Linq;

[NodeCustomEditor(typeof(ParameterNode))]
public class ParameterNodeView : BaseNodeView
{
    ParameterNode parameterNode;

    public override void Enable()
    {
        parameterNode = nodeTarget as ParameterNode;

        EnumField accessorSelector = new EnumField(parameterNode.accessor);
        accessorSelector.SetValueWithoutNotify(parameterNode.accessor);
        accessorSelector.RegisterValueChangedCallback(evt =>
        {
            parameterNode.accessor = (ParameterAccessor)evt.newValue;
            UpdatePort();
            controlsContainer.MarkDirtyRepaint();
            ForceUpdatePorts();
        });

        UpdatePort();
        controlsContainer.Add(accessorSelector);

        //    Find and remove expand/collapse button
        titleContainer.Remove(titleContainer.Q("title-button-container"));
        //    Remove Port from the #content
        topContainer.parent.Remove(topContainer);
        //    Add Port to the #title
        titleContainer.Add(topContainer);
        //    Find parameter type and add icon 
        var type = parameterNode.parameter.type.Split(new[] {','})[0].Split(new[] {'.'})[1];
        mainContainer.parent.AddToClassList("parameter-"+type);
        titleContainer.Add(new VisualElement {name = "parameterIcon"});
        titleContainer.Q("parameterIcon").SendToBack();

        parameterNode.onParameterChanged += UpdateView;
        UpdateView();
    }

    void UpdateView()
    {
        title = parameterNode.parameter?.name;
    }

    void UpdatePort()
    {
        if(parameterNode.accessor == ParameterAccessor.Set)
        {
            titleContainer.AddToClassList("input");
        }
        else
        {
            titleContainer.RemoveFromClassList("input");
        }
    }
}
