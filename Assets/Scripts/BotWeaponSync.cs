using UnityEngine;
using Photon.Pun;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Items;
using Opsive.UltimateCharacterController.AddOns.Multiplayer.PhotonPun.Character;

/// <summary>
/// Ensures bot weapons are properly synchronized across the network
/// Fixes issue where weapons don't appear for non-master clients
/// </summary>
public class BotWeaponSync : MonoBehaviour
{
    private PhotonView photonView;
    private PunCharacter punCharacter;
    private InventoryBase inventory;
    private UltimateCharacterLocomotion characterLocomotion;
    private bool weaponsInitialized = false;
    private float initAttempts = 0;

    void Start()
    {
        photonView = GetComponent<PhotonView>();
        punCharacter = GetComponent<PunCharacter>();
        inventory = GetComponent<InventoryBase>();
        characterLocomotion = GetComponent<UltimateCharacterLocomotion>();

        if (photonView == null)
        {
            Debug.LogError($"BotWeaponSync on {gameObject.name}: Missing PhotonView component!");
            return;
        }

        // Try multiple times with increasing delays
        InvokeRepeating("TryInitializeWeapons", 0.1f, 0.2f);
    }

    void TryInitializeWeapons()
    {
        if (weaponsInitialized)
        {
            CancelInvoke("TryInitializeWeapons");
            return;
        }

        initAttempts++;

        // Give up after 10 attempts (2 seconds)
        if (initAttempts > 10)
        {
            Debug.LogWarning($"[BotWeaponSync] Failed to initialize weapons for {gameObject.name} after 10 attempts");
            CancelInvoke("TryInitializeWeapons");
            return;
        }

        // On remote clients (non-master), force weapon visibility
        if (!photonView.IsMine)
        {
            // Method 1: Pickup all items
            var items = GetComponentsInChildren<CharacterItem>(true);
            if (items.Length > 0)
            {
                Debug.Log($"[BotWeaponSync] Found {items.Length} items on {gameObject.name}");
                foreach (var item in items)
                {
                    if (item != null && !item.IsActive())
                    {
                        item.Pickup();
                    }
                }
            }

            // Method 2: Load default loadout
            if (inventory != null)
            {
                inventory.LoadDefaultLoadout();
            }

            // Check if we succeeded
            if (items.Length > 0)
            {
                bool hasActiveItem = false;
                foreach (var item in items)
                {
                    if (item != null && item.IsActive())
                    {
                        hasActiveItem = true;
                        break;
                    }
                }

                if (hasActiveItem)
                {
                    weaponsInitialized = true;
                    Debug.Log($"[BotWeaponSync] Successfully initialized weapons for {gameObject.name} on attempt {initAttempts}");
                    CancelInvoke("TryInitializeWeapons");
                }
                else
                {
                    Debug.Log($"[BotWeaponSync] Attempt {initAttempts} - No active items yet for {gameObject.name}");
                }
            }
        }
        else
        {
            // On owner (Master Client), just load default loadout once
            if (inventory != null)
            {
                inventory.LoadDefaultLoadout();
                weaponsInitialized = true;
                CancelInvoke("TryInitializeWeapons");
                Debug.Log($"[BotWeaponSync] Loaded default loadout for {gameObject.name} (Owner)");
            }
        }
    }
}
