using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Blocks.Gameplay.Core;
using System.Collections.Generic;

/// <summary>
/// Custom editor for <see cref="ModularInteractable"/> that provides a visual interface
/// for adding and removing <see cref="IInteractionEffect"/> components at edit time.
/// </summary>
[CustomEditor(typeof(ModularInteractable))]
public class ModularInteractableEditor : Editor
{
    #region Fields & Properties

    private string[] m_EffectNames;
    private GUIStyle m_HeaderStyle;
    private int m_SelectedEffectIndex;
    private List<Type> m_InteractionEffectTypes;

    private const float k_AddButtonSpacing = 60f;
    private const float k_AddedEffectLabelWidth = 180f;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        FindAllImplementations();
        PrepareDropdownArrays();
    }

    /// <summary>
    /// Draws the custom inspector GUI with sections for managing interaction effects
    /// and the default inspector for remaining properties.
    /// </summary>
    public override void OnInspectorGUI()
    {
        InitializeStyles();

        serializedObject.Update();

        EditorGUILayout.LabelField("Modular Effects", m_HeaderStyle);
        EditorGUILayout.BeginVertical();
        {
            DrawCurrentEffectsSection();
            EditorGUILayout.Space(5);
            DrawAddEffectSection();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Private Methods

    private void InitializeStyles()
    {
        if (m_HeaderStyle == null)
        {
            m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, margin = { top = 10, bottom = 10 } };
        }
    }

    private void FindAllImplementations()
    {
        // Filter to MonoBehaviour-based effects only, since they can be added as components
        m_InteractionEffectTypes = TypeCache.GetTypesDerivedFrom<IInteractionEffect>()
            .Where(t => !t.IsInterface && !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t))
            .ToList();
    }

    private void PrepareDropdownArrays()
    {
        if (m_InteractionEffectTypes != null && m_InteractionEffectTypes.Count > 0)
        {
            m_EffectNames = m_InteractionEffectTypes.Select(t => t.Name).ToArray();
        }
        else
        {
            m_EffectNames = new[] { "No effects found" };
        }

        m_SelectedEffectIndex = 0;
    }

    private void DrawCurrentEffectsSection()
    {
        ModularInteractable interactable = (ModularInteractable)target;
        var currentEffects = interactable.GetComponents<IInteractionEffect>();

        if (currentEffects.Length == 0)
        {
            EditorGUILayout.LabelField("  No effects added.", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var effect in currentEffects)
            {
                var effectComponent = effect as MonoBehaviour;
                if (effectComponent == null) continue;

                Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                float buttonWidth = 60f;
                Rect buttonRect = new Rect(controlRect.x + controlRect.width - buttonWidth, controlRect.y, buttonWidth, controlRect.height);
                Rect labelRect = new Rect(controlRect.x, controlRect.y, k_AddedEffectLabelWidth, controlRect.height);

                EditorGUI.LabelField(labelRect, effectComponent.GetType().Name);
                if (GUI.Button(buttonRect, "Remove"))
                {
                    Undo.DestroyObjectImmediate(effectComponent);
                    // Exit GUI to prevent layout errors from destroyed component still being referenced
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private void DrawAddEffectSection()
    {
        ModularInteractable interactable = (ModularInteractable)target;

        if (m_EffectNames == null || m_EffectNames.Length == 0 || m_InteractionEffectTypes.Count == 0)
        {
            EditorGUILayout.LabelField("  No effects found in project.", EditorStyles.miniLabel);
            return;
        }

        Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        float buttonWidth = 45f;

        Rect buttonRect = new Rect(controlRect.x + controlRect.width - buttonWidth, controlRect.y, buttonWidth, controlRect.height);

        float popupWidth = controlRect.width - buttonWidth - k_AddButtonSpacing;
        Rect popupRect = new Rect(controlRect.x, controlRect.y, popupWidth, controlRect.height);

        m_SelectedEffectIndex = EditorGUI.Popup(popupRect, m_SelectedEffectIndex, m_EffectNames);

        Type selectedType = m_InteractionEffectTypes[m_SelectedEffectIndex];
        bool alreadyExists = interactable.GetComponent(selectedType) != null;

        // Prevent adding duplicate effects by disabling the button if effect already exists
        using (new EditorGUI.DisabledScope(alreadyExists))
        {
            if (GUI.Button(buttonRect, "Add"))
            {
                Undo.AddComponent(interactable.gameObject, selectedType);
            }
        }
    }

    #endregion
}
