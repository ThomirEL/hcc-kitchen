using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor utility — copies a Box Collider from a child handle onto this GameObject.
/// Attach to your shelf/drawer root, assign the handle in the Inspector,
/// hit "Copy Collider From Handle", then the script removes itself.
/// 
/// Attach to: the shelf/drawer root GameObject (the one needing the collider)
/// </summary>
public class CopyCollider : MonoBehaviour
{
    [Tooltip("The child handle GameObject that already has a Box Collider")]
    public GameObject handle;

#if UNITY_EDITOR
    /// <summary>
    /// Called by the custom Inspector button.
    /// Copies the handle's BoxCollider onto this GameObject, converting
    /// the collider bounds from handle local space to this object's local space.
    /// </summary>
    public void DoCopy()
    {
        if (handle == null)
        {
            Debug.LogError("[CopyCollider] No handle assigned.");
            return;
        }

        BoxCollider sourceCol = handle.GetComponent<BoxCollider>();
        if (sourceCol == null)
        {
            Debug.LogError("[CopyCollider] Handle has no BoxCollider.");
            return;
        }

        // Convert handle's collider center from handle local space → world space → this object's local space
        // This ensures the copied collider sits in the right place even if handle is offset/rotated
        Vector3 worldCenter = handle.transform.TransformPoint(sourceCol.center);
        Vector3 localCenter = transform.InverseTransformPoint(worldCenter);

        // Scale the size to account for any difference in scale between handle and this object
        Vector3 worldSize = new Vector3(
            sourceCol.size.x * handle.transform.lossyScale.x,
            sourceCol.size.y * handle.transform.lossyScale.y,
            sourceCol.size.z * handle.transform.lossyScale.z
        );
        Vector3 localSize = new Vector3(
            worldSize.x / transform.lossyScale.x,
            worldSize.y / transform.lossyScale.y,
            worldSize.z / transform.lossyScale.z
        );

        // Add or reuse a BoxCollider on this GameObject
        BoxCollider destCol = gameObject.GetComponent<BoxCollider>();
        if (destCol == null)
            destCol = Undo.AddComponent<BoxCollider>(gameObject);

        Undo.RecordObject(destCol, "Copy Collider From Handle");
        destCol.center = localCenter;
        destCol.size   = localSize;

        EditorUtility.SetDirty(gameObject);
        Debug.Log($"[CopyCollider] Collider copied from '{handle.name}' to '{gameObject.name}'. You can now remove this script.");
    }
#endif
}


#if UNITY_EDITOR
[CustomEditor(typeof(CopyCollider))]
public class CopyColliderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(6);

        var script = (CopyCollider)target;

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
        if (GUILayout.Button("▶  Copy Collider From Handle", GUILayout.Height(32)))
        {
            script.DoCopy();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "1. Assign the handle child in the field above\n" +
            "2. Click the button\n" +
            "3. Remove this script — job done",
            MessageType.Info);
    }
}
#endif