using UnityEngine;

public class NetworkedInput<T>
{
	public delegate bool HasChangedDelegate(T LastSentValue, T ClientValue);

	public delegate bool ShouldChangeDelegate(T LastSentValue, double lastReceivedTime, T ClientValue);

	public T ClientValue;

	public T ServerValue;

	public T LastSentValue;

	public double LastSentTime;

	public T LastReceivedValue;

	public double LastReceivedTime;

	private HasChangedDelegate HasChangedValidator;

	private ShouldChangeDelegate ShouldChangeValidator;

	public bool HasChanged => HasChangedValidator(LastSentValue, ClientValue);

	public bool ShouldChange => ShouldChangeValidator(LastReceivedValue, LastReceivedTime, ServerValue);

	public NetworkedInput(T initialValue = default(T), HasChangedDelegate hasChangedValidator = null, ShouldChangeDelegate shouldChangeValidator = null)
	{
		ClientValue = initialValue;
		LastSentValue = default(T);
		ServerValue = default(T);
		if (hasChangedValidator != null)
		{
			HasChangedValidator = hasChangedValidator;
		}
		else
		{
			HasChangedValidator = delegate
			{
				ref T clientValue2 = ref ClientValue;
				object obj = LastSentValue;
				return !clientValue2.Equals(obj);
			};
		}
		if (shouldChangeValidator != null)
		{
			ShouldChangeValidator = shouldChangeValidator;
			return;
		}
		ShouldChangeValidator = delegate
		{
			ref T serverValue2 = ref ServerValue;
			object obj = LastReceivedValue;
			return !serverValue2.Equals(obj);
		};
	}

	public void ClientTick()
	{
		LastSentValue = ClientValue;
		LastSentTime = Time.timeAsDouble;
	}

	public void ServerTick()
	{
		LastReceivedValue = ServerValue;
		LastReceivedTime = Time.timeAsDouble;
	}
}
