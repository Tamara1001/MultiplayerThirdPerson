using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;

[CustomEditor(typeof(Readme))]
[InitializeOnLoad]
public class ReadmeEditor : Editor
{
#pragma warning disable UDR0001
    static string s_ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";


    static string s_ReadmeSourceDirectory = "Assets/TutorialInfo";

    const float k_Space = 16f;

    static ReadmeEditor()
    {
        EditorApplication.delayCall += SelectReadmeAutomatically;
    }
#pragma warning restore UDR0001

    static void RemoveTutorial()
    {
        if (EditorUtility.DisplayDialog("Remove Readme Assets",

            $"All contents under {s_ReadmeSourceDirectory} and all Readme assets will be removed, are you sure you want to proceed?",
            "Proceed",
            "Cancel"))
        {
            if (Directory.Exists(s_ReadmeSourceDirectory))
            {
                AssetDatabase.DeleteAsset(s_ReadmeSourceDirectory);
            }
            else
            {
                Debug.Log($"Could not find the Readme folder at {s_ReadmeSourceDirectory}");
            }

            // Find and delete ALL Readme assets
            string[] readmeGuids = AssetDatabase.FindAssets("t:Readme");
            foreach (string guid in readmeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }
    }

    static void SelectReadmeAutomatically()
    {
        if (!SessionState.GetBool(s_ShowedReadmeSessionStateName, false))
        {
            var readme = SelectReadme();
            SessionState.SetBool(s_ShowedReadmeSessionStateName, true);

            if (readme && !readme.loadedLayout)
            {
                LoadLayout();
                readme.loadedLayout = true;
            }
        }
    }

    static void LoadLayout()
    {
        var assembly = typeof(EditorApplication).Assembly;
        var windowLayoutType = assembly.GetType("UnityEditor.WindowLayout", true);
        var method = windowLayoutType.GetMethod("LoadWindowLayout", BindingFlags.Public | BindingFlags.Static);
        method.Invoke(null, new object[] { Path.Combine(Application.dataPath, "TutorialInfo/Layout.wlt"), false });
    }

    static Readme SelectReadme()
    {
        string[] readmeGuids = AssetDatabase.FindAssets("t:Readme");
        
        // Prioritize the main readme at root level
        foreach (string guid in readmeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == "Assets/Readme.asset")
            {
                var readme = AssetDatabase.LoadAssetAtPath<Readme>(path);
                if (readme != null)
                {
                    Selection.objects = new UnityEngine.Object[] { readme };
                    return readme;
                }
            }
        }
        
        // Fallback to first available readme
        if (readmeGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(readmeGuids[0]);
            var readme = AssetDatabase.LoadAssetAtPath<Readme>(path);
            if (readme != null)
            {
                Selection.objects = new UnityEngine.Object[] { readme };
                return readme;
            }
        }
        
        return null;
    }

    protected override void OnHeaderGUI()
    {
        var readme = (Readme)target;
        Init();

        var iconWidth = Mathf.Min(EditorGUIUtility.currentViewWidth / 3f - 20f, 128f);

        GUILayout.BeginHorizontal("In BigTitle");
        {
            if (readme.icon != null)
            {
                GUILayout.Space(k_Space);
                GUILayout.Label(readme.icon, GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));
            }
            GUILayout.Space(k_Space);
            GUILayout.BeginVertical();
            {

                GUILayout.FlexibleSpace();
                GUILayout.Label(readme.title, TitleStyle);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
        }
        GUILayout.EndHorizontal();
    }

    public override void OnInspectorGUI()
    {
        var readme = (Readme)target;
        Init();

        foreach (var section in readme.sections)
        {
            if (!string.IsNullOrEmpty(section.heading))
            {
                GUILayout.Label(section.heading, HeadingStyle);
            }

            if (!string.IsNullOrEmpty(section.text))
            {
                GUILayout.Label(section.text, BodyStyle);
            }

            if (!string.IsNullOrEmpty(section.linkText))
            {
                if (LinkLabel(new GUIContent(section.linkText)))
                {
                    // Check for asset reference first
                    if (!string.IsNullOrEmpty(section.assetGuid))
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(section.assetGuid);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                            if (asset != null)
                            {
                                EditorGUIUtility.PingObject(asset);
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(section.url))
                    {
                        Application.OpenURL(section.url);
                    }
                }
            }

            GUILayout.Space(k_Space);
        }

        if (GUILayout.Button("Remove Readme Assets", ButtonStyle))
        {
            RemoveTutorial();
        }
    }

    bool m_Initialized;

    GUIStyle LinkStyle
    {
        get { return m_LinkStyle; }
    }

    [SerializeField]
    GUIStyle m_LinkStyle;

    GUIStyle TitleStyle
    {
        get { return m_TitleStyle; }
    }

    [SerializeField]
    GUIStyle m_TitleStyle;

    GUIStyle HeadingStyle
    {
        get { return m_HeadingStyle; }
    }

    [SerializeField]
    GUIStyle m_HeadingStyle;

    GUIStyle BodyStyle
    {
        get { return m_BodyStyle; }
    }

    [SerializeField]
    GUIStyle m_BodyStyle;

    GUIStyle ButtonStyle
    {
        get { return m_ButtonStyle; }
    }

    [SerializeField]
    GUIStyle m_ButtonStyle;

    void Init()
    {
        if (m_Initialized)
            return;
        m_BodyStyle = new GUIStyle(EditorStyles.label);
        m_BodyStyle.wordWrap = true;
        m_BodyStyle.fontSize = 14;
        m_BodyStyle.richText = true;

        m_TitleStyle = new GUIStyle(m_BodyStyle);
        m_TitleStyle.fontSize = 26;

        m_HeadingStyle = new GUIStyle(m_BodyStyle);
        m_HeadingStyle.fontStyle = FontStyle.Bold;
        m_HeadingStyle.fontSize = 18;

        m_LinkStyle = new GUIStyle(m_BodyStyle);
        m_LinkStyle.wordWrap = false;

        // Match selection color which works nicely for both light and dark skins
        m_LinkStyle.normal.textColor = new Color(0x00 / 255f, 0x78 / 255f, 0xDA / 255f, 1f);
        m_LinkStyle.stretchWidth = false;

        m_ButtonStyle = new GUIStyle(EditorStyles.miniButton);
        m_ButtonStyle.fontStyle = FontStyle.Bold;

        m_Initialized = true;
    }

    bool LinkLabel(GUIContent label, params GUILayoutOption[] options)
    {
        var position = GUILayoutUtility.GetRect(label, LinkStyle, options);

        Handles.BeginGUI();
        Handles.color = LinkStyle.normal.textColor;
        Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));
        Handles.color = Color.white;
        Handles.EndGUI();

        EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

        return GUI.Button(position, label, LinkStyle);
    }
}
