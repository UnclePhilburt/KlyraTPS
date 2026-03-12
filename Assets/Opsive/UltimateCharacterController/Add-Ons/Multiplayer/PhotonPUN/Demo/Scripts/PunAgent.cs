/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun.Demo
{
    using Opsive.Shared.Events;
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Character.Abilities.AI;
    using Opsive.UltimateCharacterController.Character.Abilities.Items;
    using Photon.Pun;
    using Photon.Realtime;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Patrols the waypoints on the server.
    /// </summary>
    public class PunAgent : MonoBehaviour
    {
        [Tooltip("The ID of the waypoints parent.")]
        [SerializeField] protected int m_WaypointsIdentifier;
        [Tooltip("The interval that the item should be used.")]
        [SerializeField] protected float m_UseInterval = 5;

        private UltimateCharacterLocomotion m_CharacterLocomotion;
        private NavMeshAgent m_NavMeshAgent;
        private NavMeshAgentMovement m_NavMeshAgentMovement;
        private Use m_Use;

        private Transform[] m_Waypoints;
        private int m_Index;
        private float m_LastUseTime;

        /// <summary>
        /// Initailizes the default values.
        /// </summary>
        private void Start()
        {
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
            m_CharacterLocomotion = GetComponent<UltimateCharacterLocomotion>();
            m_NavMeshAgentMovement = m_CharacterLocomotion.GetAbility<NavMeshAgentMovement>();
            m_Use = m_CharacterLocomotion.GetAbility<Use>();
            m_LastUseTime = Time.time;

            // Populate the waypoints at runtime.
            var objectIdentifiers = Object.FindObjectsOfType<Opsive.UltimateCharacterController.Objects.ObjectIdentifier>();
            for (int i = 0; i < objectIdentifiers.Length; ++i) {
                if (objectIdentifiers[i].ID != m_WaypointsIdentifier) {
                    continue;
                }

                m_Waypoints = new Transform[objectIdentifiers[i].transform.childCount];
                for (int j = 0; j < m_Waypoints.Length; ++j) {
                    m_Waypoints[j] = objectIdentifiers[i].transform.GetChild(j);
                }
                break;
            }

            if (m_Waypoints == null) {
                Debug.LogError($"Error: Unable to find the waypoints with ID {m_WaypointsIdentifier}.");
                return;
            }

            if (PhotonNetwork.IsMasterClient) {
                SetNextDestination();
            } else {
                enabled = false;
            }

            EventHandler.RegisterEvent<Player, GameObject>("OnPlayerLeftRoom", OnPlayerLeftRoom);

            ResetStates();
        }

        /// <summary>
        /// Sets the correct states so the item will fire indefinitely with a single shot.
        /// </summary>
        private void ResetStates()
        {
            Shared.StateSystem.StateManager.SetState(gameObject, "AssaultRifle_Trigger_Repeat", false);
            Shared.StateSystem.StateManager.SetState(gameObject, "AssaultRifle_Trigger_Simple", true);
            Shared.StateSystem.StateManager.SetState(gameObject, "AssaultRifle_Ammo_Item", false);
            Shared.StateSystem.StateManager.SetState(gameObject, "AssaultRifle_Ammo_Infinite", true);
        }

        /// <summary>
        /// Traverses the different waypoints.
        /// </summary>
        private void Update()
        {
            if (m_NavMeshAgent.remainingDistance < m_NavMeshAgent.stoppingDistance) {
                SetNextDestination();
            }

            // Use the item every interval.
            if (m_LastUseTime + m_UseInterval < Time.time) {
                m_CharacterLocomotion.TryStartAbility(m_Use);
                m_LastUseTime = Time.time;
            }
        }

        /// <summary>
        /// Sets the next NavMeshAgent destination.
        /// </summary>
        private void SetNextDestination()
        {
            var destination = m_Waypoints[m_Index];
            m_NavMeshAgentMovement.SetDestination(destination.position);
            m_Index = (m_Index + 1) % m_Waypoints.Length;
        }

        /// <summary>
        /// A player has left the room. Perform any cleanup.
        /// </summary>
        /// <param name="player">The Photon Player that left the room.</param>
        /// <param name="character">The character that the player controlled.</param>
        private void OnPlayerLeftRoom(Player player, GameObject character)
        {
            enabled = PhotonNetwork.IsMasterClient;
            m_LastUseTime = Time.time;
            ResetStates();

            if (m_Use.IsActive) {
                m_CharacterLocomotion.TryStopAbility(m_Use, true);
            }
        }

        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        private void OnDestroy()
        {
            EventHandler.UnregisterEvent<Player, GameObject>("OnPlayerLeftRoom", OnPlayerLeftRoom);
        }
    }
}