/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun
{
    using Opsive.Shared.Game;
    using Opsive.Shared.Networking;
    using Photon.Pun;
    using UnityEngine;

    /// <summary>
    /// Contains information about the object on the network.
    /// </summary>
    public class PunNetworkInfo : MonoBehaviour, INetworkInfo
    {
        private PhotonView m_PhotonView;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_PhotonView = gameObject.GetCachedComponent<PhotonView>();
        }

        /// <summary>
        /// Is the networking implementation server authoritative?
        /// </summary>
        /// <returns>True if the network transform is server authoritative.</returns>
        public bool IsServerAuthoritative()
        {
            return false;
        }

        /// <summary>
        /// Is the game instance on the server?
        /// </summary>
        /// <returns>True if the game instance is on the server.</returns>
        public bool IsServer()
        {
            return PhotonNetwork.IsMasterClient;
        }

        /// <summary>
        /// Does the network instance have authority?
        /// </summary>
        /// <returns>True if the instance has authority.</returns>
        public bool HasAuthority()
        {
            return m_PhotonView.IsMine;
        }

        /// <summary>
        /// Is the character the local player?
        /// </summary>
        /// <returns>True if the character is the local player.</returns>
        public bool IsLocalPlayer()
        {
            return m_PhotonView.IsMine;
        }

        /// <summary>
        /// Is the player a spectator?
        /// </summary>
        /// <returns>True if the player is a spectator.</returns>
        public bool IsSpectator()
        {
            return false;
        }
    }
}