using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Editor;
using Unity.Netcode.Components;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Custom editor for <see cref="CoreMovement"/> that allows dynamic management of <see cref="IMovementAbility"/> components
    /// and displays real-time network state during play mode.
    /// </summary>
    [CustomEditor(typeof(CoreMovement))]
    [CanEditMultipleObjects]
    public class CoreMovementEditor : NetworkTransformEditor
    {
        #region Fields & Properties

        private string[] m_AbilityNames;
        private GUIStyle m_HeaderStyle;
        private int m_SelectedAbilityIndex;
        private List<Type> m_MovementAbilityTypes;

        private const float k_AddButtonSpacing = 60f;
        private const float k_AddedAbilityLabelWidth = 180f;

        #endregion

        #region Unity Methods

        /// <summary>
        /// Discovers all available <see cref="IMovementAbility"/> implementations and prepares dropdown data.
        /// </summary>
        public override void OnEnable()
        {
            base.OnEnable();
            FindAllAbilityImplementations();
            PrepareDropdownArrays();
        }

        /// <summary>
        /// Renders the custom inspector with network state, ability management, and NetworkTransform properties.
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawNetworkHeader();
            serializedObject.Update();
            InitializeStyles();

            EditorGUILayout.LabelField("Movement Abilities", m_HeaderStyle);
            EditorGUILayout.BeginVertical();
            {
                DrawCurrentAbilitiesSection();
                EditorGUILayout.Space(5);
                DrawAddAbilitySection();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);

            if (target.GetType() != typeof(NetworkTransform))
            {
                UniversalEditorSharedLogic.DrawDerivedProperties(serializedObject, target.GetType(), typeof(NetworkTransform));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Displays network state information. Only meaningful during play mode when spawned.
        /// </summary>
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

        /// <summary>
        /// Discovers all concrete MonoBehaviour implementations of <see cref="IMovementAbility"/>.
        /// </summary>
        private void FindAllAbilityImplementations()
        {
            m_MovementAbilityTypes = TypeCache.GetTypesDerivedFrom<IMovementAbility>()
                .Where(t => !t.IsInterface
                            && !t.IsAbstract
                            && typeof(MonoBehaviour).IsAssignableFrom(t))
                .ToList();
        }

        private void PrepareDropdownArrays()
        {
            if (m_MovementAbilityTypes != null && m_MovementAbilityTypes.Count > 0)
            {
                m_AbilityNames = m_MovementAbilityTypes.Select(t => t.Name).ToArray();
            }
            else
            {
                m_AbilityNames = new[] { "No abilities found" };
            }

            m_SelectedAbilityIndex = 0;
        }

        /// <summary>
        /// Displays currently attached abilities with Remove buttons.
        /// </summary>
        private void DrawCurrentAbilitiesSection()
        {
            CoreMovement coreMovement = (CoreMovement)target;
            var currentAbilities = coreMovement.GetComponents<IMovementAbility>();

            if (currentAbilities.Length == 0)
            {
                EditorGUILayout.LabelField("  No abilities added.", EditorStyles.miniLabel);
            }
            else
            {
                foreach (var ability in currentAbilities)
                {
                    var abilityComponent = ability as MonoBehaviour;
                    if (abilityComponent == null) continue;

                    Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    float buttonWidth = 60f;
                    Rect buttonRect = new Rect(controlRect.x + controlRect.width - buttonWidth, controlRect.y, buttonWidth, controlRect.height);
                    Rect labelRect = new Rect(controlRect.x, controlRect.y, k_AddedAbilityLabelWidth, controlRect.height);

                    EditorGUI.LabelField(labelRect, abilityComponent.GetType().Name);

                    if (GUI.Button(buttonRect, "Remove"))
                    {
                        Undo.DestroyObjectImmediate(abilityComponent);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        /// <summary>
        /// Displays ability selection dropdown and Add button. Add button is disabled if ability already exists.
        /// </summary>
        private void DrawAddAbilitySection()
        {
            CoreMovement coreMovement = (CoreMovement)target;

            if (m_AbilityNames == null || m_AbilityNames.Length == 0 || m_MovementAbilityTypes.Count == 0)
            {
                EditorGUILayout.LabelField("  No abilities found in project.", EditorStyles.miniLabel);
                return;
            }

            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            float buttonWidth = 45f;
            Rect buttonRect = new Rect(controlRect.x + controlRect.width - buttonWidth, controlRect.y, buttonWidth, controlRect.height);
            float popupWidth = controlRect.width - buttonWidth - k_AddButtonSpacing;
            Rect popupRect = new Rect(controlRect.x, controlRect.y, popupWidth, controlRect.height);

            m_SelectedAbilityIndex = EditorGUI.Popup(popupRect, m_SelectedAbilityIndex, m_AbilityNames);

            Type selectedType = m_MovementAbilityTypes[m_SelectedAbilityIndex];
            bool alreadyExists = coreMovement.GetComponent(selectedType) != null;

            using (new EditorGUI.DisabledScope(alreadyExists))
            {
                if (GUI.Button(buttonRect, "Add"))
                {
                    Undo.AddComponent(coreMovement.gameObject, selectedType);
                }
            }
        }

        #endregion
    }
}
