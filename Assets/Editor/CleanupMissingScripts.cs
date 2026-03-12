using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor script to remove missing script references from prefabs
/// </summary>
public class CleanupMissingScripts : EditorWindow
{
    [MenuItem("Tools/Cleanup Missing Scripts in Bot Prefabs")]
    static void CleanupBotPrefabs()
    {
        string[] prefabPaths = new string[]
        {
            "Assets/Resources/BotRifleman.prefab",
            "Assets/Resources/BotSAWGunner.prefab",
            "Assets/Resources/BotFighter.prefab",
            "Assets/Resources/BotHeavyGunner.prefab"
        };

        int totalCleaned = 0;

        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                int cleaned = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefab);
                if (cleaned > 0)
                {
                    EditorUtility.SetDirty(prefab);
                    totalCleaned += cleaned;
                    Debug.Log($"<color=green>Removed {cleaned} missing script(s) from {prefab.name}</color>");
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=cyan>Cleanup complete! Removed {totalCleaned} missing script references total.</color>");
    }
}
