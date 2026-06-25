using Blocks.Gameplay.Core;
using UnityEngine.UIElements;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// Platformer-specific HUD that extends <see cref="CoreHUD"/> to display coin count.
    /// Updates the coin label when the player's Coin stat changes.
    /// </summary>
    public class PlatformerHUD : CoreHUD
    {
        #region Fields & Properties

        private Label m_CoinsLabel;

        #endregion

        #region Protected Methods

        /// <summary>
        /// Queries and caches references to UI elements from the visual tree.
        /// </summary>
        /// <param name="root">The root visual element to query from.</param>
        protected override void QueryHUDElements(VisualElement root)
        {
            base.QueryHUDElements(root);
            m_CoinsLabel = root.Q<Label>("coins-label");
        }

        /// <summary>
        /// Handles stat changes for the local player and updates the corresponding UI elements.
        /// </summary>
        /// <param name="payload">The stat change data containing stat name and values.</param>
        protected override void HandleStatChangedLocal(StatChangePayload payload)
        {
            switch (payload.statName)
            {
                case "Coin":
                    if (m_CoinsLabel != null)
                    {
                        m_CoinsLabel.text = $"Coins: {payload.currentValue:F0}";
                    }
                    break;
            }
        }

        #endregion
    }
}
