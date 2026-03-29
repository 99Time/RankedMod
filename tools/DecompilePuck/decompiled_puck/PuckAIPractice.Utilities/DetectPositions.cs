using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PuckAIPractice.GameModes;
using PuckAIPractice.Patches;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuckAIPractice.Utilities;

public class DetectPositions : MonoBehaviour
{
	private int frameCounter = 0;

	private GamePhase currentPhase = (GamePhase)1;

	private GamePhase lastPhase = (GamePhase)1;

	private void Update()
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Invalid comparison between Unknown and I4
		frameCounter++;
		if (frameCounter < 10 && 1 == 0)
		{
			return;
		}
		if ((!PracticeModeDetector.IsPracticeMode && !NetworkManager.Singleton.IsServer) || (int)NetworkBehaviourSingleton<GameManager>.Instance.Phase == 6)
		{
			if ((int)NetworkBehaviourSingleton<GameManager>.Instance.Phase == 6)
			{
				BotSpawning.DespawnBots(GoalieSession.Both);
			}
		}
		else
		{
			frameCounter = 0;
			lastPhase = currentPhase;
			BotSpawning.DetectOpenGoalAndSpawnBot();
		}
	}

	private void RunMyLogic()
	{
	}

	public static DetectPositions Create()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		GameObject val = new GameObject("DetectPositions");
		Object.DontDestroyOnLoad((Object)(object)val);
		return val.AddComponent<DetectPositions>();
	}

	public static void UpdateLabel(Player player)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		UIScoreboard instance = NetworkBehaviourSingleton<UIScoreboard>.Instance;
		if (!((Object)(object)instance == (Object)null) && !((Object)(object)player == (Object)null) && Traverse.Create((object)instance).Field("playerVisualElementMap").GetValue<Dictionary<Player, VisualElement>>()
			.TryGetValue(player, out var value))
		{
			Label val = UQueryBuilder<Label>.op_Implicit(UQueryExtensions.Query<Label>(value, "PositionLabel", (string)null));
			Label val2 = UQueryBuilder<Label>.op_Implicit(UQueryExtensions.Query<Label>(value, "Username", (string)null));
			if (FakePlayerRegistry.All.Contains(player))
			{
				((TextElement)val).text = "G";
				((TextElement)val2).text = string.Format("{0}<noparse>#{1} {2}</noparse>", "<b><color=#992d22>BOT</color></b>", player.Number.Value, player.Username.Value);
			}
		}
	}
}
