using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MyPuckMod;

public class PuckAttackBehaviour : MonoBehaviour
{
	private AssetBundle bundle;

	private Action<Dictionary<string, object>> onSceneLoadedAction;

	private Vector3 savedPosition;

	private Quaternion savedRotation;

	private GameObject zigzagPrefab;

	private GameObject gridReferencePrefab;

	private GameObject preset1Prefab;

	private GameObject preset1CPrefab;

	private GameObject targetPrefab;

	private GameObject cgatePrefab;

	private GameObject ncgatePrefab;

	private GameObject platformPrefab;

	private GameObject platformNCPrefab;

	private GameObject platformsPrefab;

	private GameObject platformTPrefab;

	private GameObject sunLightObject;

	private float warmupTimer = 0f;

	private Rigidbody puckBody;

	private Rigidbody playerBody;

	private bool scrollChatNextFrame = false;

	private bool initialized;

	private bool clampEnabled = true;

	private GameObject currentTarget;

	public static List<GameObject> spawnedObjects = new List<GameObject>();

	private Action<Dictionary<string, object>> onChatCommandAction;

	private string commandBuffer = "";

	private bool typingCommand = false;

	private bool sentWarmupMessage = false;

	private bool hasSavedSpawn = false;

	private bool timerRunning = true;

	private Vector3 originalSpawnPosition;

	private Quaternion originalSpawnRotation;

	private bool hasCapturedOriginalSpawn = false;

	private bool IsWarmupByPuckCount()
	{
		Rigidbody[] array = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
		int num = 0;
		Rigidbody[] array2 = array;
		foreach (Rigidbody rigidbody in array2)
		{
			if (rigidbody.gameObject.name.ToLower().Contains("puck"))
			{
				num++;
			}
		}
		return num > 1;
	}

	private bool IsAtLeastOnePuck()
	{
		Rigidbody[] array = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
		int num = 0;
		Rigidbody[] array2 = array;
		foreach (Rigidbody rigidbody in array2)
		{
			if (rigidbody.gameObject.name.ToLower().Contains("puck"))
			{
				num++;
			}
		}
		return num >= 1;
	}

	private Vector3 ClampToRink(Vector3 pos)
	{
		if (!clampEnabled)
		{
			return pos;
		}
		float min = -25f;
		float max = 25f;
		float min2 = -50f;
		float max2 = 50f;
		pos.x = Mathf.Clamp(pos.x, min, max);
		pos.z = Mathf.Clamp(pos.z, min2, max2);
		return pos;
	}

	private void OnApplicationQuit()
	{
		if (playerBody != null && hasCapturedOriginalSpawn)
		{
			clampEnabled = true;
			ReturnToSpawn();
			Debug.Log("/return executed automatically on quit/disconnect");
		}
	}

	private void Awake()
	{
		Debug.Log("PuckAttackBehaviour started");
		LoadBundle();
	}

	private void OnSceneLoaded(Dictionary<string, object> message)
	{
		if (message.ContainsKey("scene") && ((Scene)message["scene"]).buildIndex == 1)
		{
			RemoveSunlight();
			clampEnabled = true;
			Scoreboard scoreboard = UnityEngine.Object.FindFirstObjectByType<Scoreboard>();
			if (scoreboard != null)
			{
				scoreboard.TurnOn();
				Debug.Log("Scoreboard turned back on");
			}
		}
	}

	private void RemoveSunlight()
	{
		if (sunLightObject != null)
		{
			UnityEngine.Object.Destroy(sunLightObject);
			sunLightObject = null;
			Debug.Log("Sunlight removed for Changing Room scene");
		}
	}

	private void OnEnable()
	{
		onSceneLoadedAction = OnSceneLoaded;
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnSceneLoaded", onSceneLoadedAction);
		onChatCommandAction = OnChatCommand;
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnChatCommand", onChatCommandAction);
	}

	private void OnDisable()
	{
		if (onSceneLoadedAction != null)
		{
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnSceneLoaded", onSceneLoadedAction);
			onSceneLoadedAction = null;
		}
		if (onChatCommandAction != null)
		{
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnChatCommand", onChatCommandAction);
			onChatCommandAction = null;
		}
	}

	private void OnSceneUnloaded(Scene scene)
	{
		if (playerBody != null && hasCapturedOriginalSpawn)
		{
			clampEnabled = true;
			ReturnToSpawn();
			Debug.Log("/return executed automatically on scene unload: " + scene.name);
		}
	}

	private IEnumerator SpawnPuckDelayed()
	{
		yield return new WaitForSeconds(0.2f);
		SpawnPuckInFront();
	}

