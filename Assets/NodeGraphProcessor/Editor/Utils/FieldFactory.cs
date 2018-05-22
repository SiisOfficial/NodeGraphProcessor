﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements;
using System;
using System.Linq;
using System.Reflection;

namespace GraphProcessor
{
	public static class FieldFactory
	{
		static readonly Dictionary< Type, Type >    fieldDrawers = new Dictionary< Type, Type >();

		static readonly MethodInfo	        		createFieldMethod = typeof(FieldFactory).GetMethod("CreateFieldSpecific", BindingFlags.Static | BindingFlags.Public);

		static FieldFactory()
		{
			foreach (var type in AppDomain.CurrentDomain.GetAllTypes())
			{
				var drawerAttribute = type.GetCustomAttributes(typeof(FieldDrawerAttribute), false).FirstOrDefault() as FieldDrawerAttribute;

				if (drawerAttribute == null)
					continue ;
				
				AddDrawer(drawerAttribute.fieldType, type);
			}

			// щ(ºДºщ) ...
            AddDrawer(typeof(int), typeof(IntegerField));
			#if UNITY_2018_2
            AddDrawer(typeof(long), typeof(LongField));
            AddDrawer(typeof(float), typeof(FloatField));
			#endif
			AddDrawer(typeof(double), typeof(DoubleField));
			AddDrawer(typeof(string), typeof(TextField));
			AddDrawer(typeof(Bounds), typeof(BoundsField));
			AddDrawer(typeof(Color), typeof(ColorField));
			AddDrawer(typeof(Vector2), typeof(Vector2Field));
			AddDrawer(typeof(Vector3), typeof(Vector3Field));
			AddDrawer(typeof(Vector4), typeof(Vector4Field));
			AddDrawer(typeof(AnimationCurve), typeof(CurveField));
			AddDrawer(typeof(Enum), typeof(EnumField));
			AddDrawer(typeof(Gradient), typeof(GradientField));
		}

		static void AddDrawer(Type fieldType, Type drawerType)
		{
			var iNotifyType = typeof(INotifyValueChanged<>).MakeGenericType(fieldType);

			if (!iNotifyType.IsAssignableFrom(drawerType))
			{
				Debug.LogWarning("The custom field drawer " + drawerType + " does not implements INotifyValueChanged< " + fieldType + " >");
				return ;
			}

			fieldDrawers[fieldType] = drawerType;
		}

		public static INotifyValueChanged< T > CreateField< T >()
		{
			return CreateField(typeof(T)) as INotifyValueChanged< T >;
		}

		public static VisualElement CreateField(Type t)
		{
			Type drawerType;

			fieldDrawers.TryGetValue(t, out drawerType);

			if (drawerType == null)
				drawerType = fieldDrawers.FirstOrDefault(kp => kp.Key.IsReallyAssignableFrom(t)).Value;

			if (drawerType == null)
				throw new ArgumentException("Can't find field drawer for type: " + t);

			var field = Activator.CreateInstance(drawerType);

			return field as VisualElement;
		}

		public static INotifyValueChanged< T > CreateFieldSpecific< T >(FieldInfo field, Action< object > onValueChanged)
		{
			var fieldDrawer = CreateField< T >();

			if (fieldDrawer == null)
				return null;

			fieldDrawer.OnValueChanged((e) => {
				onValueChanged(e.newValue);
			});

			return fieldDrawer as INotifyValueChanged< T >;
		}

		public static VisualElement CreateField(FieldInfo field, Action< object > onValueChanged)
		{
			var createFieldSpecificMethod = createFieldMethod.MakeGenericMethod(field.FieldType);

			return createFieldSpecificMethod.Invoke(null, new object[]{field, onValueChanged}) as VisualElement;
		}
	}
}