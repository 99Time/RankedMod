using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PuckAIPractice.AI;
using PuckAIPractice.GameModes;
using PuckAIPractice.Patches;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PuckAIPractice.Utilities;

public static class BotSpawning
{
	private static Vector3 redGoal = new Vector3(0f, 0f, -40.23f);

	private static Vector3 blueGoal = new Vector3(0f, 0f, 40.23f);

	private static Player redGoalie = null;

	private static Player blueGoalie = null;

	private static bool blueGoalieSpawned;

	private static bool redGoalieSpawned;

	private static int botIndex = 0;

	public static void SpawnChaser(PlayerTeam team, PlayerRole role)
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Invalid comparison between Unknown and I4
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		//IL_036e: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0393: Unknown result type (might be due to invalid IL or missing references)
		if (!PracticeModeDetector.IsPracticeMode && !NetworkManager.Singleton.IsServer)
		{
			return;
		}
		PlayerManager instance = NetworkBehaviourSingleton<PlayerManager>.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			Player value = Traverse.Create((object)instance).Field("playerPrefab").GetValue<Player>();
			if (!((Object)(object)value == (Object)null))
			{
				Player val = Object.Instantiate<Player>(value);
				NetworkObject component = ((Component)val).GetComponent<NetworkObject>();
				ulong num = 7777777uL + (ulong)(((int)team == 3) ? 1 : 0);
				botIndex++;
				component.SpawnWithOwnership(num, true);
				Player component2 = ((Component)val).GetComponent<Player>();
				component2.Username.Value = FixedString32Bytes.op_Implicit("demBot_Chaser");
				component2.Team.Value = team;
				component2.Number.Value = 7;
				component2.Role.Value = role;
				string randomJersey = RandomSkins.GetRandomJersey();
				string randomStickSkin = RandomSkins.GetRandomStickSkin(role);
				string randomShaftTape = RandomSkins.GetRandomShaftTape(role);
				string randomBladeTape = RandomSkins.GetRandomBladeTape(role);
				string randomMustache = RandomSkins.GetRandomMustache();
				string randomBeard = RandomSkins.GetRandomBeard();
				string randomCountry = RandomSkins.GetRandomCountry();
				string randomVisor = RandomSkins.GetRandomVisor();
				component2.JerseyGoalieRedSkin.Value = new FixedString32Bytes(randomJersey);
				component2.JerseyGoalieBlueSkin.Value = new FixedString32Bytes(randomJersey);
				component2.StickGoalieRedSkin.Value = new FixedString32Bytes(randomStickSkin);
				component2.StickGoalieBlueSkin.Value = new FixedString32Bytes(randomStickSkin);
				component2.StickBladeGoalieBlueTapeSkin.Value = new FixedString32Bytes(randomBladeTape);
				component2.StickBladeGoalieRedTapeSkin.Value = new FixedString32Bytes(randomBladeTape);
				component2.StickShaftGoalieBlueTapeSkin.Value = new FixedString32Bytes(randomShaftTape);
				component2.StickShaftGoalieRedTapeSkin.Value = new FixedString32Bytes(randomShaftTape);
				component2.Mustache.Value = new FixedString32Bytes(randomMustache);
				component2.Beard.Value = new FixedString32Bytes(randomBeard);
				component2.Country.Value = new FixedString32Bytes(randomCountry);
				component2.VisorAttackerBlueSkin.Value = new FixedString32Bytes(randomVisor);
				component2.VisorAttackerRedSkin.Value = new FixedString32Bytes(randomVisor);
				component2.VisorGoalieBlueSkin.Value = new FixedString32Bytes(randomVisor);
				component2.VisorGoalieRedSkin.Value = new FixedString32Bytes(randomVisor);
				PlayerPosition nextUnclaimedPosition = GetNextUnclaimedPosition(component2.Team.Value, component2.Role.Value);
				nextUnclaimedPosition.Server_Claim(component2);
				PlayerBodyV2 playerBody = component2.PlayerBody;
				PlayerMesh playerMesh = playerBody.PlayerMesh;
				StickMesh stickMesh = playerBody.Stick.StickMesh;
				playerMesh.SetJersey(component2.Team.Value, randomJersey);
				playerMesh.SetNumber(component2.Number.Value.ToString());
				playerMesh.SetUsername(((object)component2.Username.Value/*cast due to .constrained prefix*/).ToString());
				playerMesh.SetRole(component2.Role.Value);
				playerMesh.PlayerHead.SetMustache(RandomSkins.GetRandomMustache());
				playerMesh.PlayerHead.SetHelmetFlag(RandomSkins.GetRandomCountry());
				playerMesh.PlayerHead.SetHelmetVisor(RandomSkins.GetRandomVisor());
				playerMesh.PlayerHead.SetBeard(RandomSkins.GetRandomBeard());
				stickMesh.SetBladeTape(RandomSkins.GetRandomBladeTape(component2.Role.Value));
				stickMesh.SetSkin(component2.Team.Value, RandomSkins.GetRandomStickSkin(component2.Role.Value));
				stickMesh.SetShaftTape(RandomSkins.GetRandomShaftTape(component2.Role.Value));
				FakePlayerRegistry.Register(component2);
			}
		}
	}

	public unsafe static void SpawnFakePlayer(int index, PlayerRole role, PlayerTeam team)
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Invalid comparison between Unknown and I4
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Invalid comparison between Unknown and I4
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_0366: Invalid comparison between Unknown and I4
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0368: Unknown result type (might be due to invalid IL or missing references)
		//IL_0332: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0350: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_0374: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Unknown result type (might be due to invalid IL or missing references)
		//IL_0378: Invalid comparison between Unknown and I4
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_0388: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Invalid comparison between Unknown and I4
		//IL_03a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0412: Unknown result type (might be due to invalid IL or missing references)
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0468: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0502: Unknown result type (might be due to invalid IL or missing references)
		//IL_0537: Unknown result type (might be due to invalid IL or missing references)
		//IL_053c: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a0: Unknown result type (might be due to invalid IL or missing references)
		if (!PracticeModeDetector.IsPracticeMode && !NetworkManager.Singleton.IsServer)
		{
			return;
		}
		PlayerManager instance = NetworkBehaviourSingleton<PlayerManager>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Player value = Traverse.Create((object)instance).Field("playerPrefab").GetValue<Player>();
		if ((Object)(object)value == (Object)null)
		{
			return;
		}
		Player val = Object.Instantiate<Player>(value);
		NetworkObject component = ((Component)val).GetComponent<NetworkObject>();
		ulong num = 7777777uL + (ulong)(((int)team == 3) ? 1 : 0);
		botIndex++;
		component.SpawnWithOwnership(num, true);
		Player component2 = ((Component)val).GetComponent<Player>();
		component2.Username.Value = FixedString32Bytes.op_Implicit("demBot" + ((object)(*(PlayerTeam*)(&team))/*cast due to .constrained prefix*/).ToString() + "_" + (((int)team == 3) ? GoalieSettings.InstanceRed.Difficulty.ToString() : GoalieSettings.InstanceBlue.Difficulty.ToString()));
		component2.Team.Value = team;
		component2.Number.Value = 7;
		component2.Role.Value = role;
		string randomJersey = RandomSkins.GetRandomJersey();
		string randomStickSkin = RandomSkins.GetRandomStickSkin(role);
		string randomShaftTape = RandomSkins.GetRandomShaftTape(role);
		string randomBladeTape = RandomSkins.GetRandomBladeTape(role);
		string randomMustache = RandomSkins.GetRandomMustache();
		string randomBeard = RandomSkins.GetRandomBeard();
		string randomCountry = RandomSkins.GetRandomCountry();
		string randomVisor = RandomSkins.GetRandomVisor();
		component2.JerseyGoalieRedSkin.Value = new FixedString32Bytes(randomJersey);
		component2.JerseyGoalieBlueSkin.Value = new FixedString32Bytes(randomJersey);
		component2.StickGoalieRedSkin.Value = new FixedString32Bytes(randomStickSkin);
		component2.StickGoalieBlueSkin.Value = new FixedString32Bytes(randomStickSkin);
		component2.StickBladeGoalieBlueTapeSkin.Value = new FixedString32Bytes(randomBladeTape);
		component2.StickBladeGoalieRedTapeSkin.Value = new FixedString32Bytes(randomBladeTape);
		component2.StickShaftGoalieBlueTapeSkin.Value = new FixedString32Bytes(randomShaftTape);
		component2.StickShaftGoalieRedTapeSkin.Value = new FixedString32Bytes(randomShaftTape);
		component2.Mustache.Value = new FixedString32Bytes(randomMustache);
		component2.Beard.Value = new FixedString32Bytes(randomBeard);
		component2.Country.Value = new FixedString32Bytes(randomCountry);
		component2.VisorAttackerBlueSkin.Value = new FixedString32Bytes(randomVisor);
		component2.VisorAttackerRedSkin.Value = new FixedString32Bytes(randomVisor);
		component2.VisorGoalieBlueSkin.Value = new FixedString32Bytes(randomVisor);
		component2.VisorGoalieRedSkin.Value = new FixedString32Bytes(randomVisor);
		PlayerPosition nextUnclaimedPosition = GetNextUnclaimedPosition(component2.Team.Value, component2.Role.Value);
		if (NetworkManager.Singleton.IsServer)
		{
			if (val.IsCharacterPartiallySpawned)
			{
				val.Server_DespawnCharacter();
				val.Server_SpawnCharacter(new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 0f), role);
				return;
			}
			val.Server_SpawnCharacter(new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 0f), role);
		}
		Vector3 val2 = (((int)component2.Team.Value == 3) ? Vector3.forward : Vector3.back);
		Vector3 position = (((int)team == 3) ? redGoal : blueGoal) + val2 * (((int)team == 3) ? GoalieSettings.InstanceRed.DistanceFromNet : GoalieSettings.InstanceBlue.DistanceFromNet);
		position.y = ((Component)component2).transform.position.y;
		((Component)component2).transform.position = position;
		component2.PlayerBody.Rigidbody.position = position;
		PlayerBodyV2 playerBody = component2.PlayerBody;
		PlayerMesh playerMesh = playerBody.PlayerMesh;
		StickMesh stickMesh = playerBody.Stick.StickMesh;
		playerMesh.SetJersey(component2.Team.Value, randomJersey);
		playerMesh.SetNumber(component2.Number.Value.ToString());
		playerMesh.SetUsername(((object)component2.Username.Value/*cast due to .constrained prefix*/).ToString());
		playerMesh.SetRole(component2.Role.Value);
		playerMesh.PlayerHead.SetMustache(RandomSkins.GetRandomMustache());
		playerMesh.PlayerHead.SetHelmetFlag(RandomSkins.GetRandomCountry());
		playerMesh.PlayerHead.SetHelmetVisor(RandomSkins.GetRandomVisor());
		playerMesh.PlayerHead.SetBeard(RandomSkins.GetRandomBeard());
		stickMesh.SetBladeTape(RandomSkins.GetRandomBladeTape(component2.Role.Value));
		stickMesh.SetSkin(component2.Team.Value, RandomSkins.GetRandomStickSkin(component2.Role.Value));
		stickMesh.SetShaftTape(RandomSkins.GetRandomShaftTape(component2.Role.Value));
		GoalieAI goalieAI = ((Component)((NetworkBehaviour)component2).NetworkObject).gameObject.AddComponent<GoalieAI>();
		goalieAI.controlledPlayer = component2;
		goalieAI.team = component2.Team.Value;
		Puck playerPuck = NetworkBehaviourSingleton<PuckManager>.Instance.GetPlayerPuck(((NetworkBehaviour)NetworkBehaviourSingleton<PuckManager>.Instance).OwnerClientId);
		goalieAI.puckTransform = ((playerPuck != null) ? ((Component)playerPuck).transform : null);
		component2.PlayerBody.Rigidbody.isKinematic = true;
		FakePlayerRegistry.Register(component2);
		Goalies.GoaliesAreRunning = true;
		NetworkBehaviourSingleton<PlayerManager>.Instance.RemovePlayer(component2);
		NetworkBehaviourSingleton<UIScoreboard>.Instance.UpdateServer(NetworkBehaviourSingleton<ServerManager>.Instance.Server, NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false).Count);
	}

	private static PlayerPosition GetNextUnclaimedPosition(PlayerTeam team, PlayerRole? role = null)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		PlayerPositionManager instance = NetworkBehaviourSingleton<PlayerPositionManager>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		List<PlayerPosition> list = null;
		if ((int)team != 2)
		{
			if ((int)team != 3)
			{
				return null;
			}
			list = instance.RedPositions;
		}
		else
		{
			list = instance.BluePositions;
		}
		foreach (PlayerPosition item in list)
		{
			if (!item.IsClaimed && (!role.HasValue || item.Role == role.Value))
			{
				return item;
			}
		}
		return null;
	}

	public static void DespawnBots(GoalieSession type)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Invalid comparison between Unknown and I4
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Invalid comparison between Unknown and I4
		HashSet<Player> hashSet = FakePlayerRegistry.All.ToHashSet();
		if (hashSet == null)
		{
		}
		foreach (Player item in hashSet)
		{
			if (type == GoalieSession.Blue && (int)item.Team.Value == 2)
			{
				Despawn(item);
			}
			else if (type == GoalieSession.Red && (int)item.Team.Value == 3)
			{
				Despawn(item);
			}
			else if (type == GoalieSession.Both)
			{
				Despawn(item);
			}
		}
	}

	public static void Despawn(Player p)
	{
		p.Server_DespawnCharacter();
		((NetworkBehaviour)p).NetworkObject.Despawn(true);
		FakePlayerRegistry.Unregister(p);
		p.Team.Value = (PlayerTeam)0;
		NetworkBehaviourSingleton<PlayerManager>.Instance.RemovePlayer(p);
	}

	public static void DetectOpenGoalAndSpawnBot()
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Invalid comparison between Unknown and I4
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Invalid comparison between Unknown and I4
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Invalid comparison between Unknown and I4
		List<Player> players = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false);
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		Player p = null;
		Player p2 = null;
		List<Player> list = FakePlayerRegistry.All.ToList();
		foreach (Player item in list)
		{
			if ((int)item.Team.Value == 2)
			{
				flag4 = true;
				p = item;
			}
			else
			{
				flag3 = true;
				p2 = item;
			}
		}
		foreach (Player item2 in players)
		{
			if ((int)item2.Role.Value == 2 && ((NetworkBehaviour)item2).IsSpawned && (Object)(object)item2.PlayerBody != (Object)null)
			{
				if ((int)item2.Team.Value == 3)
				{
					flag2 = true;
				}
				else
				{
					flag = true;
				}
			}
		}
		if (flag)
		{
			if (flag4)
			{
				Despawn(p);
				blueGoalieSpawned = false;
			}
		}
		else if (!flag4)
		{
			SpawnFakePlayer(0, (PlayerRole)2, (PlayerTeam)2);
			blueGoalieSpawned = true;
		}
		if (flag2)
		{
			if (flag3)
			{
				Despawn(p2);
				redGoalieSpawned = false;
			}
		}
		else if (!flag3)
		{
			SpawnFakePlayer(1, (PlayerRole)2, (PlayerTeam)3);
			redGoalieSpawned = true;
		}
	}
}
