#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class TagFinder : MonoBehaviour
{
    public List<string> tags = new List<string>();
    public List<GameObject> foundObjects = new List<GameObject>();

    public void UpdateObjects()
    {
        foundObjects.Clear();

        foreach (string tag in tags)
        {
            if (string.IsNullOrEmpty(tag)) continue;

            GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);

            foreach (var obj in objs)
            {
                if (!foundObjects.Contains(obj))
                    foundObjects.Add(obj);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TagFinder))]
public class TagFinderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TagFinder script = (TagFinder)target;

        if (GUILayout.Button("Find Objects By Tags"))
        {
            script.UpdateObjects();
        }
    }
}
#endif