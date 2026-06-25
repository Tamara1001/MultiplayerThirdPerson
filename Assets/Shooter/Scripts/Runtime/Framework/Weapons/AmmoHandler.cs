using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Manages ammunition for a weapon, including clip capacity, reload logic, and ammo consumption.
    /// Uses <see cref="NetworkVariable{T}"/> to synchronize ammo state across the network.
    /// Works in conjunction with <see cref="WeaponStateManager"/> to handle reload state transitions.
    /// </summary>
    public class AmmoHandler : NetworkBehaviour
    {
        #region Fields & Properties

        [Header("Broadcasting on")]
        [Tooltip("Event raised when the clip ammo count changes. Contains current and max ammo.")]
        [SerializeField] private IntPairEvent onClipAmmoChangedEvent;
        [Tooltip("Event raised when a reload begins. Contains the reload duration.")]
        [SerializeField] private FloatEvent onReloadStartedEvent;
        [Tooltip("Event raised when a reload completes.")]
        [SerializeField] private GameEvent onReloadCompletedEvent;

        private int m_ClipSize;
        private float m_ReloadTime;

        private readonly NetworkVariable<int> m_CurrentClipAmmo = new NetworkVariable<int>();
        private WeaponStateManager m_WeaponStateManager;
        private ModularWeapon m_ModularWeapon;

        #endregion

        #region Public Methods

        /// <summary>
        /// Called when the NetworkObject is spawned on the network.
        /// Initializes the clip ammo to full capacity for the owner and sets up event listeners.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            // Only the owner should initialize the ammo value to prevent conflicts
            if (IsOwner)
            {
                m_CurrentClipAmmo.Value = m_ClipSize;
            }
            m_CurrentClipAmmo.OnValueChanged += OnAmmoChanged;
            RaiseClipAmmoChangedEvent();
        }

        /// <summary>
        /// Called when the NetworkObject is despawned from the network.
        /// Cleans up event listeners to prevent memory leaks.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            m_CurrentClipAmmo.OnValueChanged -= OnAmmoChanged;
        }

        /// <summary>
        /// Initializes the ammo handler with required dependencies and configuration.
        /// This should be called by the <see cref="ModularWeapon"/> during weapon setup.
        /// </summary>
        /// <param name="stateManager">The weapon's state manager for handling reload states.</param>
        /// <param name="weapon">The modular weapon this ammo handler belongs to.</param>
        /// <param name="clipSize">The maximum ammunition capacity of the clip.</param>
        /// <param name="reloadTime">The time in seconds required to complete a reload.</param>
        public void Initialize(WeaponStateManager stateManager, ModularWeapon weapon, int clipSize, float reloadTime)
        {
            m_WeaponStateManager = stateManager;
            m_ModularWeapon = weapon;
            m_ClipSize = clipSize;
            m_ReloadTime = reloadTime;
        }

        /// <summary>
        /// Attempts to start a reload operation.
        /// This should only be called on the owner's instance.
        /// </summary>
        /// <returns>True if the reload was initiated, false otherwise.</returns>
        public bool TryReload()
        {
            if (!IsOwner) return false;

            if (m_WeaponStateManager.CanReload() && NeedsReload())
            {
                m_WeaponStateManager.TransitionToState(WeaponState.Reloading, m_ReloadTime);
                onReloadStartedEvent?.Raise(m_ReloadTime);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Completes the reload operation, refilling the clip to maximum capacity.
        /// This is typically called by the <see cref="WeaponStateManager"/> when the reload state completes.
        /// This should only be called on the owner's instance.
        /// </summary>
        public void Reload()
        {
            if (!IsOwner) return;
            m_CurrentClipAmmo.Value = m_ClipSize;
            onReloadCompletedEvent?.Raise();
        }

        /// <summary>
        /// Checks whether the weapon's clip is not at full capacity.
        /// </summary>
        /// <returns>True if the clip has less than maximum ammo, false otherwise.</returns>
        public bool NeedsReload()
        {
            return m_CurrentClipAmmo.Value < m_ClipSize;
        }

        /// <summary>
        /// Checks whether the clip contains the specified amount of ammunition.
        /// </summary>
        /// <param name="amount">The amount of ammo to check for. Defaults to 1.</param>
        /// <returns>True if the clip has at least the specified amount of ammo, false otherwise.</returns>
        public bool HasAmmo(int amount = 1)
        {
            return m_CurrentClipAmmo.Value >= amount;
        }

        /// <summary>
        /// Consumes the specified amount of ammunition from the clip.
        /// This should only be called on the owner's instance.
        /// </summary>
        /// <param name="amount">The amount of ammo to consume. Defaults to 1.</param>
        public void ConsumeAmmo(int amount = 1)
        {
            if (!IsOwner || !HasAmmo(amount)) return;

            m_CurrentClipAmmo.Value -= amount;
        }

        /// <summary>
        /// Retrieves the current and maximum ammunition values for the clip.
        /// </summary>
        /// <param name="current">Output parameter containing the current ammo count.</param>
        /// <param name="max">Output parameter containing the maximum clip capacity.</param>
        public void GetAmmoInfo(out int current, out int max)
        {
            current = m_CurrentClipAmmo.Value;
            max = m_ClipSize;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Callback invoked when the networked ammo value changes.
        /// </summary>
        /// <param name="previous">The previous ammo value.</param>
        /// <param name="current">The new ammo value.</param>
        private void OnAmmoChanged(int previous, int current)
        {
            RaiseClipAmmoChangedEvent();
        }

        /// <summary>
        /// Raises the clip ammo changed event to update UI and other listeners.
        /// </summary>
        private void RaiseClipAmmoChangedEvent()
        {
            // Only raise events for the owner when the weapon is equipped to avoid UI updates for unequipped weapons
            if (IsOwner && m_ModularWeapon.IsEquipped())
            {
                onClipAmmoChangedEvent?.Raise(new IntPair { value1 = m_CurrentClipAmmo.Value, value2 = m_ClipSize });
            }
        }

        #endregion
    }
}
