/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun.Character
{
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.Shared.Inventory;
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Character.Abilities;
    using Opsive.UltimateCharacterController.Inventory;
    using Opsive.UltimateCharacterController.Items;
    using Opsive.UltimateCharacterController.Items.Actions;
    using Opsive.UltimateCharacterController.Items.Actions.Impact;
    using Opsive.UltimateCharacterController.Items.Actions.Modules;
    using Opsive.UltimateCharacterController.Items.Actions.Modules.Magic;
    using Opsive.UltimateCharacterController.Items.Actions.Modules.Melee;
    using Opsive.UltimateCharacterController.Items.Actions.Modules.Shootable;
    using Opsive.UltimateCharacterController.Items.Actions.Modules.Throwable;
    using Opsive.UltimateCharacterController.Networking.Character;
    using Opsive.UltimateCharacterController.Networking.Game;
    using Opsive.UltimateCharacterController.Networking.Inventory;
    using Photon.Pun;
    using Photon.Realtime;
    using UnityEngine;

    /// <summary>
    /// The PunCharacter component manages the RPCs and state of the character on the Photon network.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PunCharacter : MonoBehaviourPunCallbacks, INetworkCharacterInventory, INetworkCharacter
    {
        private GameObject m_GameObject;
        private UltimateCharacterLocomotion m_CharacterLocomotion;
        private InventoryBase m_Inventory;
        private ModelManager m_ModelManager;

        private bool m_ItemsPickedUp;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_CharacterLocomotion = m_GameObject.GetCachedComponent<UltimateCharacterLocomotion>();
            m_Inventory = m_GameObject.GetCachedComponent<InventoryBase>();
            m_ModelManager = m_GameObject.GetCachedComponent<ModelManager>();
        }

        /// <summary>
        /// Registers for any interested events.
        /// </summary>
        private void Start()
        {
            if (photonView.IsMine) {
                EventHandler.RegisterEvent<Player, GameObject>("OnPlayerEnteredRoom", OnPlayerEnteredRoom);
                EventHandler.RegisterEvent<Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);
                EventHandler.RegisterEvent<Player, GameObject>("OnPlayerLeftRoom", OnPlayerLeftRoom);
            } else {
                PickupItems();
            }

            // AI agents should be disabled on the client.
            if (!PhotonNetwork.IsMasterClient && m_GameObject.GetCachedComponent<LocalLookSource>() != null) {
                m_CharacterLocomotion.enabled = false;
                if (!photonView.IsMine) {
                    EventHandler.RegisterEvent<Player, GameObject>("OnPlayerLeftRoom", OnPlayerLeftRoom);
                }
            }
        }

        /// <summary>
        /// Pickup isn't called on unequipped items. Ensure pickup is called before the item is equipped.
        /// </summary>
        private void PickupItems()
        {
            if (m_ItemsPickedUp) {
                return;
            }
            m_ItemsPickedUp = true;

            var items = m_GameObject.GetComponentsInChildren<Items.CharacterItem>(true);
            for (int i = 0; i < items.Length; ++i) {
                items[i].Pickup();
            }
        }

        /// <summary>
        /// Loads the inventory's default loadout.
        /// </summary>
        public void LoadDefaultLoadout()
        {
            photonView.RPC("LoadDefaultLoadoutRPC", RpcTarget.Others);
        }

        /// <summary>
        /// Loads the inventory's default loadout on the network.
        /// </summary>
        [PunRPC]
        private void LoadDefaultLoadoutRPC()
        {
            m_Inventory.LoadDefaultLoadout();
            EventHandler.ExecuteEvent(m_GameObject, "OnCharacterSnapAnimator", false);
        }

        /// <summary>
        /// A player has entered the room. Ensure the joining player is in sync with the current game state.
        /// </summary>
        /// <param name="player">The Photon Player that entered the room.</param>
        /// <param name="character">The character that the player controls.</param>
        private void OnPlayerEnteredRoom(Player player, GameObject character)
        {
            if (m_Inventory != null) {
                // Notify the joining player of the ItemIdentifiers that the player has within their inventory.
                var items = m_Inventory.GetAllCharacterItems();
                for (int i = 0; i < items.Count; ++i) {
                    var item = items[i];

                    photonView.RPC("PickupItemIdentifierRPC", player, item.ItemIdentifier.ID, m_Inventory.GetItemIdentifierAmount(item.ItemIdentifier));

                    if (item.DropPrefab != null) {
                        // Usable Items have a separate ItemIdentifiers amount.
                        var itemActions = item.ItemActions;
                        for (int j = 0; j < itemActions.Length; ++j) {
                            var usableAction = itemActions[j] as UsableAction;
                            if (usableAction == null) {
                                continue;
                            }

                            usableAction.InvokeOnModulesWithType<IModuleItemDefinitionConsumer>(module =>
                            {
                                var amount = module.GetItemDefinitionRemainingCount();
                                if (amount > 0 && module.ItemDefinition != null) {
                                    var moduleItemIdentifier = module.ItemDefinition.CreateItemIdentifier();
                                    photonView.RPC("PickupUsableItemActionRPC", player, item.ItemIdentifier.ID, item.SlotID, itemActions[j].ID, (module as ActionModule).ModuleGroup.ID, 
                                                                (module as ActionModule).ID, m_Inventory.GetItemIdentifierAmount(moduleItemIdentifier), amount);
                                }
                            });
                        }
                    }
                }

                // Ensure the correct item is equipped in each slot.
                for (int i = 0; i < m_Inventory.SlotCount; ++i) {
                    var item = m_Inventory.GetActiveCharacterItem(i);
                    if (item == null) {
                        continue;
                    }

                    photonView.RPC("EquipUnequipItemRPC", player, item.ItemIdentifier.ID, i, true);
                }
            }

            // The active character model needs to be synced.
            if (m_ModelManager != null && m_ModelManager.ActiveModelIndex != 0) {
                ChangeModels(m_ModelManager.ActiveModelIndex);
            }

            // ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER will be defined, but it is required here to allow the add-on to be compiled for the first time.
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            // The remote character should have the same abilities active.
            for (int i = 0; i < m_CharacterLocomotion.ActiveAbilityCount; ++i) {
                var activeAbility = m_CharacterLocomotion.ActiveAbilities[i];
                photonView.RPC("StartAbilityRPC", player, activeAbility.Index, activeAbility.GetNetworkStartData());
            }
#endif
        }

        /// <summary>
        /// The character's ability has been started or stopped.
        /// </summary>
        /// <param name="ability">The ability which was started or stopped.</param>
        /// <param name="active">True if the ability was started, false if it was stopped.</param>
        private void OnAbilityActive(Ability ability, bool active)
        {
            photonView.RPC("OnAbilityActiveRPC", RpcTarget.Others, ability.Index, active);
        }

        /// <summary>
        /// Activates or deactivates the ability on the network at the specified index.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        /// <param name="active">Should the ability be activated?</param>
        [PunRPC]
        private void OnAbilityActiveRPC(int abilityIndex, bool active)
        {
            if (active) {
                m_CharacterLocomotion.TryStartAbility(m_CharacterLocomotion.Abilities[abilityIndex]);
            } else {
                m_CharacterLocomotion.TryStopAbility(m_CharacterLocomotion.Abilities[abilityIndex], true);
            }
        }

        /// <summary>
        /// Starts the ability on the remote player.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        /// <param name="startData">Any data associated with the ability start.</param>
        [PunRPC]
        private void StartAbilityRPC(int abilityIndex, object[] startData)
        {
            var ability = m_CharacterLocomotion.Abilities[abilityIndex];
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            if (startData != null) {
                ability.SetNetworkStartData(startData);
            }
#endif
            m_CharacterLocomotion.TryStartAbility(ability, true, true);
        }

        /// <summary>
        /// Picks up the ItemIdentifier on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifiers that should be equipped.</param>
        /// <param name="amount">The number of ItemIdnetifiers to pickup.</param>
        [PunRPC]
        private void PickupItemIdentifierRPC(uint itemIdentifierID, int amount)
        {
#if UNITY_EDITOR
            var itemSetManager = m_GameObject.GetCachedComponent<ItemSetManager>();
            if (itemSetManager != null && itemSetManager.ItemCollection != ItemIdentifierTracker.ItemCollectionInstance) {
                Debug.LogWarning($"Warning: The Item Set Manager's Item Collection ({itemSetManager.ItemCollection}) " +
                                 $"does not match the Item Identifier Tracker's Item Collection ({ItemIdentifierTracker.ItemCollectionInstance}).");
            }
#endif

            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemIdentifier == null) {
                return;
            }

            m_Inventory.PickupItem(itemIdentifier, -1, amount, false, false, false, true);
        }

        /// <summary>
        /// Picks up the IUsableItem ItemIdentifier on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item being picked up.</param>
        /// <param name="itemActionID">The ID of the IUsableItem being picked up.</param>
        /// <param name="moduleGroupID">The ID of the module group containing the ItemIdentifier.</param>
        /// <param name="moduleID">The ID of the module containing the ItemIdentifier.</param>
        /// <param name="moduleAmount">The module amount within the inventory.</param>
        /// <param name="moduleItemIdentifierAmount">The ItemIdentifier amount loaded within the module.</param>
        [PunRPC]
        private void PickupUsableItemActionRPC(uint itemIdentifierID, int slotID, int itemActionID, int moduleGroupID, int moduleID, int moduleAmount, int moduleItemIdentifierAmount)
        {
            var itemType = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemType == null) {
                return;
            }

            var item = m_Inventory.GetCharacterItem(itemType, slotID);
            if (item == null) {
                return;
            }

            var usableItemAction = item.GetItemAction(itemActionID) as UsableAction;
            if (usableItemAction == null) {
                return;
            }

            usableItemAction.InvokeOnModulesWithTypeConditional<IModuleItemDefinitionConsumer>(module =>
            {
                var actionModule = module as ActionModule;
                if (actionModule.ModuleGroup.ID != moduleGroupID || actionModule.ID != moduleID) {
                    return false;
                }
                // The UsableAction has two counts: the first count is from the inventory, and the second count is set on the actual ItemAction.
                m_Inventory.PickupItem(module.ItemDefinition.CreateItemIdentifier(), -1, moduleAmount, false, false, false, false);
                module.SetItemDefinitionRemainingCount(moduleItemIdentifierAmount);
                return true;
            }, true, true);

            usableItemAction.InvokeOnModulesWithTypeConditional<ShootableClipModule>(module =>
            {
                if (module.ModuleGroup.ID != moduleGroupID || module.ID != moduleID) {
                    return false;
                }
                module.SetClipRemaining(moduleItemIdentifierAmount);
                return true;
            }, true, true);
        }

        /// <summary>
        /// Equips or unequips the item with the specified ItemIdentifier and slot.
        /// </summary>
        /// <param name="itemIdentifier">The ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item that should be equipped.</param>
        /// <param name="equip">Should the item be equipped? If false it will be unequipped.</param>
        public void EquipUnequipItem(IItemIdentifier itemIdentifier, int slotID, bool equip)
        {
            photonView.RPC("EquipUnequipItemRPC", RpcTarget.Others, itemIdentifier.ID, slotID, equip);
        }

        /// <summary>
        /// Equips or unequips the item on the network with the specified ItemIdentifier and slot.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item that should be equipped.</param>
        /// <param name="equip">Should the item be equipped? If false it will be unequipped.</param>
        [PunRPC]
        private void EquipUnequipItemRPC(uint itemIdentifierID, int slotID, bool equip)
        {
            if (equip) {
                // The character has to be alive to equip.
                if (!m_CharacterLocomotion.Alive) {
                    return;
                }

                // Ensure pickup is called before the item is equipped.
                PickupItems();
            }

            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemIdentifier == null) {
                return;
            }

            var item = m_Inventory.GetCharacterItem(itemIdentifier, slotID);
            if (item == null) {
                return;
            }

            if (equip) {
                if (m_Inventory.GetActiveCharacterItem(slotID) != item) {
                    EventHandler.ExecuteEvent<CharacterItem, int>(m_GameObject, "OnAbilityWillEquipItem", item, slotID);
                    m_Inventory.EquipItem(itemIdentifier, slotID, true);
                }
            } else {
                EventHandler.ExecuteEvent<CharacterItem, int>(m_GameObject, "OnAbilityUnequipItemComplete", item, slotID);
                m_Inventory.UnequipItem(itemIdentifier, slotID);
            }
        }

        /// <summary>
        /// The ItemIdentifier has been picked up.
        /// </summary>
        /// <param name="itemIdentifier">The ItemIdentifier that was picked up.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="amount">The number of ItemIdentifier picked up.</param>
        /// <param name="immediatePickup">Was the item be picked up immediately?</param>
        /// <param name="forceEquip">Should the item be force equipped?</param>
        public void ItemIdentifierPickup(IItemIdentifier itemIdentifier, int slotID, int amount, bool immediatePickup, bool forceEquip)
        {
            photonView.RPC("ItemIdentifierPickupRPC", RpcTarget.Others, itemIdentifier.ID, slotID, amount, immediatePickup, forceEquip);
        }

        /// <summary>
        /// The ItemIdentifier has been picked up on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that was picked up.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="amount">The number of ItemIdentifier picked up.</param>
        /// <param name="immediatePickup">Was the item be picked up immediately?</param>
        /// <param name="forceEquip">Should the item be force equipped?</param>
        [PunRPC]
        private void ItemIdentifierPickupRPC(uint itemIdentifierID, int slotID, int amount, bool immediatePickup, bool forceEquip)
        {
            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemIdentifier == null) {
                return;
            }

            m_Inventory.PickupItem(itemIdentifier, slotID, amount, immediatePickup, forceEquip);
        }

        /// <summary>
        /// Remove an item amount from the inventory.
        /// </summary>
        /// <param name="itemIdentifier">The ItemIdentifier that was removed.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="amount">The amount of ItemIdentifier to adjust.</param>
        /// <param name="drop">Should the item be dropped?</param>
        /// <param name="removeCharacterItem">Should the character item be removed?</param>
        /// <param name="destroyCharacterItem">Should the character item be destroyed?</param>
        public void RemoveItemIdentifierAmount(IItemIdentifier itemIdentifier, int slotID, int amount, bool drop, bool removeCharacterItem, bool destroyCharacterItem)
        {
            photonView.RPC("RemoveItemIdentifierAmountRPC", RpcTarget.Others, itemIdentifier.ID, slotID, amount, drop, removeCharacterItem, destroyCharacterItem);
        }

        /// <summary>
        /// Remove an item amount from the inventory on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that was removed.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="amount">The amount of ItemIdentifier to adjust.</param>
        /// <param name="drop">Should the item be dropped?</param>
        /// <param name="removeCharacterItem">Should the character item be removed?</param>
        /// <param name="destroyCharacterItem">Should the character item be destroyed?</param>
        [PunRPC]
        private void RemoveItemIdentifierAmountRPC(uint itemIdentifierID,int slotID, int amount, bool drop, bool removeCharacterItem, bool destroyCharacterItem)
        {
            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemIdentifier == null) {
                return;
            }

            m_Inventory.RemoveItemIdentifierAmount(itemIdentifier, slotID, amount, drop, removeCharacterItem, destroyCharacterItem);
        }

        /// <summary>
        /// Removes all of the items from the inventory.
        /// </summary>
        public void RemoveAllItems()
        {
            photonView.RPC("RemoveAllItemsRPC", RpcTarget.Others);
        }

        /// <summary>
        /// Removes all of the items from the inventory on the network.
        /// </summary>
        [PunRPC]
        private void RemoveAllItemsRPC()
        {
            m_Inventory.RemoveAllItems(true);
        }

        /// <summary>
        /// Returns the ItemAction with the specified slot and ID.
        /// </summary>
        /// <param name="slotID">The slot that the ItemAction belongs to.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <returns>The ItemAction with the specified slot and ID</returns>
        private CharacterItemAction GetItemAction(int slotID, int actionID)
        {
            var item = m_Inventory.GetActiveCharacterItem(slotID);
            if (item == null) {
                return null;
            }
            return item.GetItemAction(actionID);
        }

        /// <summary>
        /// Returns the module with the specified IDs.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="slotID">The SlotID of the module that is being retrieved.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <param name="actionID">The ID of the ModuleGroup being retrieved.</param>
        /// <param name="moduleID">The ID of the module being retrieved.</param>
        /// <returns>The module with the specified IDs (can be null).</returns>
        private T GetModule<T>(int slotID, int actionID, int moduleGroupID, int moduleID) where T : ActionModule
        {
            var itemAction = GetItemAction(slotID, actionID);
            if (itemAction == null) {
                return null;
            }

            if (!itemAction.ModuleGroupsByID.TryGetValue(moduleGroupID, out var moduleGroup)) {
                return null;
            }

            var module = moduleGroup.GetBaseModuleByID(moduleID) as T;
            if (module == null) {
                return null;
            }

            return module;
        }

        /// <summary>
        /// Returns the module group with the specified IDs.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being retrieved.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <param name="actionID">The ID of the ModuleGroup being retrieved.</param>
        /// <returns>The module group with the specified IDs (can be null).</returns>
        private ActionModuleGroupBase GetModuleGroup(int slotID, int actionID, int moduleGroupID)
        {
            var itemAction = GetItemAction(slotID, actionID);
            if (itemAction == null) {
                return null;
            }

            if (!itemAction.ModuleGroupsByID.TryGetValue(moduleGroupID, out var moduleGroup)) {
                return null;
            }

            return moduleGroup;
        }

        /// <summary>
        /// Initializes the ImpactCollisionData object.
        /// </summary>
        /// <param name="collisionData">The ImpactCollisionData resulting object.</param>
        /// <param name="sourceID">The ID of the impact.</param>
        /// <param name="sourceCharacterLocomotionViewID">The ID of the CharacterLocomotion component that caused the collision.</param>
        /// <param name="sourceGameObjectID">The ID of the GameObject that caused the collision.</param>
        /// <param name="sourceGameObjectSlotID">The slot ID if an item caused the collision.</param>
        /// <param name="impactGameObjectID">The ID of the GameObject that was impacted.</param>
        /// <param name="impactGameObjectSlotID">The slot ID of the item that was impacted.</param>
        /// <param name="impactColliderID">The ID of the Collider that was impacted.</param>
        /// <param name="impactPosition">The position of impact.</param>
        /// <param name="impactDirection">The direction of impact.</param>
        /// <param name="impactStrength">The strength of the impact.</param>
        /// <returns>True if the data structure was successfully initialized.</returns>
        private bool InitializeImpactCollisionData(ref ImpactCollisionData collisionData, uint sourceID, int sourceCharacterLocomotionViewID, uint sourceGameObjectID, int sourceGameObjectSlotID, 
                                                uint impactGameObjectID, int impactGameObjectSlotID, uint impactColliderID, Vector3 impactPosition, Vector3 impactDirection, float impactStrength)
        {
            UltimateCharacterLocomotion sourceCharacterLocomotion = null;
            if (sourceCharacterLocomotionViewID != -1) {
                var character = PhotonNetwork.GetPhotonView(sourceCharacterLocomotionViewID);
                if (character != null) {
                    sourceCharacterLocomotion = character.gameObject.GetCachedComponent<UltimateCharacterLocomotion>();
                }
            }

            var sourceGameObject = Utility.PunUtility.RetrieveGameObject(sourceCharacterLocomotion?.gameObject, sourceGameObjectID, sourceGameObjectSlotID);
            if (sourceGameObject == null) {
                return false;
            }

            var impactGameObject = Utility.PunUtility.RetrieveGameObject(null, impactGameObjectID, impactGameObjectSlotID);
            if (impactGameObject == null) {
                return false;
            }

            var impactColliderGameObject = Utility.PunUtility.RetrieveGameObject(null, impactColliderID, -1);
            if (impactColliderGameObject == null) {
                var impactCollider = impactGameObject.GetCachedComponent<Collider>();
                if (impactCollider == null) {
                    return false;
                }
                impactColliderGameObject = impactCollider.gameObject;
            }
            collisionData.ImpactCollider = impactColliderGameObject.GetCachedComponent<Collider>();
            if (collisionData.ImpactCollider == null) {
                return false;
            }

            // A RaycastHit cannot be sent over the network. Try to recreate it locally based on the position and normal values.
            impactDirection.Normalize();
            var ray = new Ray(impactPosition - impactDirection, impactDirection);
            if (!collisionData.ImpactCollider.Raycast(ray, out var hit, 3f)) {
                // The object has moved. Do a direct cast to try to find the object.
                ray.origin = collisionData.ImpactCollider.transform.position - impactDirection;
                if (!collisionData.ImpactCollider.Raycast(ray, out hit, 10f)) {
                    // The object can't be found. Return.
                    return false;
                }
            }
            collisionData.SetRaycast(hit);

            collisionData.SourceID = sourceID;
            collisionData.SourceCharacterLocomotion = sourceCharacterLocomotion;
            collisionData.SourceGameObject = sourceGameObject;
            collisionData.ImpactGameObject = impactGameObject;
            collisionData.ImpactPosition = impactPosition;
            collisionData.ImpactDirection = impactDirection;
            collisionData.ImpactStrength = impactStrength;
            return true;
        }

        /// <summary>
        /// Invokes the Shootable Action Fire Effect modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeShootableFireEffectModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ShootableUseDataStream data)
        {
            photonView.RPC("InvokeShootableFireEffectModulesRPC", RpcTarget.Others, itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask,
                                                                                   data.FireData.FirePoint, data.FireData.FireDirection);
        }

        /// <summary>
        /// Invokes the Shootable Action Fire Effect module on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="actionID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="firePoint">The fire point that is sent to the module.</param>
        /// <param name="fireDirection">The fire direction that is sent to the module.</param>
        [PunRPC]
        private void InvokeShootableFireEffectModulesRPC(int slotID, int actionID, int moduleGroupID, int invokedBitmask, Vector3 firePoint, Vector3 fireDirection)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<ShootableFireEffectModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) {
                return;
            }

            var data = Shared.Utility.GenericObjectPool.Get<ShootableUseDataStream>();
            if (data.FireData == null) {
                data.FireData = new ShootableFireData();
            }
            data.ShootableAction = moduleGroup.Modules[0].ShootableAction; // The action will be the same across all modules.
            data.FireData.FirePoint = firePoint;
            data.FireData.FireDirection = fireDirection;
            for (int i = 0; i < moduleGroup.ModuleCount; ++i) {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0) {
                    continue;
                }

                moduleGroup.Modules[i].InvokeEffects(data);
            }
            Shared.Utility.GenericObjectPool.Return(data);
        }

        /// <summary>
        /// Invokes the Shootable Action Dry Fire Effect modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeShootableDryFireEffectModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ShootableUseDataStream data)
        {
            photonView.RPC("InvokeShootableDryFireEffectModulesRPC", RpcTarget.Others, itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask,
                                                                                   data.FireData.FirePoint, data.FireData.FireDirection);
        }

        /// <summary>
        /// Invokes the Shootable Action Dry Fire Effect module on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="firePoint">The fire point that is sent to the module.</param>
        /// <param name="fireDirection">The fire direction that is sent to the module.</param>
        [PunRPC]
        private void InvokeShootableDryFireEffectModulesRPC(int slotID, int actionID, int moduleGroupID, int invokedBitmask, Vector3 firePoint, Vector3 fireDirection)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<ShootableFireEffectModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) {
                return;
            }

            var data = Shared.Utility.GenericObjectPool.Get<ShootableUseDataStream>();
            if (data.FireData == null) {
                data.FireData = new ShootableFireData();
            }
            data.ShootableAction = moduleGroup.Modules[0].ShootableAction; // The action will be the same across all modules.
            data.FireData.FirePoint = firePoint;
            data.FireData.FireDirection = fireDirection;
            for (int i = 0; i < moduleGroup.ModuleCount; ++i) {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0) {
                    continue;
                }

                moduleGroup.Modules[i].InvokeEffects(data);
            }
            Shared.Utility.GenericObjectPool.Return(data);
        }

        /// <summary>
        /// Invokes the Shootable Action Impact modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="context">The context being sent to the module.</param>
        public void InvokeShootableImpactModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ShootableImpactCallbackContext context)
        {
            var sourceCharacterLocomotionViewID = -1;
            if (context.ImpactCollisionData.SourceCharacterLocomotion != null) {
                var sourceCharacterLocomotionView = context.ImpactCollisionData.SourceCharacterLocomotion.gameObject.GetCachedComponent<PhotonView>();
                if (sourceCharacterLocomotionView == null) {
                    Debug.LogError($"Error: The character {context.ImpactCollisionData.SourceCharacterLocomotion.gameObject} must have a PhotonView component added.");
                    return;
                }
                sourceCharacterLocomotionViewID = sourceCharacterLocomotionView.ViewID;
            }

            var sourceGameObject = Utility.PunUtility.GetID(context.ImpactCollisionData.SourceGameObject, out var sourceGameObjectSlotID);
            if (!sourceGameObject.HasID) {
                return;
            }
            var impactGameObject = Utility.PunUtility.GetID(context.ImpactCollisionData.ImpactGameObject, out var impactGameObjectSlotID);
            if (!impactGameObject.HasID) {
                return;
            }
            var impactCollider = Utility.PunUtility.GetID(context.ImpactCollisionData.ImpactCollider.gameObject, out var colliderSlotID);
            if (!impactCollider.HasID) {
                return;
            }

            photonView.RPC("InvokeShootableImpactModulesRPC", RpcTarget.Others, itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask, 
                context.ImpactCollisionData.SourceID, sourceCharacterLocomotionViewID, sourceGameObject.ID, sourceGameObjectSlotID, impactGameObject.ID, impactGameObjectSlotID,
                impactCollider.ID, context.ImpactCollisionData.ImpactPosition, context.ImpactCollisionData.ImpactDirection, context.ImpactCollisionData.ImpactStrength);
        }

        /// <summary>
        /// Invokes the Shootable Action Impact modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="sourceID">The ID of the impact.</param>
        /// <param name="sourceCharacterLocomotionViewID">The ID of the CharacterLocomotion component that caused the collision.</param>
        /// <param name="sourceGameObjectID">The ID of the GameObject that caused the collision.</param>
        /// <param name="sourceGameObjectSlotID">The slot ID if an item caused the collision.</param>
        /// <param name="impactGameObjectID">The ID of the GameObject that was impacted.</param>
        /// <param name="impactGameObjectSlotID">The slot ID of the item that was impacted.</param>
        /// <param name="impactColliderID">The ID of the Collider that was impacted.</param>
        /// <param name="impactPosition">The position of impact.</param>
        /// <param name="impactDirection">The direction of impact.</param>
        /// <param name="impactStrength">The strength of the impact.</param>
        [PunRPC]
        private void InvokeShootableImpactModulesRPC(int slotID, int actionID, int moduleGroupID, int invokedBitmask, uint sourceID, int sourceCharacterLocomotionViewID, uint sourceGameObjectID, 
                                                    int sourceGameObjectSlotID, uint impactGameObjectID, int impactGameObjectSlotID, uint impactColliderID,
                                                    Vector3 impactPosition, Vector3 impactDirection, float impactStrength)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<ShootableImpactModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) {
                return;
            }

            var context = Shared.Utility.GenericObjectPool.Get<ShootableImpactCallbackContext>();
            if (context.ImpactCollisionData == null) {
                context.ImpactCollisionData = new ImpactCollisionData();
                context.ImpactDamageData = new ImpactDamageData();
            }

            var collisionData = context.ImpactCollisionData;
            if (!InitializeImpactCollisionData(ref collisionData, sourceID, sourceCharacterLocomotionViewID, sourceGameObjectID, sourceGameObjectSlotID, impactGameObjectID, impactGameObjectSlotID,
                                                impactColliderID, impactPosition, impactDirection, impactStrength)) {
                Shared.Utility.GenericObjectPool.Return(context);
                return;
            }
            collisionData.SourceComponent = GetItemAction(slotID, actionID);
            context.ImpactCollisionData = collisionData;
            context.ShootableAction = moduleGroup.Modules[0].ShootableAction; // The action will be the same across all modules.

            for (int i = 0; i < moduleGroup.ModuleCount; ++i) {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0) {
                    continue;
                }

                moduleGroup.Modules[i].OnImpact(context);
            }
            Shared.Utility.GenericObjectPool.Return(context);
        }

        /// <summary>
        /// Starts to reload the module.
        /// </summary>
        /// <param name="module">The module that is being reloaded.</param>
        public void StartItemReload(ShootableReloaderModule module)
        {
            photonView.RPC("StartItemReloadRPC", RpcTarget.Others, module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID);
        }

        /// <summary>
        /// Starts to reload the item on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being reloaded.</param>
        /// <param name="actionID">The ID of the ItemAction being reloaded.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being reloaded.</param>
        /// <param name="moduleID">The ID of the module being reloaded.</param>
        [PunRPC]
        private void StartItemReloadRPC(int slotID, int actionID, int moduleGroupID, int moduleID)
        {
            var module = GetModule<ShootableReloaderModule>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) {
                return;
            }
            module.StartItemReload();
        }

        /// <summary>
        /// Reloads the item.
        /// </summary>
        /// <param name="module">The module that is being reloaded.</param>
        /// <param name="fullClip">Should the full clip be force reloaded?</param>
        public void ReloadItem(ShootableReloaderModule module, bool fullClip)
        {
            photonView.RPC("ReloadItemRPC", RpcTarget.Others, module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID, fullClip);
        }

        /// <summary>
        /// Reloads the item on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being reloaded.</param>
        /// <param name="actionID">The ID of the ItemAction being reloaded.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being reloaded.</param>
        /// <param name="moduleID">The ID of the module being reloaded.</param>
        /// <param name="fullClip">Should the full clip be force reloaded?</param>
        [PunRPC]
        private void ReloadItemRPC(int slotID, int actionID, int moduleGroupID, int moduleID, bool fullClip)
        {
            var module = GetModule<ShootableReloaderModule>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) {
                return;
            }
            module.ReloadItem(fullClip);
        }

        /// <summary>
        /// The item has finished reloading.
        /// </summary>
        /// <param name="module">The module that is being realoaded.</param>
        /// <param name="success">Was the item reloaded successfully?</param>
        /// <param name="immediateReload">Should the item be reloaded immediately?</param>
        public void ItemReloadComplete(ShootableReloaderModule module, bool success, bool immediateReload)
        {
            photonView.RPC("ItemReloadCompleteRPC", RpcTarget.Others, module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID, success, immediateReload);
        }

        /// <summary>
        /// The item has finished reloading on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="moduleID">The ID of the module being invoked.</param>
        /// <param name="success">Was the item reloaded successfully?</param>
        /// <param name="immediateReload">Should the item be reloaded immediately?</param>
        [PunRPC]
        private void ItemReloadCompleteRPC(int slotID, int actionID, int moduleGroupID, int moduleID,  bool success, bool immediateReload)
        {
            var module = GetModule<ShootableReloaderModule>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) {
                return;
            }
            module.ItemReloadComplete(success, immediateReload);
        }

        /// <summary>
        /// Invokes the Melee Action Attack module.
        /// </summary>
        /// <param name="module">The module that is being invoked.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeMeleeAttackModule(MeleeAttackModule module, MeleeUseDataStream data)
        {
            photonView.RPC("InvokeMeleeAttackModuleRPC", RpcTarget.Others, module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID);
        }

        /// <summary>
        /// Invokes the Melee Action Attack modules over the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being retrieved.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being retrieved.</param>
        /// <param name="moduleID">The ID of the module being retrieved.</param>
        [PunRPC]
        private void InvokeMeleeAttackModuleRPC(int slotID, int actionID, int moduleGroupID, int moduleID)
        {
            var module = GetModule<MeleeAttackModule>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) {
                return;
            }

            var data = Shared.Utility.GenericObjectPool.Get<MeleeUseDataStream>();
            data.MeleeAction = module.MeleeAction;
            module.AttackStart(data);
            Shared.Utility.GenericObjectPool.Return(data);
        }

        /// <summary>
        /// Invokes the Melee Action Attack Effect modules.
        /// </summary>
        /// <param name="module">The module that is being invoked.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeMeleeAttackEffectModule(ActionModule module, MeleeUseDataStream data)
        {
            photonView.RPC("InvokeMeleeAttackEffectModulesRPC", RpcTarget.Others, module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID);
        }

        /// <summary>
        /// Invokes the Melee Action Attack Effects modules over the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being retrieved.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being retrieved.</param>
        /// <param name="moduleID">The bitmask of the invoked modules.</param>
        [PunRPC]
        private void InvokeMeleeAttackEffectModulesRPC(int slotID, int actionID, int moduleGroupID, int moduleID)
        {
            var module = GetModule<MeleeAttackEffectModule>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) {
                return;
            }

            module.StartEffects();
        }

        /// <summary>
        /// Invokes the Melee Action Impact modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="context">The context being sent to the module.</param>
        public void InvokeMeleeImpactModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, MeleeImpactCallbackContext context)
        {
            var sourceCharacterLocomotionViewID = -1;
            if (context.ImpactCollisionData.SourceCharacterLocomotion != null) {
                var sourceCharacterLocomotionView = context.ImpactCollisionData.SourceCharacterLocomotion.gameObject.GetCachedComponent<PhotonView>();
                if (sourceCharacterLocomotionView == null) {
                    Debug.LogError($"Error: The character {context.ImpactCollisionData.SourceCharacterLocomotion.gameObject} must have a PhotonView component added.");
                    return;
                }
                sourceCharacterLocomotionViewID = sourceCharacterLocomotionView.ViewID;
            }

            var sourceGameObject = Utility.PunUtility.GetID(context.ImpactCollisionData.SourceGameObject, out var sourceGameObjectSlotID);
            if (!sourceGameObject.HasID) {
                return;
            }
            var impactGameObject = Utility.PunUtility.GetID(context.ImpactCollisionData.ImpactGameObject, out var impactGameObjectSlotID);
            if (!impactGameObject.HasID) {
                return;
            }
            var impactCollider = Utility.PunUtility.GetID(context.ImpactCollisionData.ImpactCollider.gameObject, out var colliderSlotID);
            if (!impactCollider.HasID) {
                return;
            }

            photonView.RPC("InvokeMeleeImpactModulesRPC", RpcTarget.Others, itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask,
                context.ImpactCollisionData.SourceID, sourceCharacterLocomotionViewID, sourceGameObject.ID, sourceGameObjectSlotID, impactGameObject.ID, impactGameObjectSlotID, impactCollider.ID,
                context.ImpactCollisionData.ImpactPosition, context.ImpactCollisionData.ImpactDirection, context.ImpactCollisionData.ImpactStrength);
        }

        /// <summary>
        /// Invokes the Melee Action Impact modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="sourceID">The ID of the impact.</param>
        /// <param name="sourceCharacterLocomotionViewID">The ID of the CharacterLocomotion component that caused the collision.</param>
        /// <param name="sourceGameObjectID">The ID of the GameObject that caused the collision.</param>
        /// <param name="sourceGameObjectSlotID">The slot ID if an item caused the collision.</param>
        /// <param name="impactGameObjectID">The ID of the GameObject that was impacted.</param>
        /// <param name="impactGameObjectSlotID">The slot ID of the item that was impacted.</param>
        /// <param name="impactColliderID">The ID of the Collider that was impacted.</param>
        /// <param name="impactPosition">The position of impact.</param>
        /// <param name="impactDirection">The direction of impact.</param>
        /// <param name="impactStrength">The strength of the impact.</param>
        [PunRPC]
        private void InvokeMeleeImpactModulesRPC(int slotID, int actionID, int moduleGroupID, int invokedBitmask, uint sourceID, int sourceCharacterLocomotionViewID, uint sourceGameObjectID,
                                                    int sourceGameObjectSlotID, uint impactGameObjectID, int impactGameObjectSlotID, uint impactColliderID, 
                                                    Vector3 impactPosition, Vector3 impactDirection, float impactStrength)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<MeleeImpactModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) {
                return;
            }

            var context = Shared.Utility.GenericObjectPool.Get<MeleeImpactCallbackContext>();
            if (context.ImpactCollisionData == null) {
                context.ImpactCollisionData = new ImpactCollisionData();
                context.ImpactDamageData = new ImpactDamageData();
            }

            var collisionData = context.ImpactCollisionData;
            if (!InitializeImpactCollisionData(ref collisionData, sourceID, sourceCharacterLocomotionViewID, sourceGameObjectID, sourceGameObjectSlotID, impactGameObjectID, impactGameObjectSlotID,
                                                impactColliderID, impactPosition, impactDirection, impactStrength)) {
                Shared.Utility.GenericObjectPool.Return(context);
                return;
            }
            collisionData.SourceComponent = GetItemAction(slotID, actionID);
            context.ImpactCollisionData = collisionData;
            context.MeleeAction = moduleGroup.Modules[0].MeleeAction; // The action will be the same across all modules.

            for (int i = 0; i < moduleGroup.ModuleCount; ++i) {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0) {
                    continue;
                }

                moduleGroup.Modules[i].OnImpact(context);
            }
            Shared.Utility.GenericObjectPool.Return(context);
        }

        /// <summary>
        /// Invokes the Throwable Action Effect modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeThrowableEffectModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ThrowableUseDataStream data)
        {
            photonView.RPC("InvokeThrowableEffectModulesRPC", RpcTarget.Others, itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask);
        }

        /// <summary>
        /// Invokes the Throwable Action Effect modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        [PunRPC]
        private void InvokeThrowableEffectModulesRPC(int slotID, int actionID, int moduleGroupID, int invokedBitmask)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<ThrowableThrowEffectModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) {
                return;
            }

            var data = Shared.Utility.GenericObjectPool.Get<ThrowableUseDataStream>();
            data.ThrowableAction = moduleGroup.Modules[0].ThrowableAction; // The action will be the same across all modules.
            for (int i = 0; i < moduleGroup.ModuleCount; ++i) {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0) {
                    continue;
                }

                moduleGroup.Modules[i].InvokeEffect(data);
            }
            Shared.Utility.GenericObjectPool.Return(data);
        }

        /// <summary>
        /// Enables the object mesh renderers for the Throwable Action.
        /// </summary>
        /// <param name="module">The module that is having the renderers enabled.</param>
        /// <param name="enable">Should the renderers be enabled?</param>
        public void EnableThrowableObjectMeshRenderers(ActionModule module, bool enable)
        {
            photonView.RPC("EnableThrowableObjectMeshRenderersRPC", RpcTarget.Others, module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID, enable);
        }

        /// <summary>
        /// Enables the object mesh renderers for the Throwable Action on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="moduleID">The ID of the module being invoked.</param>
        /// <param name="enable">Should the renderers be enabled?</param>
        [PunRPC]
        private void EnableThrowableObjectMeshRenderersRPC(int slotID, int actionID, int moduleGroupID, int moduleID, bool enable)
        {
            var module = GetModule<Items.Actions.Modules.Throwable.SpawnProjectile>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) {
                return;
            }

            module.EnableObjectMeshRenderers(enable);
        }

        /// <summary>
        /// Invokes the Magic Action Begin or End modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="start">Should the module be started? If false the module will be stopped.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeMagicBeginEndModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, bool start, MagicUseDataStream data)
        {
            photonView.RPC("InvokeMagicBeginEndModulesRPC", RpcTarget.Others, itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask, start);
        }

        /// <summary>
        /// Invokes the Magic Begin or End modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="start">Should the module be started? If false the module will be stopped.</param>
        [PunRPC]
        private void InvokeMagicBeginEndModulesRPC(int slotID, int actionID, int moduleGroupID, int invokedBitmask, bool start)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<MagicStartStopModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) {
                return;
            }

            var data = Shared.Utility.GenericObjectPool.Get<MagicUseDataStream>();
            data.MagicAction = moduleGroup.Modules[0].MagicAction; // The action will be the same across all modules.
            for (int i = 0; i < moduleGroup.ModuleCount; ++i) {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0) {
                    continue;
                }

                if (start) {
                    moduleGroup.Modules[i].Start(data);
                } else {
                    moduleGroup.Modules[i].Stop(data);
                }
            }
            Shared.Utility.GenericObjectPool.Return(data);
        }

        /// <summary>
        /// Invokes the Magic Cast Effect modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="state">Specifies the state of the cast.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeMagicCastEffectsModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, INetworkCharacterInventory.CastEffectState state, MagicUseDataStream data)
        {
            var originTransform = Utility.PunUtility.GetID(data.CastData.CastOrigin?.gameObject, out var originTransformSlotID);
            photonView.RPC("InvokeMagicCastEffectsModulesRPC", RpcTarget.Others, itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask, (short)state, 
                data.CastData.CastID, data.CastData.StartCastTime, originTransform.ID, originTransformSlotID, data.CastData.CastPosition, data.CastData.CastNormal,
                data.CastData.Direction, data.CastData.CastTargetPosition);
        }

        /// <summary>
        /// Invokes the Magic Cast Effects modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="state">Specifies the state of the cast.</param>
        [PunRPC]
        private void InvokeMagicCastEffectsModulesRPC(int slotID, int actionID, int moduleGroupID, int invokedBitmask, short state, uint castID, float startCastTime,
                                                        uint originTransformID, int originTransformSlotID, Vector3 castPosition, Vector3 castNormal, Vector3 direction, Vector3 castTargetPosition)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<MagicCastEffectModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) {
                return;
            }

            var data = Shared.Utility.GenericObjectPool.Get<MagicUseDataStream>();
            if (data.CastData == null) {
                data.CastData = new MagicCastData();
            }
            data.MagicAction = moduleGroup.Modules[0].MagicAction; // The action will be the same across all modules.
            data.CastData.CastID = castID;
            data.CastData.StartCastTime = startCastTime;
            data.CastData.CastPosition = castPosition;
            data.CastData.CastNormal = castNormal;
            data.CastData.Direction = direction;
            data.CastData.CastTargetPosition = castTargetPosition;
            
            var originGameObject = Utility.PunUtility.RetrieveGameObject(null, originTransformID, originTransformSlotID);
            if (originGameObject != null) {
                data.CastData.CastOrigin = originGameObject.transform;
            }

            for (int i = 0; i < moduleGroup.ModuleCount; ++i) {
                // Not all modules are invoked.
                if ((moduleGroup.Modules[i].ID & invokedBitmask) == 0) {
                    continue;
                }

                switch ((INetworkCharacterInventory.CastEffectState)state) {
                    case INetworkCharacterInventory.CastEffectState.Start:
                        moduleGroup.Modules[i].StartCast(data);
                        break;
                    case INetworkCharacterInventory.CastEffectState.Update:
                        moduleGroup.Modules[i].OnCastUpdate(data);
                        break;
                    case INetworkCharacterInventory.CastEffectState.End:
                        moduleGroup.Modules[i].StopCast();
                        break;
                }
            }
            Shared.Utility.GenericObjectPool.Return(data);
        }

        /// <summary>
        /// Invokes the Magic Action Impact modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="context">The context being sent to the module.</param>
        public void InvokeMagicImpactModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ImpactCallbackContext context)
        {
            var sourceCharacterLocomotionViewID = -1;
            if (context.ImpactCollisionData.SourceCharacterLocomotion != null) {
                var sourceCharacterLocomotionView = context.ImpactCollisionData.SourceCharacterLocomotion.gameObject.GetCachedComponent<PhotonView>();
                if (sourceCharacterLocomotionView == null) {
                    Debug.LogError($"Error: The character {context.ImpactCollisionData.SourceCharacterLocomotion.gameObject} must have a PhotonView component added.");
                    return;
                }
                sourceCharacterLocomotionViewID = sourceCharacterLocomotionView.ViewID;
            }

            var sourceGameObject = Utility.PunUtility.GetID(context.ImpactCollisionData.SourceGameObject, out var sourceGameObjectSlotID);
            if (!sourceGameObject.HasID) {
                return;
            }
            var impactGameObject = Utility.PunUtility.GetID(context.ImpactCollisionData.ImpactGameObject, out var impactGameObjectSlotID);
            if (!impactGameObject.HasID) {
                return;
            }
            var impactCollider = Utility.PunUtility.GetID(context.ImpactCollisionData.ImpactCollider.gameObject, out var colliderSlotID);
            if (!impactCollider.HasID) {
                return;
            }

            photonView.RPC("InvokeMagicImpactModulesRPC", RpcTarget.Others, itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask,
                context.ImpactCollisionData.SourceID, sourceCharacterLocomotionViewID, sourceGameObject.ID, sourceGameObjectSlotID, impactGameObject.ID, impactGameObjectSlotID, impactCollider.ID,
                context.ImpactCollisionData.ImpactPosition, context.ImpactCollisionData.ImpactDirection, context.ImpactCollisionData.ImpactStrength);
        }

        /// <summary>
        /// Invokes the Magic Action Impact modules on the network.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="context">The context being sent to the module.</param>
        /// <param name="sourceID">The ID of the impact.</param>
        /// <param name="sourceCharacterLocomotionViewID">The ID of the CharacterLocomotion component that caused the collision.</param>
        /// <param name="sourceGameObjectID">The ID of the GameObject that caused the collision.</param>
        /// <param name="sourceGameObjectSlotID">The slot ID if an item caused the collision.</param>
        /// <param name="impactGameObjectID">The ID of the GameObject that was impacted.</param>
        /// <param name="impactGameObjectSlotID">The slot ID of the item that was impacted.</param>
        /// <param name="impactColliderID">The ID of the Collider that was impacted.</param>
        /// <param name="impactPosition">The position of impact.</param>
        /// <param name="impactDirection">The direction of impact.</param>
        /// <param name="impactStrength">The strength of the impact.</param>
        [PunRPC]
        public void InvokeMagicImpactModulesRPC(int slotID, int actionID, int moduleGroupID, int invokedBitmask, uint sourceID, int sourceCharacterLocomotionViewID, uint sourceGameObjectID,
                                                    int sourceGameObjectSlotID, uint impactGameObjectID, int impactGameObjectSlotID, uint impactColliderID, Vector3 impactPosition, Vector3 impactDirection, float impactStrength)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<MagicImpactModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) {
                return;
            }

            var context = Shared.Utility.GenericObjectPool.Get<ImpactCallbackContext>();
            if (context.ImpactCollisionData == null) {
                context.ImpactCollisionData = new ImpactCollisionData();
                context.ImpactDamageData = new ImpactDamageData();
            }
            var collisionData = context.ImpactCollisionData;
            if (!InitializeImpactCollisionData(ref collisionData, sourceID, sourceCharacterLocomotionViewID, sourceGameObjectID, sourceGameObjectSlotID, impactGameObjectID, impactGameObjectSlotID,
                                                impactColliderID, impactPosition, impactDirection, impactStrength)) {
                Shared.Utility.GenericObjectPool.Return(context);
                return;
            }
            collisionData.SourceComponent = GetItemAction(slotID, actionID);
            context.ImpactCollisionData = collisionData;

            for (int i = 0; i < moduleGroup.ModuleCount; ++i) {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0) {
                    continue;
                }

                moduleGroup.Modules[i].OnImpact(context);
            }
            Shared.Utility.GenericObjectPool.Return(context);
        }

        /// <summary>
        /// Invokes the Usable Action Geenric Effect module.
        /// </summary>
        /// <param name="module">The module that should be invoked.</param>
        public void InvokeGenericEffectModule(ActionModule module)
        {
            photonView.RPC("InvokeGenericEffectModuleRPC", RpcTarget.Others, module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID);
        }

        /// <summary>
        /// Invokes the Usable Action Geenric Effect module on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="moduleID">The ID of the module being invoked.</param>
        [PunRPC]
        private void InvokeGenericEffectModuleRPC(int slotID, int actionID, int moduleGroupID, int moduleID)
        {
            var module = GetModule<Items.Actions.Modules.GenericItemEffects>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) {
                return;
            }
            module.EffectGroup.InvokeEffects();
        }

        /// <summary>
        /// Invokes the Use Attribute Modifier Toggle module.
        /// </summary>
        /// <param name="module">The module that should be invoked.</param>
        /// <param name="on">Should the module be toggled on?</param>
        public void InvokeUseAttributeModifierToggleModule(ActionModule module, bool on)
        {
            photonView.RPC("InvokeUseAttributeModifierToggleModuleRPC", RpcTarget.Others, module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID, on);
        }

        /// <summary>
        /// Invokes the Usable Action Geenric Effect module on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="moduleID">The ID of the module being invoked.</param>
        /// <param name="on">Should the module be toggled on?</param>
        [PunRPC]
        private void InvokeUseAttributeModifierToggleModuleRPC(int slotID, int actionID, int moduleGroupID, int moduleID, bool on)
        {
            var module = GetModule<UseAttributeModifierToggle>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) {
                return;
            }
            module.ToggleGameObjects(on);
        }

        /// <summary>
        /// Pushes the target Rigidbody in the specified direction.
        /// </summary>
        /// <param name="targetRigidbody">The Rigidbody to push.</param>
        /// <param name="force">The amount of force to apply.</param>
        /// <param name="point">The point at which to apply the push force.</param>
        public void PushRigidbody(Rigidbody targetRigidbody, Vector3 force, Vector3 point)
        {
            var targetPhotonView = targetRigidbody.gameObject.GetCachedComponent<PhotonView>();
            if (targetPhotonView == null) {
                Debug.LogError($"Error: The object {targetRigidbody.gameObject} must have a PhotonView component added.");
                return;
            }

            photonView.RPC("PushRigidbodyRPC", RpcTarget.MasterClient, targetPhotonView.ViewID, force, point);
        }

        /// <summary>
        /// Pushes the target Rigidbody in the specified direction on the network.
        /// </summary>
        /// <param name="targetRigidbody">The Rigidbody to push.</param>
        /// <param name="force">The amount of force to apply.</param>
        /// <param name="point">The point at which to apply the push force.</param>
        [PunRPC]
        private void PushRigidbodyRPC(int rigidbodyPhotonViewID, Vector3 force, Vector3 point)
        {
            var targetRigidbodyPhotonView = PhotonNetwork.GetPhotonView(rigidbodyPhotonViewID);
            if (targetRigidbodyPhotonView == null) {
                return;
            }

            var targetRigidbody = targetRigidbodyPhotonView.gameObject.GetComponent<Rigidbody>();
            if (targetRigidbody == null) {
                return;
            }

            targetRigidbody.AddForceAtPosition(force, point, ForceMode.VelocityChange);
        }

        /// <summary>
        /// Sets the rotation of the character.
        /// </summary>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetRotation(Quaternion rotation, bool snapAnimator)
        {
            photonView.RPC("SetRotationRPC", RpcTarget.Others, rotation, snapAnimator);
        }

        /// <summary>
        /// Sets the rotation of the character.
        /// </summary>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        [PunRPC]
        public void SetRotationRPC(Quaternion rotation, bool snapAnimator)
        {
            m_CharacterLocomotion.SetRotation(rotation, snapAnimator);
        }

        /// <summary>
        /// Sets the position of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetPosition(Vector3 position, bool snapAnimator)
        {
            photonView.RPC("SetPositionRPC", RpcTarget.Others, position, snapAnimator);
        }

        /// <summary>
        /// Sets the position of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        [PunRPC]
        public void SetPositionRPC(Vector3 position, bool snapAnimator)
        {
            m_CharacterLocomotion.SetPosition(position, snapAnimator);
        }

        /// <summary>
        /// Resets the rotation and position to their default values.
        /// </summary>
        public void ResetRotationPosition()
        {
            // The ViewID may not be initialized yet.
            if (photonView.ViewID == 0) {
                return;
            }

            photonView.RPC("ResetRotationPositionRPC", RpcTarget.Others);
        }

        /// <summary>
        /// Resets the rotation and position to their default values on the network.
        /// </summary>
        [PunRPC]
        public void ResetRotationPositionRPC()
        {
            m_CharacterLocomotion.ResetRotationPosition();
        }

        /// <summary>
        /// Sets the position and rotation of the character on the network.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        /// <param name="stopAllAbilities">Should all abilities be stopped?</param>
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities)
        {
            photonView.RPC("SetPositionAndRotationRPC", RpcTarget.Others, position, rotation, snapAnimator, stopAllAbilities);
        }

        /// <summary>
        /// Sets the position and rotation of the character on the network..
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        /// <param name="stopAllAbilities">Should all abilities be stopped?</param>
        [PunRPC]
        public void SetPositionAndRotationRPC(Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities)
        {
            m_CharacterLocomotion.SetPositionAndRotation(position, rotation, snapAnimator, stopAllAbilities);
        }

        /// <summary>
        /// Changes the character model.
        /// </summary>
        /// <param name="modelIndex">The index of the model within the ModelManager.</param>
        public void ChangeModels(int modelIndex)
        {
            photonView.RPC("ChangeModelsRPC", RpcTarget.Others, modelIndex);
        }

        /// <summary>
        /// Changes the character model on the network.
        /// </summary>
        /// <param name="modelIndex">The index of the model within the ModelManager.</param>
        [PunRPC]
        private void ChangeModelsRPC(int modelIndex)
        {
            if (modelIndex < 0 || m_ModelManager.AvailableModels == null || modelIndex >= m_ModelManager.AvailableModels.Length) {
                return;
            }

            // ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER will be defined, but it is required here to allow the add-on to be compiled for the first time.
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            m_ModelManager.ChangeModels(m_ModelManager.AvailableModels[modelIndex], true);
#endif
        }

        /// <summary>
        /// Activates or deactivates the character.
        /// </summary>
        /// <param name="active">Is the character active?</param>
        /// <param name="uiEvent">Should the OnShowUI event be executed?</param>
        public void SetActive(bool active, bool uiEvent)
        {
            // The ViewID may not be initialized yet.
            if (photonView.ViewID == 0) {
                return;
            }

            photonView.RPC("SetActiveRPC", RpcTarget.Others, active, uiEvent);
        }

        /// <summary>
        /// Activates or deactivates the character on the network.
        /// </summary>
        /// <param name="active">Is the character active?</param>
        /// <param name="uiEvent">Should the OnShowUI event be executed?</param>
        [PunRPC]
        private void SetActiveRPC(bool active, bool uiEvent)
        {
            m_GameObject.SetActive(active);

            if (uiEvent) {
                EventHandler.ExecuteEvent(m_GameObject, "OnShowUI", active);
            }
        }

        /// <summary>
        /// Executes a bool event.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="value">The bool value.</param>
        public void ExecuteBoolEvent(string eventName, bool value)
        {
            photonView.RPC("ExecuteBoolEventRPC", RpcTarget.Others, eventName, value);
        }

        /// <summary>
        /// Executes a bool event on the network.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="value">The bool value.</param>
        [PunRPC]
        private void ExecuteBoolEventRPC(string eventName, bool value)
        {
            EventHandler.ExecuteEvent(m_GameObject, eventName, value);
        }

        /// <summary>
        /// A player has left the room. Perform any cleanup.
        /// </summary>
        /// <param name="player">The Photon Player that left the room.</param>
        /// <param name="character">The character that the player controlled.</param>
        private void OnPlayerLeftRoom(Player player, GameObject character)
        {
            // The MasterClient is responsible for the AI.
            if (PhotonNetwork.IsMasterClient && m_GameObject.GetCachedComponent<LocalLookSource>() != null) {
                m_CharacterLocomotion.enabled = true;
                return;
            }

            if (character != m_GameObject || m_CharacterLocomotion.LookSource == null || m_CharacterLocomotion.LookSource.GameObject == null) {
                return;
            }

            // The local character has left the room. The character no longer has a look source.
            UltimateCharacterController.Camera.CameraController cameraController = m_CharacterLocomotion.LookSource.GameObject.GetComponent<UltimateCharacterController.Camera.CameraController>();
            if (cameraController != null) {
                cameraController.Character = null;
            }
            EventHandler.ExecuteEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", null);
        }

        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        private void OnDestroy()
        {
            EventHandler.UnregisterEvent<Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);
            EventHandler.UnregisterEvent<Player, GameObject>("OnPlayerEnteredRoom", OnPlayerEnteredRoom);
            EventHandler.UnregisterEvent<Player, GameObject>("OnPlayerLeftRoom", OnPlayerLeftRoom);
        }
    }
}