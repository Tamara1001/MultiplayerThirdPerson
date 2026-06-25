using System;
using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// A serializable data container that holds all configuration properties for a weapon.
    /// This class is used by <see cref="ModularWeapon"/> to define weapon behavior, including
    /// damage, recoil, ammo capacity, spread mechanics, visual effects, and animation settings.
    /// <see cref="WeaponData"/> is now a ScriptableObject, allowing for reusable weapon configurations.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Shooter/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        #region Fields & Properties

        [Header("General Settings")]
        [Tooltip("The display name of the weapon.")]
        public string weaponName = "Weapon";

        [Tooltip("The ID that maps to an animation in the Aiming/Idle Blend Trees.")]
        public int weaponTypeID = 1;

        [Tooltip("The icon image for the weapon.")]
        public Sprite weaponIcon;

        [Tooltip("The reticle image for the weapon.")]
        public Sprite reticleImage;

        [Tooltip("Base size of the reticle in pixels.")]
        public float reticleBaseSize = 80f;

        [Tooltip("How much the reticle grows per degree of spread.")]
        public float reticleSpreadMultiplier = 100f;

        [Header("Attachment Node Names")]
        [Tooltip("The name of the AttachableNode GameObject for holding the weapon when aiming.")]
        public string handAttachmentNodeName = "Right_Hand_Attach";

        [Tooltip("The name of the AttachableNode GameObject for holstering the weapon when not aiming.")]
        public string idleAttachmentNodeName = "Spine_Attach";

        [Header("Weapon Properties")]
        [Tooltip("The amount of damage the weapon deals per shot.")]
        public float weaponDamage = 10f;

        [Tooltip("Recoil vector applied when firing (pitch, yaw, roll).")]
        public Vector3 recoil = new Vector3(0, 0.1f, 0.1f);

        [Tooltip("Speed at which recoil returns to neutral position.")]
        public float recoilReturnSpeed = 10f;

        [Tooltip("Layer mask defining which objects can be hit by weapon shots.")]
        public LayerMask hitMask = -1;

        [Tooltip("Minimum distance from target to allow shooting. Player won't shoot if target is closer than this.")]
        public float minShootingDistance = 1.5f;

        [Header("Ammo Settings")]
        [Tooltip("Maximum number of rounds in a clip.")]
        public int clipSize = 30;

        [Tooltip("Time in seconds required to complete a reload.")]
        public float reloadTime = 2.0f;

        [Header("Bloom / Spread Settings")]
        [Tooltip("The minimum spread angle (perfect accuracy) in degrees.")]
        public float minSpreadAngle = 0.5f;

        [Tooltip("The maximum spread angle (worst accuracy) in degrees.")]
        public float maxSpreadAngle = 5.0f;

        [Tooltip("How much the spread increases with each shot.")]
        public float spreadIncreasePerShot = 1.0f;

        [Tooltip("How quickly the spread angle recovers per second when not firing.")]
        public float spreadRecoveryRate = 3.0f;

        [Tooltip("Delay in seconds after firing before spread starts to recover.")]
        public float spreadRecoveryDelay = 0.2f;

        [Header("Animation Rigging Overrides")]
        [Tooltip("Offset for the spine's aim constraint.")]
        public Vector3 spineOffset;

        [Tooltip("Weight for the spine's aim constraint.")]
        [Range(0, 1)]
        public float spineAimWeight = 1f;

        [Header("Effects")]
        [Tooltip("Prefab instantiated at the muzzle when firing.")]
        public GameObject muzzleFlashPrefab;

        [Tooltip("Prefab for ejected bullet shell casings.")]
        public GameObject bulletShellPrefab;

        [Tooltip("Prefab spawned at impact points.")]
        public GameObject impactEffectPrefab;

        [Tooltip("Duration in seconds before impact effects are destroyed.")]
        public float impactEffectDuration = 2f;

        [Tooltip("Sound definition for weapon shooting.")]
        public SoundDef soundDefShoot;

        [Tooltip("Sound definition for bullet ricochets.")]
        public SoundDef soundDefRicochet;

        [Tooltip("Radius of the tracer line rendered when firing.")]
        public float tracerRadius = 0.02f;

        [Tooltip("Duration in seconds that the tracer is visible.")]
        public float tracerDuration = 0.1f;

        [Tooltip("Material used for rendering the bullet tracer.")]
        public Material tracerMaterial;

        [Tooltip("Intensity of camera shake when firing.")]
        public float cameraShakeIntensity = 0.02f;

        #endregion
    }
}
