using UnityEngine;
using Photon.Pun;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Inventory;
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
    private bool weaponsInitialized = false;

    void Start()
    {
        photonView = GetComponent<PhotonView>();
        punCharacter = GetComponent<PunCharacter>();
        inventory = GetComponent<InventoryBase>();

        if (photonView == null)
        {
            Debug.LogError($"BotWeaponSync on {gameObject.name}: Missing PhotonView component!");
            return;
        }

        // Initialize weapons after a short delay
        Invoke("InitializeWeapons", 0.5f);
    }

    void InitializeWeapons()
    {
        if (weaponsInitialized) return;
        weaponsInitialized = true;

        if (photonView.IsMine)
        {
            // On the owner (Master Client), load the default loadout
            if (inventory != null)
            {
                inventory.LoadDefaultLoadout();

                // Tell other clients to load weapons too
                if (punCharacter != null)
                {
                    punCharacter.LoadDefaultLoadout();
                }
            }
        }
        else
        {
            // On remote clients, ensure items are picked up
            var items = GetComponentsInChildren<Opsive.UltimateCharacterController.Items.CharacterItem>(true);
            for (int i = 0; i < items.Length; ++i)
            {
                items[i].Pickup();
            }

            // Load default loadout
            if (inventory != null)
            {
                inventory.LoadDefaultLoadout();
            }
        }

        Debug.Log($"[BotWeaponSync] Initialized weapons for {gameObject.name}. Owner: {photonView.IsMine}");
    }
}
