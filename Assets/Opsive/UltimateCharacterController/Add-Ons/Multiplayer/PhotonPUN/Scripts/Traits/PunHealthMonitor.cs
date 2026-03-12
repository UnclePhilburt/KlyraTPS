/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun.Traits
{
    using Opsive.Shared.Game;
    using Opsive.Shared.Utility;
    using Opsive.UltimateCharacterController.Inventory;
    using Opsive.UltimateCharacterController.Items.Actions;
    using Opsive.UltimateCharacterController.Networking.Game;
    using Opsive.UltimateCharacterController.Networking.Traits;
    using Opsive.UltimateCharacterController.Objects;
    using Opsive.UltimateCharacterController.Traits;
    using Opsive.UltimateCharacterController.Traits.Damage;
    using Photon.Pun;
    using UnityEngine;

    /// <summary>
    /// Synchronizes the Health component over the network.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PunHealthMonitor : MonoBehaviour, INetworkHealthMonitor
    {
        private GameObject m_GameObject;
        private Health m_Health;
        private InventoryBase m_Inventory;
        private PhotonView m_PhotonView;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Health = m_GameObject.GetCachedComponent<Health>();
            m_Inventory = m_GameObject.GetCachedComponent<InventoryBase>();
            m_PhotonView = m_GameObject.GetCachedComponent<PhotonView>();
        }

        /// <summary>
        /// The object has taken been damaged.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="direction">The direction that the object took damage from.</param>
        /// <param name="forceMagnitude">The magnitude of the force that is applied to the object.</param>
        /// <param name="frames">The number of frames to add the force to.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-explosive force will be used.</param>
        /// <param name="source">The object that did the damage.</param>
        /// <param name="hitCollider">The Collider that was hit.</param>
        public void OnDamage(float amount, Vector3 position, Vector3 direction, float forceMagnitude, int frames, float radius, IDamageSource source, Collider hitCollider)
        {
            // A source is not required. If one exists it must have a PhotonView component attached for identification purposes.
            var sourcePhotonViewID = -1;
            uint sourceItemIdentifierID = 0;
            var sourceSlotID = -1;
            var sourceItemActionID = -1;
            if (source != null) {
                // If the originator is an item then more data needs to be sent.
                CharacterItemAction itemAction = null;
                if (source is CharacterItemAction) {
                    itemAction = source as CharacterItemAction;
                } else if (source is Explosion) {
                    itemAction = source.OwnerDamageSource as CharacterItemAction;
                }
                if (itemAction != null) {
                    sourceItemActionID = itemAction.ID;
                    sourceSlotID = itemAction.CharacterItem.SlotID;
                    sourceItemIdentifierID = itemAction.CharacterItem.ItemIdentifier.ID;
                }

                if (source.SourceGameObject != null) {
                    var originatorPhotonView = source.SourceGameObject.GetCachedComponent<PhotonView>();
                    if (originatorPhotonView == null) {
                        originatorPhotonView = source.SourceOwner.GetCachedComponent<PhotonView>();
                        if (originatorPhotonView == null) {
                            Debug.LogError($"Error: The attacker {source.SourceOwner.name} must have a PhotonView component.");
                            return;
                        }
                    }
                    sourcePhotonViewID = originatorPhotonView.ViewID;
                }
            }

            // A hit collider is not required. If one exists it must have an ObjectIdentifier or PhotonView attached for identification purposes.
            (uint ID, bool) hitColliderPair;
            var hitItemSlotID = -1;
            if (hitCollider != null) {
                hitColliderPair = Utility.PunUtility.GetID(hitCollider.gameObject, out hitItemSlotID);
            } else {
                hitColliderPair = (0, false);
            }

            m_PhotonView.RPC("OnDamageRPC", RpcTarget.All, amount, position, direction, forceMagnitude, frames, radius, sourcePhotonViewID,
                                                sourceItemIdentifierID, sourceSlotID, sourceItemActionID, hitColliderPair.ID, hitItemSlotID);
        }

        /// <summary>
        /// The object has taken been damaged on the network.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="direction">The direction that the object took damage from.</param>
        /// <param name="forceMagnitude">The magnitude of the force that is applied to the object.</param>
        /// <param name="frames">The number of frames to add the force to.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-explosive force will be used.</param>
        /// <param name="sourcePhotonViewID">The PhotonView ID of the object that did the damage.</param>
        /// <param name="sourceItemIdentifierID">The ID of the source's Item Identifier.</param>
        /// <param name="sourceSlotID">The ID of the source's slot.</param>
        /// <param name="sourceItemActionID">The ID of the source's ItemAction.</param>
        /// <param name="hitColliderID">The PhotonView or ObjectIdentifier ID of the Collider that was hit.</param>
        /// <param name="hitItemSlotID">If the hit collider is an item then the slot ID of the item will be specified.</param>
        [PunRPC]
        private void OnDamageRPC(float amount, Vector3 position, Vector3 direction, float forceMagnitude, int frames, float radius, int sourcePhotonViewID, uint sourceItemIdentifierID, int sourceSlotID, int sourceItemActionID, uint hitColliderID, int hitItemSlotID)
        {
            IDamageSource source = null;
            if (sourcePhotonViewID != -1) {
                var sourceView = PhotonNetwork.GetPhotonView(sourcePhotonViewID);
                if (sourceView != null) {
                    source = sourceView.GetComponent<IDamageSource>();

                    // If the originator is null then it may have come from an item.
                    if (source == null) {
                        var itemType = ItemIdentifierTracker.GetItemIdentifier(sourceItemIdentifierID);
                        m_Inventory = sourceView.GetComponent<InventoryBase>();

                        if (itemType != null && m_Inventory != null) {
                            var item = m_Inventory.GetCharacterItem(itemType, sourceSlotID);
                            if (item != null) {
                                source = item.GetItemAction(sourceItemActionID) as IDamageSource;
                            }
                        }
                    }
                }
            }

            var hitCollider = Utility.PunUtility.RetrieveGameObject(m_GameObject, hitColliderID, hitItemSlotID);
            var pooledDamageData = GenericObjectPool.Get<DamageData>();
            pooledDamageData.SetDamage(source, amount, position, direction, forceMagnitude, frames, radius, hitCollider != null ? hitCollider.GetCachedComponent<Collider>() : null);
            m_Health.OnDamage(pooledDamageData);
            GenericObjectPool.Return(pooledDamageData);
        }

        /// <summary>
        /// The object is no longer alive.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that killed the character.</param>
        public void Die(Vector3 position, Vector3 force, GameObject attacker)
        {
            // An attacker is not required. If one exists it must have a PhotonView component attached for identification purposes.
            var attackerPhotonViewID = -1;
            if (attacker != null) {
                var attackerPhotonView = attacker.GetCachedComponent<PhotonView>();
                if (attackerPhotonView == null) {
                    Debug.LogError($"Error: The attacker {attacker.name} must have a PhotonView component.");
                    return;
                }
                attackerPhotonViewID = attackerPhotonView.ViewID;
            }

            m_PhotonView.RPC("DieRPC", RpcTarget.Others, position, force, attackerPhotonViewID);
        }

        /// <summary>
        /// The object is no longer alive on the network.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attackerViewID">The PhotonView ID of the GameObject that killed the object.</param>
        [PunRPC]
        private void DieRPC(Vector3 position, Vector3 force, int attackerViewID)
        {
            PhotonView attacker = null;
            if (attackerViewID != -1) {
                attacker = PhotonNetwork.GetPhotonView(attackerViewID);
            }
            m_Health.Die(position, force, attacker != null ? attacker.gameObject : null);
        }

        /// <summary>
        /// Adds amount to health and then to the shield if there is still an amount remaining. Will not go over the maximum health or shield value.
        /// </summary>
        /// <param name="amount">The amount of health or shield to add.</param>
        public void Heal(float amount)
        {
            m_PhotonView.RPC("HealRPC", RpcTarget.Others, amount);
        }

        /// <summary>
        /// Adds amount to health and then to the shield if there is still an amount remaining on the network.
        /// </summary>
        /// <param name="amount">The amount of health or shield to add.</param>
        [PunRPC]
        private void HealRPC(float amount)
        {
            m_Health.Heal(amount);
        }
    }
}