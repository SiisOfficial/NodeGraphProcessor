using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GraphProcessor
{
	[System.Serializable]
	public class Group
	{
		public string			title;
		public Color			color = new Color(0, 0, 0, 0.3f);
		public Rect				position;
        public Vector2          size;

		public List< string >	innerNodeGUIDs = new List< string >();
		
		// For serialization loading
		public Group() {}

        public Group(string title, Vector2 position)
		{
			this.title = title;
            this.position.position = position;
		}

        public virtual void OnCreated()
        {
            size = new Vector2(400, 200);
            position.size = size;
        }
	}
}