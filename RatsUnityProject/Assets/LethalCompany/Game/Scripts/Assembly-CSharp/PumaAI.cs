using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

public class PumaAI : EnemyAI
{
	public MultiAimConstraint lookRig;

	public bool lookingAtObjects = true;

	public Transform lookRigTarget;

	private Vector3 lookRigTargetPos;

	private Vector3 targetPlayerLookPos;

	public Transform butt;

	public Transform[] proceduralBodyTargets;

	public Transform[] IKTargetContainers;

	private float resetIKOffsetsInterval;

	private RaycastHit hit;

	public bool clingingToTree;

	public bool clingingToTreeAnim;

	public Transform animationContainer;

	public float bodyRotateOffsetX = 90f;

	public float headHeightFromGround = 2f;

	public static List<GameObject> AllTreeNodes;

	private int previousState = -1;

	private bool wasRunningForTree;

	public GameObject TargetTree;

	public Vector3 leapFromPosition;

	public Vector3 clingIdlePosition;

	public GameObject EndTargetTree;

	private bool wasOwnerLastFrame;

	[Header("Behavior")]
	public float treeHidingTimer;

	public PumaPlayerKnowledge[] playerKnowledge;

	public PumaPlayerKnowledge targetPlayerKnowledge;

	public float seenByPlayersTimer;

	private int playersSeeingPuma;

	private float timeSincePlayersSeeingPumaWhileClose;

	public float hearingMultiplier = 1.2f;

	public int focusLevel;

	private float timeSincePingingAttention;

	private float timeAtLastHeardNoise;

	private float pumaLOS = 100f;

	[Header("Climb tree logic")]
	public TreeState treeState;

	public Vector3 treeTopPosition;

	public Vector3 treeBottomPosition;

	private float startClimbingDelay;

	private Vector3 approachTreePosition;

	private float approachTreeRotation;

	private float treeClingRotation;

	public Collider[] treeColliders;

	private List<Collider> treeCollidersB = new List<Collider>(30);

	public List<PlayerControllerB> nearPlayers = new List<PlayerControllerB>();

	private float startLeapTimer;

	private float leapTimer;

	private float leapDistance;

	public float leapSpeed = 5f;

	private float updateDestinationIntervalB;

	private int playerLOSLayermask = 1073744129;

	private bool leapAnimationB;

	public AnimationCurve leapVerticalCurve;

	public AnimationCurve dropDownCurve;

	public AnimationCurve dropDownCurveRotation;

	public static List<GameObject> excludeTrees;

	private List<GameObject> excludeTreesTemporarily = new List<GameObject>();

	public static List<GameObject> stalkNodesInUse;

	private GameObject currentStalkNode;

	public GameObject lastTargetTree;

	private int excludeTreesTemporarilyTime;

	private Vector3 agentLocalVelocity;

	private float velX;

	private float velZ;

	private Vector3 previousPosition;

	private float syncVelocityInterval;

	public float animStalkSpeed = 1f;

	private float berserkModeTimer;

	private float pingAttentionTimer;

	private Vector3 pingAttentionPosition;

	[Header("Stalking Behavior")]
	public bool stalkingFrozen;

	private bool stalkingFrozenAnim;

	public Transform turnCompass;

	private float beginStalkFreezeTimer;

	private float seenByPlayersMeter;

	private bool hidingQuickly;

	private float stopStalkingTimer;

	private float hideQuicklyTimer;

	private int leapAttempts;

	private bool relocatingToNewTree;

	private bool firstLeap;

	private float startledTimer;

	private bool startled;

	private bool runAfterStartledTimer;

	private List<Transform> debugIncompatibleTrees = new List<Transform>();

	private List<Transform> debugIncompatibleTreesB = new List<Transform>();

	public ParticleSystem leafParticle;

	public AudioClip climbTreeSFX1;

	public AudioClip climbTreeSFX2;

	public AudioClip pumaLeapSFX1;

	public AudioClip pumaLeapSFX2;

	public AudioClip[] pumaGrowlSFX;

	public AudioSource growlSource;

	public AudioClip pumaRunSFX;

	public AudioClip[] pumaFootstepSFX;

	public AudioSource footstepSource;

	public AudioClip attackScream;

	public AudioClip dropSFX;

	public Mesh coneDebugMesh;

	[Header("Attack Behavior")]
	public float DangerRange = 7.5f;

	private bool frozenInDangerRange;

	private float stalkFrozenAttackTimer;

	private bool scratching;

	public AudioSource attackSFX;

	public AudioSource attackSFXFaraway;

	public float attackRunSpeed;

	public float attackAcceleration;

	private float timeAtLastScratch;

	public Transform pushPoint;

	public float scratchPushForce = 5f;

	public AudioClip[] scratchSFX;

	private bool startedScreamingInAttack;

	private bool stalkingCharge;

	private float scaredMeter;

	private float timeSpentAttacking;

	private float timeSinceGettingHit;

	private float goToShipTimer;

	private bool droppedFromTreeDead;

	private float clearDebugTextInterval;

	private Transform previousClosestNode;

	private Vector3 playerPosAtClosestNode;

	public float playerTreeLOSClose;

	public float playerTreeLOSDistanceClose;

	public float playerTreeLOSFar;

	public float playerTreeLOSDistanceFar;

	private bool disableAgentVelocity;

	private float timeAtLastDrop;

	private float angerMeter;

	private float targetNullCounter;

	private bool disqualifiedCurrentTreeFromDrop;

	private bool playedFearNoise;

	public List<GameObject> eliminatedHideTrees = new List<GameObject>();

	private float hidingQuicklySeenMeterIncrease;

	private float seenMeterLastFrame;

	private GameObject hidingTree;

	private bool sentTreeClimbRPC;

