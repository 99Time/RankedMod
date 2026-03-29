using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ThreadManager : MonoBehaviourSingleton<ThreadManager>
{
	private Queue<Action> executionQueue = new Queue<Action>();

	private void Update()
	{
		lock (executionQueue)
		{
			while (executionQueue.Count > 0)
			{
				executionQueue.Dequeue()();
			}
		}
	}

	public void Enqueue(IEnumerator action)
	{
		lock (executionQueue)
		{
			executionQueue.Enqueue(delegate
			{
				StartCoroutine(action);
			});
		}
	}

	public void Enqueue(Action action)
	{
		Enqueue(ActionWrapper(action));
	}

	public Task EnqueueAsync(Action action)
	{
		TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
		Enqueue(ActionWrapper(WrappedAction));
		return tcs.Task;
		void WrappedAction()
		{
			try
			{
				action();
				tcs.TrySetResult(result: true);
			}
			catch (Exception exception)
			{
				tcs.TrySetException(exception);
			}
		}
	}

	private IEnumerator ActionWrapper(Action a)
	{
		a();
		yield return null;
	}
}
