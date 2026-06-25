using System;
using UnityEditor;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using System.Reflection;
using Unity.Netcode.Components;
using System.Collections.Generic;
using UnityNetworkAnimatorEditor = Unity.Netcode.Editor.NetworkAnimatorEditor;
using UnityNetworkTransformEditor = Unity.Netcode.Editor.NetworkTransformEditor;

namespace Blocks.Gameplay.Core
{
    #region NetworkBehaviour Editor

    /// <summary>
    /// Universal custom editor for all <see cref="NetworkBehaviour"/> derived classes.
    /// Displays network state information and organizes properties by inheritance hierarchy.
    /// </summary>
    [CustomEditor(typeof(NetworkBehaviour), true)]
    [CanEditMultipleObjects]
    public class UniversalNetworkBehaviourEditor : Unity.Netcode.Editor.NetcodeEditorBase<NetworkBehaviour>
    {
        #region Unity Methods

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.LabelField($"{target.GetType().Name}", EditorStyles.boldLabel);
            if (Application.isPlaying && target is NetworkBehaviour networkBehaviourTarget &&
                networkBehaviourTarget.IsSpawned)
            {
                EditorGUILayout.LabelField($"Network Object ID: {networkBehaviourTarget.NetworkObjectId}",
                    EditorStyles.miniLabel);
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
            serializedObject.Update();

            if (target.GetType() != typeof(NetworkBehaviour))
            {
                UniversalEditorSharedLogic.DrawDerivedProperties(serializedObject, target.GetType(),
                    typeof(NetworkBehaviour));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }

            EditorGUILayout.LabelField("Base Network Behaviour Properties", EditorStyles.boldLabel);
            base.OnInspectorGUI();
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
    }

    #endregion

    #region Shared Editor Logic

    /// <summary>
    /// Shared utility logic for custom network component editors.
    /// Provides methods to draw properties organized by inheritance hierarchy with collapsible sections.
    /// </summary>
    public static class UniversalEditorSharedLogic
    {
        #region Fields & Properties

        private static GUIContent s_NetworkSettingsIcon;
        private static GUIContent s_ComponentSettingsIcon;
        private static GUIContent s_AdvancedSettingsIcon;

        #endregion

        #region Public Methods

        /// <summary>
        /// Draws properties for each class in the inheritance hierarchy between the most derived type
        /// and the Unity base type, organized in collapsible sections.
        /// </summary>
        /// <param name="serializedObject">The SerializedObject to draw properties from.</param>
        /// <param name="mostDerivedType">The most derived type in the hierarchy.</param>
        /// <param name="unityBaseType">The Unity base type to stop at (e.g., NetworkBehaviour).</param>
        public static void DrawDerivedProperties(SerializedObject serializedObject, Type mostDerivedType,
            Type unityBaseType)
        {
            InitializeIcons();

            // Build list of types in inheritance chain, excluding the Unity base type
            var typesToDrawInHierarchy = new List<Type>();
            Type currentTypeIterator = mostDerivedType;
            while (currentTypeIterator != null && currentTypeIterator != unityBaseType &&
                   unityBaseType.IsAssignableFrom(currentTypeIterator.BaseType))
            {
                typesToDrawInHierarchy.Add(currentTypeIterator);
                currentTypeIterator = currentTypeIterator.BaseType;
            }

            // Reverse to display base classes first, then derived classes
            typesToDrawInHierarchy.Reverse();

            foreach (Type typeToDraw in typesToDrawInHierarchy)
            {
                if (!HasDrawableFields(serializedObject, typeToDraw))
                {
                    continue;
                }

                // Persist foldout state across Unity sessions using SessionState
                string foldoutKey = $"UniversalEditor_{mostDerivedType.FullName}_{typeToDraw.Name}_Foldout";
                bool isExpanded = SessionState.GetBool(foldoutKey, true);

                GUIContent headerContent = GetHeaderContentForType(typeToDraw);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                bool newIsExpanded = EditorGUILayout.Foldout(isExpanded, headerContent, true, EditorStyles.foldoutHeader);
                EditorGUILayout.EndHorizontal();

                if (newIsExpanded)
                {
                    EditorGUILayout.Space(2);
                    EditorGUI.indentLevel++;
                    EditorHelper.DrawDeclaredProperties(serializedObject, typeToDraw);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);

                if (newIsExpanded != isExpanded)
                {
                    SessionState.SetBool(foldoutKey, newIsExpanded);
                }
            }
        }

        #endregion

        #region Private Methods

        private static void InitializeIcons()
        {
            if (s_NetworkSettingsIcon == null)
            {
                s_NetworkSettingsIcon = EditorGUIUtility.IconContent("NetworkAnimator Icon");
                s_ComponentSettingsIcon = EditorGUIUtility.IconContent("FilterByType");
                s_AdvancedSettingsIcon = EditorGUIUtility.IconContent("Settings");
            }
        }

        private static bool HasDrawableFields(SerializedObject serializedObject, Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.DeclaredOnly);

