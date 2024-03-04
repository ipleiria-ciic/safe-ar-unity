using Unity.VisualScripting;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NiceIODisable : MonoBehaviour
{
#if UNITY_EDITOR
    [InitializeOnEnterPlayMode]
    public static void DisableCodebaseWarnings()
    {
        Debug.unityLogger.logEnabled = false;
        var _ = Codebase.assemblies;
        Debug.unityLogger.logEnabled = true;
    }
#endif
}
