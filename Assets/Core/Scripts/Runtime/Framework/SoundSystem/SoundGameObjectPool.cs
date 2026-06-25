using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Creates a pool of GameSoundObjects (AudioSource and additional components such as LowPassFilter).
    /// These are allocated by SoundEmitters.
    /// A SoundEmitter can play multiple GameSoundObjects.
    /// GameSoundObjects can be reserved by the SoundEmitter if required, to ensure that the sound can always play successfully.
    /// </summary>
    public class SoundGameObjectPool
    {
        #region Fields & Properties

        /// <summary>
        /// The next available SoundGameObject in the linked list structure.
        /// </summary>
        public SoundGameObject NextSoundGameObject;

        /// <summary>
        /// The current count of available SoundGameObjects in the pool.
        /// </summary>
        public int AvailableSoundObjectCount;

        /// <summary>
        /// List containing all SoundGameObjects managed by this pool.
        /// </summary>
        private readonly List<SoundGameObject> m_SoundGameObjectList;

        /// <summary>
        /// All SoundGameObjects are instantiated under this parent.
        /// </summary>
        private readonly GameObject m_SourceHolder;

        /// <summary>
        /// Required prefab with audio components pre-attached (AudioSource, AudioDistortionFilter, AudioLowPassFilter, AudioHighPassFilter).
        /// </summary>
        private readonly GameObject m_Prefab;

#if UNITY_EDITOR
        private int m_SoundObjectCounter;
#endif

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new SoundGameObjectPool with the specified parameters.
        /// </summary>
        /// <param name="parentGameObjectName">Name for the parent GameObject that will hold all audio sources.</param>
        /// <param name="maxSoundGameObjects">Maximum number of SoundGameObjects to create in the pool.</param>
        /// <param name="prefab">Required prefab with audio components pre-attached (AudioSource, AudioDistortionFilter, AudioLowPassFilter, AudioHighPassFilter).</param>
        public SoundGameObjectPool(string parentGameObjectName, int maxSoundGameObjects, GameObject prefab)
        {
            if (maxSoundGameObjects <= 0)
            {
                return;
            }

            if (prefab == null)
            {
                throw new System.ArgumentNullException(nameof(prefab), "Prefab is required for SoundGameObjectPool. Please provide a prefab with AudioSource and filter components.");
            }

            m_SoundGameObjectList = new List<SoundGameObject>();
            m_Prefab = prefab;

            // All SoundGameObjects are instantiated under this parent.
            m_SourceHolder = new GameObject(parentGameObjectName);
            Object.DontDestroyOnLoad(m_SourceHolder);

            AvailableSoundObjectCount = maxSoundGameObjects;
            SoundGameObject lastAllocated = null;

            for (int i = 0; i < maxSoundGameObjects; i++)
            {
                SoundGameObject soundGameObject = Create();
                if (lastAllocated != null)
                {
                    lastAllocated.NextAvailable = soundGameObject;
                }

                lastAllocated = soundGameObject;
                m_SoundGameObjectList.Add(soundGameObject);
            }

            m_SoundGameObjectList[maxSoundGameObjects - 1].NextAvailable = m_SoundGameObjectList[0];
            NextSoundGameObject = m_SoundGameObjectList[0];
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new SoundGameObject with all required audio components.
        /// </summary>
        /// <returns>A new SoundGameObject instance with initialized audio components.</returns>
        public SoundGameObject Create()
        {
            GameObject go;

#if UNITY_EDITOR
            go = Object.Instantiate(m_Prefab, m_SourceHolder.transform);
            go.name = "SGO" + " " + (m_SoundObjectCounter++);
#else
            go = Object.Instantiate(m_Prefab, m_SourceHolder.transform);
            go.name = "SGO";
#endif

            SoundGameObject.AudioComponentData audioComponentData = new SoundGameObject.AudioComponentData()
            {
                ASource = go.GetComponent<AudioSource>(),
                DistortionFilter = go.GetComponent<AudioDistortionFilter>(),
                LowPassFilter = go.GetComponent<AudioLowPassFilter>(),
                HighPassFilter = go.GetComponent<AudioHighPassFilter>()
            };

            SoundGameObject audioSourceObject = new SoundGameObject(m_SourceHolder, audioComponentData);
            return audioSourceObject;
        }

        /// <summary>
        /// Replace is called only in the case where its parent has been destroyed, if using Transform instead of Position when playing the SoundEmitter.
        /// In such a case, we need replace the broken SoundGameObject with a newly created one and ensure that the linked list is still valid.
        /// </summary>
        /// <param name="oldSgo">The old SoundGameObject to replace.</param>
        /// <param name="newSgo">The new SoundGameObject to use as replacement.</param>
        /// <returns>True if replacement was successful, false if the old SoundGameObject wasn't found.</returns>
        public bool Replace(SoundGameObject oldSgo, SoundGameObject newSgo)
        {
            for (int i = 0; i < m_SoundGameObjectList.Count; i++)
            {
                if (m_SoundGameObjectList[i] == oldSgo)
                {
                    // Replace SoundGameObject with new (required if parent destroys the SoundGameObject)
                    m_SoundGameObjectList[i] = newSgo;

                    foreach (var t in m_SoundGameObjectList)
                    {
                        if (t.NextAvailable != null && t.NextAvailable == oldSgo)
                        {
                            // This would only be found if the SoundGameObject had been destroyed whilst in its SoundGameObjectPool. Unlikely
                            t.NextAvailable = newSgo;
                        }
                    }

                    return true;
                }
            }

            // Failed to patch up linked list
            return false;
        }

        #endregion
    }
}