	private void OnChatCommand(Dictionary<string, object> message)
	{
		string text = (string)message["command"];
		ulong num = (ulong)message["clientId"];
		Debug.Log("Chat command received: " + text);
		if (text == "/spawnzigzag" || text == "/szz")
		{
			SpawnZigzag();
		}
		if (text == "/p1")
		{
			SpawnPreset1();
			SpawnPreset1C();
		}
		if (text == "/gr")
		{
			SpawnGridReference();
		}
		if (text == "/rg")
		{
			SpawnRandomGates();
		}
		if (text == "/randomgates")
		{
			SpawnRandomGates();
		}
		if (text == "/targethard")
		{
			SpawnTarget(1f);
		}
		if (text == "/target")
		{
			SpawnTarget(1.5f);
		}
		if (text == "/targeteasy")
		{
			SpawnTarget(2f);
		}
		if (text == "/clearobjects" || text == "/co")
		{
			ClearObjects();
			Debug.Log("ClearObjects command executed");
		}
		if (text == "/openworld")
		{
			clampEnabled = false;
			ClearObjects();
			SetMinimapEnabled(enabled: false);
			StartCoroutine(SendCourseHintDelayed());
			if (sunLightObject != null)
			{
				UnityEngine.Object.Destroy(sunLightObject);
				sunLightObject = null;
			}
			SpawnOpenWorld();
		}
		if (text == "/return")
		{
			clampEnabled = true;
			SetMinimapEnabled(enabled: true);
			ReturnToSpawn();
		}
		if (text == "/modhelp")
		{
			string message2 = "/clearobjects or /co - Clear all Objects\r\n/randomgates or /rg - Spawn 100 Gates\r\n/targeteasy - Spawn large target\r\n/target - Spawn medium target\r\n/targethard - Spawn small target\r\n/p1 - Spawn preset 1 (obstacle course)\r\n/spawnzigzag or /szz - Spawn zigzag\r\nMiddle Mouse - Spawn Puck In Front\r\nG - Ground Pass Front\r\nF - Ground Pass Back\r\nC - High Pass Front\r\nX - High Pass Back\r\nR - Random Hit\r\nZ - Lift Puck\r\nCTRL+1 - Slow Motion 50%\r\nCTRL+2 - Slow Motion 75%\r\nCTRL+3 - Normal Speed\r\nCTRL+Tab - Set Spawn Point\r\nCTRL+CapsLock - Teleport to Spawn Point\r\n\ud83d\udd34/openworld - go to open world\ud83d\udd34\r\n/return - return from open world\r\n!!Scroll Up In Chat To See Full Message!!";
			NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage(message2);
			Invoke("ScrollChatToBottom", 0.01f);
			Debug.Log("Mod help scheduled to scroll");
		}
		if (scrollChatNextFrame)
		{
			typeof(UIChat).GetMethod("ScrollToBottom", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(NetworkBehaviourSingleton<UIChat>.Instance, null);
			scrollChatNextFrame = false;
		}
		typingCommand = false;
		commandBuffer = "";
		Debug.Log("Preset1Prefab loaded: " + (preset1Prefab != null));
		Debug.Log("Preset1CPrefab loaded: " + (preset1CPrefab != null));
	}

	private void Update()
	{
		if (Keyboard.current == null)
		{
			return;
		}
		if (!hasCapturedOriginalSpawn)
		{
			InitializeIfNeeded();
			if (initialized && playerBody != null)
			{
				originalSpawnPosition = playerBody.position;
				originalSpawnRotation = playerBody.rotation;
				hasCapturedOriginalSpawn = true;
				Debug.Log("Original spawn captured");
			}
		}
		if (IsWarmupByPuckCount() && timerRunning)
		{
			warmupTimer += Time.deltaTime;
			if (!sentWarmupMessage && warmupTimer >= 5f)
			{
				NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage("\ud83d\udd34FROM r_607`s Puck Training Mod\ud83d\udd34");
				NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage("Type /modhelp for commands and hotkeys");
				NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage("Type /openworld for Open World");
				NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage("WARNING /rg and /randomgates dont work as expected");
				Invoke("ScrollChatToBottom", 0.01f);
				sentWarmupMessage = true;
				Debug.Log("Warmup help message sent after 5 seconds");
			}
		}
		else if (timerRunning)
		{
			warmupTimer = 0f;
			sentWarmupMessage = false;
		}
		bool flag = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
		if (flag && Keyboard.current.tabKey.wasPressedThisFrame)
		{
			InitializeIfNeeded();
			if (!initialized)
			{
				return;
			}
			savedPosition = playerBody.position;
			savedRotation = playerBody.rotation;
			hasSavedSpawn = true;
			NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage("Spawn Point Set");
			Invoke("ScrollChatToBottom", 0.01f);
			Debug.Log("Spawn point saved");
		}
		if (flag && Keyboard.current.capsLockKey.wasPressedThisFrame)
		{
			if (!hasSavedSpawn)
			{
				Debug.Log("No spawn point set!");
				return;
			}
			InitializeIfNeeded();
			if (!initialized)
			{
				return;
			}
			playerBody.position = savedPosition + Vector3.up * 1f;
			float y = savedRotation.eulerAngles.y;
			playerBody.rotation = Quaternion.Euler(0f, y, 0f);
			playerBody.linearVelocity = Vector3.zero;
			playerBody.angularVelocity = Vector3.zero;
			StartCoroutine(SpawnPuckDelayed());
			Debug.Log("Teleported to spawn point");
		}
		if (Keyboard.current.gKey.wasPressedThisFrame)
		{
			FirePuck(spawnInFront: true);
		}
		if (Keyboard.current.fKey.wasPressedThisFrame)
		{
			FirePuck(spawnInFront: false);
		}
		if (Keyboard.current.cKey.wasPressedThisFrame)
		{
			AirPass(fromFront: true, 180f, 0.65f);
		}
		if (Keyboard.current.xKey.wasPressedThisFrame)
		{
			AirPass(fromFront: false, 100f, 1f);
		}
		if (Mouse.current.middleButton.wasPressedThisFrame)
		{
			SpawnPuckInFront();
		}
		if (Keyboard.current.zKey.wasPressedThisFrame)
		{
			LiftPuck();
			StartCoroutine(RandomizeRotationDelayed());
		}
		if (Keyboard.current.rKey.wasPressedThisFrame)
		{
			RandomHitPuck();
		}
		if (flag && Keyboard.current.digit1Key.wasPressedThisFrame)
		{
			Time.timeScale = 0.5f;
			Time.fixedDeltaTime = 0.02f * Time.timeScale;
		}
		if (flag && Keyboard.current.digit2Key.wasPressedThisFrame)
		{
			Time.timeScale = 0.75f;
			Time.fixedDeltaTime = 0.02f * Time.timeScale;
		}
		if (flag && Keyboard.current.digit3Key.wasPressedThisFrame)
		{
			Time.timeScale = 1f;
			Time.fixedDeltaTime = 0.02f;
		}
	}

	private void ScrollChatToBottom()
	{
		typeof(UIChat).GetMethod("ScrollToBottom", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(NetworkBehaviourSingleton<UIChat>.Instance, null);
	}

	private void InitializeIfNeeded()
	{
		if (puckBody == null || playerBody == null)
		{
			initialized = false;
		}
		if (initialized)
		{
			return;
		}
		Rigidbody[] array = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
		Rigidbody[] array2 = array;
		foreach (Rigidbody rigidbody in array2)
		{
			string text = rigidbody.gameObject.name.ToLower();
			if (text.Contains("puck"))
			{
				puckBody = rigidbody;
			}
			if (text.Contains("player") && !text.Contains("goalie"))
			{
				playerBody = rigidbody;
			}
		}
		if (puckBody != null && playerBody != null)
		{
			initialized = true;
		}
		if (puckBody != null)
		{
			Collider component = puckBody.GetComponent<Collider>();
			if (component != null && component.material == null)
			{
				PhysicsMaterial physicsMaterial = new PhysicsMaterial();
				physicsMaterial.bounciness = 1f;
				physicsMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
				physicsMaterial.dynamicFriction = 0f;
				physicsMaterial.staticFriction = 0f;
				physicsMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
				component.material = physicsMaterial;
			}
		}
	}

	private void FirePuck(bool spawnInFront)
	{
		InitializeIfNeeded();
		if (initialized)
		{
			Vector3 forward = playerBody.transform.forward;
			forward.y = 0f;
			forward.Normalize();
			float num = UnityEngine.Random.Range(-90f, 90f);
			if (!spawnInFront)
			{
				num += 180f;
			}
			Quaternion quaternion = Quaternion.Euler(0f, num, 0f);
			Vector3 vector = quaternion * forward;
			float num2 = 10f;
			Vector3 pos = playerBody.position + vector * num2;
			pos = ClampToRink(pos);
			puckBody.position = pos;
			float magnitude = playerBody.linearVelocity.magnitude;
			float value = 0.8f + magnitude * 0.22f;
			value = Mathf.Clamp(value, 0.65f, 1.1f);
			float num3 = Vector3.Dot(playerBody.linearVelocity, playerBody.transform.forward);
			Vector3 vector2 = playerBody.transform.forward * num3;
			Vector3 vector3 = vector2 * value;
			float min = 2.5f;
			float max = 6.5f;
			float num4 = Mathf.Clamp(vector3.magnitude, min, max);
			vector3 = playerBody.transform.forward * num4;
			Vector3 vector4 = playerBody.position + vector3;
			Vector3 normalized = (vector4 - pos).normalized;
			normalized.y = 0f;
			puckBody.linearVelocity = Vector3.zero;
			puckBody.angularVelocity = Vector3.zero;
			float num5 = UnityEngine.Random.Range(5f, 7.5f);
			puckBody.AddForce(normalized * num5, ForceMode.Impulse);
		}
	}

	private void AirPass(bool fromFront, float toleranceDegrees, float forceMultiplier)
	{
		InitializeIfNeeded();
		if (initialized)
		{
			Vector3 forward = playerBody.transform.forward;
			forward.y = 0f;
			forward.Normalize();
			float num = toleranceDegrees / 2f;
			float num2 = UnityEngine.Random.Range(0f - num, num);
			num2 = (fromFront ? (num2 + UnityEngine.Random.Range(-20f, 20f)) : (num2 + 180f));
			Quaternion quaternion = Quaternion.Euler(0f, num2, 0f);
			Vector3 vector = quaternion * forward;
			float num3 = UnityEngine.Random.Range(4f, 8f);
			float magnitude = playerBody.linearVelocity.magnitude;
			float a;
			if (fromFront)
			{
				a = UnityEngine.Random.Range(2f, 4f);
				float t = Mathf.InverseLerp(4f, 8f, num3);
				float b = Mathf.Lerp(2f, 1f, t);
				a = Mathf.Max(a, b);
			}
			else
			{
				a = UnityEngine.Random.Range(3f, 4f);
			}
			if (!fromFront)
			{
				num3 += magnitude * 0.3f;
			}
			num3 *= forceMultiplier;
			float num4 = 10f;
			if (fromFront)
			{
				float num5 = Mathf.InverseLerp(4f, 8f, num3);
				float num6 = Mathf.InverseLerp(1f, 3f, a);
				float t2 = (num5 + num6) * 0.2f;
				num4 = Mathf.Lerp(1f, 16f, t2);
			}
			Vector3 vector2 = ((!fromFront) ? (-forward * num4) : (forward * num4));
			Vector3 normalized = Vector3.ProjectOnPlane(vector, forward).normalized;
			Vector3 vector3 = normalized * 10f;
			Vector3 pos = playerBody.position + vector2 + vector3;
			pos = ClampToRink(pos);
			if (fromFront)
			{
				float num7 = magnitude * 0.75f;
				pos += forward * num7;
			}
			pos.y = 0.2f;
			pos = ClampToRink(pos);
			puckBody.position = pos;
			float value = 0.8f + magnitude * 0.22f;
			value = Mathf.Clamp(value, 0.65f, 1.1f);
			float num8 = Vector3.Dot(playerBody.linearVelocity, forward);
			Vector3 vector4 = forward * num8;
			Vector3 vector5 = vector4 * value;
			float min = 1f;
			float max = 6f;
			float num9 = Mathf.Clamp(vector5.magnitude, min, max);
			vector5 = forward * num9;
			Vector3 vector6 = playerBody.position + vector5;
			float num10 = 1.75f + playerBody.linearVelocity.magnitude * 0.25f;
			vector6 += forward * num10;
			Vector3 vector7 = vector6;
			vector7.y = pos.y;
			Vector3 normalized2 = (vector7 - pos).normalized;
			puckBody.linearVelocity = Vector3.zero;
			puckBody.angularVelocity = Vector3.zero;
			Vector3 force = normalized2 * num3 + Vector3.up * a;
			puckBody.rotation = UnityEngine.Random.rotation;
			Vector3 angularVelocity = new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-2f, 2f));
			puckBody.angularVelocity = angularVelocity;
			puckBody.AddForce(force, ForceMode.Impulse);
		}
	}

