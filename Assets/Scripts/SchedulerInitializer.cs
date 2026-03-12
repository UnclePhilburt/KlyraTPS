using UnityEngine;
using System.Reflection;

/// <summary>
/// Increases Opsive Scheduler limit at runtime
/// This MUST run before any Opsive components initialize
/// </summary>
public class SchedulerInitializer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void IncreaseSchedulerLimitEarly()
    {
        IncreaseLimit();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void IncreaseSchedulerLimitAfterAssemblies()
    {
        IncreaseLimit();
    }

    static void IncreaseLimit()
    {
        bool success = false;

        try
        {
            // Try to find the Scheduler type
            var schedulerType = System.Type.GetType("Opsive.Shared.Game.Scheduler, Opsive.Shared");
            if (schedulerType == null)
            {
                schedulerType = System.Type.GetType("Opsive.Shared.Game.SchedulerBase, Opsive.Shared");
            }

            if (schedulerType != null)
            {
                // Try multiple possible field names
                string[] possibleFields = {
                    "s_MaxActiveEventCount",
                    "m_MaxActiveEventCount",
                    "MaxEventCount",
                    "s_MaxEventCount",
                    "m_MaxEventCount"
                };

                foreach (var fieldName in possibleFields)
                {
                    var field = schedulerType.GetField(fieldName,
                        BindingFlags.Static | BindingFlags.Instance |
                        BindingFlags.NonPublic | BindingFlags.Public);

                    if (field != null)
                    {
                        if (field.IsStatic)
                        {
                            field.SetValue(null, 5000);
                        }

                        success = true;
                        Debug.Log($"<color=green>Scheduler limit increased to 5000 via {fieldName}</color>");
                        break;
                    }
                }

                // Try properties too
                if (!success)
                {
                    var property = schedulerType.GetProperty("MaxEventCount",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(null, 5000);
                        success = true;
                        Debug.Log("<color=green>Scheduler limit increased to 5000 via property</color>");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Scheduler limit adjustment: {e.Message}");
        }

        if (!success)
        {
            Debug.LogWarning("<color=yellow>Could not increase Scheduler limit automatically. You may need to reduce LocalLookSource usage on bots.</color>");
        }
    }
}
