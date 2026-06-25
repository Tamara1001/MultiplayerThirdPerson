using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Defines the contract for any object that can be interacted with by a CoreInteractor.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Gets the method by which this interaction is triggered.
        /// </summary>
        InteractionTriggerMode TriggerMode { get; }

        /// <summary>
        /// Gets the priority of this interactable. When multiple interactables are detected,
        /// the one with the highest priority value is chosen as the focus.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets the text to display on the UI prompt.
        /// Should return string.Empty for automatically triggered interactions.
        /// </summary>
        string InteractionPromptText { get; }

        /// <summary>
        /// Determines if the specified interactor can currently interact with this object.
        /// </summary>
        bool CanInteract(GameObject interactor);

        /// <summary>
        /// Executes the interaction logic.
        /// </summary>
        void Interact(GameObject interactor);
    }
}

public enum InteractionTriggerMode
{
    OnButtonPress, // Triggered by player input.
    OnFocusEnter, // Triggered automatically when the object gains focus.
    OnCharacterControllerHit, // Triggered automatically when a CharacterController collides with it.
    OnTriggerEnter, // Triggered by a standard physics trigger collision.
    OnRigidbodyCollision // Triggered by a standard physics collision.
}