	private void AddProceduralOffsetToLimbsOverTerrain()
	{
		bool flag = stunNormalizedTimer < 0f && !clingingToTreeAnim && ventAnimationFinished;
		if (!isEnemyDead)
		{
			if ((resetIKOffsetsInterval < 0f || !flag) && !stalkingFrozenAnim)
			{
				resetIKOffsetsInterval = 8f;
				for (int i = 0; i < proceduralBodyTargets.Length; i++)
				{
					proceduralBodyTargets[i].localPosition = Vector3.zero;
				}
				if (!flag)
				{
					animationContainer.rotation = Quaternion.Lerp(animationContainer.rotation, base.transform.rotation, 10f * Time.deltaTime);
					return;
				}
				resetIKOffsetsInterval -= Time.deltaTime;
			}
			if (Physics.Raycast(base.transform.position + Vector3.up, Vector3.down, out hit, 2f, 1073744129, QueryTriggerInteraction.Ignore))
			{
				Vector3 vector = hit.normal;
				if (Physics.Raycast(base.transform.position + base.transform.forward * 3f + base.transform.up * 3f, -base.transform.up, out hit, 20f, 1073744129, QueryTriggerInteraction.Ignore))
				{
					vector = (vector + hit.normal) / 2f;
				}
				if (Vector3.Angle(vector, Vector3.up) < 75f)
				{
					Quaternion b = Quaternion.FromToRotation(animationContainer.up, vector) * base.transform.rotation;
					animationContainer.rotation = Quaternion.Lerp(animationContainer.rotation, b, Time.deltaTime * 4f);
				}
			}
		}
		float num = 0f;
		for (int j = 0; j < proceduralBodyTargets.Length; j++)
		{
			if (j == 4 && currentBehaviourStateIndex == 2)
			{
				proceduralBodyTargets[j].localPosition = Vector3.zero;
				continue;
			}
			if (Physics.Raycast(proceduralBodyTargets[j].position + base.transform.up * 1.5f, -base.transform.up, out hit, 20f, 1073744129, QueryTriggerInteraction.Ignore))
			{
				Debug.DrawRay(proceduralBodyTargets[j].position + base.transform.up * 1.5f, -base.transform.up * 5f, Color.white);
				Debug.DrawRay(hit.point, Vector3.up * 0.2f + Vector3.right * 0.2f, Color.red);
				num = Mathf.Clamp(hit.point.y, base.transform.position.y - 1.25f, base.transform.position.y + 1.25f);
				if (j == 4)
				{
					num += Mathf.Clamp(70f / Vector3.Angle(-base.transform.forward, hit.normal), -0.25f, 1.5f);
				}
			}
			else
			{
				Debug.DrawRay(proceduralBodyTargets[j].position, Vector3.up * 0.2f, Color.yellow);
				num = base.transform.position.y;
			}
			if (j == 4)
			{
				float num2 = IKTargetContainers[j].position.y - IKTargetContainers[j].parent.position.y;
				if (isEnemyDead)
				{
					proceduralBodyTargets[j].position = new Vector3(IKTargetContainers[j].position.x, Mathf.Lerp(proceduralBodyTargets[j].position.y, num - 0.03f, 15f * Time.deltaTime), IKTargetContainers[j].position.z);
				}
				else
				{
					proceduralBodyTargets[j].position = new Vector3(IKTargetContainers[j].position.x, Mathf.Lerp(proceduralBodyTargets[j].position.y, num + 0.8f, 15f * Time.deltaTime), IKTargetContainers[j].position.z);
				}
			}
			else
			{
				float num3 = (0.54f + Mathf.Lerp(0.42f, 0.1f, Vector3.Angle(-base.transform.up, proceduralBodyTargets[j].up) / 90f)) * 0.61f;
				float num2 = IKTargetContainers[j].position.y - IKTargetContainers[j].parent.position.y;
				proceduralBodyTargets[j].position = new Vector3(IKTargetContainers[j].position.x, num + num2 + num3, IKTargetContainers[j].position.z);
			}
		}
		if (isEnemyDead)
		{
			lookRig.weight = Mathf.Lerp(lookRig.weight, 0f, Time.deltaTime * 16f);
			return;
		}
		if (!clingingToTreeAnim)
		{
			if (pingAttentionTimer > 0f)
			{
				lookRigTargetPos = Vector3.Lerp(lookRigTargetPos, pingAttentionPosition, 24f * Time.deltaTime);
				targetPlayerLookPos = lookRigTargetPos;
			}
			else
			{
				if (!hidingQuickly && (!(targetPlayer != null) || Physics.Linecast(eye.position, targetPlayer.playerEye.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
				{
					agent.angularSpeed = 320f;
					lookRig.weight = Mathf.Lerp(lookRig.weight, 0f, Time.deltaTime * 16f);
					return;
				}
				if (hidingQuickly)
				{
					float num4 = 1000f;
					PlayerControllerB playerControllerB = null;
					for (int k = 0; k < playerKnowledge.Length; k++)
					{
						if (playerKnowledge[k].playerScript.isPlayerControlled && !playerKnowledge[k].playerScript.isInsideFactory && playerKnowledge[k].seenByPumaThroughTrees)
						{
							float num5 = Vector3.Distance(eye.position, playerKnowledge[k].playerScript.playerEye.position);
							if (num5 < num4)
							{
								num4 = num5;
								playerControllerB = playerKnowledge[k].playerScript;
							}
						}
					}
					if (playerControllerB == null)
					{
						playerControllerB = targetPlayer;
					}
					if (playerControllerB != null)
					{
						lookRigTargetPos = Vector3.Lerp(lookRigTargetPos, playerControllerB.playerEye.transform.position, 14f * Time.deltaTime);
					}
				}
				else
				{
					targetPlayerLookPos = targetPlayer.playerEye.transform.position;
					lookRigTargetPos = Vector3.Lerp(lookRigTargetPos, targetPlayerLookPos, 14f * Time.deltaTime);
				}
			}
			if (base.IsOwner && (!stalkingFrozenAnim || startled))
			{
				float num6 = Vector3.Angle(base.transform.forward, Vector3.Scale(new Vector3(1f, 0f, 1f), targetPlayerLookPos - base.transform.position));
				if (currentBehaviourStateIndex != 0)
				{
					float num7 = (startled ? 45f : Mathf.Clamp(Vector3.Distance(base.transform.position, targetPlayerLookPos) + 5f, 0f, 24f));
					if (currentBehaviourStateIndex == 2 || num6 > num7)
					{
						agent.angularSpeed = 0f;
						turnCompass.LookAt(targetPlayerLookPos);
						Debug.DrawRay(turnCompass.position, turnCompass.forward * 25f, Color.red);
						turnCompass.eulerAngles = new Vector3(0f, turnCompass.eulerAngles.y, 0f);
						Debug.DrawRay(turnCompass.position, turnCompass.forward * 25f, Color.blue);
						float num8 = 3f;
						if (berserkModeTimer > 0f || currentBehaviourStateIndex == 2)
						{
							num8 = 10f;
						}
						base.transform.rotation = Quaternion.Lerp(base.transform.rotation, turnCompass.rotation, num8 * Time.deltaTime);
						base.transform.localEulerAngles = new Vector3(0f, base.transform.localEulerAngles.y, 0f);
					}
					else
					{
						turnCompass.LookAt(targetPlayerLookPos);
						base.transform.rotation = Quaternion.Lerp(base.transform.rotation, turnCompass.rotation, 0.45f * Time.deltaTime);
					}
				}
				else
				{
					agent.angularSpeed = 320f;
				}
			}
			float num9 = Vector3.Angle(base.transform.forward, Vector3.Scale(new Vector3(1f, 0f, 1f), lookRigTargetPos - base.transform.position));
			if (num9 > 22f)
			{
				lookRig.weight = Mathf.Lerp(lookRig.weight, 1f * (Mathf.Abs(num9 - 180f) / 180f), Time.deltaTime * 11f);
			}
			else
			{
				lookRig.weight = Mathf.Lerp(lookRig.weight, 1f, Time.deltaTime * 11f);
			}
		}
		lookRigTarget.position = lookRigTargetPos;
	}

	private void LateUpdate()
	{
		AddProceduralOffsetToLimbsOverTerrain();
	}

	public void RemoveTree(GameObject treeObject)
	{
		if (treeObject == TargetTree)
		{
			TargetTree = null;
		}
		if (AllTreeNodes != null)
		{
			if (!AllTreeNodes.Remove(treeObject))
			{
				Debug.Log("Puma: Attempted to remove tree '" + treeObject.name + "' but it was not in the list", treeObject);
			}
			excludeTrees.Remove(treeObject);
			excludeTreesTemporarily.Remove(treeObject);
		}
	}

	public override void Start()
	{
		base.Start();
		goToShipTimer = Time.realtimeSinceStartup;
		playerKnowledge = new PumaPlayerKnowledge[StartOfRound.Instance.connectedPlayersAmount + 1];
		for (int i = 0; i < playerKnowledge.Length; i++)
		{
			playerKnowledge[i] = new PumaPlayerKnowledge(StartOfRound.Instance.allPlayerScripts[i]);
		}
		nearPlayers = StartOfRound.Instance.allPlayerScripts.ToList();
		treeColliders = new Collider[20];
		bool flag = false;
		targetNode = allAINodes[0].transform;
		if (excludeTrees == null)
		{
			excludeTrees = new List<GameObject>();
		}
		if (stalkNodesInUse == null)
		{
			stalkNodesInUse = new List<GameObject>();
		}
		if (AllTreeNodes != null && AllTreeNodes.Count > 0)
		{
			return;
		}
		AllTreeNodes = new List<GameObject>();
		GameObject[] array = GameObject.FindGameObjectsWithTag("Tree");
		for (int j = 0; j < array.Length; j++)
		{
			if (RoundManager.Instance.underwaterBounds != null)
			{
				Debug.DrawRay(array[j].transform.position + Vector3.up, Vector3.down * 3f, Color.white, 10f);
				if (!Physics.Raycast(array[j].transform.position + Vector3.up, Vector3.down, out var hitInfo, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					Debug.Log($"Tree #{j} '{array[j].gameObject.name}' did not get raycast down", array[j].gameObject);
					continue;
				}
				flag = false;
				for (int k = 0; k < RoundManager.Instance.underwaterBounds.Length; k++)
				{
					Debug.Log($"Checking Tree #{j} '{array[j].gameObject.name}' with bounds #{k}", array[j].gameObject);
					if (RoundManager.Instance.underwaterBounds[k].Contains(hitInfo.point))
					{
						flag = true;
						Debug.Log($"Tree #{j} '{array[j].gameObject.name}' is in bounds #{k}", array[j].gameObject);
						break;
					}
				}
				if (flag)
				{
					continue;
				}
			}
			array[j].SetActive(value: false);
			if (Physics.CheckSphere(array[j].transform.position + Vector3.up * 16f, 15f, 33554432, QueryTriggerInteraction.Ignore))
			{
				AllTreeNodes.Add(array[j]);
			}
			array[j].SetActive(value: true);
		}
		targetNode = allAINodes[0].transform;
	}

	private bool GetTargetTree()
	{
		AllTreeNodes.Sort((GameObject a, GameObject b) => (base.transform.position - a.transform.position).sqrMagnitude.CompareTo((base.transform.position - b.transform.position).sqrMagnitude));
		GameObject gameObject = null;
		GameObject gameObject2 = null;
		for (int num = 0; num < AllTreeNodes.Count; num++)
		{
			if (excludeTrees.Contains(AllTreeNodes[num]) || AllTreeNodes[num] == TargetTree)
			{
				continue;
			}
			bool flag = false;
			for (int num2 = 0; num2 < playerKnowledge.Length; num2++)
			{
				if (playerKnowledge[num2].playerScript.isInsideFactory || !playerKnowledge[num2].playerScript.isPlayerControlled || playerKnowledge[num2].timeSinceSeeingPuma > 0.25f || !((AllTreeNodes[num].transform.position - base.transform.position).sqrMagnitude > (AllTreeNodes[num].transform.position - playerKnowledge[num2].playerScript.transform.position).sqrMagnitude))
				{
					continue;
				}
				flag = true;
				if (gameObject2 == null)
				{
					Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(AllTreeNodes[num].transform.position, default(NavMeshHit), 5f, agentMask);
					if (!(navMeshPosition == Vector3.zero))
					{
						gameObject2 = AllTreeNodes[num];
						break;
					}
					Debug.Log($"GetTargetTree {num} no path found");
				}
			}
			if (!flag)
			{
				if (gameObject2 != null && num > 7 && Vector3.Distance(AllTreeNodes[num].transform.position, base.transform.position) > 34f)
				{
					break;
				}
				Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(AllTreeNodes[num].transform.position);
				if (!(navMeshPosition == Vector3.zero))
				{
					mostOptimalDistance = Vector3.Distance(base.transform.position, navMeshPosition);
					gameObject = AllTreeNodes[num];
					break;
				}
				Debug.Log($"GetTargetTree {num} no path found");
			}
		}
		if (gameObject == null && gameObject2 != null)
		{
			gameObject = gameObject2;
		}
		TargetTree = gameObject;
		Debug.Log($"Set GetTargetTree: Got target tree?: {TargetTree != null}");
		return TargetTree != null;
	}

	private void GetEndTargetTreeHiding()
	{
		nearPlayers.Sort((PlayerControllerB o1, PlayerControllerB o2) => (o1.transform.position - base.transform.position).sqrMagnitude.CompareTo((o2.transform.position - base.transform.position).sqrMagnitude));
		for (int num = 0; num < nearPlayers.Count; num++)
		{
			if (!nearPlayers[num].isPlayerControlled)
			{
				continue;
			}
			float num2 = 0f;
			int index = -1;
			for (int num3 = 0; num3 < AllTreeNodes.Count; num3++)
			{
				if (!excludeTrees.Contains(AllTreeNodes[num3]))
				{
					float sqrMagnitude = (AllTreeNodes[num3].transform.position - nearPlayers[num].transform.position).sqrMagnitude;
					if (sqrMagnitude > num2)
					{
						num2 = sqrMagnitude;
						index = num3;
					}
				}
			}
			EndTargetTree = AllTreeNodes[index];
			break;
		}
	}

	private void GetEndTargetTree(int offset = 0)
	{
		debugIncompatibleTrees.Clear();
		debugIncompatibleTreesB.Clear();
		nearPlayers.Sort((PlayerControllerB o1, PlayerControllerB o2) => (o1.transform.position - base.transform.position).sqrMagnitude.CompareTo((o2.transform.position - base.transform.position).sqrMagnitude));
		int i;
		for (i = 0; i < nearPlayers.Count; i++)
		{
			Debug.Log($"Get end target tree : Player #{i} '{nearPlayers[i].gameObject.name}'");
			if (!nearPlayers[i].isPlayerControlled || nearPlayers[i].isInsideFactory)
			{
				continue;
			}
			int num = Physics.OverlapSphereNonAlloc(nearPlayers[i].transform.position, 30f, treeColliders, 33554432, QueryTriggerInteraction.Ignore);
			Debug.Log($"puma: Trees nearby player #{i}: {num}");
			if (num == 1 && !treeColliders[0].CompareTag("Tree"))
			{
				continue;
			}
			treeCollidersB.Clear();
			for (int num2 = 0; num2 < num; num2++)
			{
				if (AllTreeNodes.Contains(treeColliders[num2].gameObject))
				{
					treeCollidersB.Add(treeColliders[num2]);
				}
			}
			Debug.Log($"puma: Trees nearby player #{i} After including only trees in AllTreeNodes: {treeCollidersB.Count}");
			treeCollidersB.Sort((Collider o1, Collider o2) => (nearPlayers[i].transform.position - o1.transform.position).sqrMagnitude.CompareTo((nearPlayers[i].transform.position - o2.transform.position).sqrMagnitude));
			int num3 = 0;
			for (int num4 = 0; num4 < treeCollidersB.Count; num4++)
			{
				if (excludeTrees.Contains(treeCollidersB[num4].gameObject))
				{
					Debug.Log("Puma F 1 '" + treeCollidersB[num4].gameObject.name + "'", treeCollidersB[num4].gameObject);
					continue;
				}
				Debug.Log($"tree #{num4} distance: {Vector3.Distance(nearPlayers[i].transform.position, treeCollidersB[num4].transform.position)}", treeCollidersB[num4].gameObject);
				if (!treeCollidersB[num4].CompareTag("Tree"))
				{
					Debug.Log("Puma F 2 '" + treeCollidersB[num4].gameObject.name + "'");
					continue;
				}
				if (nearPlayers[i].HasLineOfSightToPosition(treeCollidersB[num4].transform.position + Vector3.up * 6f, playerTreeLOSClose, (int)playerTreeLOSDistanceClose, 2f, playerLOSLayermask))
				{
					debugIncompatibleTrees.Add(treeCollidersB[num4].transform);
					Debug.Log("Puma F 3 '" + treeCollidersB[num4].gameObject.name + "'", treeCollidersB[num4].gameObject);
					continue;
				}
				if (nearPlayers[i].HasLineOfSightToPosition(treeCollidersB[num4].transform.position + Vector3.up * 6f, playerTreeLOSFar, (int)playerTreeLOSDistanceFar, 2f, playerLOSLayermask))
				{
					debugIncompatibleTrees.Add(treeCollidersB[num4].transform);
					Debug.Log("Puma F 3 (b) '" + treeCollidersB[num4].gameObject.name + "'", treeCollidersB[num4].gameObject);
					continue;
				}
				EndTargetTree = treeCollidersB[num4].gameObject;
				Debug.Log($"Puma : Got EndTargetTree!!! at #{num4}; offset: {offset}", EndTargetTree.gameObject);
				if (num3 >= offset)
				{
					return;
				}
				debugIncompatibleTreesB.Add(treeCollidersB[num4].transform);
				num3++;
			}
		}
	}

	private void ChooseTargetTree()
	{
		if (!(TargetTree == null))
		{
			return;
		}
		bool flag = false;
		if (GetTargetTree())
		{
			Debug.Log("Got target tree: " + TargetTree.gameObject.name, TargetTree.gameObject);
			Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(TargetTree.transform.position, default(NavMeshHit), 5f, agentMask);
			if (navMeshPosition != Vector3.zero)
			{
				Debug.Log($"Got nav pos: {navMeshPosition}");
				Debug.DrawRay(navMeshPosition, Vector3.up * 100f, Color.magenta, 5f);
				if (SetDestinationToPosition(navMeshPosition))
				{
					Debug.Log($"Got dest {navMeshPosition}: {destination}");
					flag = true;
				}
			}
		}
		if (!flag)
		{
			Debug.Log("No target tree found!");
		}
	}

	public bool TargetClosestPlayerSkiddish(float bufferDistance = 1.5f, bool requireLineOfSight = false, float viewWidth = 70f, bool doGroundCast = false, bool requirePath = false)
	{
		mostOptimalDistance = 2000f;
		PlayerControllerB playerControllerB = targetPlayer;
		targetPlayer = null;
		int num = 0;
		for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
		{
			if (!PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]))
			{
				continue;
			}
			if (doGroundCast)
			{
				if (!Physics.Raycast(StartOfRound.Instance.allPlayerScripts[i].transform.position, Vector3.down, out hit, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) || (requirePath && PathIsIntersectedByLineOfSight(hit.point, calculatePathDistance: false, avoidLineOfSight: false)))
				{
					continue;
				}
			}
			else if (requirePath && PathIsIntersectedByLineOfSight(StartOfRound.Instance.allPlayerScripts[i].transform.position, calculatePathDistance: false, avoidLineOfSight: false))
			{
				continue;
			}
			if (requireLineOfSight && !CheckLineOfSightForPosition(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, viewWidth, 40))
			{
				continue;
			}
			int num2 = Physics.OverlapSphereNonAlloc(StartOfRound.Instance.allPlayerScripts[i].transform.position, 10f, treeColliders, 524288, QueryTriggerInteraction.Collide);
			if (num2 > 0)
			{
				num = 0;
				IVisibleThreat component = null;
				for (int j = 0; j < num2; j++)
				{
					EnemyAICollisionDetect component2 = treeColliders[j].gameObject.GetComponent<EnemyAICollisionDetect>();
					if (component2 != null && component2.mainScript != this && component2.mainScript.TryGetComponent<IVisibleThreat>(out component) && !component.IsThreatDead())
					{
						num += component.GetThreatLevel(StartOfRound.Instance.allPlayerScripts[i].transform.position);
					}
				}
				if (num >= 6)
				{
					continue;
				}
			}
			tempDist = Vector3.Distance(base.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
			if (tempDist < mostOptimalDistance)
			{
				mostOptimalDistance = tempDist;
				targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
			}
		}
		if (targetPlayer != null && bufferDistance > 0f && playerControllerB != null && Mathf.Abs(mostOptimalDistance - Vector3.Distance(base.transform.position, playerControllerB.transform.position)) < bufferDistance)
		{
			targetPlayer = playerControllerB;
		}
		return targetPlayer != null;
	}

	public override void DoAIInterval()
	{
		base.DoAIInterval();
		if (StartOfRound.Instance.livingPlayers == 0 || isEnemyDead)
		{
			return;
		}
		if (currentBehaviourStateIndex == 0)
		{
			bool flag = (clingingToTree ? TargetClosestPlayerSkiddish(4f, requireLineOfSight: false, -1f, doGroundCast: true) : TargetClosestPlayer(4f, requireLineOfSight: false, -1f, doGroundCast: true));
			Debug.Log($"Got target player?: {flag}");
			Debug.Log($"Tree state: {treeState}; {clingingToTree}");
			if (!clingingToTree)
			{
				ChooseTargetTree();
				if (TargetTree != null && Vector3.Distance(destination, TargetTree.transform.position) > 4f)
				{
					Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(TargetTree.transform.position, default(NavMeshHit), 5f, agentMask);
					if (navMeshPosition != Vector3.zero)
					{
						SetDestinationToPosition(navMeshPosition);
						return;
					}
					excludeTrees.Add(TargetTree);
					TargetTree = null;
				}
			}
			else
			{
				if (!(TargetTree != null))
				{
					return;
				}
				int num = PlayersSeeingPumaInTree();
				for (int i = 0; i < num; i++)
				{
					seenByPlayersTimer += AIIntervalTime * 0.2f;
				}
				for (int j = 0; j < playerKnowledge.Length; j++)
				{
					if (playerKnowledge[j].seeAngle != -361f && playerKnowledge[j].seeAngle < 37f && Vector3.Distance(playerKnowledge[j].playerScript.transform.position, TargetTree.transform.position) < 28f)
					{
						seenByPlayersTimer += AIIntervalTime * (float)playersSeeingPuma;
					}
				}
				if (num == 0)
				{
					seenByPlayersTimer = Mathf.Max(seenByPlayersTimer - Time.deltaTime * 2f, 0f);
				}
				if (seenByPlayersTimer >= 2f)
				{
					seenByPlayersTimer = 0f;
					treeHidingTimer = 6f;
				}
				Debug.Log("PUMA AI 5");
				if (treeState == TreeState.Idle)
				{
					Debug.Log($"Getting end target tree; hiding meter : {treeHidingTimer}");
					if (treeHidingTimer > 0f)
					{
						GetEndTargetTreeHiding();
					}
					else
					{
						GetEndTargetTree(7);
					}
				}
			}
		}
		else if (currentBehaviourStateIndex == 1)
		{
			if (!wasOwnerLastFrame)
			{
				wasOwnerLastFrame = true;
				targetPlayer = GameNetworkManager.Instance.localPlayerController;
			}
			_ = targetPlayer;
			if (TargetClosestPlayerSkiddish(8f, requireLineOfSight: false, -1f, doGroundCast: true))
			{
				if (targetPlayer != GameNetworkManager.Instance.localPlayerController)
				{
					ChangeOwnershipOfEnemy(targetPlayer.actualClientId);
					return;
				}
				for (int k = 0; k < playerKnowledge.Length; k++)
				{
					if (playerKnowledge[k].playerScript == targetPlayer)
					{
						targetPlayerKnowledge = playerKnowledge[k];
					}
				}
				movingTowardsTargetPlayer = currentBehaviourStateIndex == 2;
				if (currentBehaviourStateIndex == 1)
				{
					if (hidingQuickly)
					{
						if (!FindHidingSpot())
						{
							stopStalkingTimer += AIIntervalTime;
						}
						else
						{
							stopStalkingTimer = 0f;
						}
					}
					else if (!stalkingFrozen)
					{
						if (timeSincePlayersSeeingPumaWhileClose > 6f || stalkingCharge)
						{
							movingTowardsTargetPlayer = true;
						}
						else
						{
							ChooseClosestNodeToPlayer();
						}
					}
					else
					{
						stopStalkingTimer = 0f;
					}
				}
			}
			else
			{
				stopStalkingTimer += AIIntervalTime;
				SetDestinationToPosition(base.transform.position);
			}
			if (stopStalkingTimer > 1.75f || (hidingQuickly && stopStalkingTimer > 0.45f))
			{
				clearDebugTextInterval = 8f;
				SwitchToBehaviourState(0);
			}
		}
		else if (currentBehaviourStateIndex == 2 && targetPlayer != null && targetPlayer != GameNetworkManager.Instance.localPlayerController)
		{
			ChangeOwnershipOfEnemy(targetPlayer.actualClientId);
		}
	}

	public override bool PlayerIsTargetable(PlayerControllerB playerScript, bool cannotBeInShip = false, bool overrideInsideFactoryCheck = false, bool checkForMineshaftStartTile = true)
	{
		if (cannotBeInShip && playerScript.isInHangarShipRoom)
		{
			return false;
		}
		if (playerScript.isPlayerControlled && !playerScript.isPlayerDead && playerScript.inAnimationWithEnemy == null && (overrideInsideFactoryCheck || playerScript.isInsideFactory != isOutside) && playerScript.sinkingValue < 0.73f)
		{
			if (isOutside && (StartOfRound.Instance.hangarDoorsClosed || !(Time.realtimeSinceStartup - goToShipTimer > 60f)))
			{
				return playerScript.isInHangarShipRoom == isInsidePlayerShip;
			}
			return true;
		}
		return false;
	}

	private bool FindHidingSpot()
	{
		PlayerControllerB playerControllerB = null;
		float num = 1000f;
		for (int i = 0; i < playerKnowledge.Length; i++)
		{
			if (!(playerKnowledge[i].timeSincePumaSeeing > 2f))
			{
				float num2 = Vector3.Distance(playerKnowledge[i].playerScript.transform.position, base.transform.position);
				if (num2 < num)
				{
					playerControllerB = playerKnowledge[i].playerScript;
					num = num2;
				}
			}
		}
		if (playerControllerB == null)
		{
			return false;
		}
		int num3 = Physics.OverlapSphereNonAlloc(base.transform.position, 15f, treeColliders, 33554432);
		treeCollidersB.Clear();
		for (int j = 0; j < num3; j++)
		{
			if (eliminatedHideTrees.Count <= 0 || !eliminatedHideTrees.Contains(treeColliders[j].gameObject))
			{
				float num2 = (treeColliders[j].transform.position - playerControllerB.transform.position).sqrMagnitude;
				if (num2 > 25f && num2 < 2116f)
				{
					treeCollidersB.Add(treeColliders[j]);
				}
			}
		}
		treeCollidersB.Sort((Collider o1, Collider o2) => (o1.transform.position - base.transform.position).sqrMagnitude.CompareTo((o2.transform.position - base.transform.position).sqrMagnitude));
		bool flag = false;
		for (int num4 = 0; num4 < treeCollidersB.Count; num4++)
		{
			if (!treeCollidersB[num4].CompareTag("Tree"))
			{
				continue;
			}
			for (int num5 = 0; num5 < playerKnowledge.Length; num5++)
			{
				if (!(playerKnowledge[num5].timeSincePumaSeeing > 2f) && !(playerKnowledge[num5].timeSinceSeeingPuma > 0.5f) && Vector3.Distance(treeCollidersB[num4].transform.position, playerKnowledge[num5].playerScript.transform.position) < 8f)
				{
					Vector3.Distance(treeCollidersB[num4].transform.position, base.transform.position);
					_ = 5f;
				}
			}
			GameObject gameObject = treeCollidersB[num4].gameObject;
			Vector3 position = treeCollidersB[num4].transform.position;
			position += Vector3.Normalize(Vector3.Scale(new Vector3(1f, 0f, 1f), position - playerControllerB.transform.position)) * 4f;
			position.y = treeCollidersB[num4].transform.position.y + 10f;
			if (Physics.Raycast(position, Vector3.down, out hit, 15f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
			{
				position = hit.point + Vector3.up * 3f;
				for (int num6 = 0; num6 < playerKnowledge.Length; num6++)
				{
					if (!(playerKnowledge[num6].timeSincePumaSeeing > 2f) && !(playerKnowledge[num6].timeSinceSeeingPuma > 0.5f) && !Physics.Linecast(playerKnowledge[num6].playerScript.playerEye.transform.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
					{
						flag = true;
						Debug.DrawLine(playerKnowledge[num6].playerScript.playerEye.transform.position, position, Color.red, 6f);
						break;
					}
				}
				if (flag)
				{
					continue;
				}
				Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(position - Vector3.up * 3f, default(NavMeshHit), 3f);
				if (navMeshPosition == Vector3.zero && num4 > 14)
				{
					return false;
				}
				if (hidingTree != gameObject)
				{
					hidingTree = gameObject;
					hidingQuicklySeenMeterIncrease = 0f;
				}
				else if (seenByPlayersMeter > 5f && targetPlayer != null && Vector3.Angle(targetPlayer.playerEye.transform.forward, eye.position) < 20f)
				{
					hidingQuicklySeenMeterIncrease += seenByPlayersMeter - seenMeterLastFrame;
					seenMeterLastFrame = seenByPlayersMeter;
					if (hidingQuicklySeenMeterIncrease > 8f)
					{
						hidingQuicklySeenMeterIncrease = 0f;
						eliminatedHideTrees.Add(hidingTree);
					}
				}
				SetDestinationToPosition(navMeshPosition);
				return true;
			}
			Debug.DrawRay(position, Vector3.down * 15f, Color.cyan, 6f);
		}
		if (eliminatedHideTrees.Count > 0)
		{
			eliminatedHideTrees.Clear();
		}
		hidingQuicklySeenMeterIncrease = 0f;
		return false;
	}

	public void ChooseClosestNodeToPlayer()
	{
		if (targetNode == null)
		{
			RemoveCurrentStalkNode();
			targetNode = allAINodes[0].transform;
		}
		bool flag = false;
		Transform transform = null;
		mostOptimalDistance = 1000f;
		transform = ChooseClosestNodeToPositionPuma(RoundManager.Instance.GetNavMeshPosition(targetPlayer.transform.position), avoidLineOfSight: true, 0, Vector3.Distance(base.transform.position, targetPlayer.transform.position) < 40f);
		if (transform != null)
		{
			if (previousClosestNode != null)
			{
				if (transform != previousClosestNode && Vector3.Distance(playerPosAtClosestNode, previousClosestNode.position) > 6f)
				{
					previousClosestNode = null;
					flag = false;
				}
				else
				{
					flag = true;
				}
			}
			targetNode = transform;
		}
		float num = Vector3.Distance(targetPlayer.transform.position, base.transform.position);
		Debug.Log($"Puma dist to closest player; {num} ; mostOptimalDistance: {mostOptimalDistance} ; zoningIn: {flag} ; close enough to start zone in?: {num - mostOptimalDistance < 0.1f}");
		if (flag || num - mostOptimalDistance < 0.1f)
		{
			movingTowardsTargetPlayer = true;
			RemoveCurrentStalkNode();
			if (previousClosestNode == null)
			{
				playerPosAtClosestNode = targetPlayer.transform.position;
				previousClosestNode = transform;
			}
		}
		else
		{
			SetDestinationToPosition(targetNode.position);
			SetUsingStalkNode(targetNode.gameObject);
		}
	}

	private void SetUsingStalkNode(GameObject node)
	{
		if (!(currentStalkNode == node) && !stalkNodesInUse.Contains(node))
		{
			if (currentStalkNode != null)
			{
				stalkNodesInUse.Remove(currentStalkNode);
			}
			stalkNodesInUse.Add(node);
			currentStalkNode = node;
		}
	}

	private void RemoveCurrentStalkNode()
	{
		if (currentStalkNode != null && stalkNodesInUse.Contains(currentStalkNode))
		{
			stalkNodesInUse.Remove(currentStalkNode);
		}
	}

	public Transform ChooseClosestNodeToPositionPuma(Vector3 pos, bool avoidLineOfSight = false, int offset = 0, bool doPathCheck = false)
	{
		Array.Sort(allAINodes, (GameObject a, GameObject b) => (pos - a.transform.position).sqrMagnitude.CompareTo((pos - b.transform.position).sqrMagnitude));
		Debug.Log("First node gotten: " + allAINodes[0].gameObject.name + "; second: " + allAINodes[1].gameObject.name + "; last: " + allAINodes[allAINodes.Length - 1].gameObject.name);
		Transform result = allAINodes[0].transform;
		for (int num = 0; num < allAINodes.Length; num++)
		{
			Debug.Log($"Checking node #{num} : {allAINodes[num].gameObject.name}", allAINodes[num].gameObject);
			if (currentStalkNode != allAINodes[num] && stalkNodesInUse.Contains(allAINodes[num]))
			{
				Debug.Log($"Stalking nodes in use contains node #{num}");
				continue;
			}
			if (doPathCheck && PathIsIntersectedByLineOfSightPuma(allAINodes[num].transform.position))
			{
				Debug.Log($"Path is intersected by line of sight (#{num})");
				continue;
			}
			Debug.DrawLine(pos, allAINodes[num].transform.position, Color.yellow, AIIntervalTime);
			Debug.Log($"Position distance from {pos} to {allAINodes[num].transform.position} : {Vector3.Distance(pos, allAINodes[num].transform.position)}");
			mostOptimalDistance = Vector3.Distance(pos, allAINodes[num].transform.position);
			Debug.Log($"mostOptimalDistance: {mostOptimalDistance}");
			result = allAINodes[num].transform;
			if (offset == 0 || num >= allAINodes.Length - 1)
			{
				break;
			}
			offset--;
		}
		return result;
	}

	public bool PathIsIntersectedByLineOfSightPuma(Vector3 targetPos)
	{
		pathDistance = 0f;
		if (agent.isOnNavMesh && !agent.CalculatePath(targetPos, path1))
		{
			if (DebugEnemy)
			{
				Debug.Log($"Puma: Path could not be calculated: {targetPos}");
				Debug.DrawLine(base.transform.position, targetPos, Color.yellow, 5f);
			}
			return true;
		}
		if (DebugEnemy)
		{
			for (int i = 1; i < path1.corners.Length; i++)
			{
				Debug.DrawLine(path1.corners[i - 1], path1.corners[i], Color.red);
			}
		}
		int cornersNonAlloc = path1.GetCornersNonAlloc(RoundManager.Instance.storedPathCorners);
		Vector3[] storedPathCorners = RoundManager.Instance.storedPathCorners;
		if (path1 == null || cornersNonAlloc == 0)
		{
			return true;
		}
		bool flag = false;
		Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(targetPos, RoundManager.Instance.navHit, 0.75f, agentMask);
		if (!RoundManager.Instance.GotNavMeshPositionResult)
		{
			navMeshPosition = RoundManager.Instance.GetNavMeshPosition(targetPos, RoundManager.Instance.navHit, 2.7f, agentMask);
			flag = true;
		}
		if (Vector3.Distance(navMeshPosition, storedPathCorners[cornersNonAlloc - 1]) > 1.55f || !RoundManager.Instance.GotNavMeshPositionResult)
		{
			if (DebugEnemy)
			{
				Debug.Log($"A: Path is not complete; final waypoint of path was too far from target position: {targetPos}; gotnavmeshpos: {RoundManager.Instance.GotNavMeshPositionResult}; {flag}");
				Debug.DrawRay(storedPathCorners[cornersNonAlloc - 1], Vector3.up * 0.75f, Color.green, 4f);
				Debug.DrawRay(navMeshPosition, Vector3.up * 0.5f, Color.magenta, 4f);
			}
			return true;
		}
		Vector3 a = storedPathCorners[0];
		for (int j = 1; j < cornersNonAlloc; j++)
		{
			if (DebugEnemy)
			{
				Debug.DrawLine(storedPathCorners[j - 1], storedPathCorners[j], Color.green);
			}
			if (j > 5 && Vector3.Distance(a, storedPathCorners[j]) < 2f)
			{
				if (DebugEnemy)
				{
					Debug.Log($"Distance between corners {j} and {j - 1} under 3 meters; skipping LOS check");
					Debug.DrawRay(storedPathCorners[j - 1] + Vector3.up * 0.2f, storedPathCorners[j] + Vector3.up * 0.2f, Color.magenta, 0.2f);
				}
				continue;
			}
			a = storedPathCorners[j];
			if (targetPlayer != null)
			{
				Vector3 vector = Vector3.Lerp(storedPathCorners[j - 1], storedPathCorners[j], 0.5f);
				if (Vector3.Angle(targetPlayer.playerEye.forward, vector - targetPlayer.playerEye.position) < 100f && !Physics.Linecast(vector + Vector3.up * 0.5f, targetPlayer.playerEye.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					return true;
				}
			}
			if (j > 15)
			{
				if (DebugEnemy)
				{
					Debug.Log(enemyType.enemyName + ": Reached corner 15, stopping checks now");
				}
				return false;
			}
		}
		return false;
	}

		[Rpc(SendTo.NotMe)]
		public void StartLeapToTreeRpc(Vector3 leapFrom, Vector3 leapTo, float yRot, bool leapAnim)
		{
			treeState = TreeState.Leaping;
			leapFromPosition = leapFrom;
			clingIdlePosition = leapTo;
			leapDistance = Vector3.Distance(clingIdlePosition, leapFromPosition);
			approachTreeRotation = base.transform.eulerAngles.y;
			treeClingRotation = yRot;
			leapTimer = 0f;
			startLeapTimer = 0f;
			creatureAnimator.SetBool("Leaping", value: true);
			creatureAnimator.SetBool("leapEndB", leapAnim);
			disqualifiedCurrentTreeFromDrop = false;
			PlayLeapEffect();
		}

	private bool StartLeapToTree(bool hiding = false)
	{
		leapAttempts++;
		if (excludeTreesTemporarily.Count > 0)
		{
			excludeTreesTemporarilyTime++;
			if (excludeTreesTemporarilyTime > 6)
			{
				excludeTreesTemporarilyTime = 0;
				excludeTreesTemporarily.Clear();
			}
		}
		float num = 1000f;
		int num2 = -1;
		int num3 = -1;
		int num4 = -1;
		int num5 = -1;
		if (EndTargetTree == null)
		{
			GetEndTargetTree(12);
		}
		if (EndTargetTree == null)
		{
			GetEndTargetTreeHiding();
		}
		int num6 = Physics.OverlapSphereNonAlloc(base.transform.position, 26f + (float)leapAttempts, treeColliders, 33554432, QueryTriggerInteraction.Ignore);
		Debug.Log($"Puma leaped; leap attempts: {leapAttempts}; leap from position: {base.transform.position}");
		if (TargetTree != null)
		{
			Debug.Log("Puma leap; target tree: " + TargetTree.gameObject.name);
		}
		float sqrMagnitude = (EndTargetTree.transform.position - TargetTree.transform.position).sqrMagnitude;
		Debug.Log($"Num of trees in physics check with range {26f + (float)leapAttempts}: {num6}");
		treeCollidersB.Clear();
		for (int i = 0; i < num6; i++)
		{
			if (!excludeTrees.Contains(treeColliders[i].gameObject) && !excludeTreesTemporarily.Contains(treeColliders[i].gameObject) && Vector3.Distance(EndTargetTree.transform.position, TargetTree.transform.position) - Vector3.Distance(EndTargetTree.transform.position, treeColliders[i].transform.position) > -10f)
			{
				Debug.Log("Adding tree '" + treeColliders[i].gameObject.name + "' to list treeCollidersB");
				treeCollidersB.Add(treeColliders[i]);
			}
		}
		nearPlayers.Sort((PlayerControllerB o1, PlayerControllerB o2) => (o1.transform.position - EndTargetTree.transform.position).sqrMagnitude.CompareTo((o2.transform.position - EndTargetTree.transform.position).sqrMagnitude));
		for (int num7 = 0; num7 < treeCollidersB.Count; num7++)
		{
			Debug.Log($"i:{num7}; tree name: {treeCollidersB[num7].gameObject.name}");
			float num8 = Vector3.Distance(treeCollidersB[num7].transform.position, TargetTree.transform.position);
			if (treeCollidersB[num7].gameObject == TargetTree)
			{
				continue;
			}
			bool flag = false;
			for (int num9 = 0; num9 < nearPlayers.Count; num9++)
			{
				if (!nearPlayers[num9].isPlayerControlled)
				{
					continue;
				}
				for (int num10 = 0; num10 < playerKnowledge.Length; num10++)
				{
					if (playerKnowledge[num10].playerScript == nearPlayers[num9] && !(playerKnowledge[num10].timeSinceSeeingPuma < 3f))
					{
						Vector3.Angle(playerKnowledge[num10].playerScript.playerEye.forward, base.transform.position - playerKnowledge[num10].playerScript.playerEye.position);
						_ = 25f;
					}
				}
				float num11 = nearPlayers[num9].LineOfSightToPositionAngle(treeCollidersB[num7].transform.position + Vector3.up * 6.5f, 26, -1f, playerLOSLayermask);
				bool flag2 = Vector3.Distance(treeCollidersB[num7].transform.position, nearPlayers[num9].transform.position) < 5f;
				if ((num11 != -361f && num11 < 60f) || flag2)
				{
					if (hiding && !flag2)
					{
						num4 = num7;
						Debug.Log($"i:{num7}; Set closestTreeSeenByPlayer; p:{num9}");
					}
					flag = true;
					break;
				}
			}
			if (flag)
			{
				continue;
			}
			if (treeCollidersB[num7].gameObject != EndTargetTree)
			{
				if (num8 < 4f && (num2 != -1 || !(TargetTree != null) || AllTreeNodes.Contains(TargetTree)))
				{
					continue;
				}
				if (num8 < 8f)
				{
					num3 = num7;
					Debug.Log($"i:{num7}; Set closestTreeTooClose");
					continue;
				}
			}
			if (num8 < num)
			{
				if (sqrMagnitude < (EndTargetTree.transform.position - treeCollidersB[num7].transform.position).sqrMagnitude)
				{
					num5 = num7;
					Debug.Log($"i:{num7}; Set closestTreeFartherAway");
				}
				else
				{
					num2 = num7;
					num = num8;
					Debug.Log($"i:{num7}; Set closestTree");
				}
			}
		}
		if (DebugEnemy)
		{
			Debug.Log("TargetTree: " + TargetTree.gameObject.name + "; " + TargetTree.transform.gameObject.name, TargetTree.gameObject);
		}
		GameObject gameObject;
		if (num2 == -1)
		{
			if (num3 != -1 && (hiding || treeCollidersB[num3].transform == EndTargetTree || sqrMagnitude >= (EndTargetTree.transform.position - treeCollidersB[num3].transform.position).sqrMagnitude))
			{
				gameObject = treeCollidersB[num3].gameObject;
				if (DebugEnemy)
				{
					Debug.Log("Got tree close: " + gameObject.gameObject.name + "; " + gameObject.transform.gameObject.name, gameObject.gameObject);
					Debug.Log($"Does {gameObject.gameObject} ('{gameObject.gameObject.name}') equal {TargetTree.gameObject} ('{TargetTree.gameObject.name}'): {treeCollidersB[num3].gameObject == TargetTree}");
				}
			}
			else if (!hiding && num5 != -1)
			{
				gameObject = treeCollidersB[num5].gameObject;
				excludeTreesTemporarily.Add(TargetTree);
				if (DebugEnemy)
				{
					Debug.Log("Got tree farther away", gameObject.gameObject);
				}
			}
			else
			{
				if (!hiding || num4 == -1 || !(sqrMagnitude > (EndTargetTree.transform.position - treeCollidersB[num4].transform.position).sqrMagnitude))
				{
					if (DebugEnemy)
					{
						Debug.Log("Found NO trees around the puma which were compatible");
					}
					return false;
				}
				gameObject = treeCollidersB[num4].gameObject;
				if (DebugEnemy)
				{
					Debug.Log("Got tree seen by player", gameObject.gameObject);
				}
			}
		}
		else
		{
			gameObject = treeCollidersB[num2].gameObject;
			if (DebugEnemy)
			{
				Debug.Log("Got tree: " + gameObject.gameObject.name + "; " + gameObject.transform.gameObject.name, gameObject.gameObject);
			}
		}
		if (gameObject == lastTargetTree)
		{
			excludeTreesTemporarily.Add(TargetTree);
		}
		treeState = TreeState.Leaping;
		leapFromPosition = base.transform.position;
		clingIdlePosition = gameObject.transform.position;
		clingIdlePosition.y += 7f * gameObject.transform.localScale.y;
		GameObject gameObject2 = (lastTargetTree = TargetTree);
		TargetTree = gameObject;
		leapTimer = 0f;
		startLeapTimer = 0f;
		RoundManager.Instance.tempTransform.position = leapFromPosition;
		RoundManager.Instance.tempTransform.LookAt(clingIdlePosition, Vector3.up);
		treeClingRotation = RoundManager.Instance.tempTransform.eulerAngles.y;
		approachTreeRotation = base.transform.eulerAngles.y;
		RoundManager.Instance.tempTransform.eulerAngles = new Vector3(0f, RoundManager.Instance.tempTransform.eulerAngles.y, 0f);
		Ray ray = new Ray(gameObject.transform.position - RoundManager.Instance.tempTransform.forward * 3f + Vector3.up * 2f, RoundManager.Instance.tempTransform.forward);
		Debug.DrawRay(gameObject.transform.position - RoundManager.Instance.tempTransform.forward * 2f + Vector3.up * 2f, RoundManager.Instance.tempTransform.forward * 3f, Color.cyan, 15f);
		for (int num12 = 0; num12 < 20; num12++)
		{
			ray.origin += new Vector3(0f, 2f, 0f);
			if (!Physics.Raycast(ray, 3f, 33554432, QueryTriggerInteraction.Ignore))
			{
				if (num12 < 6)
				{
					Debug.Log("Puma: Unable to leap to tree '" + gameObject.gameObject.name + "' because it was too short. (Click to see)", gameObject.gameObject);
					excludeTrees.Add(gameObject);
					clingIdlePosition = base.transform.position;
					treeClingRotation = base.transform.eulerAngles.y;
					TargetTree = gameObject2;
					return false;
				}
				Debug.DrawRay(ray.origin, ray.direction * 3f, Color.red, 3f);
				break;
			}
			clingIdlePosition = ray.origin - Vector3.up * 8f + ray.direction * 1.7f;
			Debug.DrawRay(ray.origin, ray.direction * 3f, Color.green, 3f);
		}
		leapDistance = Vector3.Distance(clingIdlePosition, leapFromPosition);
		if (DebugEnemy)
		{
			Debug.Log("Confirm leap to tree: " + gameObject.gameObject.name, gameObject.gameObject);
			Debug.Log("(Old tree: " + gameObject2.gameObject.name, gameObject2.gameObject);
		}
		creatureAnimator.SetBool("Leaping", value: true);
		leapAnimationB = UnityEngine.Random.Range(0, 100) < 35;
		creatureAnimator.SetBool("leapEndB", leapAnimationB);
		disqualifiedCurrentTreeFromDrop = false;
		Debug.Log("Set target tree to '" + TargetTree.gameObject.name + "'", TargetTree.gameObject);
		PlayLeapEffect();
		return true;
	}

		[Rpc(SendTo.NotMe)]
		public void StartTreeDropRpc(Vector3 dropPosition, Vector3 dropFromPosition, float dropRotation, bool relocate)
		{
			approachTreeRotation = base.transform.eulerAngles.y;
			leapFromPosition = dropFromPosition;
			treeClingRotation = dropRotation;
			clingIdlePosition = dropPosition;
			leapTimer = 0f;
			treeState = TreeState.DroppingDown;
			relocatingToNewTree = relocate;
			growlSource.PlayOneShot(dropSFX);
			disqualifiedCurrentTreeFromDrop = false;
		}

	private bool StartTreeDropOnLocalClient(bool relocate = false)
	{
		relocatingToNewTree = relocate;
		if (TargetTree != null && !AllTreeNodes.Contains(TargetTree.gameObject))
		{
			if (EndTargetTree != null && TargetTree == EndTargetTree)
			{
				EndTargetTree = null;
			}
			Debug.Log("Puma: Unable to relocate; Target Tree is inaccessible");
			return false;
		}
		if (Physics.Raycast(base.transform.position, Vector3.down, out hit, 100f, 256, QueryTriggerInteraction.Ignore))
		{
			Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(hit.point, default(NavMeshHit), 7f);
			if (navMeshPosition == Vector3.zero)
			{
				Debug.Log("Puma: Unable to relocate; No nav pos");
				return false;
			}
			if (EndTargetTree == null)
			{
				GetEndTargetTree(6);
				if (EndTargetTree == null)
				{
					GetEndTargetTreeHiding();
				}
			}
			if (relocate && EndTargetTree != null)
			{
				AllTreeNodes.Sort((GameObject a, GameObject b) => (EndTargetTree.transform.position - a.transform.position).sqrMagnitude.CompareTo((EndTargetTree.transform.position - b.transform.position).sqrMagnitude));
				GameObject targetTree = TargetTree;
				TargetTree = null;
				float num = Vector3.Distance(base.transform.position, EndTargetTree.transform.position);
				for (int num2 = 0; num2 < AllTreeNodes.Count; num2++)
				{
					if (AllTreeNodes[num2].gameObject == targetTree)
					{
						continue;
					}
					float num3 = Vector3.Distance(AllTreeNodes[num2].transform.position, base.transform.position);
					if (!(num3 < 40f) || !(num3 > Mathf.Min(25f, num - 10f)))
					{
						continue;
					}
					for (int num4 = 0; num4 < StartOfRound.Instance.allPlayerScripts.Length; num4++)
					{
						if (StartOfRound.Instance.allPlayerScripts[num4].isPlayerControlled && !StartOfRound.Instance.allPlayerScripts[num4].isInsideFactory)
						{
							StartOfRound.Instance.allPlayerScripts[num4].HasLineOfSightToPosition(AllTreeNodes[num2].transform.position + Vector3.up * 6f, 70f, 40, 8f, playerLOSLayermask);
						}
					}
					TargetTree = AllTreeNodes[num2];
					break;
				}
				if (TargetTree == null)
				{
					for (int num5 = 0; num5 < AllTreeNodes.Count; num5++)
					{
						if (AllTreeNodes[num5].gameObject == targetTree)
						{
							continue;
						}
						float num3 = Vector3.Distance(AllTreeNodes[num5].transform.position, base.transform.position);
						if (num3 < 75f && num3 > Mathf.Min(16f, num - 10f))
						{
							for (int num6 = 0; num6 < StartOfRound.Instance.allPlayerScripts.Length; num6++)
							{
								if (StartOfRound.Instance.allPlayerScripts[num6].isPlayerControlled && !StartOfRound.Instance.allPlayerScripts[num6].isInsideFactory)
								{
									StartOfRound.Instance.allPlayerScripts[num6].HasLineOfSightToPosition(AllTreeNodes[num5].transform.position + Vector3.up * 6f, 58f, 18, 6f, playerLOSLayermask);
								}
							}
						}
						TargetTree = AllTreeNodes[num5];
						break;
					}
				}
				if (TargetTree == null)
				{
					Debug.Log("Puma: Unable to relocate");
					relocatingToNewTree = false;
					TargetTree = targetTree;
					return false;
				}
				Debug.Log("Puma Relocating! Set target tree to '" + TargetTree.gameObject.name + "'", TargetTree.gameObject);
			}
			if (TargetTree != null)
			{
				Debug.Log("Puma dropped from tree! Target tree: '" + TargetTree.gameObject.name + "'", TargetTree.gameObject);
			}
			else
			{
				Debug.Log("Puma dropped from tree! Target tree null");
			}
			approachTreeRotation = base.transform.eulerAngles.y;
			leapFromPosition = base.transform.position;
			clingIdlePosition = navMeshPosition;
			RoundManager.Instance.tempTransform.rotation = base.transform.rotation;
			RoundManager.Instance.tempTransform.Rotate(0f, 90f, 0f);
			treeClingRotation = RoundManager.Instance.tempTransform.eulerAngles.y;
			leapTimer = 0f;
			disqualifiedCurrentTreeFromDrop = false;
			treeState = TreeState.DroppingDown;
			growlSource.PlayOneShot(dropSFX);
			return true;
		}
		Debug.Log("Attempted to drop but unable. Adding tree '" + TargetTree.gameObject.name + "' to excluded trees", TargetTree.gameObject);
		excludeTrees.Add(TargetTree);
		if (EndTargetTree != null && TargetTree == EndTargetTree)
		{
			EndTargetTree = null;
		}
		relocatingToNewTree = false;
		return false;
	}

		[Rpc(SendTo.NotMe)]
		public void StartTreeClimbRpc(Vector3 treeBottomPos, Vector3 treeTopPos, float clingRotation)
		{
			Debug.Log($"Received tree climb RPC {treeBottomPos} ; {treeTopPos} ; {clingRotation} ; {wasRunningForTree}");
			treeTopPosition = treeTopPos;
			treeBottomPosition = treeBottomPos;
			clingingToTree = true;
			sentTreeClimbRPC = true;
			startClimbingDelay = 0f;
			treeState = TreeState.StartingClimb;
			approachTreePosition = base.transform.position;
			approachTreeRotation = base.transform.eulerAngles.y;
			treeClingRotation = clingRotation;
			relocatingToNewTree = false;
			creatureSFX.Stop();
			PlayClimbTreeEffect(treeBottomPosition);
		}

	private bool StartTreeClimbOnLocalClient()
	{
		if (!base.IsOwner)
		{
			return false;
		}
		clingingToTree = true;
		startClimbingDelay = 0f;
		treeState = TreeState.StartingClimb;
		approachTreePosition = base.transform.position;
		approachTreeRotation = base.transform.eulerAngles.y;
		CapsuleCollider component = TargetTree.GetComponent<CapsuleCollider>();
		if (component == null)
		{
			Debug.Log("Puma: Tree has no capsule collider. Adding tree '" + TargetTree.gameObject.name + "' to excluded trees (Click to see)", TargetTree.gameObject);
			excludeTrees.Add(TargetTree);
			TargetTree = null;
			clingingToTree = false;
			return false;
		}
		float num = component.radius * Mathf.Max(TargetTree.transform.lossyScale.x, TargetTree.transform.lossyScale.z);
		Debug.Log($"Tree width: {num}");
		treeBottomPosition = new Ray(TargetTree.transform.position, new Vector3(approachTreePosition.x, TargetTree.transform.position.y, approachTreePosition.z) - TargetTree.transform.position).GetPoint(num + 0.2f);
		Debug.Log($"Dist: {Vector3.Distance(treeBottomPosition, TargetTree.transform.position)}");
		Debug.DrawLine(treeBottomPosition, TargetTree.transform.position, Color.yellow, 10f);
		Debug.Log($"treeBottomPosition magnitude: {treeBottomPosition.magnitude} (-.35f : {treeBottomPosition.magnitude - 0.35f})");
		if (Vector3.Distance(destination, TargetTree.transform.position) > 4f)
		{
			Debug.LogError("Puma error: destination is far away from target tree. This is not supposed to happen?");
			destination = base.transform.position;
		}
		float num2 = Vector3.Distance(treeBottomPosition, TargetTree.transform.position);
		Debug.Log($"Distance from startingPos to Target tree: {num2}");
		Ray ray = new Ray(treeBottomPosition, TargetTree.transform.position - treeBottomPosition);
		Debug.DrawLine(treeBottomPosition, TargetTree.transform.position, Color.yellow, 10f);
		RoundManager.Instance.tempTransform.position = treeBottomPosition;
		RoundManager.Instance.tempTransform.LookAt(TargetTree.transform.position);
		float num3 = treeClingRotation;
		treeClingRotation = RoundManager.Instance.tempTransform.eulerAngles.y;
		for (int i = 0; i < 20; i++)
		{
			ray.origin += new Vector3(0f, 2f, 0f);
			if (!Physics.Raycast(ray, num2, 33554432, QueryTriggerInteraction.Ignore))
			{
				if (i < 4)
				{
					Debug.Log("Puma: Tree is too short off the ground. Adding tree '" + TargetTree.gameObject.name + "' to excluded trees (Click to see)", TargetTree.gameObject);
					excludeTrees.Add(TargetTree);
					TargetTree = null;
					clingingToTree = false;
					treeClingRotation = num3;
					return false;
				}
				Debug.DrawRay(ray.origin, ray.direction * num2, Color.red, 5f);
				break;
			}
			treeTopPosition = ray.origin - Vector3.up * 8f;
			Debug.DrawRay(ray.origin, ray.direction * num2, Color.green, 5f);
		}
		if (Vector3.Distance(treeTopPosition, treeBottomPosition) < 4f || treeBottomPosition.y > treeTopPosition.y)
		{
			Debug.Log("Adding tree '" + TargetTree.gameObject.name + "' to excluded trees (Click to see)", TargetTree.gameObject);
			excludeTrees.Add(TargetTree);
			TargetTree = null;
			clingingToTree = false;
			treeClingRotation = num3;
			return false;
		}
		relocatingToNewTree = false;
		creatureSFX.Stop();
		PlayClimbTreeEffect(treeBottomPosition);
		return true;
	}

	private void PlayClimbTreeEffect(Vector3 pos)
	{
		leafParticle.transform.position = pos;
		leafParticle.Play();
		if (UnityEngine.Random.Range(0, 100) < 50)
		{
			creatureSFX.PlayOneShot(climbTreeSFX1);
			WalkieTalkie.TransmitOneShotAudio(creatureSFX, climbTreeSFX1);
		}
		else
		{
			creatureSFX.PlayOneShot(climbTreeSFX2);
			WalkieTalkie.TransmitOneShotAudio(creatureSFX, climbTreeSFX2);
		}
		if (Vector3.Distance(base.transform.position, StartOfRound.Instance.audioListener.transform.position) < 16f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Long);
		}
	}

	private void PlayLeapEffect()
	{
		if (Vector3.Angle(GameNetworkManager.Instance.localPlayerController.playerEye.forward, base.transform.position - GameNetworkManager.Instance.localPlayerController.playerEye.position) < 100f)
		{
			if (UnityEngine.Random.Range(0, 100) < 50)
			{
				creatureSFX.PlayOneShot(pumaLeapSFX1);
			}
			else
			{
				creatureSFX.PlayOneShot(pumaLeapSFX2);
			}
		}
	}

	public override void OnDrawGizmos()
	{
		base.OnDrawGizmos();
		if (!debugEnemyAI)
		{
			return;
		}
		if (currentBehaviourStateIndex == 0 && clingingToTree)
		{
			for (int i = 0; i < playerKnowledge.Length; i++)
			{
				if (playerKnowledge[i].playerScript.isPlayerControlled && !playerKnowledge[i].playerScript.isInsideFactory)
				{
					float angle = playerTreeLOSClose;
					float distance = playerTreeLOSDistanceClose;
					VisionConeGizmo.DrawLOS(playerKnowledge[i].playerScript.playerEye, angle, distance, Color.cyan);
					angle = playerTreeLOSFar;
					distance = playerTreeLOSDistanceFar;
					VisionConeGizmo.DrawLOS(playerKnowledge[i].playerScript.playerEye, angle, distance, Color.red);
				}
			}
		}
		else if (currentBehaviourStateIndex == 1)
		{
			for (int j = 0; j < playerKnowledge.Length; j++)
			{
				if (playerKnowledge[j].playerScript.isPlayerControlled && !playerKnowledge[j].playerScript.isInsideFactory)
				{
					float angle2;
					float distance2;
					if (!hidingQuickly)
					{
						angle2 = 67f;
						distance2 = 90f;
						VisionConeGizmo.DrawLOS(playerKnowledge[j].playerScript.playerEye, angle2, distance2, Color.yellow);
						continue;
					}
					angle2 = 70f;
					distance2 = 50f;
					VisionConeGizmo.DrawLOS(playerKnowledge[j].playerScript.playerEye, angle2, distance2, Color.yellow);
					angle2 = 27f;
					distance2 = 50f;
					VisionConeGizmo.DrawLOS(playerKnowledge[j].playerScript.playerEye, angle2, distance2, Color.yellow);
				}
			}
		}
		if (EndTargetTree != null)
		{
			Gizmos.DrawSphere(EndTargetTree.transform.position + Vector3.up * (14f + Mathf.PingPong(Time.realtimeSinceStartup, 7f)), 7f);
		}
		if (TargetTree != null)
		{
			Gizmos.DrawSphere(TargetTree.transform.position + Vector3.up * 6f, 1f);
			Gizmos.DrawWireCube(TargetTree.transform.position + Vector3.up * 7f, new Vector3(4f, 14f, 4f));
		}
		for (int k = 0; k < debugIncompatibleTrees.Count; k++)
		{
			Gizmos.DrawWireSphere(debugIncompatibleTrees[k].position + Vector3.up * (22f + Mathf.PingPong(Time.realtimeSinceStartup, 5f)), 8f);
		}
		for (int l = 0; l < debugIncompatibleTreesB.Count; l++)
		{
			Gizmos.DrawWireCube(debugIncompatibleTreesB[l].position + Vector3.up * 22f, new Vector3(6f, 6f, 6f));
		}
	}

	public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesNoisePlayedInOneSpot = 0, int noiseID = 0)
	{
		base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
		if (noiseID == 7 || noiseID == 546 || currentBehaviourStateIndex == 0)
		{
			return;
		}
		Debug.Log($"Hearing noise from {noisePosition} with id {noiseID} 2");
		if (clingingToTreeAnim)
		{
			return;
		}
		Debug.Log($"Hearing noise from {noisePosition} with id {noiseID} 3");
		if (Time.realtimeSinceStartup - timeAtLastHeardNoise < 2f)
		{
			return;
		}
		Debug.Log($"Hearing noise from {noisePosition} with id {noiseID} 4");
		if (Vector3.Distance(noisePosition, base.transform.position + Vector3.up * 0.4f) < 0.75f)
		{
			return;
		}
		Debug.Log($"Hearing noise from {noisePosition} with id {noiseID} A");
		if (Vector3.Angle(noisePosition - eye.position, eye.forward) < pumaLOS && !Physics.Linecast(eye.position, noisePosition + Vector3.up * 0.7f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			return;
		}
		Debug.Log($"Hearing noise from {noisePosition} with id {noiseID} B; angle: {Vector3.Angle(noisePosition - eye.position, eye.forward)}; los: {!Physics.Linecast(eye.position, noisePosition + Vector3.up * 0.7f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)}");
		for (int i = 0; i < playerKnowledge.Length; i++)
		{
			if (hidingQuickly)
			{
				if (!playerKnowledge[i].seenByPumaThroughTrees)
				{
					continue;
				}
			}
			else if (!playerKnowledge[i].seenByPuma)
			{
				continue;
			}
			Debug.Log($"player knowledge {i} '{playerKnowledge[i].playerScript.playerUsername}' is seen by puma; {playerKnowledge[i].seenByPuma}/ {playerKnowledge[i].seenByPumaThroughTrees}");
			if (Vector3.Distance(playerKnowledge[i].playerScript.transform.position, noisePosition) < 1.5f)
			{
				return;
			}
			Debug.Log($"dist to playerknowledge {i}: {Vector3.Distance(playerKnowledge[i].playerScript.transform.position, noisePosition)}");
		}
		if (stunNormalizedTimer > 0f)
		{
			return;
		}
		float num = Vector3.Distance(noisePosition, base.transform.position);
		float num2 = noiseLoudness / num;
		if (Physics.Linecast(base.transform.position, noisePosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			num2 *= 0.5f;
		}
		num2 *= hearingMultiplier;
		if (pingAttentionTimer > 0f)
		{
			if (focusLevel >= 3)
			{
				if (num > 8f || num2 <= 0.15f)
				{
					return;
				}
			}
			else if (focusLevel == 2 && (num > 17f || num2 < 0.12f))
			{
				return;
			}
		}
		else if (num > 20f || num2 <= 0.06f)
		{
			return;
		}
		if (!(num2 <= 0.03f))
		{
			timeAtLastHeardNoise = Time.realtimeSinceStartup;
			int newFocusLevel = 2;
			bool flag = false;
			float timeToLook = 0.6f;
			if ((num2 > 0.17f && num < 15f) || (num < 9f && noiseID == 75))
			{
				flag = true;
				timeToLook = 0.9f;
				newFocusLevel = 3;
			}
			if (!stalkingFrozen || flag)
			{
				PingAttention(newFocusLevel, timeToLook, noisePosition + Vector3.up * 0.7f, flag);
			}
		}
	}

	public void PingAttention(int newFocusLevel, float timeToLook, Vector3 attentionPosition, bool startledAnim, bool sync = true)
	{
		if (newFocusLevel != 15)
		{
			if ((pingAttentionTimer >= 0f && newFocusLevel < focusLevel) || currentBehaviourStateIndex == 0 || (currentBehaviourStateIndex == 1 && timeSincePingingAttention < 0.33f))
			{
				return;
			}
			if (currentBehaviourStateIndex == 2 && timeSincePingingAttention < 3f)
			{
				timeToLook *= 0.5f;
				return;
			}
		}
		Debug.Log($"Puma: pinged attention to position; startledanim: {startledAnim}");
		Debug.DrawLine(eye.position, attentionPosition, Color.yellow, timeToLook);
		focusLevel = newFocusLevel;
		pingAttentionTimer = timeToLook;
		timeSincePingingAttention = 0f;
		pingAttentionPosition = attentionPosition;
		if (startledAnim)
		{
			if (startledTimer > 0f)
			{
				startledAnim = false;
			}
			else
			{
				startledTimer = timeToLook;
			}
		}
		if (sync)
		{
			PingPumaAttentionRpc(timeToLook, attentionPosition, startledAnim);
		}
	}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void PingPumaAttentionRpc(float timeToLook, Vector3 attentionPosition, bool setStartled = false)
		{
			Debug.Log($"Ping attention startle RPC; {setStartled}");
			pingAttentionTimer = timeToLook;
			pingAttentionPosition = attentionPosition;
			timeSincePingingAttention = 0f;
			if (setStartled)
			{
				startledTimer = timeToLook;
			}
		}

	private bool IsPlayerThreatening(PlayerControllerB playerScript, bool isStartled = false)
	{
		if (playerScript.inSpecialInteractAnimation || (bool)playerScript.inAnimationWithEnemy)
		{
			return false;
		}
		float num = Vector3.Distance(playerScript.transform.position, base.transform.position);
		if (isStartled && num < 7.5f)
		{
			return true;
		}
		if (playerScript.TryGetComponent<IVisibleThreat>(out var component))
		{
			int threatLevel = component.GetThreatLevel(eye.position);
			Debug.Log($"Puma: Assessing threat level of '{playerScript.playerUsername}'. ThreatLevel: {threatLevel}");
			if (isStartled)
			{
				if ((num < 22f && threatLevel > 4) || (num < 12f && threatLevel > 2))
				{
					return true;
				}
			}
			else if ((num < 16f && threatLevel > 8) || (num < 12f && threatLevel > 6))
			{
				return true;
			}
		}
		return false;
	}

	private void UpdatePlayerKnowledge()
	{
		playersSeeingPuma = 0;
		pumaLOS = 80f;
		if (targetPlayer != null && currentBehaviourStateIndex == 1 && !stalkingFrozen && !hidingQuickly)
		{
			if (Vector3.Distance(base.transform.position, targetPlayer.transform.position) < 25f)
			{
				pumaLOS = Mathf.Lerp(30f, 70f, Mathf.Clamp(Vector3.Distance(base.transform.position, targetPlayer.transform.position) / 25f, 0f, 1f));
			}
			if (pingAttentionTimer > 0f)
			{
				pumaLOS = Mathf.Max(pumaLOS, Mathf.Lerp(120f, pumaLOS, Mathf.Clamp(pingAttentionTimer / 1.2f, 0f, 1f)));
			}
		}
		for (int i = 0; i < playerKnowledge.Length; i++)
		{
			if (!playerKnowledge[i].playerScript.isPlayerControlled)
			{
				playerKnowledge[i].timeSinceSeeingPuma = 1000f;
				playerKnowledge[i].timeSincePumaSeeing = 1000f;
				playerKnowledge[i].seenByPuma = false;
				continue;
			}
			if (playerKnowledge[i].playerScript.isInsideFactory)
			{
				playerKnowledge[i].timeSinceSeeingPuma += Time.deltaTime;
				playerKnowledge[i].timeSincePumaSeeing += Time.deltaTime;
				playerKnowledge[i].seenByPuma = false;
				continue;
			}
			bool flag = playerKnowledge[i].seenByPuma;
			if (hidingQuickly)
			{
				flag = playerKnowledge[i].seenByPumaThroughTrees;
			}
			playerKnowledge[i].seenByPumaThroughTrees = Vector3.Distance(playerKnowledge[i].playerScript.playerEye.transform.position, eye.position) < 35f && Vector3.Angle(playerKnowledge[i].playerScript.playerEye.transform.position - eye.position, eye.forward) < pumaLOS;
			playerKnowledge[i].seenByPuma = playerKnowledge[i].seenByPumaThroughTrees && !Physics.Linecast(eye.transform.position, playerKnowledge[i].playerScript.playerEye.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore);
			bool flag2 = playerKnowledge[i].seenByPumaThroughTrees;
			if (!hidingQuickly)
			{
				flag2 = playerKnowledge[i].seenByPuma;
			}
			int range = 40;
			if (currentBehaviourStateIndex == 1)
			{
				range = 70;
			}
			int layerMask = -1;
			if (clingingToTree)
			{
				layerMask = playerLOSLayermask;
			}
			float num = playerKnowledge[i].playerScript.LineOfSightToPositionAngle(eye.position, range, -1f, layerMask);
			float num2 = playerKnowledge[i].playerScript.LineOfSightToPositionAngle(butt.position, range, -1f, layerMask);
			if (num == -361f)
			{
				playerKnowledge[i].seeAngle = num2;
			}
			else if (num2 == -361f)
			{
				playerKnowledge[i].seeAngle = num;
			}
			else
			{
				playerKnowledge[i].seeAngle = Mathf.Min(num, num2);
			}
			if (playerKnowledge[i].seeAngle != -361f && playerKnowledge[i].seeAngle < 70f && !playerKnowledge[i].playerScript.inSpecialInteractAnimation)
			{
				playerKnowledge[i].timeSinceSeeingPuma = 0f;
			}
			else
			{
				playerKnowledge[i].timeSinceSeeingPuma += Time.deltaTime;
			}
			if (playerKnowledge[i].timeSinceSeeingPuma < 1f)
			{
				playersSeeingPuma++;
			}
			if (playerKnowledge[i].playerScript.inSpecialInteractAnimation)
			{
				playerKnowledge[i].seeAngle = -361f;
			}
			if (flag2 && !clingingToTree)
			{
				if (base.IsOwner && !flag && playerKnowledge[i].timeSincePumaSeeing > 0.7f && Vector3.Angle(base.transform.position - playerKnowledge[i].playerScript.playerEye.position, playerKnowledge[i].playerScript.playerEye.forward) < 110f && IsPlayerThreatening(playerKnowledge[i].playerScript, startled))
				{
					Debug.Log($"Puma: became aware of threatening player. Startled already?: {startled}");
					PingAttention(15, 0.6f, playerKnowledge[i].playerScript.playerEye.position, startledAnim: true);
					runAfterStartledTimer = true;
				}
				playerKnowledge[i].timeSincePumaSeeing = 0f;
			}
			else
			{
				playerKnowledge[i].timeSincePumaSeeing += Time.deltaTime;
			}
		}
	}

	public override void Update()
	{
		base.Update();
		clearDebugTextInterval -= Time.deltaTime;
		if (isEnemyDead)
		{
			footstepSource.Stop();
			agent.speed = 0f;
			agent.acceleration = 50f;
			stalkingFrozenAnim = false;
			stalkingFrozen = false;
			return;
		}
		if (inSpecialAnimation && base.IsOwner)
		{
			if (updateDestinationIntervalB >= 0f)
			{
				updateDestinationIntervalB -= Time.deltaTime;
			}
			else
			{
				DoAIInterval();
				updateDestinationIntervalB = AIIntervalTime + UnityEngine.Random.Range(-0.015f, 0.015f);
			}
		}
		pingAttentionTimer -= Time.deltaTime;
		timeSincePingingAttention += Time.deltaTime;
		UpdatePlayerKnowledge();
		switch (currentBehaviourStateIndex)
		{
		case 0:
			if (previousState != currentBehaviourStateIndex)
			{
				if (!sentTreeClimbRPC)
				{
					clingingToTree = false;
				}
				wasRunningForTree = false;
				stopStalkingTimer = 0f;
				stalkingFrozen = false;
				stalkingFrozenAnim = false;
				inSpecialAnimation = false;
				targetPlayer = null;
				movingTowardsTargetPlayer = false;
				goToShipTimer = Time.realtimeSinceStartup;
				scaredMeter = 0f;
				angerMeter = 0f;
				seenByPlayersMeter = 0f;
				previousClosestNode = null;
				relocatingToNewTree = false;
				disqualifiedCurrentTreeFromDrop = false;
				creatureAnimator.SetBool("Attacking", value: false);
				creatureAnimator.SetBool("Startled", value: false);
				startled = false;
				RemoveCurrentStalkNode();
				RoundManager.PlayRandomClip(creatureVoice, pumaGrowlSFX, randomize: true, 1f, 931208);
				previousState = currentBehaviourStateIndex;
				if (base.IsOwner && !base.IsHost)
				{
					ChangeOwnershipOfEnemy(StartOfRound.Instance.allPlayerScripts[0].actualClientId);
					break;
				}
				if (base.IsOwner)
				{
					ChooseTargetTree();
				}
			}
			if (base.IsOwner && !base.IsHost)
			{
				Debug.Log($"Is not host! Setting ownership to host.\nOur clientId:{GameNetworkManager.Instance.localPlayerController.actualClientId}.\nHost client id: {StartOfRound.Instance.allPlayerScripts[0].actualClientId}\ncurrentOwnershipOnThisClient: {currentOwnershipOnThisClient}");
				ChangeOwnershipOfEnemy(StartOfRound.Instance.allPlayerScripts[0].actualClientId);
				break;
			}
			if (Time.realtimeSinceStartup - timeAtLastDrop < 1f && targetPlayer != null && Vector3.Distance(targetPlayer.transform.position, base.transform.position) < 5.25f)
			{
				SwitchToBehaviourState(2);
				break;
			}
			if (attackSFX.isPlaying)
			{
				attackSFX.volume = Mathf.Lerp(attackSFX.volume, -0.4f, Time.deltaTime);
				attackSFXFaraway.volume = Mathf.Lerp(attackSFX.volume, -0.4f, Time.deltaTime);
				if (attackSFX.volume < 0.02f)
				{
					attackSFX.Stop();
					attackSFXFaraway.Stop();
				}
			}
			if (!clingingToTree)
			{
				if (!wasRunningForTree)
				{
					Debug.Log("Puma: running anim set to true A");
					creatureAnimator.SetBool("Running", value: true);
					creatureAnimator.SetBool("Climbing", value: false);
					treeHidingTimer = 4f;
					wasRunningForTree = true;
					firstLeap = true;
					leapAttempts = 0;
					creatureSFX.clip = pumaRunSFX;
					creatureSFX.Play();
				}
				lookingAtObjects = false;
				agent.speed = 24f;
				agent.acceleration = 60f;
				inSpecialAnimation = false;
				if (base.IsOwner && TargetTree != null && Vector3.Distance(base.transform.position, TargetTree.transform.position) < 4f && StartTreeClimbOnLocalClient())
				{
					StartTreeClimbRpc(treeBottomPosition, treeTopPosition, treeClingRotation);
				}
				break;
			}
			sentTreeClimbRPC = false;
			if (TargetTree == null && base.IsOwner)
			{
				AllTreeNodes.Remove(TargetTree);
				if (treeState == TreeState.DroppingDown)
				{
					break;
				}
				Debug.Log("Puma: Target tree is null! Set to dropping down");
				if (StartTreeDropOnLocalClient(relocate: true))
				{
					Debug.Log("Puma: Dropping down; found Target Tree to relocate to: " + TargetTree.gameObject.name, TargetTree.gameObject);
					StartTreeDropRpc(clingIdlePosition, leapFromPosition, treeClingRotation, relocate: true);
					break;
				}
				relocatingToNewTree = false;
				Debug.Log("Puma: Target tree was destroyed while puma is climbing, but the puma could not drop down successfully!");
				if (treeState == TreeState.Idle)
				{
					treeHidingTimer = 4f;
					if (StartLeapToTree(hiding: true))
					{
						StartLeapToTreeRpc(leapFromPosition, clingIdlePosition, treeClingRotation, leapAnimationB);
					}
				}
			}
			else
			{
				if (wasRunningForTree || (!creatureAnimator.GetBool("TreeMode") && treeState != TreeState.DroppingDown))
				{
					Debug.Log("Puma: running anim set to false B");
					creatureAnimator.SetBool("Running", value: false);
					creatureAnimator.SetBool("TreeMode", value: true);
					clingingToTreeAnim = true;
					wasRunningForTree = false;
					inSpecialAnimation = true;
					agent.enabled = false;
					growlSource.Stop();
				}
				if (treeState != TreeState.ClimbingUp && treeState != TreeState.StartingClimb)
				{
					treeHidingTimer -= Time.deltaTime;
				}
				RunTreeMode();
			}
			break;
		case 1:
		{
			if (previousState != currentBehaviourStateIndex)
			{
				TargetTree = null;
				clingingToTree = false;
				wasOwnerLastFrame = false;
				beginStalkFreezeTimer = 0f;
				seenByPlayersMeter = 0f;
				runAfterStartledTimer = false;
				startledTimer = 0f;
				startled = false;
				timeSincePlayersSeeingPumaWhileClose = 0f;
				growlSource.Play();
				playedFearNoise = false;
				disableAgentVelocity = false;
				stopStalkingTimer = 0f;
				leapAttempts = 0;
				scaredMeter = 0f;
				angerMeter = 0f;
				Debug.Log("Puma: running anim set to false C");
				creatureAnimator.SetBool("Running", value: false);
				creatureAnimator.SetBool("Attacking", value: false);
				creatureAnimator.SetBool("Climbing", value: false);
				creatureAnimator.SetBool("TreeMode", value: false);
				creatureAnimator.SetBool("Landed", value: true);
				movingTowardsTargetPlayer = false;
				previousState = currentBehaviourStateIndex;
			}
			if (!base.IsOwner)
			{
				targetPlayer = StartOfRound.Instance.allPlayerScripts[StartOfRound.Instance.ClientPlayerList[base.OwnerClientId]];
				disableAgentVelocity = false;
				frozenInDangerRange = false;
				beginStalkFreezeTimer = 0f;
				stopStalkingTimer = 0f;
				stalkFrozenAttackTimer = 0f;
			}
			else if (targetPlayer != null && targetPlayer != GameNetworkManager.Instance.localPlayerController)
			{
				ChangeOwnershipOfEnemy(targetPlayer.actualClientId);
				break;
			}
			if (targetPlayer == null)
			{
				break;
			}
			targetNullCounter = 0f;
			if (base.IsOwner && agent.enabled && disableAgentVelocity)
			{
				agent.velocity = Vector3.zero;
				disableAgentVelocity = false;
			}
			CalculateAnimationDirection();
			if (attackSFX.isPlaying)
			{
				attackSFX.volume = Mathf.Lerp(attackSFX.volume, -0.4f, Time.deltaTime);
				attackSFXFaraway.volume = Mathf.Lerp(attackSFX.volume, -0.4f, Time.deltaTime);
				if (attackSFX.volume < 0.02f)
				{
					attackSFX.Stop();
					attackSFXFaraway.Stop();
				}
			}
			if (startledTimer > 0f || stunNormalizedTimer > 0f)
			{
				startledTimer -= Time.deltaTime;
				if (stunNormalizedTimer > 0f)
				{
					runAfterStartledTimer = true;
				}
				if (!startled)
				{
					startled = true;
					agent.acceleration = 500f;
					agent.speed = 0f;
					creatureAnimator.SetBool("Startled", value: true);
				}
				break;
			}
			if (startled)
			{
				startled = false;
				creatureAnimator.SetBool("Startled", value: false);
				if (runAfterStartledTimer && base.IsOwner)
				{
					clearDebugTextInterval = 8f;
					SwitchToBehaviourState(0);
					break;
				}
			}
			bool flag2 = false;
			if (targetPlayer != null && Vector3.Angle(Vector3.Scale(targetPlayer.GetPlayerVelocity(), new Vector3(1f, 0f, 1f)), Vector3.Scale(base.transform.position - targetPlayer.transform.position, new Vector3(1f, 0f, 1f))) < 76f && targetPlayer.isSprinting)
			{
				flag2 = true;
			}
			float num3 = Vector3.Distance(eye.position, targetPlayer.transform.position);
			bool flag3 = true;
			bool flag4 = false;
			float num4 = 0f;
			float num5 = 0f;
			bool flag5 = false;
			for (int i = 0; i < playerKnowledge.Length; i++)
			{
				if (!playerKnowledge[i].playerScript.isPlayerControlled || playerKnowledge[i].playerScript.isInsideFactory)
				{
					continue;
				}
				if (stalkingFrozen || hidingQuickly)
				{
					Debug.Log($"Stalking frozen. Client #{i} A; seenByPumaThroughTrees: {playerKnowledge[i].seenByPumaThroughTrees}; hidingQuickly: {hidingQuickly}");
					if (!playerKnowledge[i].seenByPumaThroughTrees)
					{
						continue;
					}
					float num6 = playerKnowledge[i].seeAngle;
					float num7 = 1f;
					if (hidingQuickly)
					{
						num6 = Vector3.Angle(eye.position - playerKnowledge[i].playerScript.playerEye.transform.position, playerKnowledge[i].playerScript.playerEye.transform.forward);
						if (num6 > 27f && num6 < 70f)
						{
							flag3 = false;
							seenByPlayersMeter = Mathf.Max(0f, seenByPlayersMeter - Time.deltaTime * 1.4f);
							continue;
						}
						if (playerKnowledge[i].seeAngle == -361f)
						{
							num7 = 0.7f;
						}
					}
					else if (num6 == -361f || !playerKnowledge[i].seenByPuma)
					{
						continue;
					}
					if (!(num6 < 70f) || num6 == -361f)
					{
						continue;
					}
					flag3 = false;
					if (playerKnowledge[i].playerScript == GameNetworkManager.Instance.localPlayerController)
					{
						flag5 = true;
					}
					if (base.IsOwner)
					{
						if (num6 < 25f && num3 < 3.5f)
						{
							Debug.Log("Player was within 3.5 meters and made eye contact. Attack!");
							if (targetPlayerKnowledge.timeSincePumaSeeing < 0.4f && flag2)
							{
								scaredMeter = 2f;
							}
							SwitchToBehaviourState(2);
							break;
						}
						if (stalkingFrozen && targetPlayer != null && Vector3.Distance(targetPlayer.transform.position, base.transform.position) < DangerRange + 8f && num6 >= 25f)
						{
							stalkFrozenAttackTimer += Time.deltaTime;
							if (stalkFrozenAttackTimer > 1.5f)
							{
								stalkingFrozen = false;
								stalkingFrozenAnim = false;
								inSpecialAnimation = false;
								agent.acceleration = 26f;
								disableAgentVelocity = true;
								agent.velocity = Vector3.zero;
								stopStalkingTimer = 0f;
								stalkingCharge = true;
								Debug.Log($"Send Stalking charge RPC #{i}");
								SyncStalkingChargeRpc();
								break;
							}
						}
					}
					float num8 = Vector3.Distance(playerKnowledge[i].playerScript.transform.position, base.transform.position);
					if ((!(num8 < DangerRange - 3f) || !(num6 > 26f)) && !(num6 > 15f))
					{
						if (num8 < 8f)
						{
							num7 += 0.6f;
						}
						flag4 = true;
						num5 = Time.deltaTime / (num6 * 0.2f) / (Vector3.Distance(playerKnowledge[i].playerScript.transform.position, base.transform.position) * 0.03f) * num7;
						if (num5 > num4)
						{
							num4 = num5;
						}
					}
				}
				else
				{
					if (!playerKnowledge[i].seenByPuma || !(playerKnowledge[i].seeAngle < 67f) || playerKnowledge[i].seeAngle == -361f || !(Vector3.Distance(playerKnowledge[i].playerScript.transform.position, base.transform.position) < 90f))
					{
						continue;
					}
					flag3 = false;
					if ((stalkingCharge && playerKnowledge[i].seeAngle > 27f) || !base.IsOwner || stalkingFrozen)
					{
						continue;
					}
					beginStalkFreezeTimer += Time.deltaTime;
					if (beginStalkFreezeTimer > 0.08f)
					{
						beginStalkFreezeTimer = 0f;
						stalkingFrozen = true;
						SetDestinationToPosition(base.transform.position);
						stalkingCharge = false;
						agent.acceleration = 500f;
						growlSource.Stop();
						if (targetPlayer != null && Vector3.Distance(base.transform.position, targetPlayer.transform.position) < DangerRange)
						{
							frozenInDangerRange = true;
							stalkFrozenAttackTimer = 0f;
						}
						Debug.Log($"Send Stalk frozen RPC on local client! #{i}");
						SyncStalkingFrozenRpc(frozen: true);
					}
				}
			}
			if (flag4)
			{
				if (flag5 && seenByPlayersMeter > 2.35f && !playedFearNoise)
				{
					playedFearNoise = true;
					if (base.IsOwner && Vector3.Distance(base.transform.position, targetPlayer.transform.position) < DangerRange)
					{
						GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.5f);
					}
					else if (Vector3.Distance(base.transform.position, GameNetworkManager.Instance.localPlayerController.transform.position) < 22f)
					{
						GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.2f);
					}
				}
				seenByPlayersMeter = Mathf.Min(20f, seenByPlayersMeter + num4);
			}
			if (!base.IsOwner)
			{
				break;
			}
			if (seenByPlayersMeter >= 16f)
			{
				clearDebugTextInterval = 8f;
				SwitchToBehaviourState(0);
			}
			else if (seenByPlayersMeter > 2f && !hidingQuickly)
			{
				if (FindHidingSpot())
				{
					Debug.Log("Puma: Set to hiding quickly");
					hidingQuickly = true;
					playedFearNoise = false;
					agent.speed = 14f;
					agent.acceleration = 60f;
					stopStalkingTimer = 0f;
					SyncHidingQuicklyRpc(hiding: true);
					growlSource.Play();
					agent.SetDestination(destination);
					if (stalkingFrozen)
					{
						Debug.Log("Puma: Was stalking frozen; setting agent velocity to zero");
						stalkingFrozen = false;
						stalkingFrozenAnim = false;
						inSpecialAnimation = false;
						disableAgentVelocity = true;
						agent.velocity = Vector3.zero;
						Debug.Log($"Puma: Agent velocity: {agent.velocity}");
					}
				}
				else
				{
					agent.velocity = Vector3.zero;
					SwitchToBehaviourState(0);
				}
			}
			if (stalkingFrozenAnim)
			{
				agent.speed = 0f;
			}
			if (!(targetPlayer != null))
			{
				break;
			}
			if (num3 > 4f && num3 < 9f)
			{
				if (Vector3.Angle(Vector3.Scale(targetPlayer.GetPlayerVelocity(), new Vector3(1f, 0f, 1f)), Vector3.Scale(base.transform.position - targetPlayer.transform.position, new Vector3(1f, 0f, 1f))) < 40f && targetPlayer.isSprinting)
				{
					scaredMeter = Mathf.Clamp(scaredMeter + Time.deltaTime * 2f, 0f, 1f);
					if (scaredMeter >= 1f)
					{
						Debug.Log("Puma: Scared meter > 1; Running away!");
						clearDebugTextInterval = 8f;
						SwitchToBehaviourState(0);
						break;
					}
				}
				else
				{
					scaredMeter = Mathf.Clamp(scaredMeter - Time.deltaTime * 6f, 0f, 1f);
				}
			}
			else
			{
				scaredMeter = Mathf.Clamp(scaredMeter - Time.deltaTime * 3f, 0f, 1f);
			}
			if (flag3)
			{
				if (stalkingFrozen || hidingQuickly)
				{
					seenByPlayersMeter = 0f;
					hidingQuickly = false;
					if (eliminatedHideTrees.Count > 0)
					{
						eliminatedHideTrees.Clear();
					}
					hidingQuicklySeenMeterIncrease = 0f;
					Debug.Log("Puma: Set hiding quickly false A");
					if (frozenInDangerRange && Vector3.Distance(base.transform.position, targetPlayer.transform.position) < DangerRange)
					{
						SwitchToBehaviourState(2);
						break;
					}
					stalkingFrozen = false;
					playedFearNoise = false;
					stalkingFrozenAnim = false;
					inSpecialAnimation = false;
					stalkingCharge = false;
					agent.acceleration = 26f;
					disableAgentVelocity = true;
					agent.velocity = Vector3.zero;
					stopStalkingTimer = 0f;
					growlSource.Play();
					SyncStalkingFrozenRpc(frozen: false);
				}
				float b;
				if (num3 > 10f)
				{
					timeSincePlayersSeeingPumaWhileClose = Mathf.Max(timeSincePlayersSeeingPumaWhileClose - Time.deltaTime * 2f, 0f);
					b = ((!(timeSincePlayersSeeingPumaWhileClose > 6f)) ? 8f : 15f);
				}
				else
				{
					if (num3 < 2.5f)
					{
						if (targetPlayerKnowledge.timeSincePumaSeeing < 0.4f && flag2)
						{
							scaredMeter = 2f;
						}
						SwitchToBehaviourState(2);
						break;
					}
					bool flag6 = false;
					for (int j = 0; j < playerKnowledge.Length; j++)
					{
						if (playerKnowledge[j].seenByPuma && playerKnowledge[j].playerScript.isPlayerControlled && !playerKnowledge[j].playerScript.isInsideFactory && playerKnowledge[j].playerScript.HasLineOfSightToPosition(base.transform.position + Vector3.up * 2f, 80f, 20, -1f, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
						{
							flag6 = true;
							break;
						}
					}
					if (!flag6)
					{
						timeSincePlayersSeeingPumaWhileClose = Mathf.Min(timeSincePlayersSeeingPumaWhileClose + Time.deltaTime, 7f);
					}
					else
					{
						timeSincePlayersSeeingPumaWhileClose = Mathf.Max(timeSincePlayersSeeingPumaWhileClose - Time.deltaTime * 4f, 0f);
					}
					if (timeSincePlayersSeeingPumaWhileClose > 6f)
					{
						b = 12f;
					}
					else
					{
						agent.acceleration = 50f;
						b = ((num3 > 7f) ? 6f : ((!(num3 > 4.5f)) ? 9f : 5f));
					}
				}
				agent.speed = Mathf.Lerp(agent.speed, b, 3f * Time.deltaTime);
			}
			else if (stalkingCharge)
			{
				agent.acceleration = 35f;
				float b2;
				if (num3 > 7f)
				{
					b2 = 3.5f;
				}
				else if (num3 > 5f)
				{
					b2 = 5.5f;
				}
				else
				{
					b2 = 7f;
					agent.acceleration = 45f;
				}
				agent.speed = Mathf.Lerp(agent.speed, b2, 5f * Time.deltaTime);
			}
			break;
		}
		case 2:
		{
			if (previousState != currentBehaviourStateIndex)
			{
				TargetTree = null;
				clingingToTree = false;
				stalkingFrozen = false;
				stalkingFrozenAnim = false;
				hidingQuickly = false;
				inSpecialAnimation = false;
				wasOwnerLastFrame = false;
				stopStalkingTimer = 0f;
				leapAttempts = 0;
				attackSFX.clip = attackScream;
				growlSource.Stop();
				creatureSFX.clip = pumaRunSFX;
				creatureSFX.Play();
				attackSFX.pitch = UnityEngine.Random.Range(0.94f, 1.1f);
				angerMeter = 0f;
				goToShipTimer = Time.realtimeSinceStartup;
				timeSpentAttacking = 0f;
				startedScreamingInAttack = false;
				RemoveCurrentStalkNode();
				creatureAnimator.SetBool("Attacking", value: true);
				previousState = currentBehaviourStateIndex;
			}
			if (base.IsOwner)
			{
				if (targetPlayer == null || !PlayerIsTargetable(targetPlayer))
				{
					Debug.Log($"Puma: Stopped attacking. Reason: No targetable player; targetPlayer null:{targetPlayer == null}");
					clearDebugTextInterval = 8f;
					SwitchToBehaviourState(0);
					break;
				}
				timeSpentAttacking += Time.deltaTime;
				if (timeSpentAttacking > 5f && Time.realtimeSinceStartup - timeAtLastScratch > 2f)
				{
					Debug.Log("Puma: Stopped attacking. Reason: no scratches in the last 2 seconds");
					clearDebugTextInterval = 8f;
					SwitchToBehaviourState(0);
					break;
				}
				if (StartOfRound.Instance.hangarDoorsClosed && targetPlayer.isInHangarShipRoom && !isInsidePlayerShip)
				{
					Debug.Log("Puma: Stopped attacking. Reason: target player is inside closed ship");
					SwitchToBehaviourState(0);
					break;
				}
				if (scaredMeter >= 0.8f && timeSpentAttacking > 1.2f)
				{
					Debug.Log($"Puma: Stopped attacking. Reason: fearful; {scaredMeter}");
					SwitchToBehaviourState(0);
					break;
				}
				if (scaredMeter >= 2f && timeSpentAttacking > 0.5f)
				{
					Debug.Log($"Puma: Stopped attacking. Reason: VERY fearful; {scaredMeter}");
					SwitchToBehaviourState(0);
					break;
				}
			}
			if (targetPlayer == null && !base.IsOwner)
			{
				targetPlayer = StartOfRound.Instance.allPlayerScripts[StartOfRound.Instance.ClientPlayerList[base.OwnerClientId]];
			}
			agent.stoppingDistance = 2.6f;
			agent.speed = attackRunSpeed;
			agent.acceleration = attackAcceleration;
			if (startedScreamingInAttack)
			{
				attackSFX.volume = Mathf.Lerp(attackSFX.volume, 1f, 10f * Time.deltaTime);
				attackSFXFaraway.volume = Mathf.Lerp(attackSFXFaraway.volume, 1f, 10f * Time.deltaTime);
			}
			float num = Vector3.Distance(base.transform.position, targetPlayer.transform.position);
			if (num < 6f)
			{
				if (!startedScreamingInAttack)
				{
					startedScreamingInAttack = true;
					attackSFX.Play();
					attackSFXFaraway.Play();
				}
				float num2 = Vector3.Distance(base.transform.position, GameNetworkManager.Instance.localPlayerController.transform.position);
				if (num2 < 23f)
				{
					GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.26f);
				}
				else if (num2 < 50f)
				{
					GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.08f);
				}
			}
			scratching = num < 6f;
			creatureAnimator.SetBool("Scratching", scratching);
			if (!base.IsOwner)
			{
				break;
			}
			bool flag = true;
			if (num < agent.stoppingDistance + 1f)
			{
				agent.acceleration = 5f;
				if (agent.velocity.sqrMagnitude <= 0f && num < agent.stoppingDistance - 0.5f)
				{
					SetDestinationToPosition(base.transform.position + Vector3.Normalize((base.transform.position - targetPlayer.transform.position) * 1000f));
					flag = false;
				}
			}
			if (flag)
			{
				movingTowardsTargetPlayer = true;
			}
			break;
		}
		}
	}

	public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
		base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
		if (isEnemyDead)
		{
			return;
		}
		enemyHP -= force;
		timeSinceGettingHit = 0f;
		if (base.IsOwner)
		{
			if (currentBehaviourStateIndex == 2 || currentBehaviourStateIndex == 1)
			{
				SwitchToBehaviourState(0);
			}
			if (enemyHP <= 0)
			{
				KillEnemyOnOwnerClient();
			}
		}
	}

	public override void KillEnemy(bool destroy = false)
	{
		attackSFX.Stop();
		growlSource.Stop();
		footstepSource.Stop();
		creatureSFX.Stop();
		attackSFXFaraway.Stop();
		base.KillEnemy(destroy);
		if (clingingToTree)
		{
			inSpecialAnimation = true;
			if (!droppedFromTreeDead)
			{
				droppedFromTreeDead = true;
				StartCoroutine(FallFromTreeDead());
			}
		}
	}

	private IEnumerator FallFromTreeDead()
	{
		yield return null;
		if (Physics.Raycast(base.transform.position, Vector3.down, out hit, 80f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			float fallProgress = 0f;
			Vector3 startPos = base.transform.position;
			Vector3 endPos = hit.point;
			creatureSFX.PlayOneShot(dropSFX);
			while (fallProgress < 1f)
			{
				fallProgress = Mathf.Min(fallProgress + Time.deltaTime * 2f, 1f);
				base.transform.position = Vector3.Lerp(startPos, endPos, fallProgress / 1f);
				yield return null;
			}
			if (Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, base.transform.position) < 10f)
			{
				HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
			}
			clingingToTreeAnim = false;
			clingingToTree = false;
		}
	}

	public override void AnimationEventA()
	{
		if (!isEnemyDead && stalkingFrozen)
		{
			if (!stalkingFrozenAnim || !inSpecialAnimation)
			{
				Debug.Log("Puma: inSpecialAnimation set to True for stalk frozen.");
			}
			stalkingFrozenAnim = true;
			inSpecialAnimation = true;
		}
	}

	public override void AnimationEventB()
	{
		if (!(StartOfRound.Instance.audioListener.transform.position.y < -110f))
		{
			RoundManager.PlayRandomClip(footstepSource, pumaFootstepSFX, randomize: true, UnityEngine.Random.Range(0.2f, 1f));
		}
	}

	public override void AnimationEventC()
	{
		if (currentBehaviourStateIndex == 2)
		{
			timeAtLastScratch = Time.realtimeSinceStartup;
			RoundManager.PlayRandomClip(creatureSFX, scratchSFX, randomize: true, UnityEngine.Random.Range(0.6f, 1f));
		}
	}

	public override void OnCollideWithPlayer(Collider other)
	{
		base.OnCollideWithPlayer(other);
		PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
		if (playerControllerB != null && Time.realtimeSinceStartup - timeAtLastScratch < 3f)
		{
			timeAtLastScratch = 0f;
			Vector3 vector = Vector3.Normalize(playerControllerB.playerEye.transform.position - pushPoint.position) * scratchPushForce + UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0f, 10f);
			playerControllerB.externalForceAutoFade += vector;
			playerControllerB.DamagePlayer(7, hasDamageSFX: true, callRPC: true, CauseOfDeath.Scratching, 10, fallDamage: false, vector * 1.25f);
			playerControllerB.AddBloodToBody();
			PumaDamagePlayerRpc((int)playerControllerB.playerClientId);
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
		}
	}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void PumaDamagePlayerRpc(int scratchPlayer)
		{
	PlayerControllerB obj = StartOfRound.Instance.allPlayerScripts[scratchPlayer];
			obj.AddBloodToBody();
			obj.scratchesOnFace.SetActive(value: true);
		}

	private void CalculateAnimationDirection(float maxSpeed = 1f)
	{
		if (base.IsOwner)
		{
			if (agent.speed >= 0f)
			{
				agentLocalVelocity = base.transform.InverseTransformDirection(Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f) / (Time.deltaTime * 2f));
				Debug.DrawRay(base.transform.position, Vector3.Normalize(agentLocalVelocity * 2000f), Color.cyan);
				velX = Mathf.Lerp(velX, agentLocalVelocity.x, 10f * Time.deltaTime);
				velZ = Mathf.Lerp(velZ, agentLocalVelocity.z, 10f * Time.deltaTime);
				previousPosition = base.transform.position;
			}
			if (stalkingFrozenAnim)
			{
				animStalkSpeed = 0f;
			}
			else if (hidingQuickly)
			{
				animStalkSpeed = 1.3f;
			}
			else
			{
				animStalkSpeed = Mathf.Clamp(agent.speed / 5f, 0f, 1f);
			}
			syncVelocityInterval -= Time.deltaTime;
			if (syncVelocityInterval <= 0f)
			{
				syncVelocityInterval = 0.17f;
				SyncMovementVelocityRpc(velX, velZ, animStalkSpeed);
			}
		}
		creatureAnimator.SetFloat("VelX", Mathf.Clamp(velX, 0f - maxSpeed, maxSpeed));
		creatureAnimator.SetFloat("VelZ", Mathf.Clamp(velZ, 0f - maxSpeed, maxSpeed));
		creatureAnimator.SetFloat("stalkSpeedMultiplier", animStalkSpeed);
		if (DebugEnemy)
		{
			Debug.Log($"Puma: Set animations. IsOwner:{base.IsOwner}; VelX/VelZ:{velX}/{velZ}; animStalkSpeed:{animStalkSpeed}");
		}
	}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void SyncMovementVelocityRpc(float syncVelX, float syncVelZ, float animSpeed)
		{
			Debug.Log($"Sync movement RPC; {syncVelX} {syncVelZ} {animSpeed}");
			velX = syncVelX;
			velZ = syncVelZ;
			animStalkSpeed = animSpeed;
		}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void SyncStalkingFrozenRpc(bool frozen)
		{
			Debug.Log("Stalk frozen RPC received.");
			if (isEnemyDead)
			{
				return;
			}

			if (currentBehaviourStateIndex == 0)
			{
				frozen = false;
			}

			Debug.Log($"Accepted Sync stalk frozen RPC. Frozen: {frozen}. State: {currentBehaviourStateIndex}");
			stalkingFrozen = frozen;
			if (frozen)
			{
				growlSource.Stop();
				stalkingCharge = false;
			}

			if (!frozen)
			{
				seenByPlayersMeter = 0f;
				if (currentBehaviourStateIndex != 0 || !clingingToTree)
				{
					inSpecialAnimation = false;
					growlSource.Play();
				}

				stalkingFrozenAnim = false;
				hidingQuickly = false;
				if (eliminatedHideTrees.Count > 0)
				{
					eliminatedHideTrees.Clear();
				}

				hidingQuicklySeenMeterIncrease = 0f;
			}
		}

		[Rpc(SendTo.NotMe)]
		public void SyncStalkingChargeRpc()
		{
			if (!isEnemyDead)
			{
				Debug.Log($"Puma: Sync stalk charge RPC. State: {currentBehaviourStateIndex}");
				stalkingFrozen = false;
				growlSource.Stop();
				if (currentBehaviourStateIndex != 0 || !clingingToTree)
				{
					inSpecialAnimation = false;
				}

				stalkingFrozenAnim = false;
				hidingQuickly = false;
				stalkingCharge = true;
			}
		}

		[Rpc(SendTo.NotMe)]
		public void SyncHidingQuicklyRpc(bool hiding)
		{
			if (!isEnemyDead)
			{
				Debug.Log($"Puma: Sync Hiding Quickly RPC. Hiding: {hiding} State: {currentBehaviourStateIndex}");
				if (hiding)
				{
					stalkingFrozen = false;
					stalkingFrozenAnim = false;
					inSpecialAnimation = false;
				}

				hidingQuickly = hiding;
			}
		}

	private void RunTreeMode()
	{
		switch (treeState)
		{
		case TreeState.StartingClimb:
			Debug.Log($"Starting puma climb {startClimbingDelay} ; {treeBottomPosition} ; {inSpecialAnimation}");
			creatureAnimator.SetBool("Climbing", value: true);
			startClimbingDelay += Time.deltaTime;
			base.transform.position = Vector3.Lerp(approachTreePosition, treeBottomPosition, Mathf.Min(startClimbingDelay / 0.24f, 1f));
			base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(approachTreeRotation, treeClingRotation, Mathf.Min(startClimbingDelay / 0.35f, 0.35f)), base.transform.eulerAngles.z);
			if (startClimbingDelay >= 0.24f)
			{
				base.transform.position = treeBottomPosition;
				base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, treeClingRotation, base.transform.eulerAngles.z);
				treeState = TreeState.ClimbingUp;
			}
			break;
		case TreeState.ClimbingUp:
			Debug.Log($"Set puma climbing up! from {treeBottomPosition} to {treeTopPosition} ; {inSpecialAnimation}");
			creatureAnimator.SetBool("Climbing", value: true);
			if (base.transform.position.y < treeTopPosition.y)
			{
				base.transform.position = new Vector3(base.transform.position.x, Mathf.Min(base.transform.position.y + Time.deltaTime * 14f, treeTopPosition.y), base.transform.position.z);
				break;
			}
			clingIdlePosition = base.transform.position;
			treeState = TreeState.Idle;
			break;
		case TreeState.Idle:
		{
			Debug.Log($"Set puma tree mode to idle; {clingIdlePosition}");
			creatureAnimator.SetBool("Climbing", value: false);
			base.transform.position = clingIdlePosition;
			base.transform.eulerAngles = new Vector3(0f, treeClingRotation, 0f);
			bool flag = false;
			for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
			{
				if (PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]))
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				treeHidingTimer = 4f;
				Debug.Log("Puma No players targetable");
				break;
			}
			startLeapTimer += Time.deltaTime;
			if (!base.IsOwner || (!(startLeapTimer > 0.75f) && !firstLeap) || !(targetPlayer != null))
			{
				break;
			}
			startLeapTimer = UnityEngine.Random.Range(-0.25f, 0.5f);
			bool flag2 = PlayersSeeingPumaInTree() > 0;
			bool flag3 = false;
			bool flag4 = false;
			if (flag2 && TargetTree != null)
			{
				for (int j = 0; j < playerKnowledge.Length; j++)
				{
					if (playerKnowledge[j].playerScript.isPlayerControlled && !playerKnowledge[j].playerScript.isInsideFactory && playerKnowledge[j].seeAngle != -361f && playerKnowledge[j].seeAngle < 36f && Vector3.Distance(TargetTree.transform.position, playerKnowledge[j].playerScript.transform.position) < 4.5f)
					{
						flag4 = true;
						angerMeter = Mathf.Min(angerMeter + 0.23f, 0.45f);
						if (angerMeter >= 0.45f)
						{
							flag3 = true;
						}
						break;
					}
				}
			}
			if (flag3)
			{
				if (StartTreeDropOnLocalClient())
				{
					StartTreeDropRpc(clingIdlePosition, leapFromPosition, treeClingRotation, relocate: false);
					break;
				}
			}
			else if (!flag4)
			{
				angerMeter = Mathf.Max(0f, angerMeter - 0.12f);
			}
			if (!(treeHidingTimer > 0f) && (flag2 || !(seenByPlayersTimer < 1.2f)))
			{
				break;
			}
			if (!disqualifiedCurrentTreeFromDrop && treeHidingTimer <= -2f && Vector3.Distance(TargetTree.transform.position, targetPlayer.transform.position) < 60f && Vector3.Angle(targetPlayer.playerEye.forward, TargetTree.transform.position - targetPlayer.playerEye.position) > 70f)
			{
				Debug.Log($"The puma is on its target tree!\nOn end target tree: {EndTargetTree != null && TargetTree != null && EndTargetTree == TargetTree}\nDist: {Vector3.Distance(TargetTree.transform.position, targetPlayer.transform.position)}");
				if (StartTreeDropOnLocalClient())
				{
					StartTreeDropRpc(clingIdlePosition, leapFromPosition, treeClingRotation, relocate: false);
				}
				else
				{
					disqualifiedCurrentTreeFromDrop = true;
				}
			}
			else if ((leapAttempts > 5 && playersSeeingPuma > 0) || leapAttempts > 10)
			{
				leapAttempts = 4;
				bool flag5 = true;
				if (EndTargetTree != null && Vector3.Distance(EndTargetTree.transform.position, TargetTree.transform.position) < 8f)
				{
					flag5 = false;
				}
				Debug.Log($"Attempt drop. Relocate. {flag5}");
				if (StartTreeDropOnLocalClient(flag5))
				{
					StartTreeDropRpc(clingIdlePosition, leapFromPosition, treeClingRotation, flag5);
				}
				else
				{
					relocatingToNewTree = false;
				}
			}
			else
			{
				firstLeap = false;
				if (StartLeapToTree(treeHidingTimer > 0f))
				{
					StartLeapToTreeRpc(leapFromPosition, clingIdlePosition, treeClingRotation, leapAnimationB);
				}
			}
			break;
		}
		case TreeState.Leaping:
			leapAttempts = 0;
			if (startLeapTimer < 0.16f)
			{
				startLeapTimer += Time.deltaTime;
				leapTimer += Time.deltaTime / leapDistance * (leapSpeed / 6f);
				float num = Mathf.Min(leapTimer / 0.4f, 1f);
				creatureAnimator.SetBool("Leaping", num < 0.5f);
				base.transform.position = Vector3.Lerp(leapFromPosition, clingIdlePosition, num);
				base.transform.position = new Vector3(base.transform.position.x, base.transform.position.y + leapVerticalCurve.Evaluate(num), base.transform.position.z);
				break;
			}
			if (leapTimer < 0.4f)
			{
				leapTimer += Time.deltaTime / leapDistance * leapSpeed;
				float num2 = Mathf.Min(leapTimer / 0.4f, 1f);
				creatureAnimator.SetBool("Leaping", num2 < 0.42f);
				base.transform.position = Vector3.Lerp(leapFromPosition, clingIdlePosition, num2);
				base.transform.eulerAngles = new Vector3(0f, Mathf.LerpAngle(approachTreeRotation, treeClingRotation, Mathf.Min(leapTimer / 0.18f, 1f)), 0f);
				base.transform.position = new Vector3(base.transform.position.x, base.transform.position.y + leapVerticalCurve.Evaluate(num2), base.transform.position.z);
				break;
			}
			if (leapAnimationB)
			{
				if (treeHidingTimer > 0f)
				{
					startLeapTimer = UnityEngine.Random.Range(0f, 0.2f);
				}
				else
				{
					startLeapTimer = UnityEngine.Random.Range(-0.5f, 0f);
				}
			}
			else if (treeHidingTimer > 0f)
			{
				startLeapTimer = UnityEngine.Random.Range(0.5f, 0.6f);
			}
			else
			{
				startLeapTimer = UnityEngine.Random.Range(-0.25f, 0.5f);
			}
			if (Vector3.Distance(base.transform.position, StartOfRound.Instance.audioListener.transform.position) < 14f)
			{
				HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
			}
			creatureAnimator.SetBool("Leaping", value: false);
			treeState = TreeState.Idle;
			break;
		case TreeState.DroppingDown:
		{
			creatureAnimator.SetBool("Dropping", value: true);
			leapTimer += Time.deltaTime * 1.25f;
			float time = Mathf.Min(leapTimer / 0.75f, 1f);
			if (leapTimer < 0.75f)
			{
				creatureAnimator.SetBool("Landed", value: false);
				base.transform.position = Vector3.Lerp(leapFromPosition, clingIdlePosition, dropDownCurve.Evaluate(time));
				base.transform.eulerAngles = new Vector3(0f, Mathf.LerpAngle(approachTreeRotation, treeClingRotation, dropDownCurveRotation.Evaluate(time)), 0f);
			}
			else if (leapTimer < 1f)
			{
				creatureAnimator.SetBool("Landed", value: true);
				base.transform.position = clingIdlePosition;
				base.transform.eulerAngles = new Vector3(0f, treeClingRotation, 0f);
				clingingToTreeAnim = false;
			}
			else if (isEnemyDead)
			{
				clingingToTree = false;
				clingingToTreeAnim = false;
			}
			else
			{
				EndDroppingDownAnimationOnLocalClient();
			}
			break;
		}
		}
	}

	private int PlayersSeeingPumaInTree()
	{
		int num = 0;
		for (int i = 0; i < playerKnowledge.Length; i++)
		{
			if (playerKnowledge[i].playerScript.isPlayerControlled && !playerKnowledge[i].playerScript.isInsideFactory && !Physics.Linecast(playerKnowledge[i].playerScript.playerEye.transform.position, TargetTree.transform.position + Vector3.up * 7.5f, playerLOSLayermask, QueryTriggerInteraction.Ignore))
			{
				float num2 = Vector3.Angle(playerKnowledge[i].playerScript.playerEye.forward, TargetTree.transform.position + Vector3.up * 7.5f - playerKnowledge[i].playerScript.playerEye.transform.position);
				float num3 = Vector3.Distance(playerKnowledge[i].playerScript.playerEye.transform.position, TargetTree.transform.position);
				if ((num3 < playerTreeLOSDistanceClose && num2 < playerTreeLOSClose) || (TimeOfDay.Instance.currentLevelWeather != LevelWeatherType.Foggy && num3 < playerTreeLOSDistanceFar && num2 < playerTreeLOSFar))
				{
					num++;
				}
			}
		}
		return num;
	}

	private void EndDroppingDownAnimationOnLocalClient()
	{
		if (clingingToTree || currentBehaviourStateIndex == 0)
		{
			inSpecialAnimation = false;
			serverPosition = base.transform.position;
			clingingToTree = false;
			treeState = TreeState.ClimbingUp;
			creatureAnimator.SetBool("TreeMode", value: false);
			creatureAnimator.SetBool("Dropping", value: false);
			creatureAnimator.SetBool("Climbing", value: false);
			Debug.Log($"Puma: Landed on the ground! relocatingToNewTree:{relocatingToNewTree}");
			timeAtLastDrop = Time.realtimeSinceStartup;
			if (relocatingToNewTree)
			{
				SwitchToBehaviourStateOnLocalClient(0);
			}
			else
			{
				SwitchToBehaviourStateOnLocalClient(1);
			}
		}
	}
}
