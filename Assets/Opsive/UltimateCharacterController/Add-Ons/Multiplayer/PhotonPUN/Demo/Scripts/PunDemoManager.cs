/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun.Demo
{
    using Opsive.Shared.Events;
    using Opsive.UltimateCharacterController.Demo;
    using Photon.Realtime;
    using UnityEngine;

    /// <summary>
    /// Initializes the demo manager to the local player.
    /// </summary>
    public class PunDemoManager : DemoManager
    {
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            EventHandler.RegisterEvent<Player, GameObject>("OnPlayerEnteredRoom", OnPlayerEnteredRoom);
        }

        /// <summary>
        /// Prevent the base class from initializing the character.
        /// </summary>
        protected override void Start() { }

        /// <summary>
        /// A player has entered the room. Initialize the Demo manager to the local player.
        /// </summary>
        /// <param name="player">The Photon Player that entered the room.</param>
        /// <param name="character">The character that the player controls.</param>
        private void OnPlayerEnteredRoom(Player player, GameObject character)
        {
#if FIRST_PERSON_CONTROLLER && THIRD_PERSON_CONTROLLER
            if (player.IsLocal) {
                m_DefaultFirstPersonStart = PlayerPrefs.GetInt("START_PERSPECTIVE", m_DefaultFirstPersonStart ? 1 : 0) == 1;
                SelectStartingPerspective(m_DefaultFirstPersonStart, false);
            }
#endif
        }

        /// <summary>
        /// The object has been destroyed.
        /// </summary>
        private void OnDestroy()
        {
            EventHandler.UnregisterEvent<Player, GameObject>("OnPlayerEnteredRoom", OnPlayerEnteredRoom);
        }
    }
}