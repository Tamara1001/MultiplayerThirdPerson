namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Defines the possible states a weapon can be in during its operational lifecycle.
    /// Used by <see cref="WeaponStateManager"/> to control weapon behavior and state transitions.
    /// </summary>
    public enum WeaponState
    {
        /// <summary>
        /// The weapon is ready to fire and can accept fire input.
        /// </summary>
        ReadyToFire,

        /// <summary>
        /// The weapon is currently executing a fire action.
        /// </summary>
        Firing,

        /// <summary>
        /// The weapon is reloading ammunition and cannot fire.
        /// </summary>
        Reloading,

        /// <summary>
        /// The weapon is in cooldown period after firing and cannot fire again yet.
        /// </summary>
        Cooldown,

        /// <summary>
        /// The weapon has no ammunition and cannot fire until reloaded.
        /// </summary>
        Empty
    }
}
