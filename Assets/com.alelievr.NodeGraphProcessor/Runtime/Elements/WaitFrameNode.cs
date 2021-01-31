using System;
using System.Collections;
using GraphProcessor;
using UnityEngine;

[Serializable, NodeMenuItem("Functions/Wait Frame")]
public class WaitFrameNode : WaitableNode
{
	public override string name => "Wait Frame";

	public override string headerClass => "wait-node";

	[SerializeField, Input(name = "Frame")]
	public int frame = 1;

	private static WaitFrameMonoBehaviour waitFrameMonoBehaviour;

	protected override void Process()
	{
		if(waitFrameMonoBehaviour == null)
		{
			var go  = new GameObject(name: "OKKU-WaitFrameGameObject");
			waitFrameMonoBehaviour = go.AddComponent<WaitFrameMonoBehaviour>();
		}

		waitFrameMonoBehaviour.Process(frame, ProcessFinished);
	}
}

public class WaitFrameMonoBehaviour : MonoBehaviour
{
	public void Process(int frame, Action callback)
	{
		StartCoroutine(_Process(frame, callback));
	}

	private IEnumerator _Process(int frame, Action callback)
	{
		for(int i = 0; i < frame; i++)
		{
			yield return new WaitForEndOfFrame();
			i++;
		}

		callback.Invoke();
	}
}