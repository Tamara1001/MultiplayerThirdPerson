using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;
using Blocks.Gameplay.Shooter;

namespace Blocks.Gameplay.Core
{
    public class PowerUp : NetworkBehaviour
    {
        public enum PowerUpType { FireRate, MoveSpeed, JumpHeight }

        [SerializeField] private PowerUpType type;
        [SerializeField] private float multiplier = 1.5f;
        [SerializeField] private float buffDuration = 10f;

        private bool m_BeingCollected;

        private void OnTriggerEnter(Collider other)
        {
            if (m_BeingCollected) return;

            var playerNetObj = other.GetComponentInParent<NetworkObject>();
            if (playerNetObj == null || !playerNetObj.IsOwner) return;

            m_BeingCollected = true;
            ApplyBuffToLocalPlayer(playerNetObj);

            if (IsOwner) NetworkObject.Despawn();
            else DespawnRpc();
        }

        private void ApplyBuffToLocalPlayer(NetworkObject player)
        {
            switch (type)
            {
                case PowerUpType.MoveSpeed:
                    player.GetComponent<CoreMovement>()?.ApplyMoveSpeedBuff(multiplier, buffDuration);
                    break;
                case PowerUpType.JumpHeight:
                    player.GetComponent<CoreMovement>()?.ApplyJumpHeightBuff(multiplier, buffDuration);
                    break;
                case PowerUpType.FireRate:
                    var weaponController = player.GetComponent<WeaponController>();
                    if (weaponController != null && weaponController.CurrentWeapon is ModularWeapon mw)
                    {
                        mw.ApplyFireRateBuff(multiplier, buffDuration);
                    }
                    break;
            }
        }

        [Rpc(SendTo.Owner)]
        private void DespawnRpc()
        {
            if (NetworkObject.IsSpawned) NetworkObject.Despawn();
        }
    }
}