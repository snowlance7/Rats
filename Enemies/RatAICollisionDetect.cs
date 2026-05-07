using GameNetcodeStuff;
using Rats;
using UnityEngine;

public class RatAICollisionDetect : MonoBehaviour, IHittable
{
    public RatAI mainScript = null!;

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            mainScript.OnCollideWithPlayer(other);
        }
        else if (other.CompareTag("Enemy"))
        {
            EnemyAICollisionDetect? enemyCollision = other.gameObject.GetComponent<EnemyAICollisionDetect>();
            if (enemyCollision != null)
            {
                mainScript.OnCollideWithEnemy(other, enemyCollision.mainScript);
            }
        }
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB? playerWhoHit, bool playHitSFX, int hitID)
    {
        int id = playerWhoHit != null ? (int)playerWhoHit.actualClientId : -1;
        mainScript.HitEnemyServerRpc(force, id);
        return true;
    }
}