	private void LoadBundle()
	{
		string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string text = Path.Combine(directoryName, "Windows", "puckobjects");
		string text2 = Path.Combine(directoryName, "Linux", "puckobjects");
		string text3 = Path.Combine(directoryName, "puckobjects");
		Debug.Log("modPath: " + directoryName);
		Debug.Log("Windows path: " + text + " | Exists: " + File.Exists(text));
		Debug.Log("Linux path: " + text2 + " | Exists: " + File.Exists(text2));
		Debug.Log("Root path: " + text3 + " | Exists: " + File.Exists(text3));
		if (File.Exists(text))
		{
			bundle = AssetBundle.LoadFromFile(text);
			Debug.Log("Loaded Windows bundle");
		}
		else if (File.Exists(text2))
		{
			bundle = AssetBundle.LoadFromFile(text2);
			Debug.Log("Loaded Linux bundle");
		}
		else
		{
			if (!File.Exists(text3))
			{
				Debug.LogError("NO BUNDLE FOUND ANYWHERE");
				return;
			}
			bundle = AssetBundle.LoadFromFile(text3);
			Debug.Log("Loaded ROOT bundle");
		}
		if (bundle == null)
		{
			Debug.LogError("Bundle failed to load!");
			return;
		}
		Debug.Log("Bundle loaded successfully!");
		LoadAssetsFromBundle();
	}

