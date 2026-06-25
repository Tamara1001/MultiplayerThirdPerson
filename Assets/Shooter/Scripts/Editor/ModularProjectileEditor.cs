using System;
using UnityEngine;
using System.Linq;
using UnityEditor;
using Blocks.Gameplay.Core;
using System.Collections.Generic;

namespace Blocks.Gameplay.Shooter.Editor
{
    /// <summary>
    /// Custom editor for <see cref="ModularProjectile"/> that provides a modular interface
    /// for selecting and managing movement behaviors and projectile effects.
    /// Automatically discovers all implementations of <see cref="IMovementBehavior"/> and <see cref="IProjectileEffect"/>
    /// and presents them as dropdown selections in the inspector.
    /// </summary>
    [CustomEditor(typeof(ModularProjectile))]
    public class ModularProjectileEditor : Unity.Netcode.Editor.NetworkTransformEditor
    {
        #region Fields & Properties

        private List<Type> m_MovementBehaviorTypes;
        private List<Type> m_ProjectileEffectTypes;

        private string[] m_MovementBehaviorNames;
        private string[] m_ProjectileEffectNames;

        private GUIStyle m_HeaderStyle;
        private const float k_SLabelWidth = 150f;

        #endregion

        #region Unity Methods

        /// <summary>
        /// Called when the editor is enabled. Discovers all available behavior and effect types
        /// and prepares them for display in dropdown menus.
        /// </summary>
        public override void OnEnable()
        {
            base.OnEnable();
            FindAllImplementations();
            PrepareDropdownArrays();
        }

        /// <summary>
        /// Draws the custom inspector GUI with dropdown selectors for modular behaviors and effects,
        /// followed by the base <see cref="ModularProjectile"/> properties and network transform settings.
        /// </summary>
        public override void OnInspectorGUI()
        {
            InitializeStyles();

            ModularProjectile projectile = (ModularProjectile)target;
            if (projectile == null) return;

            serializedObject.Update();

            EditorGUILayout.LabelField("Modular Behaviors", m_HeaderStyle);

            EditorGUILayout.BeginVertical();
            {
                DrawMovementBehaviorSelector(projectile, k_SLabelWidth);
                DrawProjectileEffectSelector(projectile, k_SLabelWidth);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Modular Projectile Properties", EditorStyles.boldLabel);
            EditorHelper.DrawDeclaredProperties(serializedObject, typeof(ModularProjectile));
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("Base Network Transform Properties", EditorStyles.boldLabel);
            base.OnInspectorGUI();

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Discovers all MonoBehaviour types that implement <see cref="IMovementBehavior"/> and <see cref="IProjectileEffect"/>
        /// using Unity's TypeCache for efficient type lookup.
        /// </summary>
        private void FindAllImplementations()
        {
            List<Type> FindImplementations<T>()
            {
                return TypeCache.GetTypesDerivedFrom<T>()
                    .Where(t => !t.IsInterface && !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t))
                    .ToList();
            }

            m_MovementBehaviorTypes = FindImplementations<IMovementBehavior>();
            m_ProjectileEffectTypes = FindImplementations<IProjectileEffect>();
        }

        /// <summary>
        /// Converts the discovered type lists into string arrays containing type names
        /// for display in dropdown menus.
        /// </summary>
        private void PrepareDropdownArrays()
        {
            m_MovementBehaviorNames = m_MovementBehaviorTypes.Select(t => t.Name).ToArray();
            m_ProjectileEffectNames = m_ProjectileEffectTypes.Select(t => t.Name).ToArray();
        }

        /// <summary>
        /// Initializes the custom GUI style used for section headers in the inspector.
        /// </summary>
        private void InitializeStyles()
        {
            if (m_HeaderStyle == null)
            {
                m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, margin = { top = 10, bottom = 10 } };
            }
        }

