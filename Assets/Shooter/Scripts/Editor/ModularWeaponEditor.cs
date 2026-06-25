using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Shooter.Editor
{
    /// <summary>
    /// Custom editor for <see cref="ModularWeapon"/> that provides a specialized inspector interface.
    /// Allows dynamic selection of shooting behaviors and firing mechanisms at edit-time,
    /// and displays network-related properties with enhanced styling.
    /// </summary>
    [CustomEditor(typeof(ModularWeapon))]
    public class ModularWeaponEditor : Unity.Netcode.Editor.NetcodeEditorBase<ModularWeapon>
    {
        #region Fields & Properties

        private List<Type> m_ShootingBehaviorTypes;
        private List<Type> m_FiringMechanismTypes;

        private string[] m_ShootingBehaviorNames;
        private string[] m_FiringMechanismNames;

        private GUIStyle m_HeaderStyle;
        private const float k_LabelWidth = 150f;


        #endregion

        #region Unity Methods

        public override void OnEnable()
        {
            base.OnEnable();
            FindAllImplementations();
            PrepareDropdownArrays();

        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();

            ModularWeapon weapon = (ModularWeapon)target;
            if (weapon == null) return;

            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.LabelField($"{target.GetType().Name}", EditorStyles.boldLabel);
            if (Application.isPlaying && weapon.IsSpawned)
            {
                EditorGUILayout.LabelField($"Network Object ID: {weapon.NetworkObjectId}", EditorStyles.miniLabel);
                if (weapon.IsOwner)
                {
                    EditorGUILayout.LabelField("Is Owner: Yes", EditorStyles.miniLabel);
                }

                if (weapon.IsServer)
                {
                    EditorGUILayout.LabelField("Is Server: Yes", EditorStyles.miniLabel);
                }

                if (weapon.IsHost)
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

            serializedObject.Update();

            EditorGUILayout.LabelField("Modular Behaviors", m_HeaderStyle);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginVertical();
            {
                DrawShootingBehaviorSelector(weapon, k_LabelWidth);
                DrawFiringMechanismSelector(weapon, k_LabelWidth);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Modular Weapon Properties", EditorStyles.boldLabel);
            DrawPropertiesExcluding(serializedObject, "m_Script");
            EditorGUILayout.Space(5);


            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.LabelField("Base Network Behaviour Properties", EditorStyles.boldLabel);
            base.OnInspectorGUI();

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Private Methods


        /// <summary>
        /// Finds all MonoBehaviour implementations of shooting behaviors and firing mechanisms.
        /// Uses TypeCache for efficient discovery of types implementing the required interfaces.
        /// </summary>
        private void FindAllImplementations()
        {
            List<Type> FindImplementations<T>()
            {
                return TypeCache.GetTypesDerivedFrom<T>()
                    .Where(t => !t.IsInterface && !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t))
                    .ToList();
            }

            m_ShootingBehaviorTypes = FindImplementations<IShootingBehavior>();
            m_FiringMechanismTypes = FindImplementations<IFiringMechanism>();
        }

        /// <summary>
        /// Prepares dropdown string arrays from the discovered type lists.
        /// Extracts type names for display in the inspector popup fields.
        /// </summary>
        private void PrepareDropdownArrays()
        {
            m_ShootingBehaviorNames = m_ShootingBehaviorTypes.Select(t => t.Name).ToArray();
            m_FiringMechanismNames = m_FiringMechanismTypes.Select(t => t.Name).ToArray();
        }

        /// <summary>
        /// Initializes custom GUI styles used in the inspector.
        /// </summary>
        private void InitializeStyles()
        {
            if (m_HeaderStyle == null)
            {
                m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, margin = { top = 10, bottom = 10 } };
            }
        }

        /// <summary>
        /// Draws a popup selector for choosing the shooting behavior component.
        /// Allows adding, removing, or changing the shooting behavior at edit-time with full undo support.
        /// </summary>
        /// <param name="weapon">The ModularWeapon being edited.</param>
        /// <param name="labelWidth">The width of the label portion of the control.</param>
        private void DrawShootingBehaviorSelector(ModularWeapon weapon, float labelWidth)
        {
            IShootingBehavior currentBehavior = weapon.GetComponentInChildren<IShootingBehavior>();
            int currentIndex = (currentBehavior != null)
                ? m_ShootingBehaviorTypes.FindIndex(t => t == currentBehavior.GetType())
                : -1;

            var displayNames = new List<string> { "None" };
            displayNames.AddRange(m_ShootingBehaviorNames);
            int displayIndex = currentIndex + 1;

            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(controlRect.x, controlRect.y, labelWidth, controlRect.height);
            Rect popupRect = new Rect(controlRect.x + labelWidth, controlRect.y, controlRect.width - labelWidth, controlRect.height);

            EditorGUI.LabelField(labelRect, "Shooting Behavior");
            int newDisplayIndex = EditorGUI.Popup(popupRect, displayIndex, displayNames.ToArray());

            if (newDisplayIndex != displayIndex)
            {
                Undo.RecordObject(weapon.gameObject, "Change Shooting Behavior");

                // Remove existing shooting behavior components
                foreach (var existing in weapon.GetComponentsInChildren<MonoBehaviour>().Where(c => c is IShootingBehavior))
                {
                    Undo.DestroyObjectImmediate(existing);
                }

                // Add new shooting behavior if a type was selected
                if (newDisplayIndex > 0)
                {
                    Type newType = m_ShootingBehaviorTypes[newDisplayIndex - 1];
                    Undo.AddComponent(weapon.gameObject, newType);
                }

                EditorUtility.SetDirty(weapon);
            }
        }

        /// <summary>
        /// Draws a popup selector for choosing the firing mechanism component.
        /// Allows adding, removing, or changing the firing mechanism at edit-time with full undo support.
        /// </summary>
        /// <param name="weapon">The ModularWeapon being edited.</param>
        /// <param name="labelWidth">The width of the label portion of the control.</param>
        private void DrawFiringMechanismSelector(ModularWeapon weapon, float labelWidth)
        {
            IFiringMechanism currentMechanism = weapon.GetComponent<IFiringMechanism>();
            int currentIndex = (currentMechanism != null)
                ? m_FiringMechanismTypes.FindIndex(t => t == currentMechanism.GetType())
                : -1;

            var displayNames = new List<string> { "None" };
            displayNames.AddRange(m_FiringMechanismNames);
            int displayIndex = currentIndex + 1;

            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(controlRect.x, controlRect.y, labelWidth, controlRect.height);
            Rect popupRect = new Rect(controlRect.x + labelWidth, controlRect.y, controlRect.width - labelWidth, controlRect.height);

            EditorGUI.LabelField(labelRect, "Firing Mechanism");
            int newDisplayIndex = EditorGUI.Popup(popupRect, displayIndex, displayNames.ToArray());

            if (newDisplayIndex != displayIndex)
            {
                Undo.RecordObject(weapon.gameObject, "Change Firing Mechanism");

                // Remove existing firing mechanism components
                foreach (var existing in weapon.GetComponents<IFiringMechanism>())
                {
                    if (existing is Component component)
                    {
                        Undo.DestroyObjectImmediate(component);
                    }
                }

                // Add new firing mechanism if a type was selected
                if (newDisplayIndex > 0)
                {
                    Type newType = m_FiringMechanismTypes[newDisplayIndex - 1];
                    Undo.AddComponent(weapon.gameObject, newType);
                }

                EditorUtility.SetDirty(weapon);
            }
        }

        #endregion
    }
}
