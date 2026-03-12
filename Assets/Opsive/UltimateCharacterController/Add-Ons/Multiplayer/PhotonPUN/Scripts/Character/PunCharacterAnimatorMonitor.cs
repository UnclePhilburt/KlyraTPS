/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun.Character
{
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Networking.Utility;
    using Photon.Pun;
    using Photon.Realtime;
    using UnityEngine;

    /// <summary>
    /// Synchronizes the Ultimate Character Controller animator across the network.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PunCharacterAnimatorMonitor : MonoBehaviour, IPunObservable
    {
        /// <summary>
        /// Specifies which parameters are dirty.
        /// </summary>
        public enum ParameterDirtyFlags : short
        {
            HorizontalMovement = 1,     // The Horizontal Movement parameter has changed.
            ForwardMovement = 2,        // The Forward Movement parameter has changed.
            Pitch = 4,                  // The Pitch parameter has changed.
            Yaw = 8,                    // The Yaw parameter has changed.
            Speed = 16,                 // The Speed parameter has changed.
            Height = 32,                // The Height parameter has changed.
            Moving = 64,                // The Moving parameter has changed.
            Aiming = 128,               // The Aiming parameter has changed.
            MovementSetID = 256,        // The Movement Set ID parameter has changed.
            AbilityIndex = 512,         // The Ability Index parameter has changed.
            AbilityIntData = 1024,      // The Ability Int Data parameter has changed.
            AbilityFloatData = 2048     // The Ability Float Data parameter has changed.
        }

        private GameObject m_GameObject;
        private PhotonView m_PhotonView;
        private AnimatorMonitor m_AnimatorMonitor;
        private int m_SnappedAbilityIndex;
        private short m_DirtyFlag;
        private byte m_ItemDirtySlot;

        private float m_NetworkHorizontalMovement;
        private float m_NetworkForwardMovement;
        private float m_NetworkPitch;
        private float m_NetworkYaw;
        private float m_NetworkSpeed;
        private float m_NetworkAbilityFloatData;

        public short DirtyFlag { get => m_DirtyFlag; set => m_DirtyFlag = value; }
        public byte ItemDirtySlot { get => m_ItemDirtySlot; set => m_ItemDirtySlot = value; }
        private float HorizontalMovement { get => m_AnimatorMonitor.HorizontalMovement; }
        private float ForwardMovement { get => m_AnimatorMonitor.ForwardMovement; }
        private float Pitch { get => m_AnimatorMonitor.Pitch; }
        private float Yaw { get => m_AnimatorMonitor.Yaw; }
        private float Speed { get => m_AnimatorMonitor.Speed; }
        private float Height { get => m_AnimatorMonitor.Height; }
        private bool Moving { get => m_AnimatorMonitor.Moving; }
        private bool Aiming { get => m_AnimatorMonitor.Aiming; }
        private int MovementSetID { get => m_AnimatorMonitor.MovementSetID; }
        private int AbilityIndex { get => m_AnimatorMonitor.AbilityIndex; }
        private int AbilityIntData { get => m_AnimatorMonitor.AbilityIntData; }
        private float AbilityFloatData { get => m_AnimatorMonitor.AbilityFloatData; }
        private bool HasItemParameters { get => m_AnimatorMonitor.HasItemParameters; }
        private int ParameterSlotCount { get => m_AnimatorMonitor.ParameterSlotCount; }
        private int[] ItemSlotID { get => m_AnimatorMonitor.ItemSlotID; }
        private int[] ItemSlotStateIndex { get => m_AnimatorMonitor.ItemSlotStateIndex; }
        private int[] ItemSlotSubstateIndex { get => m_AnimatorMonitor.ItemSlotSubstateIndex; }

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_PhotonView = GetComponent<PhotonView>();

            var modelManager = m_GameObject.GetCachedComponent<ModelManager>();
            if (modelManager != null) {
                m_AnimatorMonitor = modelManager.ActiveModel.GetCachedComponent<AnimatorMonitor>();
            } else {
                m_AnimatorMonitor = m_GameObject.GetComponentInChildren<AnimatorMonitor>();
            }
            EventHandler.RegisterEvent<GameObject>(m_GameObject, "OnCharacterSwitchModels", OnSwitchModels);
        }

        /// <summary>
        /// Verify the update mode of the animator.
        /// </summary>
        private void Start()
        {
            // Remote players do not move within the FixedUpdate loop.
            if (!m_PhotonView.IsMine) {
                var animators = GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animators.Length; ++i) {
                    animators[i].updateMode = AnimatorUpdateMode.Normal;
                }
            } else {
                EventHandler.RegisterEvent<Player, GameObject>("OnPlayerEnteredRoom", OnPlayerEnteredRoom);
            }
        }

        /// <summary>
        /// A player has entered the room. Ensure the joining player is in sync with the current game state.
        /// </summary>
        /// <param name="player">The Photon Player that entered the room.</param>
        /// <param name="character">The character that the player controls.</param>
        private void OnPlayerEnteredRoom(Player player, GameObject character)
        {
            m_PhotonView.RPC("SynchronizeParametersRPC", player, HorizontalMovement, ForwardMovement, Pitch, Yaw, Speed, 
                                    Height, Moving, Aiming, MovementSetID, AbilityIndex, AbilityIntData, AbilityFloatData);
            if (HasItemParameters) {
                for (int i = 0; i < ParameterSlotCount; ++i) {
                    m_PhotonView.RPC("SynchronizeItemParametersRPC", player, i, ItemSlotID[i], ItemSlotStateIndex[i], ItemSlotSubstateIndex[i]);
                }
            }
        }

        /// <summary>
        /// Sets the initial parameter values.
        /// </summary>
        [PunRPC]
        private void SynchronizeParametersRPC(float horizontalMovement, float forwardMovement, float pitch, float yaw, float speed, float height, bool moving, bool aiming, 
                                                int movementSetID, int abilityIndex, int abilityIntData, float abilityFloatData)
        {
            m_AnimatorMonitor.SetHorizontalMovementParameter(horizontalMovement, 1);
            m_AnimatorMonitor.SetForwardMovementParameter(forwardMovement, 1);
            m_AnimatorMonitor.SetPitchParameter(pitch, 1);
            m_AnimatorMonitor.SetYawParameter(yaw, 1);
            m_AnimatorMonitor.SetSpeedParameter(speed, 1);
            m_AnimatorMonitor.SetHeightParameter(height);
            m_AnimatorMonitor.SetMovingParameter(moving);
            m_AnimatorMonitor.SetAimingParameter(aiming);
            m_AnimatorMonitor.SetMovementSetIDParameter(movementSetID);
            m_AnimatorMonitor.SetAbilityIndexParameter(abilityIndex);
            m_AnimatorMonitor.SetAbilityIntDataParameter(abilityIntData);
            m_AnimatorMonitor.SetAbilityFloatDataParameter(abilityFloatData, 1);

            SnapAnimator();
        }

        /// <summary>
        /// Sets the initial item parameter values.
        /// </summary>
        [PunRPC]
        private void SynchronizeItemParametersRPC(int slotID, int itemID, int itemStateIndex, int itemSubstateIndex)
        {
            m_AnimatorMonitor.SetItemIDParameter(slotID, itemID);
            m_AnimatorMonitor.SetItemStateIndexParameter(slotID, itemStateIndex, false);
            m_AnimatorMonitor.SetItemSubstateIndexParameter(slotID, itemSubstateIndex, false);

            SnapAnimator();
        }

        /// <summary>
        /// Snaps the animator to the default values.
        /// </summary>
        private void SnapAnimator()
        {
            EventHandler.ExecuteEvent(m_GameObject, "OnCharacterSnapAnimator", true);
        }

        /// <summary>
        /// The animator has snapped into position.
        /// </summary>
        public void AnimatorSnapped()
        {
            m_SnappedAbilityIndex = m_AnimatorMonitor.AbilityIndex;
        }

        /// <summary>
        /// Reads/writes the continuous animator parameters.
        /// </summary>
        private void Update()
        {
            // Local players will update the animator through the regular UltimateCharacterLocomotion.Move method.
            if (m_PhotonView.IsMine) {
                return;
            }

            m_AnimatorMonitor.SetHorizontalMovementParameter(m_NetworkHorizontalMovement, 1);
            m_AnimatorMonitor.SetForwardMovementParameter(m_NetworkForwardMovement, 1);
            m_AnimatorMonitor.SetPitchParameter(m_NetworkPitch, 1);
            m_AnimatorMonitor.SetYawParameter(m_NetworkYaw, 1);
            m_AnimatorMonitor.SetSpeedParameter(m_NetworkSpeed, 1);
            m_AnimatorMonitor.SetAbilityFloatDataParameter(m_NetworkAbilityFloatData, 1);
        }

        /// <summary>
        /// Called by PUN several times per second, so that your script can write and read synchronization data for the PhotonView.
        /// </summary>
        /// <param name="stream">The stream that is being written to/read from.</param>
        /// <param name="info">Contains information about the message.</param>
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting) {
                stream.SendNext(m_DirtyFlag);
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.HorizontalMovement) != 0) {
                    stream.SendNext(NetworkCompression.FloatToShortMovement(HorizontalMovement));
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.ForwardMovement) != 0) {
                    stream.SendNext(NetworkCompression.FloatToShortMovement(ForwardMovement));
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.Pitch) != 0) {
                    stream.SendNext(NetworkCompression.FloatToShort(Pitch));
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.Yaw) != 0) {
                    stream.SendNext(NetworkCompression.FloatToShort(Yaw));
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.Speed) != 0) {
                    stream.SendNext(NetworkCompression.FloatToShort(Speed));
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.Height) != 0) {
                    stream.SendNext(NetworkCompression.FloatToShort(Height));
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.Moving) != 0) {
                    stream.SendNext(Moving);
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.Aiming) != 0) {
                    stream.SendNext(Aiming);
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.MovementSetID) != 0) {
                    stream.SendNext(MovementSetID);
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.AbilityIndex) != 0) {
                    stream.SendNext(AbilityIndex);
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.AbilityIntData) != 0) {
                    stream.SendNext(AbilityIntData);
                }
                if ((m_DirtyFlag & (short)ParameterDirtyFlags.AbilityFloatData) != 0) {
                    stream.SendNext(NetworkCompression.FloatToShort(AbilityFloatData));
                }
                if (HasItemParameters) {
                    stream.SendNext(m_ItemDirtySlot);
                    for (int i = 0; i < ParameterSlotCount; ++i) {
                        if ((m_ItemDirtySlot & (i + 1)) == 0) {
                            continue;
                        }
                        stream.SendNext(ItemSlotID[i]);
                        stream.SendNext(ItemSlotStateIndex[i]);
                        stream.SendNext(ItemSlotSubstateIndex[i]);
                    }
                }

                m_DirtyFlag = 0;
                m_ItemDirtySlot = 0;
            } else { // Reading.
                var dirtyFlag = (short)stream.ReceiveNext();
                if ((dirtyFlag & (short)ParameterDirtyFlags.HorizontalMovement) != 0) {
                    m_NetworkHorizontalMovement = NetworkCompression.ShortToFloatMovement(System.Convert.ToInt16(stream.ReceiveNext()));
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.ForwardMovement) != 0) {
                    m_NetworkForwardMovement = NetworkCompression.ShortToFloatMovement(System.Convert.ToInt16(stream.ReceiveNext()));
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.Pitch) != 0) {
                    m_NetworkPitch = NetworkCompression.ShortToFloat((short)stream.ReceiveNext());
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.Yaw) != 0) {
                    m_NetworkYaw = NetworkCompression.ShortToFloat((short)stream.ReceiveNext());
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.Speed) != 0) {
                    m_NetworkSpeed = NetworkCompression.ShortToFloat((short)stream.ReceiveNext());
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.Height) != 0) {
                    m_AnimatorMonitor.SetHeightParameter(NetworkCompression.ShortToFloat((short)stream.ReceiveNext()));
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.Moving) != 0) {
                    m_AnimatorMonitor.SetMovingParameter((bool)stream.ReceiveNext());
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.Aiming) != 0) {
                    m_AnimatorMonitor.SetAimingParameter((bool)stream.ReceiveNext());
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.MovementSetID) != 0) {
                    m_AnimatorMonitor.SetMovementSetIDParameter((int)stream.ReceiveNext());
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.AbilityIndex) != 0) {
                    var abilityIndex = (int)stream.ReceiveNext();
                    // When the animator is snapped the ability index will be reset. It may take some time for that value to propagate across the network.
                    // Wait to set the ability index until it is the correct reset value.
                    if (m_SnappedAbilityIndex == 0 || abilityIndex == m_SnappedAbilityIndex) {
                        m_AnimatorMonitor.SetAbilityIndexParameter(abilityIndex);
                        m_SnappedAbilityIndex = 0;
                    }
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.AbilityIntData) != 0) {
                    m_AnimatorMonitor.SetAbilityIntDataParameter((int)stream.ReceiveNext());
                }
                if ((dirtyFlag & (short)ParameterDirtyFlags.AbilityFloatData) != 0) {
                    m_NetworkAbilityFloatData = NetworkCompression.ShortToFloat((short)stream.ReceiveNext());
                }
                if (HasItemParameters) {
                    var itemDirtySlot = (byte)stream.ReceiveNext();
                    for (int i = 0; i < ParameterSlotCount; ++i) {
                        if ((itemDirtySlot & (i + 1)) == 0) {
                            continue;
                        }
                        m_AnimatorMonitor.SetItemIDParameter(i, (int)stream.ReceiveNext());
                        m_AnimatorMonitor.SetItemStateIndexParameter(i, (int)stream.ReceiveNext(), false);
                        m_AnimatorMonitor.SetItemSubstateIndexParameter(i, (int)stream.ReceiveNext(), false);
                    }
                }
            }
        }

        /// <summary>
        /// The character's model has switched.
        /// </summary>
        /// <param name="activeModel">The active character model.</param>
        private void OnSwitchModels(GameObject activeModel)
        {
            m_AnimatorMonitor = activeModel.GetCachedComponent<AnimatorMonitor>();
        }

        /// <summary>
        /// The object has been destroyed.
        /// </summary>
        private void OnDestroy()
        {
            EventHandler.UnregisterEvent<Player, GameObject>("OnPlayerEnteredRoom", OnPlayerEnteredRoom);
            EventHandler.UnregisterEvent<GameObject>(m_GameObject, "OnCharacterSwitchModels", OnSwitchModels);
        }
    }
}