        /// <summary>
        /// Draws a dropdown selector for choosing the active <see cref="IMovementBehavior"/> component.
        /// Handles adding, removing, and swapping behavior components with proper Undo support.
        /// </summary>
        /// <param name="projectile">The target projectile being edited.</param>
        /// <param name="labelWidth">The width of the label portion of the control.</param>
        private void DrawMovementBehaviorSelector(ModularProjectile projectile, float labelWidth)
        {
            IMovementBehavior currentBehavior = projectile.GetComponent<IMovementBehavior>();
            int currentIndex = (currentBehavior != null)
                ? m_MovementBehaviorTypes.FindIndex(t => t == currentBehavior.GetType())
                : -1;

            // Prepend "None" option to the dropdown list
            var displayNames = new List<string> { "None" };
            displayNames.AddRange(m_MovementBehaviorNames);

            // Offset index by 1 to account for "None" option at index 0
            int displayIndex = currentIndex + 1;

            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(controlRect.x, controlRect.y, labelWidth, controlRect.height);
            Rect popupRect = new Rect(controlRect.x + labelWidth, controlRect.y, controlRect.width - labelWidth, controlRect.height);

            EditorGUI.LabelField(labelRect, "Movement Behavior");
            int newDisplayIndex = EditorGUI.Popup(popupRect, displayIndex, displayNames.ToArray());

            if (newDisplayIndex != displayIndex)
            {
                // Record the change for Unity's Undo system
                Undo.RecordObject(projectile.gameObject, "Change Movement Behavior");

                // Remove all existing movement behavior components
                foreach (var existing in projectile.GetComponents<IMovementBehavior>())
                {
                    if (existing is Component component) Undo.DestroyObjectImmediate(component);
                }

                // Add the newly selected behavior component (if not "None")
                if (newDisplayIndex > 0)
                {
                    // Subtract 1 to account for "None" offset
                    Type newType = m_MovementBehaviorTypes[newDisplayIndex - 1];
                    Undo.AddComponent(projectile.gameObject, newType);
                }

                EditorUtility.SetDirty(projectile);
            }
        }

        /// <summary>
        /// Draws a dropdown selector for choosing the active <see cref="IProjectileEffect"/> component.
        /// Handles adding, removing, and swapping effect components with proper Undo support.
        /// </summary>
        /// <param name="projectile">The target projectile being edited.</param>
        /// <param name="labelWidth">The width of the label portion of the control.</param>
        private void DrawProjectileEffectSelector(ModularProjectile projectile, float labelWidth)
        {
            IProjectileEffect currentEffect = projectile.GetComponent<IProjectileEffect>();
            int currentIndex = (currentEffect != null)
                ? m_ProjectileEffectTypes.FindIndex(t => t == currentEffect.GetType())
                : -1;

            // Prepend "None" option to the dropdown list
            var displayNames = new List<string> { "None" };
            displayNames.AddRange(m_ProjectileEffectNames);

            // Offset index by 1 to account for "None" option at index 0
            int displayIndex = currentIndex + 1;

            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(controlRect.x, controlRect.y, labelWidth, controlRect.height);
            Rect popupRect = new Rect(controlRect.x + labelWidth, controlRect.y, controlRect.width - labelWidth, controlRect.height);

            EditorGUI.LabelField(labelRect, "Projectile Effect");
            int newDisplayIndex = EditorGUI.Popup(popupRect, displayIndex, displayNames.ToArray());

            if (newDisplayIndex != displayIndex)
            {
                // Record the change for Unity's Undo system
                Undo.RecordObject(projectile.gameObject, "Change Projectile Effect");

                // Remove all existing projectile effect components
                foreach (var existing in projectile.GetComponents<IProjectileEffect>())
                {
                    if (existing is Component component) Undo.DestroyObjectImmediate(component);
                }

                // Add the newly selected effect component (if not "None")
                if (newDisplayIndex > 0)
                {
                    // Subtract 1 to account for "None" offset
                    Type newType = m_ProjectileEffectTypes[newDisplayIndex - 1];
                    Undo.AddComponent(projectile.gameObject, newType);
                }

                EditorUtility.SetDirty(projectile);
            }
        }

        #endregion
    }
}
