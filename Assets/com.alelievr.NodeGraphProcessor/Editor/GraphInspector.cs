using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;

namespace GraphProcessor
{
    public class GraphInspector : Editor
    {
        protected VisualElement root;
        protected BaseGraph     graph;

        VisualElement   parameterContainer;

        void OnEnable()
        {
            graph = target as BaseGraph;
            graph.onExposedParameterListChanged += UpdateExposedParameters;
            graph.onExposedParameterModified += UpdateExposedParameters;
        }

        void OnDisable()
        {
            graph.onExposedParameterListChanged -= UpdateExposedParameters;
            graph.onExposedParameterModified -= UpdateExposedParameters;
        }

        public sealed override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            CreateInspector();
            return root;
        }

        protected virtual void CreateInspector()
        {
            parameterContainer = new VisualElement{
                name = "ExposedParameters"
            };
            FillExposedParameters(parameterContainer);

            root.Add(parameterContainer);
        }

        protected void FillExposedParameters(VisualElement parameterContainer)
        {
            if (graph.exposedParameters.Count != 0)
                parameterContainer.Add(new Label("Exposed Parameters:"));

            var hasHidden = false;
            
            foreach (var param in graph.exposedParameters)
            {
                if(param.settings.isHidden)
                {
                    hasHidden = true;
                    continue;
                }
                DrawParameter(param, parameterContainer);
            }
            
            if(!hasHidden) return;

            var hiddenTitle = new Label("Hidden Parameters:");
            hiddenTitle.style.marginTop = 10;
            parameterContainer.Add(hiddenTitle);
            
            foreach (var param in graph.exposedParameters)
            {
                if(!param.settings.isHidden) continue;
                DrawParameter(param, parameterContainer);
            }
        }

        void DrawParameter(ExposedParameter param, VisualElement parameterContainer)
        {
                VisualElement prop = new VisualElement();
                prop.style.display = DisplayStyle.Flex;
                Type paramType = Type.GetType(param.type);
                if(param.serializedValue.serializedObjectValue is SerializableObject.SerializedObject serializedObjectValue)
                {
                    var field = FieldFactory.CreateField(paramType, serializedObjectValue.value, (newValue) =>
                    {
                        Undo.RegisterCompleteObjectUndo(graph, "Changed Parameter " + param.name + " to " + newValue);
                       serializedObjectValue.value = newValue as UnityEngine.Object;
                    }, param.name);
                    prop.Add(field);
                } 
                else if(param.serializedValue.serializedObjectValue is SerializableObject.SerializedVector2 serializedVector2Value)
                {
                    var field = FieldFactory.CreateField(paramType, serializedVector2Value.value, (newValue) =>
                    {
                        Undo.RegisterCompleteObjectUndo(graph, "Changed Parameter " + param.name + " to " + newValue);
                        (param.serializedValue.serializedObjectValue as SerializableObject.SerializedVector2).value = (Vector2) newValue;
                    }, param.name);
                    prop.Add(field);
                }
                else if(param.serializedValue.serializedObjectValue is SerializableObject.SerializedVector3 serializedVector3Value)
                {
                    var field = FieldFactory.CreateField(paramType, serializedVector3Value.value, (newValue) =>
                    {
                        Undo.RegisterCompleteObjectUndo(graph, "Changed Parameter " + param.name + " to " + newValue);
                        serializedVector3Value.value = newValue as Vector3? ?? new Vector3();
                    }, param.name);
                    prop.Add(field);
                }
                else if(param.serializedValue.serializedObjectValue is SerializableObject.SerializedColor serializedColorValue)
                {
                    var field = FieldFactory.CreateField(paramType, serializedColorValue.value, (newValue) =>
                    {
                        Undo.RegisterCompleteObjectUndo(graph, "Changed Parameter " + param.name + " to " + newValue);
                        serializedColorValue.value = newValue is Color value ? value : new Color();
                    }, param.name);
                    prop.Add(field);
                }
                else if(param.serializedValue.serializedObjectValue is SerializableObject.SerializedAnimationCurve serializedAnimationCurveValue)
                {
                    var field = FieldFactory.CreateField(paramType, serializedAnimationCurveValue.value, (newValue) =>
                    {
                        Undo.RegisterCompleteObjectUndo(graph, "Changed Parameter " + param.name + " to " + newValue);
                        serializedAnimationCurveValue.value = newValue as AnimationCurve;
                    }, param.name);
                    prop.Add(field);
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

                parameterContainer.Add(prop);
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