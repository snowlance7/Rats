/*using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.Utilities;
using static Rats.Plugin;
using static Rats.RatManager;

// Cheese to lure the rats away from the grate so the player can block it
// Rats will scout and do a normal search, if they see a player after 20 seconds, they will add a counter to the threatcounter, when max threat, they will run back to sewer and gather rats they touch and get defending rats and swarm around the player for 20 seconds, any rats nearby will also swarm. then attack.

namespace Rats
{
    public class RatKingAI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;
        public static RatKingAI? Instance { get; private set; }
        bool despawning = false;

#pragma warning disable 0649
        public ScanNodeProperties ScanNode = null!;
        public AudioClip[] SqueakSFX = null!;
        public AudioClip[] AttackSFX = null!;
        public AudioClip[] HitSFX = null!;
        public AudioClip[] NibbleSFX = null!;
        public Transform TurnCompass = null!;
        public Transform RatMouth = null!;
#pragma warning restore 0649

        public SewerGrate Nest;
        DeadBodyInfo? heldBody;
        private float timeSinceAttackCheck;
        EnemyAI? targetEnemy;
        bool holdingFood;

        float timeSinceCollision;
        float timeSinceAddThreat;

        bool grabbingBody;
        bool returningBodyToNest;

        int groupCount;
        List<List<RatAI>> ratGroups = [];

        bool AtNest { get { return agent.enabled && Nest != null && Vector3.Distance(transform.position, Nest.transform.position) < 1f; } }

        // Constants
        const float timeAgentStopped = 1f;

        // Config Values
        float defenseRadius = 5f;
        float timeToIncreaseThreat = 3f;
        int threatToAttackPlayer = 100;
        int threatToAttackEnemy = 50;
        int playerFoodAmount = 30;
        int ratKingDamage = 2;

        float ratUpdateInterval = 0.2f;
        float ratUpdatePercentage = 0.2f;

        public enum State
        {
            Inactive,
            Roaming,
            Attacking,
            Eating
        }

        public void SwitchToBehaviourStateCustom(State state)
        {
            if (currentBehaviourStateIndex == (int)state) { return; }

            StopRoutines();

            switch (state)
            {
                case State.Roaming:
                    break;
                case State.Attacking:
                    break;
                case State.Eating:
                    break;
                default:
                    break;
            }

            SwitchToBehaviourClientRpc((int)state);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServerOrHost) { return; }
            if (!despawning) { return; }
            NetworkObject.Despawn(true);
        }

        public override void Start()
        {
            if (Instance != null)
            {
                despawning = true;
                return;
            }

            Instance = this;

            defenseRadius = configDefenseRadius.Value;
            timeToIncreaseThreat = configTimeToIncreaseThreat.Value;
            threatToAttackPlayer = configThreatToAttackPlayer.Value;
            threatToAttackEnemy = configThreatToAttackEnemy.Value;
            playerFoodAmount = configPlayerFoodAmount.Value;
            ratKingDamage = configRatKingDamage.Value;

            thisEnemyIndex = RoundManager.Instance.numberOfEnemiesInScene;
            RoundManager.Instance.numberOfEnemiesInScene++;
            allAINodes = RoundManager.Instance.insideAINodes;
            RoundManager.Instance.SpawnedEnemies.Add(this);
            path1 = new NavMeshPath();
            ventAnimationFinished = true;
            HottestRat();


            if (IsServerOrHost)
            {
                StartCoroutine(RatUpdater());
            }
        }

        IEnumerator RatUpdater()
        {
            yield return null;

            while (!StartOfRound.Instance.shipIsLeaving)
            {
                yield return new WaitForSeconds(AIIntervalTime);
                if (RatManager.Rats.Count <= 0) { break; }

                ratGroups.Clear();
                groupCount = (int)(1 / ratUpdatePercentage);
                RegisterRats();

                foreach (var ratGroup in ratGroups)
                {
                    yield return new WaitForSeconds(ratUpdateInterval);

                    foreach (var rat in ratGroup)
                    {
                        rat.DoAIInterval();
                    }
                }
            }
        }

        public void RegisterRats()
        {
            foreach (var rat in RatManager.Rats)
            {
                // Assign the rat to a group based on round-robin
                int groupIndex = ratGroups.Count > 0 ? (ratGroups.Sum(group => group.Count) % groupCount) : 0;
                ratGroups[groupIndex].Add(rat);
            }
        }

        public override void Update()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || despawning)
            {
                return;
            };

            if (!base.IsOwner)
            {
                SetClientCalculatingAI(enable: false);
            }
            else
            {
                if (!inSpecialAnimation)
                {
                    SetClientCalculatingAI(enable: true);
                }
                if (updateDestinationInterval >= 0f)
                {
                    updateDestinationInterval -= Time.deltaTime;
                }
                else
                {
                    DoAIInterval();
                    updateDestinationInterval = AIIntervalTime + UnityEngine.Random.Range(-0.015f, 0.015f);
                }
            }

            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
        }

        public override void DoAIInterval()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || stunNormalizedTimer > 0f)
            {
                return;
            };

            if (moveTowardsDestination)
            {
                agent.SetDestination(destination);
            }

            foreach (var nest in Nests)
            {
                nest.NestVent = GetClosestVentToPosition(nest.transform.position);
            }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Inactive:

                    break;

                case (int)State.Roaming:

                    break;

                case (int)State.Attacking:

                    break;

                case (int)State.Eating:

                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        void GrabBody()
        {
            if (targetPlayer != null && targetPlayer.deadBody != null)
            {
                int limb = UnityEngine.Random.Range(0, targetPlayer.deadBody.bodyParts.Length);
                GrabBodyClientRpc(targetPlayer.actualClientId, limb);
            }
        }

        void DropBody(bool deactivate = false)
        {
            if (heldBody != null)
            {
                DropBodyClientRpc(deactivate);
            }
        }

        void HottestRat()
        {
            if (UnityEngine.Random.Range(0f, 1f) < 0.01f)
            {
                ScanNode.headerText = "HottestRat";
            }
        }

        bool CheckLineOfSightForDeadBody()
        {
            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                DeadBodyInfo deadBody = player.deadBody;
                if (deadBody == null || deadBody.deactivated) { continue; }
                path1 = new NavMeshPath();
                if (CheckLineOfSightForPosition(deadBody.grabBodyObject.transform.position, 60, 60, 5) && agent.CalculatePath(RoundManager.Instance.GetNavMeshPosition(deadBody.grabBodyObject.transform.position, RoundManager.Instance.navHit, 1.75f), path1))
                {
                    targetPlayer = deadBody.playerScript;
                    return true;
                }
            }
            return false;
        }

        void CheckForThreatsInLOS()
        {
            if (timeSinceAddThreat > timeToIncreaseThreat)
            {
                if (CheckLineOfSightForDeadBody())
                {
                    StopRoutines();
                    grabbingBody = true;
                    return;
                }
                PlayerControllerB player = CheckLineOfSightForPlayer(60f, 60, 5);
                if (PlayerIsTargetable(player))
                {
                    AddThreat(player);
                    return;
                }
                EnemyAI? enemy = CheckLineOfSightForEnemy(30);
                if (enemy != null && enemy.enemyType.canDie)
                {
                    AddThreat(enemy);
                }
            }
        }

        bool CalculatePath(Vector3 fromPos, Vector3 toPos)
        {
            Vector3 from = RoundManager.Instance.GetNavMeshPosition(fromPos, RoundManager.Instance.navHit, 1.75f);
            Vector3 to = RoundManager.Instance.GetNavMeshPosition(toPos, RoundManager.Instance.navHit, 1.75f);

            path1 = new NavMeshPath();
            if (!agent.enabled) { return false; }
            return NavMesh.CalculatePath(from, to, agent.areaMask, path1) && Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(to, RoundManager.Instance.navHit, 2.7f)) <= 1.55f; // TODO: Test this
        }

        public EnemyAI? CheckLineOfSightForEnemy(int range)
        {
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.enemyType == enemyType || !enemy.enemyType.canDie) { continue; }
                if (CheckLineOfSightForPosition(enemy.transform.position, 60, range, 5))
                {
                    return enemy;
                }
            }
            return null;
        }

        public EnemyAI? GetClosestEnemy(float range)
        {
            EnemyAI? target = null;
            float closestDistance = range;

            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.enemyType == enemyType || !enemy.enemyType.canDie) { continue; }
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    target = enemy;
                    closestDistance = distance;
                }
            }

            return target;
        }

        bool TargetPlayerNearNest()
        {
            if (Nest == null) { return false; }
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (PlayerIsTargetable(player))
                {
                    if (Vector3.Distance(player.transform.position, Nest.transform.position) < defenseRadius)
                    {
                        targetPlayer = player;
                    }
                }
            }

            return targetPlayer != null;
        }

        void WarpToVent(EnemyVent fromVent, EnemyVent toVent)
        {
            OpenVents(fromVent, toVent);
            Vector3 warpPos = RoundManager.Instance.GetNavMeshPosition(ToVent!.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);
            transform.position = warpPos;
            agent.Warp(warpPos);
        }

        int GetVentIndex(EnemyVent? vent)
        {
            for (int i = 0; i < RoundManager.Instance.allEnemyVents.Length; i++)
            {
                if (RoundManager.Instance.allEnemyVents[i] == vent)
                {
                    return i;
                }
            }
            return -1;
        }

        void OpenVents(EnemyVent? vent, EnemyVent? vent2 = null)
        {
            if (vent == null) { return; }
            int index = -1;
            int index2 = -1;

            if (!vent.ventIsOpen)
            {
                index = GetVentIndex(vent);
            }

            if (vent2 != null && !vent2.ventIsOpen)
            {
                index2 = GetVentIndex(vent2);
            }

            if (index == -1 && index2 == -1) { return; }

            OpenVentsClientRpc(index, index2);
        }

        void SetStatus(string status)
        {
            if (testing)
            {
                ScanNode.subText = status;
            }
        }

        void Roam()
        {
            if (ratCoroutine != null) { return; }
            SetStatus("Lost");
            ratCoroutine = StartCoroutine(RoamCoroutine());
        }

        IEnumerator RoamCoroutine() // TODO: Fix these so they use agent.velocity instead of a distance check.
        {
            yield return null;
            while (ratCoroutine != null)
            {
                float timeStuck = 0f;
                TargetRandomNode();
                Vector3 position = RoundManager.Instance.GetNavMeshPosition(targetNode.position, RoundManager.Instance.navHit, 1.75f, agent.areaMask);
                if (!SetDestinationToPosition(position)) { continue; }
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        PlaySqueakSFXClientRpc();
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviourStateCustom(State.Roaming);
        }

        IEnumerator SwarmCoroutine(PlayerControllerB player, float radius)
        {
            yield return null;
            logIfDebug("Starting swarm on player");
            while (ratCoroutine != null && player != null)
            {
                if (player == null || player.isPlayerDead) { break; }
                float timeStuck = 0f;
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(player.transform.position, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(position, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }

                if (currentBehaviourStateIndex != (int)State.Attacking)
                {
                    PlaySqueakSFXClientRpc(true);
                }
            }

            SwitchToBehaviourStateCustom(State.Roaming);
        }

        IEnumerator SwarmCoroutine(EnemyAI enemy, float radius)
        {
            yield return null;
            while (ratCoroutine != null && enemy != null)
            {
                float timeStuck = 0f;
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(enemy.transform.position, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(position, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }

                if (currentBehaviourStateIndex != (int)State.Attacking)
                {
                    PlaySqueakSFXClientRpc(true);
                }
            }

            SwitchToBehaviourStateCustom(State.Roaming);
        }

        IEnumerator SwarmCoroutine(Vector3 position, float radius)
        {
            yield return null;
            while (ratCoroutine != null)
            {
                //logIfDebug("Choosing new position");
                float timeStuck = 0f;
                Vector3 pos = position;
                pos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(pos, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }

                if (currentBehaviourStateIndex != (int)State.Attacking)
                {
                    PlaySqueakSFXClientRpc(true);
                }
            }

            SwitchToBehaviourStateCustom(State.Roaming);
        }

        IEnumerator ScoutCoroutine()
        {
            yield return null;
            while (ratCoroutine != null && agent.enabled)
            {
                float timeStuck = 0f;
                TargetRandomNode();
                Vector3 position = targetNode.transform.position;
                if (!SetDestinationToPosition(position)) { continue; }
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        PlaySqueakSFXClientRpc();
                        SwitchToBehaviourStateCustom(State.Roaming);
                        ratCoroutine = null;
                        yield break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviourStateCustom(State.Roaming);
        }

        void TargetRandomNode()
        {
            //logIfDebug("Choosing new target node...");

            //GameObject[] nodes = !isOutside ? GameObject.FindGameObjectsWithTag("AINode") : GameObject.FindGameObjectsWithTag("OutsideAINode");
            GameObject[] nodes = allAINodes;

            int randIndex = UnityEngine.Random.Range(0, nodes.Length);
            targetNode = nodes[randIndex].transform;
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
        {
            if (!isEnemyDead)
            {
                enemyHP -= force;
                if (enemyHP <= 0)
                {
                    DropBody();
                    RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
                    KillEnemyOnOwnerClient();
                    return;
                }
            }
        }

        public void StopRoutines()
        {
            StopAllCoroutines();
            ratCoroutine = null;
        }

        public override void CancelSpecialAnimationWithPlayer()
        {
            StopRoutines();
        }

        public override void HitFromExplosion(float distance)
        {
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            KillEnemyOnOwnerClient();
        }

        public bool PlayerIsTargetable(PlayerControllerB playerScript)
        {
            if (playerScript == null) { return false; }
            if (playerScript.isPlayerDead) { return false; }
            if (currentBehaviourStateIndex == (int)State.Roaming) { return false; }
            if (!playerScript.isPlayerControlled) { return false; }
            if (playerScript.inAnimationWithEnemy != null) { return false; }
            if (playerScript.sinkingValue >= 0.73f) { return false; }
            return true;
        }

        public bool IsPlayerNearANest(PlayerControllerB player)
        {
            foreach (SewerGrate nest in SewerGrate.Nests)
            {
                if (Vector3.Distance(player.transform.position, nest.transform.position) < defenseRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            if (isEnemyDead) { return; }
            if (timeSinceCollision > 1f)
            {
                timeSinceCollision = 0f;
                PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
                if (player == null) { return; }
                if (currentBehaviourStateIndex == (int)State.Attacking || IsPlayerNearANest(player))
                {
                    int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                    player.DamagePlayer(ratKingDamage, true, true, CauseOfDeath.Mauling, deathAnim);
                    PlayAttackSFXServerRpc();
                }
            }
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
        {
            base.OnCollideWithEnemy(other, collidedEnemy);
            if (IsServerOrHost)
            {
                if (isEnemyDead || currentBehaviourStateIndex == (int)State.Roaming || Nest == null) { return; }
                if (timeSinceCollision > 1f)
                {
                    if (collidedEnemy != null && collidedEnemy.enemyType != this.enemyType && collidedEnemy.enemyType.canDie && !(stunNormalizedTimer > 0f))
                    {
                        logIfDebug("Collided with: " + collidedEnemy.enemyType.enemyName);
                        timeSinceCollision = 0f;

                        if (collidedEnemy.isEnemyDead)
                        {
                            if (SewerGrate.EnemyFoodAmount.ContainsKey(collidedEnemy))
                            {
                                if (SewerGrate.EnemyFoodAmount[collidedEnemy] <= 1)
                                {
                                    holdingFood = SewerGrate.EnemyFoodAmount[collidedEnemy] == 1;
                                    collidedEnemy.NetworkObject.Despawn(true);
                                    return;
                                }
                                else
                                {
                                    SewerGrate.EnemyFoodAmount[collidedEnemy] -= 1;
                                    holdingFood = true;
                                }
                            }
                            else
                            {
                                SewerGrate.EnemyHitCount.Remove(collidedEnemy);
                                Nest.AddEnemyFoodAmount(collidedEnemy);
                                SewerGrate.EnemyFoodAmount[collidedEnemy] -= 1;
                                holdingFood = true;
                            }

                            SwitchToBehaviourStateCustom(State.Roaming);
                        }
                        else
                        {
                            if (!SewerGrate.EnemyHitCount.ContainsKey(collidedEnemy))
                            {
                                SewerGrate.EnemyHitCount.Add(collidedEnemy, enemyHitsToDoDamage);
                            }

                            SewerGrate.EnemyHitCount[collidedEnemy] -= 1;

                            if (SewerGrate.EnemyHitCount[collidedEnemy] <= 0)
                            {
                                collidedEnemy.HitEnemy(1, null, true);
                                SewerGrate.EnemyHitCount[collidedEnemy] = enemyHitsToDoDamage;
                            }
                        }
                    }
                }
            }

            if (holdingFood)
            {
                RoundManager.PlayRandomClip(creatureSFX, NibbleSFX, true, 1f, -1);
                if (collidedEnemy != null && localPlayer.HasLineOfSightToPosition(collidedEnemy.transform.position))
                {
                    localPlayer.IncreaseFearLevelOverTime(0.8f);
                }
            }
        }

        void AddThreat(EnemyAI enemy, int amount = 1)
        {
            if (enemy == null) { return; }

            if (Nest != null && !Nest.IsRallying)
            {
                timeSinceAddThreat = 0f;

                if (Nest.EnemyThreatCounter.ContainsKey(enemy))
                {
                    Nest.EnemyThreatCounter[enemy] += amount;
                }
                else
                {
                    Nest.EnemyThreatCounter.Add(enemy, amount);
                }

                PlaySqueakSFXClientRpc(true);

                int threat = Nest.EnemyThreatCounter[enemy];
                logIfDebug($"{enemy.enemyType.enemyName}: {threat} threat");

                //if (RatTypeState == RatType.DefenseRat) { return; }

                if (Nest.EnemyThreatCounter[enemy] > threatToAttackEnemy || enemy.isEnemyDead)
                {
                    SetTarget(enemy);
                    //SwitchToBehaviourStateCustom(State.Swarming);
                }
            }
        }

        void AddThreat(PlayerControllerB player, int amount = 1)
        {
            if (player == null) { return; }

            if (Nest != null && !Nest.IsRallying)
            {
                timeSinceAddThreat = 0f;

                if (Nest.PlayerThreatCounter.ContainsKey(player))
                {
                    Nest.PlayerThreatCounter[player] += amount;
                }
                else
                {
                    Nest.PlayerThreatCounter.Add(player, amount);
                }

                PlaySqueakSFXClientRpc(true);

                int threat = Nest.PlayerThreatCounter[player];
                logIfDebug($"{player.playerUsername}: {threat} threat");

                //if (RatTypeState == RatType.DefenseRat) { return; }

                if (Nest.PlayerThreatCounter[player] > threatToAttackPlayer || player.isPlayerDead)
                {
                    SetTarget(player);
                    //SwitchToBehaviourStateCustom(State.Swarming);
                }
            }
        }

        public void SetTarget(PlayerControllerB player)
        {
            timeSinceAttackCheck = 0f;
            targetEnemy = null;
            targetPlayer = player;
        }

        public void SetTarget(EnemyAI enemy)
        {
            timeSinceAttackCheck = 0f;
            targetPlayer = null;
            targetEnemy = enemy;
        }

        void logIfDebug(string message)
        {
            if (testing)
            {
                LoggerInstance.LogDebug(message);
            }
        }

        public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB setStunnedByPlayer = null)
        {
            if (isEnemyDead)
            {
                return;
            }
            if (setToStunned)
            {
                if (!(postStunInvincibilityTimer >= 0f))
                {
                    stunnedByPlayer = setStunnedByPlayer;
                    postStunInvincibilityTimer = 0.5f;
                    stunNormalizedTimer = 5;
                }
            }
            else
            {
                stunnedByPlayer = null;
                if (stunNormalizedTimer > 0f)
                {
                    stunNormalizedTimer = 0f;
                }
            }
        }

        public override void KillEnemy(bool destroy = false)
        {
            if (destroy && enemyType.canBeDestroyed)
            {
                Debug.Log("Destroy enemy called");
                if (base.IsServer)
                {
                    Debug.Log("Despawn network object in kill enemy called!");
                    if (thisNetworkObject.IsSpawned)
                    {
                        thisNetworkObject.Despawn();
                    }
                }
            }
            else
            {
                if (ScanNode != null && (bool)ScanNode.gameObject.GetComponent<Collider>())
                {
                    ScanNode.gameObject.GetComponent<Collider>().enabled = false;
                }
                isEnemyDead = true;
                try
                {
                    if (creatureAnimator != null)
                    {
                        creatureAnimator.SetBool("stunned", value: false);
                        creatureAnimator.SetTrigger("KillEnemy");
                    }
                }
                catch (Exception arg)
                {
                    Debug.LogError($"enemy did not have bool in animator in KillEnemy, error returned; {arg}");
                }
                StopAllCoroutines();
                agent.enabled = false;
                //Rats.Remove(this);
                //if (RatTypeState == RatType.DefenseRat) { MainNest.DefenseRatCount -= 1; }
            }
        }

        public override void OnDestroy()
        {
            agent.enabled = false;
            StopAllCoroutines();

            if (NetworkObject != null && NetworkObject.IsSpawned && IsSpawned)
            {
                NetworkObject.OnNetworkBehaviourDestroyed(this);
            }
            if (!m_VarInit)
            {
                InitializeVariables();
            }
            for (int i = 0; i < NetworkVariableFields.Count; i++)
            {
                NetworkVariableFields[i].Dispose();
            }

            if (RoundManager.Instance.SpawnedEnemies.Contains(this))
            {
                RoundManager.Instance.SpawnedEnemies.Remove(this);
            }

            //Rats.Remove(this);
        }

        public new bool SetDestinationToPosition(Vector3 position, bool checkForPath = false)
        {
            if (checkForPath)
            {
                position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);
                path1 = new NavMeshPath();
                if (!agent.CalculatePath(position, path1))
                {
                    return false;
                }
                if (Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.55f)
                {
                    return false;
                }
            }
            moveTowardsDestination = true;
            movingTowardsTargetPlayer = false;
            destination = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, -1f);
            return true;
        }

        // RPC's

        [ServerRpc(RequireOwnership = false)]
        public void PlayAttackSFXServerRpc()
        {
            if (IsServerOrHost)
            {
                PlayAttackSFXClientRpc();
            }
        }

        [ClientRpc]
        public void PlayAttackSFXClientRpc()
        {
            RoundManager.PlayRandomClip(creatureSFX, AttackSFX, true, 1, -1);
        }

        [ClientRpc]
        public new void SwitchToBehaviourClientRpc(int stateIndex)
        {
            if (currentBehaviourStateIndex != stateIndex)
            {
                currentBehaviourStateIndex = stateIndex;
                currentBehaviourState = enemyBehaviourStates[stateIndex];
            }
        }

        [ClientRpc]
        public void DamagePlayerClientRpc(ulong clientId, int damage = 1)
        {
            if (localPlayer.actualClientId == clientId)
            {
                int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                localPlayer.DamagePlayer(damage, true, true, CauseOfDeath.Mauling, deathAnim);
            }

            RoundManager.PlayRandomClip(creatureSFX, AttackSFX, true, 1, -1);
        }

        [ClientRpc]
        public void PlaySqueakSFXClientRpc(bool shortsfx = false)
        {
            if (shortsfx)
            {
                RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            }
            else
            {
                RoundManager.PlayRandomClip(creatureSFX, SqueakSFX, true, 1, -1);
            }
        }

        [ClientRpc]
        public void OpenVentsClientRpc(int ventIndex, int ventIndex2)
        {
            if (ventIndex != -1)
            {
                EnemyVent vent = RoundManager.Instance.allEnemyVents[ventIndex];
                if (vent == null)
                {
                    RoundManager.Instance.RefreshEnemyVents();
                    vent = RoundManager.Instance.allEnemyVents[ventIndex];
                    if (vent == null)
                    {
                        LoggerInstance.LogError("Cant get vent to open on client");
                        return;
                    }
                }
                vent.ventIsOpen = true;
                vent.ventAnimator.SetTrigger("openVent");
                vent.lowPassFilter.lowpassResonanceQ = 0f;
            }
            if (ventIndex2 != -1)
            {
                EnemyVent vent = RoundManager.Instance.allEnemyVents[ventIndex2];
                if (vent == null)
                {
                    RoundManager.Instance.RefreshEnemyVents();
                    vent = RoundManager.Instance.allEnemyVents[ventIndex2];
                    if (vent == null)
                    {
                        LoggerInstance.LogError("Cant get vent to open on client");
                        return;
                    }
                }
                vent.ventIsOpen = true;
                vent.ventAnimator.SetTrigger("openVent");
                vent.lowPassFilter.lowpassResonanceQ = 0f;
            }

        }

        [ServerRpc(RequireOwnership = false)]
        public void AddThreatServerRpc(ulong clientId)
        {
            if (IsServerOrHost)
            {
                if (timeSinceAddThreat > timeToIncreaseThreat)
                {
                    PlayerControllerB player = PlayerFromId(clientId);
                    AddThreat(player);
                }
            }
        }

        [ClientRpc]
        public void SetNestClientRpc(NetworkObjectReference nestRef)
        {
            if (nestRef.TryGet(out NetworkObject netObj))
            {
                Nest = netObj.GetComponent<SewerGrate>();
            }
        }

        [ClientRpc]
        public void GrabBodyClientRpc(ulong clientId, int attachedLimbIndex)
        {
            targetPlayer = PlayerFromId(clientId);

            if (targetPlayer != null && targetPlayer.deadBody != null)
            {
                heldBody = targetPlayer.deadBody;
                heldBody.attachedTo = RatMouth;
                heldBody.attachedLimb = targetPlayer.deadBody.bodyParts[attachedLimbIndex];
                heldBody.matchPositionExactly = true;
            }

            grabbingBody = false;
            returningBodyToNest = true;
            RoundManager.PlayRandomClip(creatureSFX, NibbleSFX, true, 1f, -1);
        }

        [ClientRpc]
        public void DropBodyClientRpc(bool deactivate)
        {
            returningBodyToNest = false;
            grabbingBody = false;
            targetPlayer = null;

            if (heldBody != null)
            {
                heldBody.attachedTo = null;
                heldBody.attachedLimb = null;
                heldBody.matchPositionExactly = false;
                if (deactivate) { heldBody.DeactivateBody(setActive: false); }
                heldBody = null;
            }
        }
    }
}*/