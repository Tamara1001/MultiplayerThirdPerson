using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [CreateAssetMenu(menuName = "Game Events/Player State Event")]
    public class PlayerStateEvent : GameEvent<PlayerStatePayload> { }

    [System.Serializable]
    public struct PlayerStatePayload
    {
        public ulong playerId;
        public PlayerLifeState newState;
        public PlayerLifeState oldState;
    }
}
