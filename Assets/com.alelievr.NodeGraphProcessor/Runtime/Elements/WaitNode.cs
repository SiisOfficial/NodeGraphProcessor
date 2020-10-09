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
	public float waitTime;

	protected override void Process()
	{
		var go  = new GameObject(name:"OKKU-WaitGameObject");
		var wmb = go.AddComponent<WaitableMonoBehaviour>();

		wmb.Process(waitTime, SetIsFinished);
	}

	protected override IEnumerator AsyncProcess()
	{
		yield return new WaitForSeconds(waitTime);

		isFinished = true;
	}

	private void SetIsFinished()
	{
		isFinished = true;
		onProcessFinished.Invoke(this);
	}
}

public class WaitableMonoBehaviour : MonoBehaviour
{
	public void Process(float time, Action callback)
	{
		StartCoroutine(_Process(time, callback));
	}

	private IEnumerator _Process(float time, Action callback)
	{
		yield return new WaitForSeconds(time);
		callback.Invoke();
		yield return new WaitForEndOfFrame();

		Destroy(gameObject);
	}
}