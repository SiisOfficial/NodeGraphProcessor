using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Linq;

namespace GraphProcessor
{
	public class ExposedParameterFieldView : BlackboardField
	{
		protected BaseGraphView	graphView;

		public ExposedParameter	parameter { get; private set; }

		public ExposedParameterFieldView(BaseGraphView graphView, ExposedParameter param, string shortType) : base(null, param.name, shortType)
		{
			this.graphView = graphView;
			parameter = param;
			this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
			this.AddToClassList("parameter-"+shortType);
			this.Q("icon").visible = true;

			(this.Q("textField") as TextField).RegisterValueChangedCallback((e) => {
				param.name = e.newValue;
				text = e.newValue;
				if(param.name == "inputVector3" || param.name == "inputVector2" || param.name == "inputFloat" || param.name == "inputInteger")
				{
					param.settings.isHidden = true;
				}
				graphView.graph.UpdateExposedParameterName(param, e.newValue);
			});

			var parameterElem = this.Q("pill");
			
			parameterElem.RegisterCallback<MouseOverEvent>((e)=>
			{
				graphView.Query<ParameterNodeView>(className: "pName-" + parameter.name).Build().ForEach(view => view.AddToClassList("Highlight"));
			});
			
			parameterElem.RegisterCallback<MouseOutEvent>((e)=>
			{
				graphView.Query<ParameterNodeView>(className: "pName-" + parameter.name).Build().ForEach(view => view.RemoveFromClassList("Highlight"));
			});
        }

		void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Rename", (a) => OpenTextEditor(), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", (a) => graphView.graph.RemoveExposedParameter(parameter), DropdownMenuAction.AlwaysEnabled);

            evt.StopPropagation();
        }
	}
}