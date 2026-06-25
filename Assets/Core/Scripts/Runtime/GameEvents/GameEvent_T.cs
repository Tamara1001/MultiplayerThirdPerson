using System.Collections.Generic;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An abstract, generic base class for ScriptableObject-based events that carry a payload of type <typeparamref name="T"/>.
    /// This class provides the core functionality for a decoupled event system, allowing different components to communicate
    /// by raising and listening for events without direct references. Derived classes, such as <see cref="BoolEvent"/> or
    /// <see cref="Vector2Event"/>, specify the concrete payload type.
    /// </summary>
    /// <typeparam name="T">The type of the data payload that this event will carry.</typeparam>
    public abstract class GameEvent<T> : ScriptableObject
    {
        #region Private Fields

        /// <summary>
        /// A list of all the methods (actions) that have subscribed to this event.
        /// Each listener must be a method that accepts a single parameter of type <typeparamref name="T"/>.
        /// </summary>
        private readonly List<System.Action<T>> m_Listeners = new List<System.Action<T>>();

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the last value that was raised by this event. This can be useful for new listeners
        /// that need to know the most recent state immediately upon registering.
        /// </summary>
        public T LastValue { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Invokes all registered listener methods, passing the provided value to each of them.
        /// It also updates the <see cref="LastValue"/> property.
        /// </summary>
        /// <param name="value">The data payload of type <typeparamref name="T"/> to be sent to all listeners.</param>
        public void Raise(T value)
        {
            LastValue = value;

            // Iterate backwards for safe removal of listeners during the Raise call.
            for (int i = m_Listeners.Count - 1; i >= 0; i--)
            {
                m_Listeners[i].Invoke(value);
            }
        }

        /// <summary>
        /// Subscribes a listener method to this event. The listener will be invoked whenever the
        /// <see cref="Raise"/> method is called.
        /// </summary>
        /// <param name="listener">The method to be called when the event is raised. It must accept one parameter of type <typeparamref name="T"/>.</param>
        public void RegisterListener(System.Action<T> listener)
        {
            // Add the listener only if it's not already in the list to prevent duplicate registrations.
            if (!m_Listeners.Contains(listener))
            {
                m_Listeners.Add(listener);
            }
        }

        /// <summary>
        /// Unsubscribes a listener from this event. The listener will no longer be invoked.
        /// </summary>
        /// <param name="listener">The method to be removed from the event's subscribers.</param>
        public void UnregisterListener(System.Action<T> listener)
        {
            // Remove the listener only if it exists in the list to avoid errors.
            if (m_Listeners.Contains(listener))
            {
                m_Listeners.Remove(listener);
            }
        }

        #endregion
    }
}
