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
using UnityEditor;
using Frame = UnityEngine.GameObject;
using UnityEngine.Assertions;
using Unity.MLAgents.SideChannels;


public class SequenceLearningTask_test : Agent
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
    List<int> labelsCandidates = new List<int>();
    List<int> labelsSelectedObjects = new List<int>();

    //List<Vector3> debugMiddlePointsTraining = new List<Vector3>();
    //List<Vector3> debugMiddlePointsCandidates = new List<Vector3>();


    [SerializeField]
    public bool gizmoCameraHistory = true;
    [SerializeField]
    public bool gizmoCameraMidpoint = true;

    [SerializeField]
    public bool gizmoCamHistoryRelative = true;

    [HideInInspector]
    int numSt = 2;
    [HideInInspector]
    int numSc = 1;
    [HideInInspector]
    int numFt = 4;
    [HideInInspector]
    int numFc = 1;
    [HideInInspector]
    float distance = 3.5f;

    int K = 1; // Number of replication of the matching task
    int sizeCanvas = 64;
    string nameDataset = "none";
    float test_type = 0f;

    public class Sequence
    {
        public List<Frame> frames = new List<Frame>();
    }

    public class perceivableObject
    {
        public List<Sequence> sequences = new List<Sequence>();
    }

    List<perceivableObject> candidates = new List<perceivableObject>();
    List<perceivableObject> training = new List<perceivableObject>();

    GameObject info;
    List<Vector3> debugPointsCandidates = new List<Vector3>();
    List<Vector3> debugPointsTraining = new List<Vector3>();
    //List<List<Vector3>> debugPointHistoryCenterRelativeCT = new List<List<Vector3>>();

    //List<Vector3> debugRotation = new List<Vector3>();

    List<Vector3> candidateCameraPosRelativeToObj = new List<Vector3>();
    List<Vector3> trainingCameraPosRelativeToObj = new List<Vector3>();

    public int numEpisodes = 0;

    EnvironmentParameters envParameters;

    public void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }


    StringLogSideChannel sendEnvParamsChannel;
    StringLogSideChannel sendDebugLogChannel;

    public void Awake()
    {
        // We create the Side Channel
        sendEnvParamsChannel = new StringLogSideChannel("621f0a70-4f87-11ea-a6bf-784f4387d1f7");
        sendDebugLogChannel = new StringLogSideChannel("8e8d2cbd-ea04-444d-9180-56ed79a2b94e");

        // The channel must be registered with the SideChannelManager class
        SideChannelManager.RegisterSideChannel(sendEnvParamsChannel);
        SideChannelManager.RegisterSideChannel(sendDebugLogChannel);


    }

    public void OnDestroy()
    {
        // De-register the Debug.Log callback
        if (Academy.IsInitialized)
        {
            SideChannelManager.UnregisterSideChannel(sendEnvParamsChannel);
            SideChannelManager.UnregisterSideChannel(sendDebugLogChannel);

        }
    }

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
        //Assert.IsTrue(datasetList.Count >= K, "The elements in the datasetList are less than K!");
    }


    private static string GetArg(string name)
    {
        //UnityEngine.Debug.Log("I AM HERE READING YOUR COMMAND LINE");

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


        K = int.Parse(tmp[0].Split(':')[1]);
        numSt = int.Parse(tmp[1].Split(':')[1]);
        numSc = int.Parse(tmp[2].Split(':')[1]);
        numFt = int.Parse(tmp[3].Split(':')[1]);
        numFc = int.Parse(tmp[4].Split(':')[1]);
        sizeCanvas = int.Parse(tmp[5].Split(':')[1]);

        string str = "SLT test Mode - From Unity Info: \nK: " + K.ToString() + " St: " + numSt.ToString() + " Sc: " + numSc.ToString() + " Ft: " + numFt.ToString() + " Fc: " + numFc.ToString() + " size_canvas: " + sizeCanvas.ToString();
        sendDebugLogChannel.SendEnvInfoToPython(str);
        UnityEngine.Debug.Log(str);
        int totIndex = 0;
        string tmpName;
        int kk = 0;
        //debugPointHistoryCenterRelativeCT.Add(new List<Vector3>());
        training.Add(new perceivableObject());
        for (int sq = 0; sq < numSt; sq++)
        {
            training[kk].sequences.Add(new Sequence());

            for (int fsq = 0; fsq < numFt; fsq++)
            {
                tmpName = "Q" + kk + "S" + sq + "F" + fsq;
                training[kk].sequences[sq].frames.Add(cameraContainer.transform.Find(tmpName).gameObject);
                trainingCameraPosRelativeToObj.Add(new Vector3(0f, 0f, 0f));

                totIndex += 1;
            }
        }


        totIndex = 0;
       
        candidates.Add(new perceivableObject());
        for (int sc = 0; sc < numSc; sc++)
        {
            candidates[0].sequences.Add(new Sequence());

            for (int fsc = 0; fsc < numFc; fsc++)
            {
                tmpName = "C0" + "S" + sc + "F" + fsc;
                candidates[0].sequences[sc].frames.Add(cameraContainer.transform.Find(tmpName).gameObject);
                candidateCameraPosRelativeToObj.Add(new Vector3(0f, 0f, 0f));
                totIndex += 1;
            }
        }
       


        UnityEngine.Debug.Log("ATTENZIONE");
        var cmlNameDataset = GetArg("-name_dataset");
        if (!(cmlNameDataset == null))
        {
            nameDataset = cmlNameDataset;
            UnityEngine.Debug.Log("New Dataset from command line: " + nameDataset);
        }
        else
        {
            nameDataset = tmp[6].Split(':')[1];
        }


        FillDataset();
        sendEnvParamsChannel.SendEnvInfoToPython((datasetList.Count).ToString());

        // TODO: This only works for K = 1 for now
        // Create Positions
        for (int k = 0; k < 2; k++)
        {
            positions.Add(new Vector3(10 * k, 0, 0));
        }
        //Random.InitState((int)System.DateTime.Now.Ticks); // DELETE
        Random.InitState(2);
        envParameters = Academy.Instance.EnvironmentParameters;
        test_type = envParameters.GetWithDefault("test_type", 0f);
        sendDebugLogChannel.SendEnvInfoToPython("TEST TYPE: " + test_type.ToString());

    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            int index = 0;
            Gizmos.color = new Vector4(0, 0, 1F, 0.5F);
            foreach (perceivableObject q in training)
            {
                foreach (Sequence s in q.sequences)
                {
                    foreach (Frame f in s.frames)
                    {
                        Gizmos.DrawSphere(f.transform.position, 0.5F);
                        Gizmos.DrawWireSphere(f.transform.position, 0.5F);

                    }
                }

            }
            Gizmos.color = new Vector4(0, 1F, 0F, 0.5F);
            foreach (perceivableObject c in candidates)
            {
                foreach (Sequence s in c.sequences)
                {
                    foreach (Frame f in s.frames)
                    {
                        Gizmos.DrawSphere(f.transform.position, 0.5F);
                        Gizmos.DrawWireSphere(f.transform.position, 0.5F);

                    }
                }

            }
            //Gizmos.color = new Vector4(1, 0F, 0F, 0.5F);

            //foreach (Vector3 vec in debugRotation)
            //{
            //    Gizmos.DrawLine(cloneObjs[0].transform.position, (2 * distance * vec) + cloneObjs[0].transform.position);
            //}

            for (int k = 0; k < 2; k++)
            {
                Gizmos.color = new Vector4(153 / 255F, 51 / 255F, 1F, 0.2F);
                Gizmos.DrawWireSphere(cloneObjs[k].transform.position, distance);
                Gizmos.DrawSphere(cloneObjs[k].transform.position, distance);
            }

            //if (gizmoCameraHistory)
            //{

            //    index = 0;
            //    foreach (Vector3 vec in debugPointsCandidates)
            //    {

            //        Gizmos.color = new Vector4(0F, 1F, 0F, 0.5F);
            //        Gizmos.DrawSphere(vec, 0.1F);
            //        Gizmos.DrawWireSphere(vec, 0.1F);
            //        //Gizmos.DrawLine(positions[(int)(index / numSc) % C], vec);
            //        index += 1;
            //    }
            //    index = 0;
            //    foreach (Vector3 vec in debugPointsTraining)
            //    {

            //        Gizmos.color = new Vector4(0F, 0F, 1F, 0.5F);
            //        Gizmos.DrawSphere(vec, 0.1F);
            //        Gizmos.DrawWireSphere(vec, 0.1F);
            //        //Gizmos.DrawLine(positions[(int)(index / Q) % C], vec);
            //        index += 1;
            //    }


            //}
            //if (gizmoCameraMidpoint)
            //{
            //    for (int k = 0; k < K; k++)
            //    {
            //        Gizmos.color = new Vector4(0F, 1F, 0F, 0.5F);
            //        Gizmos.DrawSphere(debugMiddlePointsCandidates[0] + cloneObjs[k].transform.position, 0.2F);
            //        Gizmos.color = new Vector4(0F, 0F, 1F, 0.5F);

            //        Gizmos.DrawSphere(debugMiddlePointsTraining[0] + cloneObjs[k].transform.position, 0.3F);
            //    }
            //}
            //if (gizmoCamHistoryRelative)
            //{
            //    for (int k = 0; k < K; k++)
            //    {
            //        foreach (Vector3 vec in debugPointHistoryCenterRelativeCT[k])
            //        {
            //            Gizmos.color = new Vector4(1F, 0F, 0f, 1f);
            //            Gizmos.DrawSphere(vec, 0.1F);
            //            Gizmos.DrawLine(cloneObjs[k].transform.position, Vector3.forward * simParams.distance + cloneObjs[k].transform.position);

            //            Gizmos.DrawWireSphere(vec, 0.1F);

            //        }
            //        Gizmos.color = new Vector4(1F, 1F, 0f, 1f);
            //        Gizmos.DrawLine(cloneObjs[k].transform.position, debugPointHistoryCenterRelativeCT[k][debugPointHistoryCenterRelativeCT[k].Count - 1]);
            //    }
            //}
            //index = 0;
            //foreach (Vector3 vec in supportRelativePosition)
            //{
            //    Gizmos.color = new Vector4(1F, 0F, 0f, 1f);
            //    Gizmos.DrawSphere(vec, 0.5F);
            //    Gizmos.DrawWireSphere(vec, 0.5F);
            //    Gizmos.DrawLine(positions[(int)(index / numSc) % C], positions[(int)(index / numSc) % C] + distance * Vector3.forward);
            //    Gizmos.DrawLine(positions[(int)(index / numSc) % C], vec);
            //    index += 1;
            //}
        }
    }
