using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


public class DatasetUtils : MonoBehaviour
{

    static void ChangeRendererProperty(GameObject gm)
    {
        if (gm.GetComponent<Renderer>() == null)
        {
            for (int i = 0; i < gm.transform.childCount; i++)
            {
                ChangeRendererProperty(gm.transform.GetChild(i).gameObject);
            }
        }
        else
        {
            gm.GetComponent<Renderer>().receiveShadows = true;
        }
    }
    public static void AdjustObject(GameObject gm)
    {
        // The hierarchy must be DATASET NAME -> Obj1 (with changed transform) -> Obj1 (just a container with default values) -> MeshA, MeshB, etc with Renderer
        // Or DATASET NAME -> Obj1  -> MeshA, MeshB, etc with Renderer
        ChangeRendererProperty(gm);

        var cc = gm.transform.GetChild(0);
        GameObject newChild;
        if (cc.GetComponent<Renderer>() != null)
        {
            newChild = new GameObject(gm.name);
            newChild.transform.position = gm.transform.position;

            for (int i = gm.transform.childCount - 1; i>= 0; i--)
            {
                gm.transform.GetChild(i).parent = newChild.transform;
            }
            newChild.transform.parent = gm.transform;

        }
        ScaleAndMovePivotObj(gm);
    }
    static void ScaleAndMovePivotObj(GameObject gm)
    {
        // Assume the hierarchy is gm -> Obj1 (change the position) -> MeshA, MeshB etc. (with renderer)
        Bounds bb = new Bounds();
        int children = gm.transform.GetChild(0).transform.childCount;
        //Debug.Log("CHILDREN: " + children);
        for (int i = 0; i < children; i++)
        {
            var obj = gm.transform.GetChild(0).transform.GetChild(i);
            //Debug.Log(obj.name);
            if (i == 0)
            {
                bb = obj.GetComponent<Renderer>().bounds;
            }
            else
            {
                bb.Encapsulate(obj.GetComponent<Renderer>().bounds);
            }
        }
        var center = bb.center;
        var size = bb.size;
        //Debug.Log("HERE: " + (gm.transform.GetChild(0).transform.position - center));
        gm.transform.GetChild(0).transform.position += (gm.transform.GetChild(0).transform.position - center);

        float maxSize = 3f;
        gm.transform.localScale = gm.transform.localScale / (Mathf.Max(Mathf.Max(size.x, size.y), size.z) / maxSize);
    }

}


#if UNITY_EDITOR
[ExecuteInEditMode]
[CustomEditor(typeof(DatasetUtils))]
public class DatasetUtilsEditor : Editor
{
    // Start is called before the first frame update

    [CanEditMultipleObjects]
    public Object datasetToAdjust;

    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();

            datasetToAdjust = EditorGUILayout.ObjectField(datasetToAdjust, typeof(Object), true);

            if (GUILayout.Button("Adjust Dataset"))
            {
                GameObject adjusted = (GameObject)GameObject.Instantiate(datasetToAdjust, GameObject.Find(datasetToAdjust.name).transform.parent);
                adjusted.name = datasetToAdjust.name + "ADJ";
                adjusted.transform.localPosition = new Vector3(0f, 0f, 0f);
                List<GameObject> Children = new List<GameObject>(); ;
                foreach (Transform child in adjusted.transform)
                {
                    Children.Add(child.gameObject);
                }
                foreach (var child in Children)
                {
                    DatasetUtils.AdjustObject(child.gameObject);
                }
                GameObject.Find(datasetToAdjust.name).SetActive(false);

            }
            EditorGUILayout.EndHorizontal();
        }
    }
}

#endif