	private void LoadAssetsFromBundle()
	{
		if (bundle == null)
		{
			Debug.LogError("Bundle is null in LoadAssetsFromBundle!");
			return;
		}
		string[] allAssetNames = bundle.GetAllAssetNames();
		foreach (string text in allAssetNames)
		{
			Debug.Log("FOUND ASSET: " + text);
			string text2 = text.ToLower();
			string text3 = Path.GetFileNameWithoutExtension(text).ToLower();
			if (text2.Contains("zigzag"))
			{
				zigzagPrefab = bundle.LoadAsset<GameObject>(text);
			}
			if (text2.Contains("gridrefrence"))
			{
				gridReferencePrefab = bundle.LoadAsset<GameObject>(text);
			}
			if (text3.Contains("target"))
			{
				targetPrefab = bundle.LoadAsset<GameObject>(text);
			}
			if (text3.Contains("cgate"))
			{
				cgatePrefab = bundle.LoadAsset<GameObject>(text);
			}
			if (text3.Contains("ncgate"))
			{
				ncgatePrefab = bundle.LoadAsset<GameObject>(text);
			}
			if (text3.Contains("ncpreset1"))
			{
				preset1Prefab = bundle.LoadAsset<GameObject>(text);
			}
			else if (text3.Contains("cpreset1"))
			{
				preset1CPrefab = bundle.LoadAsset<GameObject>(text);
			}
			if (text3.Contains("platformnc"))
			{
				platformNCPrefab = bundle.LoadAsset<GameObject>(text);
			}
			else if (text3.Contains("platforms"))
			{
				platformsPrefab = bundle.LoadAsset<GameObject>(text);
			}
			else if (text3.Contains("platformt"))
			{
				platformTPrefab = bundle.LoadAsset<GameObject>(text);
			}
			else if (text3.Contains("platform"))
			{
				platformPrefab = bundle.LoadAsset<GameObject>(text);
			}
		}
	}

