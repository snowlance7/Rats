﻿// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// EnemyAICollisionDetect
using GameNetcodeStuff;
using Rats;
using Unity.Netcode;
using UnityEngine;

public class RatAICollisionDetect : MonoBehaviour, IHittable
{
    public RatAI mainScript;

    public bool canCollideWithEnemies;

    public bool onlyCollideWhenGrounded;

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (onlyCollideWhenGrounded)
            {
                CharacterController component = other.gameObject.GetComponent<CharacterController>();
                if (!(component != null) || !component.isGrounded)
                {
                    return;
                }
                mainScript.OnCollideWithPlayer(other);
            }
            mainScript.OnCollideWithPlayer(other);
        }
        else if (!onlyCollideWhenGrounded && canCollideWithEnemies && other.CompareTag("Enemy"))
        {
            EnemyAICollisionDetect component2 = other.gameObject.GetComponent<EnemyAICollisionDetect>();
            if (component2 != null)
            {
                mainScript.OnCollideWithEnemy(other, component2.mainScript);
            }
        }
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
    {
        if (onlyCollideWhenGrounded)
        {
            Debug.Log("Enemy collision detect returned false");
            return false;
        }
        mainScript.HitEnemyOnLocalClient(force, hitDirection, playerWhoHit, playHitSFX, hitID);
        return true;
    }
}
