/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun.Demo
{
    using UnityEngine;
    using Photon.Pun;

    /// <summary>
    /// Spawns the AI agent.
    /// </summary>
    public class PunAgentSpawner : MonoBehaviour
    {
        [Tooltip("The amount of time to spawn the agent after the game starts.")]
        [SerializeField] protected float m_Delay;
        [Tooltip("A reference to the agent's prefab")]
        [SerializeField] protected GameObject m_AgentPrefab;
        [Tooltip("The location that the AI should spawn.")]
        [SerializeField] protected Transform m_SpawnLocation;

        /// <summary>
        /// Spawns the AI agent.
        /// </summary>
        private void Start()
        {
            if (!PhotonNetwork.IsMasterClient) {
                return;
            }

            Invoke("SpawnAgent", m_Delay);
        }

        /// <summary>
        /// Spawns the AI agent.
        /// </summary>
        private void SpawnAgent()
        {
            PhotonNetwork.Instantiate(m_AgentPrefab.name, m_SpawnLocation.position, m_SpawnLocation.rotation);
        }
    }
}