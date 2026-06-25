using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A fluent builder for creating visual effects using either Unity primitives or prefabs.
    /// </summary>
    public class EffectBuilder
    {
        #region Fields & Properties

        private readonly PrimitiveType? m_PrimitiveType;
        private readonly GameObject m_PrefabToInstantiate;
        private Vector3 m_Position = Vector3.zero;
        private Vector3 m_Scale = Vector3.one;
        private Quaternion m_Rotation = Quaternion.identity;
        private Material m_Material;
        private float m_Duration = 1f;
        private string m_Name = "VisualEffect";
        private Transform m_Parent;
        private bool m_RemoveCollider = true;
        private Color? m_Color;

        #endregion

        #region Public Methods

        /// <summary>
        /// Constructor for primitive-based effects
        /// </summary>
        public EffectBuilder(PrimitiveType primitiveType)
        {
            m_PrimitiveType = primitiveType;
            m_PrefabToInstantiate = null;
            m_Name = $"{primitiveType.ToString()}Effect";
        }

        /// <summary>
        /// Constructor for prefab-based effects
        /// </summary>
        public EffectBuilder(GameObject prefab)
        {
            m_PrimitiveType = null;
            m_PrefabToInstantiate = prefab;
            if (prefab != null)
            {
                m_Name = $"{prefab.name}Effect";
            }
        }

        /// <summary>
        /// Sets the world position of the effect.
        /// </summary>
        public EffectBuilder WithPosition(Vector3 position)
        {
            m_Position = position;
            return this;
        }

        /// <summary>
        /// Sets the scale of the effect.
        /// </summary>
        public EffectBuilder WithScale(Vector3 scale)
        {
            m_Scale = scale;
            return this;
        }

        /// <summary>
        /// Sets uniform scale of the effect.
        /// </summary>
        public EffectBuilder WithScale(float uniformScale)
        {
            m_Scale = Vector3.one * uniformScale;
            return this;
        }

        /// <summary>
        /// Sets the rotation of the effect.
        /// </summary>
        public EffectBuilder WithRotation(Quaternion rotation)
        {
            m_Rotation = rotation;
            return this;
        }

        /// <summary>
        /// Sets the rotation to look in a specific direction.
        /// </summary>
        public EffectBuilder WithLookDirection(Vector3 direction)
        {
            if (direction != Vector3.zero)
            {
                m_Rotation = Quaternion.LookRotation(direction);
            }

            return this;
        }

        /// <summary>
        /// Sets the material to apply to the effect.
        /// </summary>
        public EffectBuilder WithMaterial(Material material)
        {
            m_Material = material;
            return this;
        }

        /// <summary>
        /// Sets the color of the effect (will create a new material if none provided).
        /// </summary>
        public EffectBuilder WithColor(Color color)
        {
            m_Color = color;
            return this;
        }

        /// <summary>
        /// Sets how long the effect stays visible before being destroyed.
        /// </summary>
        public EffectBuilder WithDuration(float duration)
        {
            m_Duration = duration;
            return this;
        }

        /// <summary>
        /// Sets the name of the effect GameObject.
        /// </summary>
        public EffectBuilder WithName(string name)
        {
            m_Name = name;
            return this;
        }

        /// <summary>
        /// Sets the parent transform for the effect.
        /// </summary>
        public EffectBuilder WithParent(Transform parent)
        {
            m_Parent = parent;
            return this;
        }

        /// <summary>
        /// Sets whether to remove the collider component (default: true). Only applies to primitive-based effects.
        /// </summary>
        public EffectBuilder WithCollider(bool keepCollider)
        {
            m_RemoveCollider = !keepCollider;
            return this;
        }

        /// <summary>
        /// Special configuration for tracer effects between two points.
        /// Works best with cylinder primitives or elongated prefabs.
        /// </summary>
        public EffectBuilder WithTracerPositioning(Vector3 startPosition, Vector3 endPosition)
        {
            // Calculate tracer positioning: midpoint between start and end
            Vector3 midpoint = (startPosition + endPosition) * 0.5f;
            float distance = Vector3.Distance(startPosition, endPosition);
            Vector3 direction = (endPosition - startPosition).normalized;

            m_Position = midpoint;

            // Scale the effect to span the distance between points
            // Cylinders use Y-axis for length, prefabs use Z-axis
            if (m_PrimitiveType == PrimitiveType.Cylinder)
            {
                // Cylinder scale: Y = half-distance (scaled by 2 when Unity creates it), X/Z = radius
                m_Scale = new Vector3(0.04f, distance * 0.5f, 0.04f);
            }
            else
            {
                m_Scale = new Vector3(m_Scale.x, m_Scale.y, distance);
            }

            if (direction != Vector3.zero)
            {
                if (m_PrimitiveType == PrimitiveType.Cylinder)
                {
                    // Cylinders point along Y-axis, so rotate 90 degrees after aligning to direction
                    m_Rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
                }
                else
                {
                    m_Rotation = Quaternion.LookRotation(direction);
                }
            }

            return this;
        }

        /// <summary>
        /// Creates and returns the configured visual effect GameObject.
        /// </summary>
        public GameObject Create()
        {
            GameObject effect;

            // Create either primitive or instantiate prefab
            if (m_PrimitiveType.HasValue)
            {
                effect = GameObject.CreatePrimitive(m_PrimitiveType.Value);
            }
            else if (m_PrefabToInstantiate != null)
            {
                effect = Object.Instantiate(m_PrefabToInstantiate);
            }
            else
            {
                Debug.LogError("VisualEffectBuilder: No primitive type or prefab specified!");
                return null;
            }

            effect.name = m_Name;

            effect.transform.position = m_Position;
            effect.transform.localScale = m_Scale;
            effect.transform.rotation = m_Rotation;

            if (m_Parent != null)
            {
                effect.transform.SetParent(m_Parent, true);
            }

            // Primitives come with colliders by default, remove if not needed
            if (m_RemoveCollider && m_PrimitiveType.HasValue)
            {
                var collider = effect.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
            }

            // Apply visual appearance to all renderers in the effect hierarchy
            var renderers = effect.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (m_Material != null)
                {
                    renderer.material = m_Material;
                }
                else if (m_Color.HasValue)
                {
                    // No material provided, create a new Standard material with the specified color
                    var material = new Material(Shader.Find("Standard")) { color = m_Color.Value };
                    renderer.material = material;
                }
            }

            if (m_Duration > 0)
            {
                Object.Destroy(effect, m_Duration);
            }

            return effect;
        }

        #endregion
    }
}
