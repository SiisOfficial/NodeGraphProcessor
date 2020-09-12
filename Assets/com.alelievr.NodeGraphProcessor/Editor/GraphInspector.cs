using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;

namespace GraphProcessor
{
	public class GraphInspector : Editor
	{
		protected VisualElement root;
		protected BaseGraph     graph;

		VisualElement parameterContainer;

		protected virtual void OnEnable()
		{
			graph                               =  target as BaseGraph;
			graph.onExposedParameterListChanged += UpdateExposedParameters;
			graph.onExposedParameterModified    += UpdateExposedParameters;
		}

		protected virtual void OnDisable()
		{
			graph.onExposedParameterListChanged -= UpdateExposedParameters;
			graph.onExposedParameterModified    -= UpdateExposedParameters;
		}

		public sealed override VisualElement CreateInspectorGUI()
		{
			root = new VisualElement();
			CreateInspector();
			return root;
		}

		protected virtual void CreateInspector()
		{
			parameterContainer = new VisualElement
			{
				name = "ExposedParameters"
			};
			parameterContainer.style.marginTop    = 5;
			parameterContainer.style.marginBottom = 5;
			FillExposedParameters(parameterContainer);

			root.Add(parameterContainer);
		}

		protected void FillExposedParameters(VisualElement parameterContainer)
		{
			if(graph.exposedParameters.Count != 0)
			{
				var parametersTitle = new Label("Exposed Parameters:");
				parametersTitle.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
				parameterContainer.Add(parametersTitle);
			}
			else return;

			var hasHidden  = false;
			var hasDynamic = false;

			foreach(var param in graph.exposedParameters)
			{
				if(param.settings.isHidden)
				{
					hasHidden = true;
					if(param.name == "inputVector3" || param.name == "inputVector2" || param.name == "inputFloat" || param.name == "inputInteger" || param.name == "inputGameObject")
						hasDynamic = true;

					continue;
				}

				DrawParameter(param, parameterContainer);
			}

			if(!hasHidden) return;

			var hiddenTitle = new Label("Hidden Parameters:");
			hiddenTitle.style.marginTop               = 10;
			hiddenTitle.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
			parameterContainer.Add(hiddenTitle);

			foreach(var param in graph.exposedParameters)
			{
				if(!param.settings.isHidden || param.name == "inputVector3" || param.name == "inputVector2" || param.name == "inputFloat" ||
				   param.name == "inputInteger" || param.name == "inputGameObject") continue;
				DrawParameter(param, parameterContainer);
			}

			if(!hasDynamic) return;

			var dynamicTitle = new Label("Dynamics:");
			dynamicTitle.style.marginTop               = 10;
			dynamicTitle.style.marginLeft              = 3;
			dynamicTitle.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
			parameterContainer.Add(dynamicTitle);

			foreach(var param in graph.exposedParameters)
			{
				if(param.name == "inputVector3" || param.name == "inputVector2" || param.name == "inputFloat" || param.name == "inputInteger" || param.name == "inputGameObject")
					DrawDynamic(param.name, Type.GetType(param.type)?.Name, parameterContainer);
			}
		}

		void DrawDynamic(string paramName, string paramType, VisualElement paramContainer)
		{
			VisualElement prop = new VisualElement();
			prop.style.display = DisplayStyle.Flex;
			var label = new Label {text = paramName + "     (" + paramType + ")"};
			label.style.marginLeft   = 6;
			label.style.marginTop    = 1;
			label.style.marginBottom = 1;
			prop.Add(label);
			paramContainer.Add(prop);
		}

		void DrawParameter(ExposedParameter param, VisualElement paramContainer)
		{
			VisualElement prop = new VisualElement();
			prop.style.display = DisplayStyle.Flex;
			Type paramType = Type.GetType(param.type);

			if(paramType == typeof(Transform) || paramType == typeof(AudioSource) || paramType == typeof(Enum) || paramType == typeof(Component) ||
			   paramType == typeof(BaseGraph) || paramType == typeof(Renderer) || paramType == typeof(SpriteRenderer) || paramType == typeof(Collider) ||
			   paramType == typeof(Collider2D) || paramType == typeof(Rigidbody) || paramType == typeof(Rigidbody2D) || paramType == typeof(Animator) ||
			   paramType == typeof(GameObject)
			)
			{
				var label = new Label {text = param.name + "     (" + paramType.Name + ")"};
				label.style.marginLeft   = 3;
				label.style.marginTop    = 1;
				label.style.marginBottom = 1;
				prop.Add(label);
			}
			else
			{
				var field = FieldFactory.CreateField(paramType, param.serializedValue.value, (newValue) =>
				{
					Undo.RegisterCompleteObjectUndo(graph, "Changed Parameter " + param.name + " to " + newValue);
					param.serializedValue.value = newValue;
				}, param.name);
				prop.Add(field);
			}

			paramContainer.Add(prop);
		}

		void UpdateExposedParameters(string guid) => UpdateExposedParameters();

		void UpdateExposedParameters()
		{
			parameterContainer.Clear();
			FillExposedParameters(parameterContainer);
		}

		// Don't use ImGUI
		public sealed override void OnInspectorGUI() {}
	}
}