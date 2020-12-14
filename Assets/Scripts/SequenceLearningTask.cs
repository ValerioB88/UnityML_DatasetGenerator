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


public class SequenceLearningTask : Agent
{

    [HideInInspector]
    public bool runEnable = true;


    List<GameObject> datasetList = new List<GameObject>();
    //List<GameObject> selectedObjs = new List<GameObject>();
    List<GameObject> trainingObj = new List<GameObject>();
    List<GameObject> candidateObjs = new List<GameObject>();

    [HideInInspector]
    public GameObject cameraContainer;

    [HideInInspector]
    public GameObject agent;
    Dictionary<string, int> mapNameToNum = new Dictionary<string, int>();

    List<Vector3> positions = new List<Vector3>();
    List<int> labelsCandidates = new List<int>();
    List<int> labelsSelectedObjects = new List<int>();

    List<Vector3> debugMiddlePointsAreaT = new List<Vector3>();
    List<Vector3> debugMiddlePointsAreaC = new List<Vector3>();


    [SerializeField]
    public bool gizmoCameraHistory = true;
    [SerializeField]
    public bool gizmoCameraMidpoint = true;

    [SerializeField]
    public bool gizmoCamHistoryRelative = true;


    int numSt = 1;
    int numSc = 1;
    int numFt = 4;
    int numFc = 1;
    int K = 2;
    int Q = 1; // always 1
    int sizeCanvas = 64;
    string nameDataset = "none";
    float newLevel = 0f; 

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
    List<Vector3> debugMiddlePointsSequenceC = new List<Vector3>();
    List<Vector3> debugMiddlePointsSequenceT = new List<Vector3>();
    List<List<Vector3>> debugPointHistoryCenterRelativeCT = new List<List<Vector3>>();

    //List<Vector3> debugRotation = new List<Vector3>();

    //List<Vector3> candidateCameraPosRelativeToObj = new List<Vector3>();
    //List<Vector3> trainingCameraPosRelativeToObj = new List<Vector3>();
    List<Vector3> cameraPositions = new List<Vector3>();
    public int numEpisodes = 0;
    public SimulationParameters simParams = new SimulationParameters(); 


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
        //for (int i = children - 1; i >= 0; i--)
        //{
        //    var obj = dataset.transform.GetChild(i);
        //    GameObject pp = new GameObject("p_" + obj.name);
        //    pp.transform.position = obj.transform.position;
        //    pp.transform.rotation = obj.transform.rotation;
        //    pp.transform.parent = dataset.transform;
        //    obj.parent = pp.transform;

        //}
        for (int i = 0; i < children; i++)
        {
            var obj = dataset.transform.GetChild(i);

            //UnityEngine.Debug.Log(dataset.transform.GetChild(i).gameObject.name);
            datasetList.Add(obj.gameObject);
            datasetList[i].transform.position = new Vector3(10 * i, 0, 0);
            mapNameToNum.Add(datasetList[i].name, i);
            SetLayerRecursively(datasetList[i], 8 + i);
        }
        //Assert.IsTrue(datasetList.Count >= K, "The elements in the datasetList are less than K!");
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


        K = int.Parse(tmp[0].Split(':')[1]);
        numSt = int.Parse(tmp[1].Split(':')[1]);
        numSc = int.Parse(tmp[2].Split(':')[1]);
        numFt = int.Parse(tmp[3].Split(':')[1]);
        numFc = int.Parse(tmp[4].Split(':')[1]);
        sizeCanvas = int.Parse(tmp[5].Split(':')[1]);

        sendDebugLogChannel.SendEnvInfoToPython("SLT From Unity Info: \nK: " + K.ToString() + " St: " + numSt.ToString() + " Sc: " + numSc.ToString() + " Ft: " + numFt.ToString() + " Fc: " + numFc.ToString() + " size_canvas: " + sizeCanvas.ToString());
        int totIndex = 0;
        string tmpName;
        for (int k = 0; k < K; k++)
        {
            debugPointHistoryCenterRelativeCT.Add(new List<Vector3>());
            training.Add(new perceivableObject());
            for (int sq = 0; sq < numSt; sq++)
            {
                training[k].sequences.Add(new Sequence());

                for (int fsq = 0; fsq < numFt; fsq++)
                {
                    tmpName = "T" + k.ToString("D2") + "S" + sq + "F" + fsq;
                    training[k].sequences[sq].frames.Add(cameraContainer.transform.Find(tmpName).gameObject);
                    //trainingCameraPosRelativeToObj.Add(new Vector3(0f, 0f, 0f));
                    //cameraPositions.Add(new Vector3(0f, 0f, 0f));

                    totIndex += 1;
                }
            }
        }