            return fields.Any(field =>
                (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null) &&
                field.GetCustomAttribute<NonSerializedAttribute>() == null &&
                field.GetCustomAttribute<HideInInspector>() == null &&
                field.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() == null &&
                serializedObject.FindProperty(field.Name) != null
            );
        }

        private static GUIContent GetHeaderContentForType(Type type)
        {
            // Choose icon based on type name to visually categorize different component types
            if (type.Name.Contains("Network"))
            {
                return new GUIContent($" {type.Name} Settings", s_NetworkSettingsIcon.image);
            }

            if (type.Name.Contains("Manager"))
            {
                return new GUIContent($" {type.Name} Settings", s_AdvancedSettingsIcon.image);
            }

            return new GUIContent($" {type.Name} Settings", s_ComponentSettingsIcon.image);
        }

        #endregion
    }

    #endregion

    #region NetworkTransform Editor

    /// <summary>
    /// Universal custom editor for all <see cref="NetworkTransform"/> derived classes.
    /// Extends Unity's NetworkTransform editor with support for derived class properties.
    /// </summary>
    [CustomEditor(typeof(NetworkTransform), true)]
    [CanEditMultipleObjects]
    public class UniversalNetworkTransformEditor : UnityNetworkTransformEditor
    {
        #region Unity Methods

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (target.GetType() != typeof(NetworkTransform))
            {
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.LabelField($"{target.GetType().Name}", EditorStyles.boldLabel);

                if (Application.isPlaying && target is NetworkTransform networkTransform && networkTransform.IsSpawned)
                {
                    EditorGUILayout.LabelField($"Network Object ID: {networkTransform.NetworkObjectId}",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                UniversalEditorSharedLogic.DrawDerivedProperties(serializedObject, target.GetType(),
                    typeof(NetworkTransform));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Base Network Transform Properties", EditorStyles.boldLabel);
            }

            serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }

        #endregion
    }

    #endregion

    #region NetworkAnimator Editor

    /// <summary>
    /// Universal custom editor for all <see cref="NetworkAnimator"/> derived classes.
    /// Extends Unity's NetworkAnimator editor with support for derived class properties
    /// and provides a collapsible section for base NetworkAnimator settings.
    /// </summary>
    [CustomEditor(typeof(NetworkAnimator), true)]
    [CanEditMultipleObjects]
    public class UniversalNetworkAnimatorEditor : UnityNetworkAnimatorEditor
    {
        #region Fields & Properties

        private bool m_ShowBaseProperties = true;

        #endregion

        #region Unity Methods

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Type targetType = target.GetType();

            if (targetType != typeof(NetworkAnimator))
            {
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.LabelField($"{targetType.Name}", EditorStyles.boldLabel);

                if (Application.isPlaying && target is NetworkAnimator networkAnimator && networkAnimator.IsSpawned)
                {
                    EditorGUILayout.LabelField($"Network Object ID: {networkAnimator.NetworkObjectId}",
                        EditorStyles.miniLabel);
                }

                Animator animator = (target as NetworkAnimator)?.Animator;
                if (animator != null)
                {
                    EditorGUILayout.LabelField($"Using Animator: {animator.name}", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                UniversalEditorSharedLogic.DrawDerivedProperties(serializedObject, targetType, typeof(NetworkAnimator));
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                var foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
                m_ShowBaseProperties = EditorGUILayout.Foldout(m_ShowBaseProperties, "Base Network Animator Settings",
                    true, foldoutStyle);
                EditorGUILayout.EndHorizontal();
            }

            if (targetType == typeof(NetworkAnimator) || m_ShowBaseProperties)
            {
                if (targetType != typeof(NetworkAnimator))
                {
                    // Exclude derived fields from base inspector to avoid duplicates
                    EditorGUILayout.BeginVertical("box");
                    string[] propertiesToExclude = GetDerivedFieldNames(targetType, typeof(NetworkAnimator));
                    DrawPropertiesExcluding(serializedObject, propertiesToExclude);
                }
                else
                {
                    base.OnInspectorGUI();
                }

                if (targetType != typeof(NetworkAnimator))
                {
                    EditorGUILayout.EndVertical();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Private Methods

        private string[] GetDerivedFieldNames(Type derivedType, Type baseType)
        {
            var fieldNames = new List<string>();
            Type currentType = derivedType;

            const BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            while (currentType != null && currentType != baseType)
            {
                var fields = currentType.GetFields(fieldFlags);
                foreach (var field in fields)
                {
                    bool isSerializable = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                    bool isHidden = field.GetCustomAttribute<HideInInspector>() != null || field.GetCustomAttribute<NonSerializedAttribute>() != null;
                    bool isCompilerGenerated = field.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() != null;

                    if (isSerializable && !isHidden && !isCompilerGenerated)
                    {
                        fieldNames.Add(field.Name);
                    }
                }

                currentType = currentType.BaseType;
            }

            return fieldNames.ToArray();
        }

        #endregion
    }

    #endregion
}