	private void SpawnZigzag()
	{
		Debug.Log("SpawnZigzag called");
		if (!IsWarmupByPuckCount())
		{
			return;
		}
		if (zigzagPrefab == null)
		{
			Debug.LogError("zigzagPrefab is null!");
			return;
		}
		InitializeIfNeeded();
		if (initialized)
		{
			Vector3 position = playerBody.position;
			if (Physics.Raycast(playerBody.position, Vector3.down, out var hitInfo, 10f))
			{
				position = hitInfo.point;
			}
			position.y += 0.05f;
			float y = playerBody.transform.eulerAngles.y;
			Quaternion rotation = Quaternion.Euler(0f, y, 0f);
			GameObject gameObject = UnityEngine.Object.Instantiate(zigzagPrefab, position, rotation);
			spawnedObjects.Add(gameObject);
			Collider componentInChildren = gameObject.GetComponentInChildren<Collider>();
			if (componentInChildren != null)
			{
				float y2 = componentInChildren.bounds.min.y - gameObject.transform.position.y;
				gameObject.transform.position -= new Vector3(0f, y2, 0f);
			}
			Debug.Log("Zigzag spawned");
		}
	}

	private void SpawnGridReference()
	{
		Debug.Log("SpawnGridReference called");
		if (gridReferencePrefab == null)
		{
			Debug.LogError("gridReferencePrefab is null!");
			return;
		}
		InitializeIfNeeded();
		if (initialized)
		{
			Vector3 position = playerBody.position;
			if (Physics.Raycast(playerBody.position, Vector3.down, out var hitInfo, 10f))
			{
				position = hitInfo.point;
			}
			position.y += 0.05f;
			float y = playerBody.transform.eulerAngles.y;
			Quaternion rotation = Quaternion.Euler(0f, y, 0f);
			GameObject item = UnityEngine.Object.Instantiate(gridReferencePrefab, position, rotation);
			spawnedObjects.Add(item);
			Debug.Log("GridReference spawned");
		}
	}

	private void ClearObjects()
	{
		foreach (GameObject spawnedObject in spawnedObjects)
		{
			if (spawnedObject != null)
			{
				UnityEngine.Object.Destroy(spawnedObject);
			}
		}
		spawnedObjects.Clear();
		Debug.Log("All spawned objects cleared");
	}

	private void SpawnPreset1()
	{
		Debug.Log("SpawnPreset1 called");
		if (preset1Prefab == null)
		{
			Debug.LogError("ncPreset1 is null!");
			return;
		}
		Vector3 zero = Vector3.zero;
		zero.y = 0.05f;
		Quaternion rotation = Quaternion.Euler(0f, 0f, 0f);
		GameObject item = UnityEngine.Object.Instantiate(preset1Prefab, zero, rotation);
		spawnedObjects.Add(item);
		Debug.Log("ncPreset1 spawned at center");
	}