        totIndex = 0;
        for (int k = 0; k < K; k++)
        {
            candidates.Add(new perceivableObject());
            for (int sc = 0; sc < numSc; sc++)
            {
                candidates[k].sequences.Add(new Sequence());

                for (int fsc = 0; fsc < numFc; fsc++)
                {
                    tmpName = "C" + k.ToString("D2") + "S" + sc + "F" + fsc;
                    candidates[k].sequences[sc].frames.Add(cameraContainer.transform.Find(tmpName).gameObject);
                    //candidateCameraPosRelativeToObj.Add(new Vector3(0f, 0f, 0f));
                    //cameraPositions.Add(new Vector3(0f, 0f, 0f));

                    totIndex += 1;
                }
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

        //// Create Positions
        //for (int k = 0; k < K; k++)
        //{
        //    positions.Add(new Vector3(10 * k, 0, 0));
        //}
        //Random.InitState((int)System.DateTime.Now.Ticks); // DELETE
        Random.InitState(2);

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
            Gizmos.color = new Vector4(1, 0F, 0F, 0.5F);

            //foreach (Vector3 vec in debugRotation)
            //{
            //    Gizmos.DrawLine(cloneObjs[0].transform.position, (2 * simParams.distance * vec) + cloneObjs[0].transform.position);
            //}

            for (int k = 0; k < K; k++)
            {
                Gizmos.color = new Vector4(153 / 255F, 51 / 255F, 1F, 0.2F);
                Gizmos.DrawWireSphere(trainingObj[k].transform.position, simParams.distance);
                Gizmos.DrawSphere(trainingObj[k].transform.position, simParams.distance);
                Gizmos.DrawWireSphere(candidateObjs[k].transform.position, simParams.distance);
                Gizmos.DrawSphere(candidateObjs[k].transform.position, simParams.distance);
            }

            if (gizmoCameraHistory)
            {

                index = 0;
                foreach (Vector3 vec in debugMiddlePointsSequenceC)
                {

                    Gizmos.color = new Vector4(0F, 1F, 0F, 0.5F);
                    Gizmos.DrawSphere(vec, 0.1F);
                    Gizmos.DrawWireSphere(vec, 0.1F);
                    //Gizmos.DrawLine(positions[(int)(index / numSc) % C], vec);
                    index += 1;
                }
                index = 0;
                foreach (Vector3 vec in debugMiddlePointsSequenceT)
                {

                    Gizmos.color = new Vector4(0F, 0F, 1F, 0.5F);
                    Gizmos.DrawSphere(vec, 0.1F);
                    Gizmos.DrawWireSphere(vec, 0.1F);
                    //Gizmos.DrawLine(positions[(int)(index / Q) % C], vec);
                    index += 1;
                }


            }
            if (gizmoCameraMidpoint)
            {
                for (int k = 0; k < K; k++)
                {
                    Gizmos.color = new Vector4(0F, 1F, 0F, 0.5F);
                    Gizmos.DrawSphere(debugMiddlePointsAreaC[k], 0.2F);
                    Gizmos.color = new Vector4(0F, 0F, 1F, 0.5F);

                    Gizmos.DrawSphere(debugMiddlePointsAreaT[k],  0.3F);
                }
            }
            if (gizmoCamHistoryRelative)
            {
                for (int k = 0; k < K; k++)
                {
                    foreach (Vector3 vec in debugPointHistoryCenterRelativeCT[k])
                    {
                        Gizmos.color = new Vector4(1F, 0F, 0f, 1f);
                        Gizmos.DrawSphere(vec, 0.1F);
                        Gizmos.DrawLine(trainingObj[k].transform.position, Vector3.forward * simParams.distance + trainingObj[k].transform.position);

                        Gizmos.DrawWireSphere(vec, 0.1F);

                    }
                    Gizmos.color = new Vector4(1F, 1F, 0f, 1f);
                    Gizmos.DrawLine(trainingObj[k].transform.position, debugPointHistoryCenterRelativeCT[k][debugPointHistoryCenterRelativeCT[k].Count - 1]);
                }
            }
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

    public class SimulationParameters
    {
        public float distance = 3.5f;
        // TRAINING
        // THIS GOES FROM 0 to 180 (cover the whole sphere) (relative to the world)
        public float minCenterPointInclTcameras = 45; 
        public float maxCenterPointInclTcameras = 120;

        // This is almost always 0-360
        public float minCenterPointAziTcameras = 0; 
        public float maxCenterPointAziTcameras = 360;

        // CANDIDATE 
        // This is almost always 0-360
        public float minCenterPointInclCcameras = 45; 
        public float maxCenterPointInclCcameras = 120;

        public float minCenterPointAziCcameras = 0; 
        public float maxCenterPointAziCcameras = 360;

        // from 0 to 180 / 360, it's degree and direction of the area around the center point for training cameras.
        public float areaInclTcameras = 45;
        public float areaAziTcameras = 45;

        public float areaInclCcameras = 45; 
        public float areaAziCcameras = 45;

        // DISTANCE
        // This goes from -180 to +180 (where 0 is the position of the first training camera)
        // this + areaDegC is the maximum distance between the 2 centers
        public float minInclTCcameras = 0; public float maxInclTCcameras = 0;
        public float minAziTCcameras = 0; public float maxAziTCcameras = 0;

        // THIS SHOULD BE POSITIVE! Distance across frames
        public float minDegreeFrames = 14; public float maxDegreeFrames = 15;
        public float probMatching = 0.9f;
        public void UpdateParameters(EnvironmentParameters envParameters)
        {
            distance = envParameters.GetWithDefault("distance", distance);
            minCenterPointInclTcameras = envParameters.GetWithDefault("minCenterPointInclTcameras", minCenterPointInclTcameras); // [0 180]
            maxCenterPointInclTcameras = envParameters.GetWithDefault("maxCenterPointInclTcameras", maxCenterPointInclTcameras); // [0 180]
            minCenterPointInclCcameras = envParameters.GetWithDefault("minCenterPointInclCcameras", minCenterPointInclCcameras); // [0 180]
            maxCenterPointInclCcameras = envParameters.GetWithDefault("maxCenterPointInclCcameras", maxCenterPointInclCcameras); //[0 180]
            areaInclTcameras = envParameters.GetWithDefault("areaInclTcameras", areaInclTcameras); // [0 180];
            areaAziTcameras = envParameters.GetWithDefault("areaAziTcameras", areaAziTcameras); // [0 360]
            areaInclCcameras = envParameters.GetWithDefault("areaInclCcameras", areaInclCcameras);// [0 180];
            areaAziCcameras = envParameters.GetWithDefault("areaAziCcameras", areaAziCcameras); //[0 360];
            minDegreeFrames = envParameters.GetWithDefault("minDegreeFrames", minDegreeFrames); // [0 inf]
            maxDegreeFrames = envParameters.GetWithDefault("maxDegreeFrames", maxDegreeFrames); // [0 inf]
            probMatching = envParameters.GetWithDefault("probMatching", probMatching); // [0 inf]
        }
    }

    public override void OnEpisodeBegin()
    {
        
        var envParameters = Academy.Instance.EnvironmentParameters;
        newLevel = envParameters.GetWithDefault("newLevel", newLevel);
        // This default is used ONLY IF THE "newLevel" IS NEVER RECEIVED! Otherwise the default is the PREVIOUS one!!!

        if (newLevel == 1f)
        {
            simParams.UpdateParameters(envParameters);
            sendDebugLogChannel.SendEnvInfoToPython( "Parameters Updated: \nDistance [" + simParams.distance.ToString() + "]" +
            "\nCenterPointInclTcameras: [" + simParams.minCenterPointInclTcameras + "," + simParams.maxCenterPointInclTcameras + "]" +
            "\nCenterPointInclCcameras: [" + simParams.minCenterPointInclCcameras + "," + simParams.maxCenterPointInclCcameras + "]" +
            "\nareaInclTcameras: [" + simParams.areaInclTcameras + "]" +
            "\nareaAziTcameras: [" + simParams.areaAziTcameras + "]" +
            "\nDegreeFrames: [" + simParams.minDegreeFrames + "," + simParams.maxDegreeFrames + "]" +
            "\nareaInclCcameras: [" + simParams.areaInclCcameras + "]" +
            "\nareaAziCcameras: [" + simParams.areaAziCcameras + "]" + 
            "\nprobMatching: [" + simParams.probMatching + "].");
        }


        //Assert.IsTrue(minDegreeQueryCamera + maxDegreeSQcameras > minDegreeCandidatesCamera && maxDegreeQueriesCamera + minDegreeSQcameras < maxDegreeCandidatesCamera);
        //if (!(minDegreeQueryCamera + maxDegreeSQcameras > minDegreeCandidatesCamera && maxDegreeQueriesCamera + minDegreeSQcameras < maxDegreeCandidatesCamera))
        //{
        //    UnityEngine.Debug.Log("CAMERAS ARE NOT PLACED PROPERLY.");
        //    Application.Quit(1);

        //}


        trainingObj.Clear();
        candidateObjs.Clear();
        cameraPositions.Clear();
        labelsSelectedObjects.Clear();

        //debugRotation.Clear();

        if (numEpisodes % 300 == 0)
        {
            debugMiddlePointsSequenceT.Clear();
            debugMiddlePointsSequenceC.Clear();
            for (int k = 0; k < K; k++)
            {
                debugPointHistoryCenterRelativeCT[k].Clear();
            }
        }

        debugMiddlePointsAreaC.Clear();
        debugMiddlePointsAreaT.Clear();

        Vector3 middlePointSequenceT;
        Vector3 middlePointSequenceC;
        
        int indexTraining = 0;
        int indexCandidate = 0;

        for (int k = 0; k < K; k++)
        {
            // Place the selected objects on their position
            var trainingObjIdx = Random.Range(0, datasetList.Count);
            int candidateObjIdx = trainingObjIdx;
            if (Random.Range(0f, 1f) > simParams.probMatching)
            {
                do
                {
                    candidateObjIdx = Random.Range(0, datasetList.Count);
                } while (candidateObjIdx == trainingObjIdx);
            }

            //labelsSelectedObjects.Add(mapNameToNum[cloneObjs[k].name]);
            labelsSelectedObjects.Add(candidateObjIdx);
            labelsSelectedObjects.Add(trainingObjIdx);

            trainingObj.Add(datasetList[trainingObjIdx]);
            candidateObjs.Add(datasetList[candidateObjIdx]);

            // MIDDLE POINTS FOR TRAINING
            var objPosT = datasetList[trainingObjIdx].transform.position; // I should get the local to world: f(0, 0, 0) -> world position
            var objToLookAtT = datasetList[trainingObjIdx].transform; //.transform.GetChild(0);

            float azimuthCenterPointT = RandomAngle(simParams.minCenterPointAziTcameras, simParams.maxCenterPointAziTcameras, simParams.minCenterPointAziTcameras, simParams.maxCenterPointAziTcameras);
            float inclinationCenterPointT = RandomAngle(simParams.minCenterPointInclTcameras, simParams.maxCenterPointInclTcameras, simParams.minCenterPointInclTcameras, simParams.maxCenterPointInclTcameras);
            var middlePointTarea = GetPositionAroundSphere(inclinationCenterPointT, azimuthCenterPointT, Vector3.up) * simParams.distance;
            debugMiddlePointsAreaT.Add(middlePointTarea + objPosT);

            float azimuthSequence = 0f;
            float inclinationSequence = 0f;
            var delta = Random.Range(simParams.minDegreeFrames, simParams.maxDegreeFrames);

           
            // MIDDLE POINTS FOR CANDIDATES 
            var objPosC = datasetList[candidateObjIdx].transform.position; // I should get the local to world: f(0, 0, 0) -> world position
            var objToLookAtC = datasetList[candidateObjIdx].transform; //.transform.GetChild(0);


            float azimuthCenterPointC = RandomAngle(azimuthCenterPointT + simParams.minAziTCcameras,
                                               azimuthCenterPointT + simParams.maxAziTCcameras,
                                               simParams.minCenterPointAziCcameras, simParams.maxCenterPointAziCcameras);
            float inclinationCenterPointC = RandomAngle(inclinationCenterPointT + simParams.minInclTCcameras,
                                                        inclinationCenterPointT + simParams.maxInclTCcameras,
                                                        simParams.minCenterPointInclCcameras, simParams.maxCenterPointInclCcameras);

            var middlePointCarea = GetPositionAroundSphere(inclinationCenterPointC, azimuthCenterPointC, Vector3.up) * simParams.distance;
            debugMiddlePointsAreaC.Add(middlePointCarea + objPosC);

            for (int sc = 0; sc < numSc; sc++)
            {
                azimuthSequence = RandomAngle(azimuthCenterPointC - simParams.areaAziCcameras / 2F, azimuthCenterPointC + simParams.areaAziCcameras / 2F, simParams.minCenterPointAziCcameras, simParams.maxCenterPointAziCcameras);
                inclinationSequence = RandomAngle(inclinationCenterPointC - simParams.areaInclCcameras / 2F, inclinationCenterPointC + simParams.areaInclCcameras / 2F, simParams.minCenterPointInclCcameras, simParams.maxCenterPointInclCcameras);

                middlePointSequenceC = GetPositionAroundSphere(inclinationSequence, azimuthSequence, Vector3.up) * simParams.distance;

                var randomDirection = Vector3.Normalize(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(1f, 1f)));
                //debugPointsCandidates.Add(middlePointSequenceC + objPosC);
                debugMiddlePointsSequenceC.Add(middlePointSequenceC + objPosC);

                //if (sc == 0 && k == 0)
                //    debugRotation.Add(randomDirection);

                for (int fsc = 0; fsc < numFc; fsc++)
                {
                    candidates[k].sequences[sc].frames[fsc].transform.position = objPosC + Quaternion.AngleAxis(delta * (fsc - numFc / 2), randomDirection) * middlePointSequenceC;
                    candidates[k].sequences[sc].frames[fsc].transform.LookAt(objToLookAtC, Vector3.up);
                    candidates[k].sequences[sc].frames[fsc].GetComponent<Camera>().cullingMask = 1 << (8 + candidateObjIdx);
                    //debugPointsCandidates.Add(candidates[k].sequences[sc].frames[fsc].transform.position);
                    //candidateCameraPosRelativeToObj[indexCandidate] = candidates[k].sequences[sc].frames[fsc].transform.position - objPos;
                    cameraPositions.Add(candidates[k].sequences[sc].frames[fsc].transform.position - objPosC);

                    indexCandidate += 1;
                }
            }


                // This should be 0F, 180F, 0F, 1F but change it to 0F, 0F for testing. 
            for (int st = 0; st < numSt; st++)
            {
                //UnityEngine.Debug.Log("DELTA: " + delta);
                azimuthSequence = RandomAngle(azimuthCenterPointT - simParams.areaAziTcameras / 2F, azimuthCenterPointT + simParams.areaAziTcameras / 2F, simParams.minCenterPointAziTcameras, simParams.maxCenterPointAziTcameras);
                inclinationSequence = RandomAngle(inclinationCenterPointT - simParams.areaInclTcameras / 2F, inclinationCenterPointT + simParams.areaInclTcameras / 2F, simParams.minCenterPointInclTcameras, simParams.maxCenterPointInclTcameras);

                middlePointSequenceT = GetPositionAroundSphere(inclinationSequence, azimuthSequence, Vector3.up) * simParams.distance;
                debugMiddlePointsSequenceT.Add(middlePointSequenceT + objPosT);

                var randomDirection = Vector3.Normalize(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
                //if (st == 0 && k == 0)
                //debugRotation.Add(randomDirectionT[st]);

                // 
                for (int fst = 0; fst < numFt; fst++)
                {
                    training[k].sequences[st].frames[fst].transform.position = objPosT + Quaternion.AngleAxis(delta * (fst - numFt / 2), randomDirection) * middlePointSequenceT;
                    training[k].sequences[st].frames[fst].transform.LookAt(objToLookAtT);
                    training[k].sequences[st].frames[fst].GetComponent<Camera>().cullingMask = 1 << (8 + trainingObjIdx);
                    //debugPointsTraining.Add(training[k].sequences[st].frames[indexFrames].transform.position);
                    //trainingCameraPosRelativeToObj[indexTraining] = training[k].sequences[st].frames[fst].transform.position - objPos;
                    cameraPositions.Add(training[k].sequences[st].frames[fst].transform.position - objPosT);
                    //indexTraining += 1;
                }
                // break;
            }
            //  break;
        
            //debugPointHistoryCenterRelativeCT[k].Add(objPos + (Quaternion.FromToRotation(middlePointTarea - objPos, (Vector3.forward * distance)) * middlePointCarea - objPos));
            //var pos = middlePointSequenceC.transform.position;
            //var rot = middlePointSequenceC.transform.rotation;
            //var cloneMiddlePointAreaT = GameObject.Instantiate(middlePointTarea);
            //var cloneMiddlePointSeqc = GameObject.Instantiate(middlePointSequenceC, cloneMiddlePointAreaT.transform);
            //cloneMiddlePointSeqc.name = "IMPORTANT1";
            //cloneMiddlePointSeqc.transform.position = pos;
            //cloneMiddlePointSeqc.transform.rotation = rot;
            var diffAzi = azimuthCenterPointT - azimuthSequence;
            var diffIncl = inclinationCenterPointT - inclinationSequence;
            var diffPos = simParams.distance * GetPositionAroundSphere(diffIncl + 90f, diffAzi - 90, Vector3.up);
            //var rotObj = (Vector3.forward * simParams.distance)+ middlePointTarea.transform.worldToLocalMatrix.MultiplyPoint(middlePointSequenceC.transform.position);
            //var cloneMiddlePoint = GameObject.Instantiate(middlePointSequenceC);
            //cloneMiddlePoint.name = "IO";


            // objPos + (Quaternion.FromToRotation(middlePointTarea.transform.position, (Vector3.forward * simParams.distance)) * middlePointSequenceC.transform.position);

            //cloneMiddlePointAreaT.transform.LookAt(objToLookAt, Vector3.up);


            //var diffAngle = Vector3.SignedAngle(cloneMiddlePoint.transform.up, Vector3.up, Vector3.forward);
            //UnityEngine.Debug.Log("ANGLE: " + diffAngle);
            //cloneMiddlePoint.transform.position = Quaternion.AngleAxis(diffAngle, Vector3.forward) * cloneMiddlePoint.transform.position;

            debugPointHistoryCenterRelativeCT[k].Add(diffPos + objPosT); // we just take the middle point of the LAST sequences of candidates for this object
        }

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
            //foreach (GameObject cln in trainingObj)
            //{
            //    Destroy(cln);
            //}
            //labelsCandidates.Clear();
            labelsSelectedObjects.Clear();
            GameObject episodeContainer = GameObject.Find("Episode Container");
            //Destroy(episodeContainer);
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
            //UnityEngine.Debug.Log(labelsSelectedObjects[i] + " Name: " + datasetList[labelsSelectedObjects[i]]);
            //sendDebugLogChannel.SendEnvInfoToPython("LABEL " + i + ": " + labelsSelectedObjects[i]);
            sensor.AddObservation(labelsSelectedObjects[i]);
        }

        //for (int i = 0; i < labelsSelectedObjects.Count; i++)
        //{
        //    sensor.AddObservation(labelsSelectedObjects[i]);
        //}

        //Support Camera Position, X, Y and Z
        foreach (Vector3 pos in cameraPositions)
        {
            sensor.AddObservation(pos);

        }

    }


    public override void OnActionReceived(float[] vectorAction)
    {
        //foreach (GameObject cln in trainingObj)
        //{
        //    Destroy(cln);
        //}
        //labelsCandidates.Clear();
        //GameObject episodeContainer = GameObject.Find("Episode Container");
        //Destroy(episodeContainer);
        OnEpisodeBegin();
    }

}

#if UNITY_EDITOR

//[System.Serializable]
[ExecuteInEditMode]
[CustomEditor(typeof(SequenceLearningTask))]
//[CanEditMultipleObjects]
public class SequenceMLtaskEditor : Editor
{
    int K;
    int numSt;
    int numSc;
    int numFt;
    int numFc;
    int sizeCanvas;
    string nameDataset;
    public Object source;
    void OnEnable()
    {
        var mt = (SequenceLearningTask)target;
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
            var mt = (SequenceLearningTask)target;
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
            int currentK = EditorGUILayout.IntSlider(K, 1, 30);
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
                    SequenceBuildSceneCLI.numSt = currentSt;
                    SequenceBuildSceneCLI.numFc = currentFc;
                    SequenceBuildSceneCLI.numFt = currentFt;
                    SequenceBuildSceneCLI.numSc = currentSc;
                    SequenceBuildSceneCLI.K = currentK;
                    SequenceBuildSceneCLI.sizeCanvas = currentSC;

                    if (currentDS != null)
                    {
                        SequenceBuildSceneCLI.nameDataset = currentDS;
                    }
                    SequenceBuildSceneCLI.UpdateComponents();

                }

            }
        }
    }
}
#endif
