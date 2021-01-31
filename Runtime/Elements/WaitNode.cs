using System;
using System.Collections;
using GraphProcessor;
using UnityEngine;

[Serializable, NodeMenuItem("Functions/Wait")]
public class WaitNode : WaitableNode
{
	public override string name => "Wait";

	public override string headerClass => "wait-node";

	[SerializeField, Input(name = "Time")]
	public float waitTime = 1f;
	
	private static WaitMonoBehaviour waitMonoBehaviour;

	protected override void Process()
	{
		if(waitMonoBehaviour == null)
		{
			var go  = new GameObject(name: "OKKU-WaitGameObject");
			waitMonoBehaviour = go.AddComponent<WaitMonoBehaviour>();
		}

		waitMonoBehaviour.Process(waitTime, ProcessFinished);
	}
}

public class WaitMonoBehaviour : MonoBehaviour
{
	public void Process(float time, Action callback)
	{
		StartCoroutine(_Process(time, callback));
	}

	private IEnumerator _Process(float time, Action callback)
	{
		yield return new WaitForSeconds(time);
		callback.Invoke();
	}
}