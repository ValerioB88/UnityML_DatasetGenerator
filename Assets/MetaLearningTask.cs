using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using UnityEngine.PlayerLoop;
using System.Collections;
using Unity.MLAgents.Policies;
using Unity.Barracuda;
using System.Data;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
using UnityEditor;

#endif 
using UnityEngine.Assertions;
using Unity.MLAgents.SideChannels;

public class MetaLearningTask : Agent
{
    [HideInInspector]
    public bool runEnable = true;

    List<GameObject> datasetList = new List<GameObject>();
    List<GameObject> selectedObjs = new List<GameObject>();
    List<GameObject> cloneObjs = new List<GameObject>();
  
    [HideInInspector]
    public GameObject cameraContainer;
    
    [HideInInspector]
    public GameObject agent;
    Dictionary<string, int> mapNameToNum = new Dictionary<string, int>();

    List<Vector3> positions = new List<Vector3>();
    List<int> labelsSupport = new List<int>();
    List<int> labelsTesting = new List<int>();

    int N;
    int K;
    int Q;
    int sizeCanvas;
    string nameDataset = "none";
    GameObject info;
    List<GameObject> supportCameras = new List<GameObject>();
    List<GameObject> queryCameras = new List<GameObject>();

    // The position used to scatter other camera around, one for each object k 
    List<Vector3> standardPositionTestingCameras = new List<Vector3>();
    List<Vector3> standardPositionSupportCameras = new List<Vector3>();
    public float distance;
    List<Vector3> debugPointsSupport = new List<Vector3>();
    List<Vector3> debugPointsQuery = new List<Vector3>();

    bool firstEpisode = true;


    List<GameObject> standardQueryCamera = new List<GameObject>();
    List<GameObject> standardSupportCamera = new List<GameObject>(); 

    public void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    
    StringLogSideChannel stringChannel;
    public void Awake()
    {
        // We create the Side Channel
        stringChannel = new StringLogSideChannel();

        // The channel must be registered with the SideChannelManager class
        //SideChannelManager.RegisterSideChannel(stringChannel);
    }

    //public void OnDestroy()
    //{
    //    // De-register the Debug.Log callback
    //    Application.logMessageReceived -= stringChannel.SendDebugStatementToPython;
    //    if (Academy.IsInitialized)
    //    {
    //        SideChannelManager.UnregisterSideChannel(stringChannel);
    //    }
    //}

    void FillDataset()
    {
      
        GameObject dataset = GameObject.Find(nameDataset);
        if (dataset == null)
            Assert.IsTrue(false, "Dataset " + nameDataset + " not found.");
        int children = dataset.transform.childCount;
        for (int i = children - 1; i >= 0; i--)
        {
            var obj = dataset.transform.GetChild(i);
            GameObject pp = new GameObject("p_" + obj.name);
            pp.transform.position = obj.transform.position;
            pp.transform.rotation = obj.transform.rotation;
            pp.transform.parent = dataset.transform;
            obj.parent = pp.transform;
           
        }
        for (int i = 0; i < children; i++)
        {
            var obj = dataset.transform.GetChild(i);
            
            //UnityEngine.Debug.Log(dataset.transform.GetChild(i).gameObject.name);
            datasetList.Add(obj.gameObject);
            mapNameToNum.Add(datasetList[i].name, i);
        }
        Assert.IsTrue(datasetList.Count >= K, "The elements in the datasetList are less than K!");
    }


