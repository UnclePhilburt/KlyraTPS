/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun.Objects
{
    using Opsive.Shared.Game;
    using Opsive.UltimateCharacterController.Items.Actions.Impact;
    using Opsive.UltimateCharacterController.Networking.Objects;
    using Opsive.UltimateCharacterController.Objects;
    using Photon.Pun;
    using UnityEngine;

    /// <summary>
    /// Initializes the projectile over the network.
    /// </summary>
    public class PunProjectile : Projectile, ISpawnDataObject
    {
        private object[] m_SpawnData;
        private object[] m_InstantiationData;
        private ImpactDamageData m_DamageData;
        public object[] InstantiationData { get => m_InstantiationData; set => m_InstantiationData = value; }

        /// <summary>
        /// Returns the initialization data that is required when the object spawns. This allows the remote players to initialize the object correctly.
        /// </summary>
        /// <returns>The initialization data that is required when the object spawns.</returns>
        public object[] SpawnData()
        {
            if (m_SpawnData == null) {
                m_SpawnData = new object[10];
            }
            m_SpawnData[0] = m_ID;
            m_SpawnData[1] = m_Velocity;
            m_SpawnData[2] = m_Torque;
            m_SpawnData[3] = m_ImpactDamageData.DamageAmount;
            m_SpawnData[4] = m_ImpactDamageData.ImpactForce;
            m_SpawnData[5] = m_ImpactDamageData.ImpactForceFrames;
            m_SpawnData[6] = m_ImpactLayers.value;
            m_SpawnData[7] = m_ImpactDamageData.ImpactStateName;
            m_SpawnData[8] = m_ImpactDamageData.ImpactStateDisableTimer;
            m_SpawnData[9] = m_Owner != null ? m_Owner.GetCachedComponent<PhotonView>().ViewID : -1;
            return m_SpawnData;
        }

        /// <summary>
        /// The object has been spawned. Initialize the projectile.
        /// </summary>
        public void ObjectSpawned()
        {
            if (m_InstantiationData == null) {
                return;
            }

            // Initialize the projectile from the data within the InstantiationData field.
            if (m_DamageData == null) {
                m_DamageData = new ImpactDamageData();
            }
            m_DamageData.DamageAmount = (float)m_InstantiationData[3];
            m_DamageData.ImpactForce = (float)m_InstantiationData[4];
            m_DamageData.ImpactForceFrames = (int)m_InstantiationData[5];
            m_ImpactLayers = (int)m_InstantiationData[6];
            m_DamageData.ImpactStateName = (string)m_InstantiationData[7];
            m_DamageData.ImpactStateDisableTimer = (float)m_InstantiationData[8];

            var owner = PhotonNetwork.GetPhotonView((int)m_InstantiationData[9]);
            Initialize((uint)m_InstantiationData[0], (Vector3)m_InstantiationData[1], (Vector3)m_InstantiationData[2], owner != null ? owner.gameObject : null, m_DamageData, true);
        }
    }
}
