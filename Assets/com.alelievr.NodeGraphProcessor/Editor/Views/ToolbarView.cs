using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor;
using System.Linq;
using System;

using Status = UnityEngine.UIElements.DropdownMenuAction.Status;

namespace GraphProcessor
{
	public class ToolbarView : VisualElement
	{
		protected class ToolbarButtonData
		{
			public string			name;
			public bool				toggle;
			public bool				value;
			public Action			buttonCallback;
			public Action< bool >	toggleCallback;
		}

		List< ToolbarButtonData >	leftButtonDatas = new List< ToolbarButtonData >();
		List< ToolbarButtonData >	rightButtonDatas = new List< ToolbarButtonData >();
		protected BaseGraphView		graphView;

		ToolbarButtonData showProcessOrder;
		ToolbarButtonData showParameters;

		public ToolbarView(BaseGraphView graphView)
		{
			name = "ToolbarView";
			this.graphView = graphView;

			graphView.initialized += AddButtons;
			
			graphView.initialized += () => {
				leftButtonDatas.Clear();
				rightButtonDatas.Clear();
				AddButtons();
			};

			/*
			 TODO: UIElements Toolbar. This is an example
			 var tln = new Toolbar();
			var asdf = new ToolbarButton(
				() => Debug.Log("heey?")
			);
			asdf.text = "heeeey";
			tln.Add(asdf);
			tln.pickingMode = PickingMode.Position;
			graphView.editorWindow.rootVisualElement.Add(tln);
			tln.BringToFront();*/

			graphView.editorWindow.rootVisualElement.Add(new IMGUIContainer(DrawImGUIToolbar));
		}

		protected ToolbarButtonData AddButton(string name, Action callback, bool left = true)
		{
			var data = new ToolbarButtonData{
				name = name,
				toggle = false,
				buttonCallback = callback
			};
			((left) ? leftButtonDatas : rightButtonDatas).Add(data);
			return data;
		}

		protected ToolbarButtonData AddToggle(string name, bool defaultValue, Action< bool > callback, bool left = true)
		{
			var data = new ToolbarButtonData{
				name = name,
				toggle = true,
				value = defaultValue,
				toggleCallback = callback
			};
			((left) ? leftButtonDatas : rightButtonDatas).Add(data);
			return data;
		}

		/// <summary>
		/// Also works for toggles
		/// </summary>
		/// <param name="name"></param>
		/// <param name="left"></param>
		protected void RemoveButton(string name, bool left)
		{
			((left) ? leftButtonDatas : rightButtonDatas).RemoveAll(b => b.name == name);
		}

		protected virtual void AddButtons()
		{
			AddButton("Center", graphView.ResetPositionAndZoom);

			bool processOrderVisible = graphView.GetPinnedElementStatus< ProcessOrderView >() != Status.Hidden;
			showProcessOrder = AddToggle("Show Process Order", processOrderVisible, (v) => graphView.ToggleView< ProcessOrderView>());
			
			bool exposedParamsVisible = graphView.GetPinnedElementStatus< ExposedParameterView >() != Status.Hidden;
			showParameters = AddToggle("Show Parameters", exposedParamsVisible, (v) => graphView.ToggleView< ExposedParameterView>());

			AddButton("Show In Project", () => EditorGUIUtility.PingObject(graphView.graph), false);
		}
		
		public virtual void UpdateButtonStatus()
		{
			if (showProcessOrder != null)
				showProcessOrder.value = graphView.GetPinnedElementStatus< ProcessOrderView >() != Status.Hidden;
			if (showParameters != null)
				showParameters.value = graphView.GetPinnedElementStatus< ExposedParameterView >() != Status.Hidden;
		}

		void DrawImGUIButtonList(List< ToolbarButtonData > buttons)
		{
			foreach (var button in buttons.ToList())
			{
				if (button.toggle)
				{
					EditorGUI.BeginChangeCheck();
					button.value = GUILayout.Toggle(button.value, button.name, EditorStyles.toolbarButton);
					if (EditorGUI.EndChangeCheck() && button.toggleCallback != null)
						button.toggleCallback(button.value);
				}
				else
				{
					if (GUILayout.Button(button.name, EditorStyles.toolbarButton) && button.buttonCallback != null)
						button.buttonCallback();
				}
			}
		}

		void DrawImGUIToolbar()
		{
			GUILayout.BeginHorizontal(EditorStyles.toolbar);

			DrawImGUIButtonList(leftButtonDatas);

			GUILayout.FlexibleSpace();

			DrawImGUIButtonList(rightButtonDatas);

			GUILayout.EndHorizontal();
		}
	}
}