#endif

    Vector3 GetPositionAroundSphere(float angle, float direction, Vector3 aroundPosition)
    {

        // direction in DEGREES

        float azimuth = direction * Mathf.Deg2Rad; // * 2.0F * UnityEngine.Mathf.PI;
        float cosDistFromZenith = Mathf.Cos(angle * Mathf.Deg2Rad); //Random.Range(Mathf.Min(a, b), Mathf.Max(a, b));
        float sinDistFromZenith = Mathf.Sqrt(1.0F - cosDistFromZenith * cosDistFromZenith);
        Vector3 pqr = new Vector3(Mathf.Cos(azimuth) * sinDistFromZenith, UnityEngine.Mathf.Sin(azimuth) * sinDistFromZenith, cosDistFromZenith);
        Vector3 rAxis = aroundPosition; // Vector3.up when around zenith
        Vector3 pAxis = Mathf.Abs(rAxis[0]) < 0.9 ? new Vector3(1F, 0F, 0F) : new Vector3(0F, 1F, 0F);
        Vector3 qAxis = Vector3.Normalize(Vector3.Cross(rAxis, pAxis));
        pAxis = Vector3.Cross(qAxis, rAxis);
        Vector3 position = pqr[0] * pAxis + pqr[1] * qAxis + pqr[2] * rAxis;
        return position;
    }

    float RandomAngle(float angleA, float angleB, float minRange, float maxRange)
    {
        var boundedA = Mathf.Min(Mathf.Max(angleA, minRange), maxRange);
        var boundedB = Mathf.Min(Mathf.Max(angleB, minRange), maxRange);

        var min = Mathf.Min(boundedA, boundedB);
        var max = Mathf.Max(boundedA, boundedB);
        return Random.Range(min, max);
        //return Mathf.Cos(Random.Range(min, max) * Mathf.Deg2Rad);
    }

    //public Transform Target;
    public CameraSensor VisualObs;

    public override void OnEpisodeBegin()
    {

        distance = envParameters.GetWithDefault("distance", 3.5f);

        int objTindex = (int)envParameters.GetWithDefault("objT", 0f);
        int objCindex = (int)envParameters.GetWithDefault("objC", 2f);
        int degree = (int)envParameters.GetWithDefault("degree", 45f);
        int rotation = (int)envParameters.GetWithDefault("rotation", 45f);




        // This default is used ONLY IF THE "newLevel" IS NEVER RECEIVED! Otherwise the default is the PREVIOUS one!!!



        //Assert.IsTrue(minDegreeQueryCamera + maxDegreeSQcameras > minDegreeCandidatesCamera && maxDegreeQueriesCamera + minDegreeSQcameras < maxDegreeCandidatesCamera);
        //if (!(minDegreeQueryCamera + maxDegreeSQcameras > minDegreeCandidatesCamera && maxDegreeQueriesCamera + minDegreeSQcameras < maxDegreeCandidatesCamera))
        //{
        //    UnityEngine.Debug.Log("CAMERAS ARE NOT PLACED PROPERLY.");
        //    Application.Quit(1);

        //}


        selectedObjs.Clear();
        cloneObjs.Clear();
        //debugRotation.Clear();

        //if (numEpisodes % 300 == 0)
        //{
        //    debugPointsTraining.Clear();
        //    debugPointsCandidates.Clear();
        //    for (int kk = 0; kk < K; kk++)
        //    {
        //        debugPointHistoryCenterRelativeCT[kk].Clear();
        //    }
        //}

        //debugMiddlePointsCandidates.Clear();
        //debugMiddlePointsTraining.Clear();
        GameObject episodeContainer = new GameObject("Episode Container");
        // Draw K classes from the list
        List<int> listNumbers = new List<int>();
        selectedObjs.Add(datasetList[objTindex]);
        selectedObjs.Add(datasetList[objCindex]);

        // Place the selected objects on their position
        for (int kk = 0; kk < 2; kk++)
        {
            cloneObjs.Add(Instantiate(selectedObjs[kk], positions[kk], Quaternion.identity));
            cloneObjs[kk].transform.parent = episodeContainer.transform;
            cloneObjs[kk].name = selectedObjs[kk].name;
            // Unity generates 32 layers. Layers from 8 and above are unused.
            SetLayerRecursively(cloneObjs[kk], 8 + kk);
            labelsSelectedObjects.Add(mapNameToNum[cloneObjs[kk].name]);
            // Move the object inside, down
        }

        var objPos = cloneObjs[0].transform.position;
        ///////// TRAINING CAMERAS
        List<Vector3> positionsTcameras = new List<Vector3>();

        int k = 0;
        var objToLookAt = cloneObjs[0].transform.GetChild(0);
        int delta = 10;
        int indexTraining = 0;
        for (int st = 0; st < numSt; st++)
        {
            if (st == 0)
            {
                positionsTcameras.Clear();
                positionsTcameras.Add(GetPositionAroundSphere(90, rotation - 15, Vector3.up) * distance);
                positionsTcameras.Add(GetPositionAroundSphere(90, rotation + 0, Vector3.up) * distance);
                positionsTcameras.Add(GetPositionAroundSphere(90, rotation + 15, Vector3.up) * distance);
                                                                 
            }                                                    
            if (st == 1)                                         
            {                                                    
                positionsTcameras.Clear();                       
                positionsTcameras.Add(GetPositionAroundSphere(90, rotation - 60, Vector3.up) * distance);
                positionsTcameras.Add(GetPositionAroundSphere(90, rotation -75, Vector3.up) * distance);
                positionsTcameras.Add(GetPositionAroundSphere(90, rotation -90, Vector3.up) * distance);

            }
            for (int fst = 0; fst < numFt; fst++)
            {
                training[k].sequences[st].frames[fst].transform.position = objPos + positionsTcameras[fst];
                training[k].sequences[st].frames[fst].transform.LookAt(objToLookAt);
                training[k].sequences[st].frames[fst].GetComponent<Camera>().cullingMask = 1 << (8 + k);
                debugPointsTraining.Add(training[k].sequences[st].frames[fst].transform.position);
                trainingCameraPosRelativeToObj[indexTraining] = positionsTcameras[fst];
                indexTraining += 1;
            }
        }

        k = 0;
        int sc = 0;
        int fsc = 0;
        var degreesC = GetPositionAroundSphere(90, rotation + degree, Vector3.up) * distance;
        objPos = cloneObjs[1].transform.position;
        objToLookAt = cloneObjs[1].transform.GetChild(0);

        candidates[0].sequences[sc].frames[fsc].transform.position = objPos + Quaternion.AngleAxis(delta * (fsc - numFc / 2), Vector3.up) * degreesC;
        candidates[0].sequences[sc].frames[fsc].transform.LookAt(objToLookAt, Vector3.up);
        candidates[0].sequences[sc].frames[fsc].GetComponent<Camera>().cullingMask = 1 << (8 + 1);
        debugPointsCandidates.Add(candidates[0].sequences[sc].frames[fsc].transform.position);
        candidateCameraPosRelativeToObj[0] = candidates[0].sequences[sc].frames[fsc].transform.position - objPos;



        numEpisodes += 1;
        //UnityEngine.Debug.Break();
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
            labelsCandidates.Clear();
            labelsSelectedObjects.Clear();
            GameObject episodeContainer = GameObject.Find("Episode Container");
            Destroy(episodeContainer);
            OnEpisodeBegin();

        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        // Remember all observations, including Visual ones, are passed in alphabetical orders.
        // This observations corresponds to the order for "Vector Observation", so you camera name should
        // come before "V".

        //var envParameters = Academy.Instance.EnvironmentParameters;

        //List<int> labelsAll = new List<int>();

        //// Another way of doing that is to use channels, but for now this is fine.

        for (int i = 0; i < labelsSelectedObjects.Count; i++)
        {
            sensor.AddObservation(labelsSelectedObjects[i]);
        }

        //for (int i = 0; i < labelsSelectedObjects.Count; i++)
        //{
        //    sensor.AddObservation(labelsSelectedObjects[i]);
        //}

        //Support Camera Position, X, Y and Z
        foreach (Vector3 pos in candidateCameraPosRelativeToObj)
        {
            sensor.AddObservation(pos);

        }

        // Training Camera Position, X, Y and Z
        foreach (Vector3 pos in trainingCameraPosRelativeToObj)
        {

            sensor.AddObservation(pos);
        }
    }


    public override void OnActionReceived(float[] vectorAction)
    {
        foreach (GameObject cln in cloneObjs)
        {
            Destroy(cln);
        }
        labelsCandidates.Clear();
        labelsSelectedObjects.Clear();
        GameObject episodeContainer = GameObject.Find("Episode Container");
        Destroy(episodeContainer);
        OnEpisodeBegin();
    }

}

