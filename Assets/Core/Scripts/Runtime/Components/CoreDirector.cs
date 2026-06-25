using UnityEngine;
using UnityEngine.Audio;
using Unity.Cinemachine;

namespace Blocks.Gameplay.Core
{
    public class CoreDirector : MonoBehaviour
    {
        private static CoreDirector s_Instance;
        public static CoreDirector GetInstance() => s_Instance;

        [Header("Audio Settings")]
        [Tooltip("The Unity Audio Mixer for managing audio groups and effects.")]
        [SerializeField] private AudioMixer unityAudioMixer;
        [Tooltip("Prefab with AudioSource and audio filter components pre-attached. Leave empty to create at runtime (slower).")]
        [SerializeField] private GameObject soundGameObjectPrefab;
        [Tooltip("Maximum number of pooled sound GameObjects.")]
        [SerializeField] private int maxSoundGameObjects = 100;
        [Tooltip("Maximum number of concurrent sound emitters.")]
        [SerializeField] private int maxSoundEmitters = 20;

        [Header("Camera Shake Settings")]
        [Tooltip("The Cinemachine Impulse Source component for camera shake effects.")]
        [SerializeField] private CinemachineImpulseSource impulseSource;

        private bool m_SoundMute;
        private SoundSystem m_SoundSystem;
        private SoundGameObjectPool m_SoundGameObjects;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            m_SoundSystem = new SoundSystem();
            m_SoundMute = false;
            var audioListener = FindAnyObjectByType<AudioListener>();
            if (audioListener != null)
            {
                m_SoundGameObjects = new SoundGameObjectPool("SoundSystemSources", maxSoundGameObjects, soundGameObjectPrefab);
                m_SoundSystem.Init(audioListener.transform, maxSoundEmitters, m_SoundGameObjects, unityAudioMixer);
            }
            else
            {
                Debug.LogWarning("[CoreDirector] No AudioListener found in scene. Sound system will not be initialized.");
            }

            if (impulseSource == null)
            {
                impulseSource = GetComponent<CinemachineImpulseSource>();
                if (impulseSource == null)
                {
                    Debug.LogWarning("[CoreDirector] No CinemachineImpulseSource found. Camera shake will not work. Add a CinemachineImpulseSource component to the CoreDirector prefab.");
                }
            }
        }

        private void Update()
        {
            if (m_SoundSystem != null)
            {
                m_SoundSystem.UpdateSoundSystem(m_SoundMute);
            }
        }

        /// <summary>
        /// Mutes or unmutes the sound system.
        /// </summary>
        public void SetMute(bool mute)
        {
            m_SoundMute = mute;
        }

        /// <summary>
        /// Begins a new sound request using a fluent builder pattern.
        /// </summary>
        /// <param name="soundDef">The SoundDef to play.</param>
        /// <returns>A SoundRequestBuilder to configure and play the sound.</returns>
        public static SoundRequestBuilder RequestAudio(SoundDef soundDef)
        {
            var instance = GetInstance();
            if (instance == null || instance.m_SoundSystem == null)
            {
                Debug.LogError("[CoreDirector] SoundSystem is not initialized. Ensure CoreDirector exists in the scene and an AudioListener is present.");
                // Return a dummy builder that does nothing to prevent null reference exceptions
                return new SoundRequestBuilder(null, null);
            }
            return new SoundRequestBuilder(instance.m_SoundSystem, soundDef);
        }

        /// <summary>
        /// Begins a new camera shake request using a fluent builder pattern.
        /// </summary>
        /// <returns>A CameraShakeBuilder to configure and execute the camera shake.</returns>
        public static CameraShakeBuilder RequestCameraShake()
        {
            var instance = GetInstance();
            if (instance == null || instance.impulseSource == null)
            {
                Debug.LogError("[CoreDirector] ImpulseSource is not initialized. Ensure CoreDirector exists and has a CinemachineImpulseSource component.");
                // Return a builder with null source - it will safely do nothing
                return new CameraShakeBuilder(null);
            }
            return new CameraShakeBuilder(instance.impulseSource);
        }

        /// <summary>
        /// Creates a visual effect builder using a prefab GameObject.
        /// </summary>
        public static EffectBuilder CreatePrefabEffect(GameObject prefab)
        {
            return new EffectBuilder(prefab);
        }

        /// <summary>
        /// Creates a tracer effect between two points using a cylinder primitive.
        /// </summary>
        public static EffectBuilder CreateTracer(Vector3 startPosition, Vector3 endPosition)
        {
            return new EffectBuilder(PrimitiveType.Cylinder)
                .WithTracerPositioning(startPosition, endPosition)
                .WithName("Tracer")
                .WithDuration(0.1f);
        }

        /// <summary>
        /// Creates an impact marker at a position using a sphere primitive.
        /// </summary>
        public static EffectBuilder CreateImpactMarker(Vector3 position)
        {
            return new EffectBuilder(PrimitiveType.Sphere)
                .WithPosition(position)
                .WithScale(Vector3.one * 0.1f)
                .WithName("ImpactMarker")
                .WithDuration(0.5f);
        }
    }
}