	private void SpawnPreset1C()
	{
		if (preset1CPrefab == null)
		{
			Debug.LogError("cPreset1 is null!");
			return;
		}
		Vector3 vector = new Vector3(0f, 5f, 0f);
		if (Physics.Raycast(vector, Vector3.down, out var hitInfo, 10f))
		{
			vector = hitInfo.point;
		}
		vector.y += 0.05f;
		GameObject gameObject = UnityEngine.Object.Instantiate(preset1CPrefab, vector, Quaternion.identity);
		spawnedObjects.Add(gameObject);
		AddMeshCollidersIfMissing(gameObject);
		Rigidbody component = gameObject.GetComponent<Rigidbody>();
		if (component == null)
		{
			component = gameObject.AddComponent<Rigidbody>();
			component.isKinematic = true;
			component.useGravity = false;
		}
		if (puckBody != null)
		{
			puckBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
		}
		Debug.Log("cPreset1 spawned with proper collisions");
	}

	private void AddMeshCollidersIfMissing(GameObject obj)
	{
		MeshFilter[] componentsInChildren = obj.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		MeshFilter[] array = componentsInChildren;
		foreach (MeshFilter meshFilter in array)
		{
			if (!(meshFilter.sharedMesh == null))
			{
				MeshCollider component = meshFilter.GetComponent<MeshCollider>();
				if (component == null)
				{
					MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
					meshCollider.sharedMesh = meshFilter.sharedMesh;
					meshCollider.convex = false;
					PhysicsMaterial physicsMaterial = new PhysicsMaterial();
					physicsMaterial.bounciness = 0.15f;
					physicsMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
					physicsMaterial.dynamicFriction = 0f;
					physicsMaterial.staticFriction = 0f;
					physicsMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
					meshCollider.material = physicsMaterial;
					meshFilter.gameObject.layer = LayerMask.NameToLayer("Ice");
				}
			}
		}
	}

	private IEnumerator EnableCollisionsNextFrame(GameObject obj)
	{
		yield return null;
		MeshCollider[] cols = obj.GetComponentsInChildren<MeshCollider>();
		MeshCollider[] array = cols;
		foreach (MeshCollider col in array)
		{
			col.enabled = true;
		}
	}

	private void SpawnTarget(float scale)
	{
		if (targetPrefab == null)
		{
			return;
		}
		InitializeIfNeeded();
		if (initialized)
		{
			Vector3 forward = playerBody.transform.forward;
			forward.y = 0f;
			forward.Normalize();
			float y = UnityEngine.Random.Range(-60f, 60f);
			float num = UnityEngine.Random.Range(10f, 25f);
			float magnitude = playerBody.linearVelocity.magnitude;
			float num2 = magnitude * 0.5f;
			float num3 = num + num2;
			Quaternion quaternion = Quaternion.Euler(0f, y, 0f);
			Vector3 vector = quaternion * forward;
			Vector3 vector2 = playerBody.position + vector * num3;
			if (Physics.Raycast(vector2 + Vector3.up * 5f, Vector3.down, out var hitInfo, 20f))
			{
				vector2.y = hitInfo.point.y + 0.05f;
			}
			else
			{
				vector2.y = 0.05f;
			}
			vector2 = ClampToRink(vector2);
			GameObject gameObject = UnityEngine.Object.Instantiate(targetPrefab, vector2, Quaternion.identity);
			gameObject.transform.localScale *= scale;
			spawnedObjects.Add(gameObject);
			AddMeshCollidersIfMissing(gameObject);
			Rigidbody component = gameObject.GetComponent<Rigidbody>();
			if (component == null)
			{
				component = gameObject.AddComponent<Rigidbody>();
				component.isKinematic = true;
				component.useGravity = false;
			}
			MovingTarget movingTarget = gameObject.AddComponent<MovingTarget>();
			movingTarget.playerBody = playerBody;
			movingTarget.mod = this;
			currentTarget = gameObject;
			Debug.Log("Target spawned inside court bounds");
		}
	}

	private void SpawnRandomGates()
	{
		if (cgatePrefab == null || ncgatePrefab == null)
		{
			Debug.LogError("Gate prefabs not loaded!");
			return;
		}
		InitializeIfNeeded();
		if (!initialized)
		{
			return;
		}
		float minInclusive = -20f;
		float maxInclusive = 20f;
		float minInclusive2 = -45f;
		float maxInclusive2 = 45f;
		int num = 100;
		float num2 = 3f;
		List<Vector3> list = new List<Vector3>();
		int num3 = 0;
		int num4 = 500;
		while (list.Count < num && num3 < num4)
		{
			num3++;
			float x = UnityEngine.Random.Range(minInclusive, maxInclusive);
			float z = UnityEngine.Random.Range(minInclusive2, maxInclusive2);
			Vector3 origin = new Vector3(x, playerBody.position.y + 5f, z);
			if (!Physics.Raycast(origin, Vector3.down, out var hitInfo, 20f))
			{
				continue;
			}
			origin = hitInfo.point + Vector3.up * 0.05f;
			bool flag = false;
			foreach (Vector3 item2 in list)
			{
				if (Vector3.Distance(item2, origin) < num2)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(origin);
				Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
				GameObject gameObject = UnityEngine.Object.Instantiate(cgatePrefab, origin, rotation);
				spawnedObjects.Add(gameObject);
				GameObject item = UnityEngine.Object.Instantiate(ncgatePrefab, origin, rotation);
				spawnedObjects.Add(item);
				AddMeshCollidersIfMissing(gameObject);
				Rigidbody component = gameObject.GetComponent<Rigidbody>();
				if (component == null)
				{
					component = gameObject.AddComponent<Rigidbody>();
					component.isKinematic = true;
					component.useGravity = false;
				}
			}
		}
		Debug.Log("Spawned " + list.Count + " random gates");
	}

