using System;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Provides utility methods for custom Unity Editor inspectors, particularly for drawing
    /// properties declared in specific types using reflection.
    /// </summary>
    public static class EditorHelper
    {
        #region Public Methods

        /// <summary>
        /// Draws all serializable fields declared in a specific type, filtering out non-serialized,
        /// hidden, and compiler-generated fields. Useful for custom editors that need to display
        /// only the fields from a particular class in an inheritance hierarchy.
        /// </summary>
        /// <param name="serializedObject">The SerializedObject containing the properties to draw.</param>
        /// <param name="declaringType">The specific type whose declared fields should be drawn.</param>
        public static void DrawDeclaredProperties(SerializedObject serializedObject, Type declaringType)
        {
            if (serializedObject == null || declaringType == null)
            {
                Debug.LogError("SerializedObject or declaringType is null in DrawDeclaredProperties.");
                return;
            }

            // Use DeclaredOnly to get fields from this specific type, excluding inherited fields
            const BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.DeclaredOnly;

            FieldInfo[] fields = declaringType.GetFields(fieldFlags);

            var drawableFields = new List<FieldInfo>();

            foreach (FieldInfo field in fields)
            {
                bool isSerializable = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                bool isNotSerialized = field.GetCustomAttribute<NonSerializedAttribute>() != null;
                bool isHidden = field.GetCustomAttribute<HideInInspector>() != null;
                bool isCompilerGenerated =
                    field.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() != null;

                // Only draw fields that Unity will serialize and that aren't explicitly hidden or auto-generated (like backing fields for properties)
                if (isSerializable && !isNotSerialized && !isHidden && !isCompilerGenerated)
                {
                    drawableFields.Add(field);
                }
            }

            if (drawableFields.Count > 0)
            {
                foreach (FieldInfo field in drawableFields)
                {
                    DrawPropertyField(serializedObject, field);
                }
            }
        }

        #endregion

        #region Private Methods

        private static void DrawPropertyField(SerializedObject serializedObject, FieldInfo field)
        {
            SerializedProperty property = serializedObject.FindProperty(field.Name);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        #endregion
    }
}
