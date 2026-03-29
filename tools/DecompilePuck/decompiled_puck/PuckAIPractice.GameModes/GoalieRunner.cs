using UnityEngine;

namespace PuckAIPractice.GameModes;

public class GoalieRunner : MonoBehaviour
{
	public static GoalieRunner Instance { get; private set; }

	private void Awake()
	{
		if ((Object)(object)Instance == (Object)null)
		{
			Instance = this;
			Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
		}
		else
		{
			Object.Destroy((Object)(object)this);
		}
	}

	public static void Initialize()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		if ((Object)(object)Instance == (Object)null)
		{
			GameObject val = new GameObject("GoalieRunner");
			((Object)val).hideFlags = (HideFlags)61;
			val.AddComponent<GoalieRunner>();
		}
	}
}