#if UNITY_EDITOR

//[System.Serializable]
[ExecuteInEditMode]
[CustomEditor(typeof(SequenceLearningTask_test))]
//[CanEditMultipleObjects]
public class SequenceMLtaskEditor_test : Editor
{
    [HideInInspector]
    int K = 1;
    [HideInInspector]
    int numSt = 2;
    [HideInInspector]
    int numSc = 1;
    [HideInInspector]
    int numFt = 3;
    [HideInInspector]
    int numFc = 1;
    int sizeCanvas;
    string nameDataset;
    public Object source;
    void OnEnable()
    {
        var mt = (SequenceLearningTask_test)target;
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
            var mt = (SequenceLearningTask_test)target;
            GameObject info = GameObject.Find("Info");
            string infostr = info.transform.GetChild(0).name;
            var tmp = infostr.Split('_');
            K = int.Parse(tmp[0].Split(':')[1]);
            numSt = int.Parse(tmp[1].Split(':')[1]);
            numSc = int.Parse(tmp[2].Split(':')[1]);
            numFt = int.Parse(tmp[3].Split(':')[1]);
            numFc = int.Parse(tmp[4].Split(':')[1]);
            sizeCanvas = int.Parse(tmp[5].Split(':')[1]);
            source = GameObject.Find(tmp[6].Split(':')[1]);



            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Size Canvas"), GUILayout.Width(80));
            int currentSC = EditorGUILayout.IntSlider(sizeCanvas, 10, 250);
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("K"), GUILayout.Width(20));
            int currentK = EditorGUILayout.IntSlider(K, 1, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("numSt"), GUILayout.Width(60));
            int currentSt = EditorGUILayout.IntSlider(numSt, 1, 5);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("numSc"), GUILayout.Width(60));
            int currentSc = EditorGUILayout.IntSlider(numSc, 1, 5);
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("numFt"), GUILayout.Width(60));
            int currentFt = EditorGUILayout.IntSlider(numFt, 1, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("numFc"), GUILayout.Width(60));
            int currentFc = EditorGUILayout.IntSlider(numFc, 1, 10);
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
                if (currentSc != numSc || currentSt != numSt || currentK != K ||
                    currentFc != numFc || currentFt != numFt || currentSC != sizeCanvas || currentDS != nameDataset)
                {
                    SequenceBuildSceneCLI_test.numSt = currentSt;
                    SequenceBuildSceneCLI_test.numFc = currentFc;
                    SequenceBuildSceneCLI_test.numFt = currentFt;
                    SequenceBuildSceneCLI_test.numSc = currentSc;
                    SequenceBuildSceneCLI_test.K = currentK;
                    SequenceBuildSceneCLI_test.sizeCanvas = currentSC;

                    if (currentDS != null)
                    {
                        SequenceBuildSceneCLI_test.nameDataset = currentDS;
                    }
                    SequenceBuildSceneCLI_test.UpdateComponents();

                }

            }
        }
    }
}
#endif
