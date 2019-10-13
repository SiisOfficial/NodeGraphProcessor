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

		//	We should need to change this to just add the class, not the texture.
		public ExposedParameterFieldView(BaseGraphView graphView, ExposedParameter param, string shortType) : base(GetIconFromType(shortType), param.name, shortType)
		{
			this.graphView = graphView;
			parameter = param;
			this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

			(this.Q("textField") as TextField).RegisterValueChangedCallback((e) => {
				param.name = e.newValue;
				text = e.newValue;
				graphView.graph.UpdateExposedParameterName(param, e.newValue);
			});
        }

		void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Rename", (a) => OpenTextEditor(), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", (a) => graphView.graph.RemoveExposedParameter(parameter), DropdownMenuAction.AlwaysEnabled);

            evt.StopPropagation();
        }

		static Texture GetIconFromType(string type)
		{
			var textureName = "console.erroricon.sml";
			var isBuiltIn = true;

			switch(type)
			{
				case "Rigidbody":
					textureName = "Rigidbody Icon";
					break;
				case "Vector3":
					textureName = "Transform Icon";
					break;
				case "GameObject":
					textureName = "GameObject Icon";
					break;
				case "Color":
					textureName = "ColorPicker-HueRing";
					break;
			}

			
			return isBuiltIn ? (Texture)EditorGUIUtility.Load(textureName) : (Texture)Resources.Load(textureName);
		} 
	}
}