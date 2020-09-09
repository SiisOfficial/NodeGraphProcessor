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

		public Toggle     hideInInspector        { get; private set; }
		public bool       canBeSlider            { get; private set; }
		public Toggle     showAsSlider           { get; private set; }
		public FloatField sliderMin              { get; private set; }
		public FloatField sliderMax              { get; private set; }
		public Label      thisIsDynamicParameter { get; private set; }

		public ExposedParameterPropertyView(BaseGraphView graphView, ExposedParameter param, string shortType) : base()
		{
			baseGraphView                              =  graphView;
			parameter                                  =  param;
			graphView.graph.onExposedParameterModified += UpdateSettingsVisibility;

			thisIsDynamicParameter                    = new Label("This is Dynamic");
			thisIsDynamicParameter.style.marginBottom = -5;
			Add(thisIsDynamicParameter);

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

			if(shortType != "Single" && shortType != "Int32")
			{
				UpdateSettingsVisibility(param.name);
				return;
			}

			thisIsDynamicParameter.style.marginBottom = -10;

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
			UpdateSettingsVisibility(param.name);
		}

		private void UpdateSettingsVisibility(string obj)
		{
			if(obj != parameter.name) return;
			parameter.settings.isHidden = parameter.name == "inputVector3" || parameter.name == "inputVector2" || parameter.name == "inputFloat" ||
										  parameter.name == "inputInteger";

			if(parameter.settings.isHidden)
			{
				thisIsDynamicParameter.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
				thisIsDynamicParameter.style.height  = new StyleLength(StyleKeyword.Auto);
				hideInInspector.value                = true;
				hideInInspector.visible              = false;
				hideInInspector.style.height         = 0;
				if(canBeSlider)
				{
					showAsSlider.value        = false;
					showAsSlider.visible      = false;
					showAsSlider.style.height = 0;
					UpdateSliderFields();
				}
			}
			else
			{
				thisIsDynamicParameter.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
				thisIsDynamicParameter.style.height  = 0;
				hideInInspector.visible              = true;
				hideInInspector.style.height         = new StyleLength(StyleKeyword.Auto);
				if(canBeSlider)
				{
					showAsSlider.visible      = true;
					showAsSlider.style.height = !hideInInspector.value ? new StyleLength(StyleKeyword.Auto) : 0;
					UpdateSliderFields();
				}
			}
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