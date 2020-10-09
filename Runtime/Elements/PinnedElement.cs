using UnityEngine.UIElements;
using UnityEngine;
using System;

namespace GraphProcessor
{
	[System.Serializable]
	public class PinnedElement
	{
		public static readonly Vector2	defaultSize = new Vector2(250, 400);

		public Rect				position = new Rect(new Vector2(5f, 5f), defaultSize);
		public bool				opened = true;
		public SerializableType	editorType;

		public PinnedElement(Type editorType)
		{
			this.editorType = new SerializableType(editorType);
		}
	}
}