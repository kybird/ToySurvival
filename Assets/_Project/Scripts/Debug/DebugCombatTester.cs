using System.Collections.Generic;
using Protocol;
using UnityEngine;

public class DebugCombatTester : MonoBehaviour
{
    void Update()
    {
        // K: Spawn Mock Projectile
        if (Input.GetKeyDown(KeyCode.K))
        {
            SpawnMockProjectile();
        }

        // L: Mock Damage on MyPlayer
        if (Input.GetKeyDown(KeyCode.L))
        {
            MockDamage(NetworkManager.Instance.MyPlayerId, 10);
        }
    }

    void SpawnMockProjectile()
    {
        ObjectInfo info = new ObjectInfo();
        info.ObjectId = 9999; // Mock ID
        info.Type = ObjectType.Projectile;
        info.TypeId = 1; // Projectile_1

        GameObject myPlayer = ObjectManager.Instance.GetMyPlayer();
        if (myPlayer != null)
        {
            info.X = myPlayer.transform.position.x + 1.0f;
            info.Y = myPlayer.transform.position.y;
        }

        Debug.Log("[DebugCombatTester] Spawning Mock Projectile");
        ObjectManager.Instance.Spawn(info);
    }

    void MockDamage(int targetId, int damage)
    {
        Debug.Log($"[DebugCombatTester] Mock Damage on {targetId}: {damage}");
        ObjectManager.Instance.OnDamage(targetId, damage);

        // Mock HP Change packet handling
        S_HpChange hpChange = new S_HpChange();
        hpChange.ObjectId = targetId;
        hpChange.CurrentHp = 50; // Mock value
        hpChange.MaxHp = 100;

        PacketHandler.Handle_S_HpChange(hpChange);
    }
}