    private static string GetArg(string name)
    {
        UnityEngine.Debug.Log("I AM HERE READING YOUR COMMAND LINE");

        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            UnityEngine.Debug.Log(args[i]);
            if (args[i] == name && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    void Start()
    {
       
        GameObject cameraContainer = GameObject.Find("CameraContainer");
        info = GameObject.Find("Info");
        

        string infostr = info.transform.GetChild(0).name;
        var tmp = infostr.Split('_');
        N = int.Parse(tmp[0].Split(':')[1]);
        K = int.Parse(tmp[1].Split(':')[1]);
        Q = int.Parse(tmp[2].Split(':')[1]);
        sizeCanvas = int.Parse(tmp[3].Split(':')[1]);

        for (int k = 0; k < K; k++)
        {
            standardQueryCamera.Add(new GameObject("StandardQueryCamera_" + k));
            standardSupportCamera.Add(new GameObject("StandardSupportCamera" + k));
        }

  

        UnityEngine.Debug.Log("ATTENZIONE");
        var cmlNameDataset = GetArg("-name_dataset");
        if (! (cmlNameDataset == null))
        {
            nameDataset = cmlNameDataset;
            UnityEngine.Debug.Log("New Dataset from command line: " + nameDataset);
        }
        else
        {
            nameDataset = tmp[4].Split(':')[1];
        }


        // Rebuild the list

        int totIndex = 0;
        for (int k = 0; k < K; k++)
        {
            for (int n = 0; n < N; n++)
            {
                supportCameras.Add(cameraContainer.transform.Find(totIndex.ToString("D3") + "_SupportCamera").gameObject);
                totIndex += 1;
            }
        }

        for (int k = 0; k < K; k++)
        {
            for (int q = 0; q < Q; q++)
            {
                queryCameras.Add(cameraContainer.transform.Find(totIndex.ToString("D3") + "_QueryCamera").gameObject);
                totIndex += 1;
            }
        }

        //var b  =prova.previousK;
        // Collect all the objects in the Dataset game object
        FillDataset();


        // Create Positions
        for (int k = 0; k < K; k++)
        {
            positions.Add(new Vector3(10 * k, 0, 0));
        }



    }

    void OnDrawGizmos()
    { 
        Gizmos.color = new Vector4(0, 0, 1F, 0.5F);
        foreach (GameObject cam in supportCameras)
        {
            Gizmos.DrawSphere(cam.transform.position, 0.5F);
            Gizmos.DrawWireSphere(cam.transform.position, 0.5F);

        }
        Gizmos.color = new Vector4(0, 1F, 0, 0.5F);

        foreach (GameObject cam in queryCameras)
        {
            Gizmos.DrawSphere(cam.transform.position, 0.5F);
            Gizmos.DrawWireSphere(cam.transform.position, 0.5F);

        }
        // PLOT THE CENTER LITTLE SPHERE 
        Gizmos.color = new Vector4(1F, 0, 0, 0.5F);
        foreach (GameObject cam in supportCameras)
        {
            Gizmos.DrawSphere(cam.transform.position, 0.1F);
            Gizmos.DrawWireSphere(cam.transform.position, 0.1F);

        }

        foreach (GameObject cam in queryCameras)
        {
            Gizmos.DrawSphere(cam.transform.position, 0.1F);
            Gizmos.DrawWireSphere(cam.transform.position, 0.1F);

        }

        for (int k = 0; k < K; k++)
        {
            Gizmos.color = new Vector4(153/255F, 51/255F, 1F, 0.2F);
            Gizmos.DrawWireSphere(cloneObjs[k].transform.position, distance);
            Gizmos.DrawSphere(cloneObjs[k].transform.position, distance);

            // Plot standard pposition
            Gizmos.color = new Vector4(1F, 1F, 0, 0.5F);
            Gizmos.DrawSphere(standardQueryCamera[k].transform.position, 0.1F);
            Gizmos.DrawWireSphere(standardQueryCamera[k].transform.position, 0.1F);

             Gizmos.color = new Vector4(1F, 1F, 0, 0.5F);
            Gizmos.DrawSphere(standardSupportCamera[k].transform.position, 0.1F);
            Gizmos.DrawWireSphere(standardSupportCamera[k].transform.position, 0.1F);
        }

        foreach (Vector3 vec in debugPointsQuery)
        {

            Gizmos.color = new Vector4(0F, 1F, 0, 0.5F);
            Gizmos.DrawSphere(vec, 0.1F);
            Gizmos.DrawWireSphere(vec, 0.1F);
        }

        foreach (Vector3 vec in debugPointsSupport)
        {

            Gizmos.color = new Vector4(0F, 0F, 1F, 0.5F);
            Gizmos.DrawSphere(vec, 0.1F);
            Gizmos.DrawWireSphere(vec, 0.1F);
        }

    }


    Vector3 GetRandomAroundSphere(float angleA, float angleB, float direction, Vector3 aroundPosition)
    {
        Assert.IsTrue(angleA >= 0 && angleB >= 0 && angleA <= 180 && angleB <= 180, "Both angles should be[0, 180]");
        // THe object rotates in direction only if there is a minimum amount of angle displacement.
        if (angleA == angleB)
        {
            if (angleB != 180)
            {
                angleB += 1;
            }
            else
            {
                angleA -= 1;
            }
        }
        var v = direction; //Random.Range(minDirection, maxDirection);
        var a = Mathf.Cos(Mathf.Deg2Rad * angleA);
        var b = Mathf.Cos(Mathf.Deg2Rad * angleB);

        float azimuth = v * 2.0F * UnityEngine.Mathf.PI;
        float cosDistFromZenith = Random.Range(Mathf.Min(a, b), Mathf.Max(a, b));
        float sinDistFromZenith = UnityEngine.Mathf.Sqrt(1.0F - cosDistFromZenith * cosDistFromZenith);
        Vector3 pqr = new Vector3(UnityEngine.Mathf.Cos(azimuth) * sinDistFromZenith, UnityEngine.Mathf.Sin(azimuth) * sinDistFromZenith, cosDistFromZenith);
        Vector3 rAxis = aroundPosition; // Vector3.up when around zenith
        Vector3 pAxis = UnityEngine.Mathf.Abs(rAxis[0]) < 0.9 ? new Vector3(1F, 0F, 0F) : new Vector3(0F, 1F, 0F);
        Vector3 qAxis = Vector3.Normalize(Vector3.Cross(rAxis, pAxis));
        pAxis = Vector3.Cross(qAxis, rAxis);
        Vector3 position = pqr[0] * pAxis + pqr[1] * qAxis + pqr[2] * rAxis;
        return position;
    }

    //public Transform Target;
    public CameraSensor VisualObs;
    public override void OnEpisodeBegin()
    {
        // Fill the object dataset, but only once. You need to do it here as need to wait for python side_channel message.
        //if (firstEpisode)
        //{
        //    FillDataset();
        //    firstEpisode = false;
        //}

        // Get a random position test cam. that can be anywhere on the sphere
        // Scatter each test camera according to "scatterCameraDegree" around the random position test cam.
        // Place random position support cam. at some distance around the random position test cam. according
        // to values provided by the channels minDegreeSQcameras and maxDegreeSQcameras.
        // Finally scatter support cameras around the random pos. supp. cam. according to "scatterCameraDegree". 

        float scatterCameraDegrees = 10; // NORMALLY 10 // this is fixed
        float scatterCameraDirection = 0.1F;  // Normally 0.1 

        var envParameters = Academy.Instance.EnvironmentParameters;
        distance = envParameters.GetWithDefault("distance", 3.0f);
        float minDegreeSQcameras = envParameters.GetWithDefault("minDegreeSQcameras", 0);
        float maxDegreeSQcameras = envParameters.GetWithDefault("maxDegreeSQcameras", 180F);
        float minDirectionSQcameras = envParameters.GetWithDefault("minDirectionSQcamera", -0.1f);  
        float maxDirectionSQcameras = envParameters.GetWithDefault("maxDirectionSQcamera", 0.1f); // from -0.5 to 0.5
        
        float minDegreeQueryCamera = envParameters.GetWithDefault("minDegreeQueryCamera", 0f); // not sent from unity, not useful
        float maxDegreeQueryCamera = envParameters.GetWithDefault("maxDegreeQueryCamera", 180f);



        selectedObjs.Clear();
        cloneObjs.Clear();
        standardPositionTestingCameras.Clear();
        standardPositionSupportCameras.Clear();

        GameObject episodeContainer = new GameObject("Episode Container");
        // Draw K classes from the list
        List<int> listNumbers = new List<int>();
        int number;
        for (int k = 0; k < K; k++)
        {
            do
            {
                number = Random.Range(0, datasetList.Count);
            } while (listNumbers.Contains(number));
            listNumbers.Add(number);
            selectedObjs.Add(datasetList[number]);
        };

        // Place the selected objects on their position
        for (int k = 0; k < K; k++)
        {
            cloneObjs.Add(Instantiate(selectedObjs[k], positions[k], Quaternion.identity));
            cloneObjs[k].transform.parent = episodeContainer.transform;
            cloneObjs[k].name = selectedObjs[k].name;
            // Unity generates 32 layers. Layers from 8 and above are unused.
            SetLayerRecursively(cloneObjs[k], 8 + k);
            // Move the object inside, down
                    }

        // Place the query Cameras somewhere facing the object, looking at it
        int queryIndex = 0;
        for (int k = 0; k < K; k++)
        {
            var objToLookAt = cloneObjs[k].transform.GetChild(0);
            objToLookAt.transform.Translate(new Vector3(0f, -2 / 3f * distance, 0f));

            var objPos = cloneObjs[k].transform.position;
            // Thi should be -0.5F, 0.5F or 0F, 1F
            var directionCameraQuery = Random.Range(0F, 1F);
            // This should be 0F, 180F, 0F, 1F but change it to 0F, 0F for testing. 
            standardQueryCamera[k].transform.position = objPos + GetRandomAroundSphere(minDegreeQueryCamera, maxDegreeQueryCamera, directionCameraQuery, Vector3.up) * distance;
            standardQueryCamera[k].transform.LookAt(objToLookAt.transform);
            //standardPositionTestingCameras.Add();

            var sQcPos = standardQueryCamera[k].transform.position;
            for (int q = 0; q < Q; q++)
            {
                queryCameras[queryIndex].transform.position = objPos + 
                    (GetRandomAroundSphere(0, scatterCameraDegrees, directionCameraQuery + Random.Range(-scatterCameraDirection / 2, scatterCameraDirection / 2), Vector3.Normalize(sQcPos - objPos))) * distance;


                queryCameras[queryIndex].transform.LookAt(objToLookAt, standardQueryCamera[k].transform.up);
                queryCameras[queryIndex].GetComponent<Camera>().cullingMask = 1 << (8 + k);
                labelsTesting.Add(mapNameToNum[cloneObjs[k].name]);
                debugPointsQuery.Add(queryCameras[queryIndex].transform.position);

                queryIndex += 1;
            }

            var directionCameraSupport = Mathf.Min(directionCameraQuery + Random.Range(minDirectionSQcameras, maxDirectionSQcameras), 1);
            standardSupportCamera[k].transform.position = objPos + GetRandomAroundSphere(minDegreeSQcameras, maxDegreeSQcameras, directionCameraSupport, Vector3.Normalize(sQcPos - objPos)) * distance;
            standardSupportCamera[k].transform.LookAt(objToLookAt.transform);
            var sScPos = standardSupportCamera[k].transform.position; 

            // Move the support cameras
            for (int n = 0; n < N; n++)
            {
                // Scatter them around the standardPositionSupportCamera point
                supportCameras[n + N * k].transform.position = objPos +  
                    (GetRandomAroundSphere(0, scatterCameraDegrees, directionCameraSupport + Random.Range(-scatterCameraDirection/2, scatterCameraDirection/2), Vector3.Normalize(sScPos - objPos)) * distance);
                                
                supportCameras[n + N * k].transform.LookAt(objToLookAt.transform, standardQueryCamera[k].transform.up);
                supportCameras[n + N * k].GetComponent<Camera>().cullingMask = 1 << (8 + k);
                labelsSupport.Add(mapNameToNum[cloneObjs[k].name]);
                debugPointsSupport.Add(supportCameras[n + N * k].transform.position);
            }
        }
        this.RequestDecision();
    }
    IEnumerator waiter()
    {

        //Wait for 4 seconds
        yield return new WaitForSeconds(4);
    }
  
    public override void Heuristic(float[] actionsOut)
    {
        if (Input.GetKeyDown("space"))
        {
            UnityEngine.Debug.Log("SPACE");
            foreach (GameObject cln in cloneObjs)
            {
                Destroy(cln);
            }
            labelsSupport.Clear();
            labelsTesting.Clear();
            GameObject episodeContainer = GameObject.Find("Episode Container");
            Destroy(episodeContainer);
            OnEpisodeBegin();

        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        var envParameters = Academy.Instance.EnvironmentParameters;

        List<int> labelsAll = new List<int>();

        // Another way of doing that is to use channels, but for now this is fine.

        for (int i = 0; i <  labelsSupport.Count ;  i++)
        {
            sensor.AddObservation(labelsSupport[i]);
        }

        for (int i = 0; i <  labelsTesting.Count; i++)
        {
            sensor.AddObservation(labelsTesting[i]);
        }
    }


    public override void OnActionReceived(float[] vectorAction)
    {
        foreach (GameObject cln in cloneObjs)
        {
            Destroy(cln);
        }
        labelsSupport.Clear();
        labelsTesting.Clear();
        GameObject episodeContainer = GameObject.Find("Episode Container");
        Destroy(episodeContainer);
        OnEpisodeBegin();
    }

}

#if UNITY_EDITOR

//[System.Serializable]
[ExecuteInEditMode]
[CustomEditor(typeof(MetaLearningTask))]
//[CanEditMultipleObjects]
public class MLtaskEditor : Editor
{

    int N;
    int K;
    int Q;
    int sizeCanvas;
    string nameDataset;
    public Object source;
    void OnEnable()
    {
        var mt = (MetaLearningTask)target;
        if (mt.runEnable)
        {
            mt.agent = GameObject.Find("Agent");
            UnityEngine.Debug.Log("HI");
            //BuildSceneCLI.UpdateComponents(mt.N, mt.K, mt.Q, mt.sizeCanvas);
            mt.runEnable = false;           
        }

    }



    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying)
        {
            base.OnInspectorGUI();
            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            //serializedObject.Update();
            var mt = (MetaLearningTask)target;
            GameObject info = GameObject.Find("Info");
            string infostr = info.transform.GetChild(0).name;
            var tmp = infostr.Split('_');
            N = int.Parse(tmp[0].Split(':')[1]);
            K = int.Parse(tmp[1].Split(':')[1]);
            Q = int.Parse(tmp[2].Split(':')[1]);
            sizeCanvas = int.Parse(tmp[3].Split(':')[1]);
            source = GameObject.Find(tmp[4].Split(':')[1]);



            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Size Canvas"), GUILayout.Width(80));
            int currentSC = EditorGUILayout.IntSlider(sizeCanvas, 10, 250);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("N"), GUILayout.Width(20));
            int currentN = EditorGUILayout.IntSlider(N, 1, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("K"), GUILayout.Width(20));
            int currentK = EditorGUILayout.IntSlider(K, 1, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Q"), GUILayout.Width(20));
            int currentQ = EditorGUILayout.IntSlider(Q, 1, 30);
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Dataset Obj."), GUILayout.Width(70));
            source = EditorGUILayout.ObjectField(source, typeof(Object), true);
            EditorGUILayout.EndHorizontal();

            string currentDS = null;
            if (source != null)
            {
                currentDS = source.name;
                //UnityEngine.Debug.Log(currentDS);
            }
            if (GUI.changed)
            {
                if (currentN != N || currentQ != Q || currentK != K || currentSC != sizeCanvas || currentDS != nameDataset)
                {
                    BuildSceneCLI.N = currentN;
                    BuildSceneCLI.K = currentK;
                    BuildSceneCLI.Q = currentQ;
                    BuildSceneCLI.sizeCanvas = currentSC;
                    if (currentDS != null)
                    {
                        BuildSceneCLI.nameDataset = currentDS;
                    }
                    BuildSceneCLI.UpdateComponents();

                }

            }
        }
    }
}
#endif
