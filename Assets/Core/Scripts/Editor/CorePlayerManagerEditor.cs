using System;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Unity.Netcode;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Custom inspector for <see cref="CorePlayerManager"/> that provides addon management UI.
    /// </summary>
    [CustomEditor(typeof(CorePlayerManager))]
    [CanEditMultipleObjects]
    public class CorePlayerManagerEditor : Editor
    {
        #region Fields & Properties

        private string[] m_AddonNames;
        private GUIStyle m_HeaderStyle;
        private int m_SelectedAddonIndex;
        private List<Type> m_PlayerAddonTypes;

        private const float k_AddButtonSpacing = 60f;
        private const float k_AddedAddonLabelWidth = 180f;

        #endregion

        #region Unity Methods

        private void OnEnable()
        {
            FindAllAddonImplementations();
            PrepareDropdownArrays();
        }

        #endregion

        #region Public Methods

        public override void OnInspectorGUI()
        {
            DrawNetworkHeader();
            serializedObject.Update();

            InitializeStyles();
            EditorGUILayout.LabelField("Player Addons", m_HeaderStyle);
            EditorGUILayout.BeginVertical();
            {
                DrawCurrentAddonsSection();
                EditorGUILayout.Space(5);
                DrawAddAddonSection();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);
            UniversalEditorSharedLogic.DrawDerivedProperties(serializedObject, typeof(CorePlayerManager), typeof(NetworkBehaviour));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("Base Network Behaviour Properties", EditorStyles.boldLabel);

            serializedObject.ApplyModifiedProperties();
            DrawBaseNetworkBehaviourProperties();
        }

        #endregion

        #region Private Methods

        private void DrawNetworkHeader()
        {
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.LabelField($"{target.GetType().Name}", EditorStyles.boldLabel);

            if (Application.isPlaying && target is NetworkBehaviour networkBehaviourTarget && networkBehaviourTarget.IsSpawned)
            {
                EditorGUILayout.LabelField($"Network Object ID: {networkBehaviourTarget.NetworkObjectId}", EditorStyles.miniLabel);

                if (networkBehaviourTarget.IsOwner)
                {
                    EditorGUILayout.LabelField("Is Owner: Yes", EditorStyles.miniLabel);
                }

                if (networkBehaviourTarget.IsServer)
                {
                    EditorGUILayout.LabelField("Is Server: Yes", EditorStyles.miniLabel);
                }

                if (networkBehaviourTarget.IsHost)
                {
                    EditorGUILayout.LabelField("Is Host: Yes", EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Network Object ID: Not Spawned", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void InitializeStyles()
        {
            if (m_HeaderStyle == null)
            {
                m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, margin = { top = 10, bottom = 10 } };
            }
        }

        private void FindAllAddonImplementations()
        {
            m_PlayerAddonTypes = TypeCache.GetTypesDerivedFrom<IPlayerAddon>()
                .Where(t => !t.IsInterface
                            && !t.IsAbstract
                            && typeof(MonoBehaviour).IsAssignableFrom(t))
                .ToList();
        }

        private void PrepareDropdownArrays()
        {
            if (m_PlayerAddonTypes != null && m_PlayerAddonTypes.Count > 0)
            {
                m_AddonNames = m_PlayerAddonTypes.Select(t => t.Name).ToArray();
            }
            else
            {
                m_AddonNames = new[] { "No addons found" };
            }

            m_SelectedAddonIndex = 0;
        }

        private void DrawCurrentAddonsSection()
        {
            CorePlayerManager playerManager = (CorePlayerManager)target;
            var currentAddons = playerManager.GetComponents<IPlayerAddon>();

            if (currentAddons.Length == 0)
            {
                EditorGUILayout.LabelField("  No addons added.", EditorStyles.miniLabel);
            }
            else
            {
                foreach (var addon in currentAddons)
                {
                    var addonComponent = addon as MonoBehaviour;
                    if (addonComponent == null) continue;

                    Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    float buttonWidth = 60f;
                    Rect buttonRect = new Rect(controlRect.x + controlRect.width - buttonWidth, controlRect.y, buttonWidth, controlRect.height);
                    Rect labelRect = new Rect(controlRect.x, controlRect.y, k_AddedAddonLabelWidth, controlRect.height);

                    EditorGUI.LabelField(labelRect, addonComponent.GetType().Name);

                    if (GUI.Button(buttonRect, "Remove"))
                    {
                        Undo.DestroyObjectImmediate(addonComponent);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void DrawAddAddonSection()
        {
            CorePlayerManager playerManager = (CorePlayerManager)target;

            if (m_AddonNames == null || m_AddonNames.Length == 0 || m_PlayerAddonTypes.Count == 0)
            {
                EditorGUILayout.LabelField("  No addons found in project.", EditorStyles.miniLabel);
                return;
            }

            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            float buttonWidth = 45f;

            Rect buttonRect = new Rect(controlRect.x + controlRect.width - buttonWidth, controlRect.y, buttonWidth, controlRect.height);

            float popupWidth = controlRect.width - buttonWidth - k_AddButtonSpacing;
            Rect popupRect = new Rect(controlRect.x, controlRect.y, popupWidth, controlRect.height);

            m_SelectedAddonIndex = EditorGUI.Popup(popupRect, m_SelectedAddonIndex, m_AddonNames);

            Type selectedType = m_PlayerAddonTypes[m_SelectedAddonIndex];
            bool alreadyExists = playerManager.GetComponent(selectedType) != null;

            using (new EditorGUI.DisabledScope(alreadyExists))
            {
                if (GUI.Button(buttonRect, "Add"))
                {
                    Undo.AddComponent(playerManager.gameObject, selectedType);
                }
            }
        }

        private void DrawBaseNetworkBehaviourProperties()
        {
            SerializedProperty scriptProp = serializedObject.FindProperty("m_Script");
            if (scriptProp != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(scriptProp);
                }
            }
        }

        #endregion
    }
}