	public void SpawnPuckInFront()
	{
		InitializeIfNeeded();
		if (initialized)
		{
			Vector3 forward = playerBody.transform.forward;
			forward.y = 0f;
			forward.Normalize();
			Vector3 vector = playerBody.position + forward * 2.1f;
			if (Physics.Raycast(vector + Vector3.up, Vector3.down, out var hitInfo, 10f))
			{
				vector = hitInfo.point;
			}
			vector.y += 0.2f;
			puckBody.position = vector;
			puckBody.linearVelocity = playerBody.linearVelocity + forward * 0.25f;
			puckBody.angularVelocity = Vector3.zero;
			Debug.Log("Puck spawned with player speed");
		}
	}

	private void LiftPuck()
	{
		InitializeIfNeeded();
		if (initialized)
		{
			float num = UnityEngine.Random.Range(1.5f, 3f);
			puckBody.AddForce(Vector3.up * num, ForceMode.Impulse);
		}
	}

	private IEnumerator RandomizeRotationDelayed()
	{
		yield return new WaitForSeconds(0.05f);
		if (!(puckBody == null))
		{
			puckBody.rotation = Quaternion.Euler(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-10f, 10f));
		}
	}

	private void RandomHitPuck()
	{
		InitializeIfNeeded();
		if (initialized)
		{
			Vector3 normalized = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(-1f, 1f)).normalized;
			float num = UnityEngine.Random.Range(0.6f, 2f);
			puckBody.AddForce(normalized * num, ForceMode.Impulse);
		}
	}

	private void SpawnOpenWorld()
	{
		StartCoroutine(ShowReturnMessageDelayed());
		if (platformPrefab == null || platformNCPrefab == null || platformsPrefab == null || platformTPrefab == null)
		{
			Debug.LogError("One or more platform prefabs are null!");
			return;
		}
		InitializeIfNeeded();
		if (!initialized)
		{
			return;
		}
		Vector3 zero = Vector3.zero;
		zero.y = 0.05f;
		Quaternion identity = Quaternion.identity;
		GameObject gameObject = UnityEngine.Object.Instantiate(platformPrefab, zero, identity);
		spawnedObjects.Add(gameObject);
		AddMeshCollidersIfMissing(gameObject);
		ApplyLighting(gameObject);
		GameObject gameObject2 = UnityEngine.Object.Instantiate(platformNCPrefab, zero, identity);
		spawnedObjects.Add(gameObject2);
		AddMeshCollidersIfMissing(gameObject2);
		ApplyLighting(gameObject2);
		GameObject gameObject3 = UnityEngine.Object.Instantiate(platformsPrefab, zero, identity);
		spawnedObjects.Add(gameObject3);
		AddMeshCollidersIfMissing(gameObject3);
		ApplyLighting(gameObject3);
		GameObject gameObject4 = UnityEngine.Object.Instantiate(platformTPrefab, zero, identity);
		spawnedObjects.Add(gameObject4);
		AddMeshCollidersIfMissing(gameObject4);
		SetupKinematic(gameObject4);
		ApplyLighting(gameObject4);
		if (gameObject3.GetComponentsInChildren<Collider>().Length == 0)
		{
			BoxCollider boxCollider = gameObject3.AddComponent<BoxCollider>();
			boxCollider.size = new Vector3(10f, 1f, 10f);
			Debug.Log("Fallback BoxCollider added to platforms");
		}
		SetupKinematic(gameObject3);
		SetupKinematic(gameObject);
		SetupKinematic(gameObject2);
		SetupKinematic(gameObject3);
		SetupKinematic(gameObject4);
		StartCoroutine(TeleportPlayerRightAboveCourse());
		Light[] array = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
		Light[] array2 = array;
		foreach (Light light in array2)
		{
			if (light.type == LightType.Directional && light.gameObject != sunLightObject)
			{
				light.enabled = false;
			}
		}
		SpawnSunLight();
		QualitySettings.realtimeReflectionProbes = true;
		QualitySettings.pixelLightCount = 4;
		Scoreboard scoreboard = UnityEngine.Object.FindFirstObjectByType<Scoreboard>();
		if (scoreboard != null)
		{
			scoreboard.TurnOff();
			Debug.Log("Scoreboard turned off for Open World");
		}
		Debug.Log("Course spawned with ALL FOUR prefabs");
	}

	private IEnumerator TeleportPlayerRightAboveCourse()
	{
		yield return new WaitForSeconds(0.2f);
		if (!(playerBody == null))
		{
			Vector3 newPos = Vector3.zero + Vector3.right * 200f + Vector3.up * 5f;
			playerBody.position = newPos;
			yield return new WaitForSeconds(0.4f);
			playerBody.rotation = Quaternion.Euler(0f, 180f, 0f);
			playerBody.linearVelocity = Vector3.zero;
			playerBody.angularVelocity = Vector3.zero;
			Debug.Log("Player teleported 200m right and 5m above ice");
		}
	}

	private IEnumerator ShowReturnMessageDelayed()
	{
		yield return new WaitForSeconds(5f);
		NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage("Type /return to go back to ice");
		Invoke("ScrollChatToBottom", 0.01f);
	}

	private IEnumerator SendCourseHintDelayed()
	{
		yield return new WaitForSeconds(30f);
		NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage("HINT: Use Spawn Points (CTRL+Tab to set spawn, CTRL+Caps to respawn)");
		Invoke("ScrollChatToBottom", 0.01f);
		Debug.Log("Course hint message sent after 30 seconds");
	}

	private void SetupKinematic(GameObject obj)
	{
		Rigidbody[] componentsInChildren = obj.GetComponentsInChildren<Rigidbody>();
		if (componentsInChildren.Length == 0)
		{
			Rigidbody rigidbody = obj.AddComponent<Rigidbody>();
			rigidbody.isKinematic = true;
			rigidbody.useGravity = false;
			return;
		}
		Rigidbody[] array = componentsInChildren;
		foreach (Rigidbody rigidbody2 in array)
		{
			rigidbody2.isKinematic = true;
			rigidbody2.useGravity = false;
		}
	}

	private void SpawnSunLight()
	{
		if (!(sunLightObject != null))
		{
			sunLightObject = new GameObject("ModSunLight");
			Light light = sunLightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1f;
			light.shadows = LightShadows.Soft;
			light.shadowStrength = 1f;
			light.shadowBias = 0.05f;
			light.color = new Color(1f, 0.7f, 0.3f);
			Vector3 position = Vector3.zero + Vector3.right * 200f + Vector3.up * 20f;
			sunLightObject.transform.position = position;
			sunLightObject.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
			UnityEngine.Object.DontDestroyOnLoad(sunLightObject);
			light.cullingMask = -1;
			Debug.Log("Sun light spawned above course");
		}
	}

	private void ApplyLighting(GameObject obj)
	{
		Renderer[] componentsInChildren = obj.GetComponentsInChildren<Renderer>(includeInactive: true);
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			Material[] materials = renderer.materials;
			foreach (Material material in materials)
			{
				if (!(material == null))
				{
					if (shader != null)
					{
						material.shader = shader;
					}
					if (material.HasProperty("_Surface"))
					{
						material.SetFloat("_Surface", 0f);
					}
					if (material.HasProperty("_EmissionColor"))
					{
						material.DisableKeyword("_EMISSION");
					}
				}
			}
			renderer.shadowCastingMode = ShadowCastingMode.On;
			renderer.receiveShadows = true;
			renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
			renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
		}
	}

	private void ReturnToSpawn()
	{
		InitializeIfNeeded();
		if (!initialized)
		{
			return;
		}
		if (!hasCapturedOriginalSpawn)
		{
			Debug.Log("Original spawn not captured yet!");
			return;
		}
		playerBody.position = originalSpawnPosition;
		playerBody.rotation = originalSpawnRotation;
		playerBody.linearVelocity = Vector3.zero;
		playerBody.angularVelocity = Vector3.zero;
		ClearObjects();
		if (sunLightObject != null)
		{
			UnityEngine.Object.Destroy(sunLightObject);
			sunLightObject = null;
		}
		Light[] array = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
		Light[] array2 = array;
		foreach (Light light in array2)
		{
			if (light.type == LightType.Directional)
			{
				light.enabled = true;
			}
		}
		Scoreboard scoreboard = UnityEngine.Object.FindFirstObjectByType<Scoreboard>();
		if (scoreboard != null)
		{
			scoreboard.TurnOn();
			Debug.Log("Scoreboard turned back on");
		}
		Debug.Log("Returned to ORIGINAL spawn, cleared objects, restored lighting");
	}

	private void SetMinimapEnabled(bool enabled)
	{
		UIMinimap uIMinimap = UnityEngine.Object.FindFirstObjectByType<UIMinimap>();
		if ((UnityEngine.Object)(object)uIMinimap == null)
		{
			Debug.Log("Minimap not found!");
		}
		else if (enabled)
		{
			uIMinimap.Show();
		}
		else
		{
			uIMinimap.Hide();
		}
	}
}
