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
	public class ExposedParameterPropertyView : VisualElement
	{
		protected BaseGraphView baseGraphView;

		public ExposedParameter parameter { get; private set; }

		public Toggle     hideInInspector { get; private set; }
		public bool       canBeSlider     { get; private set; }
		public Toggle     showAsSlider    { get; private set; }
		public FloatField sliderMin       { get; private set; }
		public FloatField sliderMax       { get; private set; }

		public ExposedParameterPropertyView(BaseGraphView graphView, ExposedParameter param, string shortType) : base()
		{
			baseGraphView = graphView;
			parameter      = param;

			hideInInspector = new Toggle
			{
				text  = "Hide in Inspector",
				value = parameter.settings.isHidden
			};
			hideInInspector.RegisterValueChangedCallback(e =>
			{
				baseGraphView.graph.UpdateExposedParameterVisibility(parameter, e.newValue);
				UpdateSliderOption();
			});

			Add(hideInInspector);

			if(shortType != "Single" && shortType != "Int32") return;

			canBeSlider = true;

			showAsSlider = new Toggle
			{
				text  = "Show as Slider",
				value = parameter.settings.isSlider
			};
			showAsSlider.RegisterValueChangedCallback(e =>
			{
				baseGraphView.graph.UpdateExposedSliderVisibility(parameter, e.newValue);
				UpdateSliderFields();
			});

			sliderMin = new FloatField
			{
				label = "Min Value",
				value = parameter.settings.sliderMinValue
			};
			sliderMin.RegisterValueChangedCallback(e => baseGraphView.graph.UpdateExposedSliderMinValue(parameter, e.newValue));

			sliderMax = new FloatField
			{
				label = "Max Value",
				value = parameter.settings.sliderMaxValue
			};
			sliderMax.RegisterValueChangedCallback(e => baseGraphView.graph.UpdateExposedSliderMaxValue(parameter, e.newValue));

			Add(showAsSlider);
			Add(sliderMin);
			Add(sliderMax);
			UpdateSliderFields();
		}

		private void UpdateSliderFields()
		{
			if(!canBeSlider) return;
			sliderMin.visible      = showAsSlider.value;
			sliderMin.style.height = showAsSlider.value ? new StyleLength(StyleKeyword.Auto) : 0;
			sliderMax.visible      = showAsSlider.value;
			sliderMax.style.height = showAsSlider.value ? new StyleLength(StyleKeyword.Auto) : 0;
		}

		private void UpdateSliderOption()
		{
			if(!canBeSlider) return;
			showAsSlider.visible      = !hideInInspector.value;
			showAsSlider.style.height = !hideInInspector.value ? new StyleLength(StyleKeyword.Auto) : 0;
			if(hideInInspector.value) showAsSlider.value = false;
			UpdateSliderFields();
		}
	}
}