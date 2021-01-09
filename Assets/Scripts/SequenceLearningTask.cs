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
using System.Linq;

public class SequenceLearningTask : Agent
{

    [HideInInspector]
    public bool runEnable = true;
    List<GameObject> datasetList = new List<GameObject>();
    [HideInInspector]
    public GameObject cameraContainer;
    [HideInInspector]
    public GameObject agent;
    GameObject info;

    Dictionary<string, int> mapNameToNum = new Dictionary<string, int>();
    List<int> indexSelectedObjects = new List<int>();
    List<Vector3> cameraPositions = new List<Vector3>();

    public bool SeeGizmoCameraHistory = true;
    public bool SeeGizmoCameraMidpoint = true;
    public bool SeeCamHistoryRelative = true;

    StringLogSideChannel sendEnvParamsChannel;
    StringLogSideChannel sendDebugLogChannel;

 
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


    List<Vector3> gizmoMiddlePointsSequenceC = new List<Vector3>();
    List<Vector3> gizmoMiddlePointsSequenceT = new List<Vector3>();
    List<GameObject> gizmoTrainingObj = new List<GameObject>();
    List<GameObject> gizmoCandidateObjs = new List<GameObject>();
    List<List<Vector3>> gizmoPointHistoryCenterRelativeCT = new List<List<Vector3>>();
    List<Vector3> gizmoMiddlePointsAreaT = new List<Vector3>();
    List<Vector3> gizmoMiddlePointsAreaC = new List<Vector3>();



    public int numEpisodes = 0;
    EnvironmentParameters envParams;
    public SimulationParameters simParams = new SimulationParameters();
    public TaskType taskType;
    public TrainComparisonType trainCompType; 

    public void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }




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

    public enum TaskType
    {
        TRAIN,
        TEST_EDELMAN_SAME_AXIS,
        TEST_EDELMAN_ORTHOGONAL,
        TEST_GOKER
    }

    public enum TrainComparisonType
    {
        ALL,
        GROUP
        //GOKER
    }


    void FillDataset()
    {

        //GameObject datasetsStr = GameObject.Find(nameDataset);
        GameObject datasetObj = new GameObject("ActiveDataset");
        //GameObject separator = GameObject.Find("Separator");

        int totChildren = 0;
        var datasetListStr = nameDataset.Split('+');
        foreach (var datasetStr in datasetListStr)
        {
            var thisDataset = GameObject.Find(datasetStr);
            if (thisDataset == null)
                Assert.IsTrue(false, "Dataset " + nameDataset + " not found.");
            int children = thisDataset.transform.childCount;

            for (int i = 0; i < children; i++)
            {
                var obj = thisDataset.transform.GetChild(i);
                //var newObj = ScaleAndMovePivotObj(obj.gameObject);
                var newObj = GameObject.Instantiate(obj);
                newObj.transform.position = new Vector3(0f, 0f, 0f);
                newObj.transform.parent = datasetObj.transform;
                datasetList.Add(newObj.gameObject);
                datasetList[datasetList.Count-1].transform.position = new Vector3(15 * totChildren, 0, 0);
                totChildren += 1;
                //mapNameToNum.Add(datasetList[i].name, i);
                //SetLayerRecursively(datasetList[i], 8 + i);
                //var newSeparator = GameObject.Instantiate(separator);
                //newSeparator.transform.position = new Vector3(10 * i + 5, 0, 0);
            }
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
            gizmoPointHistoryCenterRelativeCT.Add(new List<Vector3>());
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


        envParams = Academy.Instance.EnvironmentParameters;
        var taskTypeTmp = (int)envParams.GetWithDefault("taskType", -1f);
        if (taskTypeTmp != -1)
        {
            taskType = (TaskType)taskTypeTmp;
        }
        
        // TEST EDELMAN
        if ((taskType == TaskType.TEST_EDELMAN_ORTHOGONAL || taskType == TaskType.TEST_EDELMAN_SAME_AXIS) && (K != 1 || numSt != 2 || numFt != 3 || numSc != 1 || numFc != 1))
        {
            string Str = "UNITY >> TaskType [TEST_EDELMAN] is designed to work only for K = 1, numSt = 2, numFt = 3. Build the scene again!";
            sendDebugLogChannel.SendEnvInfoToPython(Str);
            Assert.IsTrue(false, Str);
            K = 1;
        }
        // TEST GOKER
        if ((taskType == TaskType.TEST_GOKER) && (K != 1 || numSt != 1 || numFt != 1 || numSc != 1 || numFc != 1))
        {
            string Str = "UNITY >> TaskType [TEST_GOKER] is designed to work only for K = 1, numSt = 1, numFt = 1. Build the scene again!";
            sendDebugLogChannel.SendEnvInfoToPython(Str);
            Assert.IsTrue(false, Str);
            K = 1;
        }



        var compTmp = (int)envParams.GetWithDefault("trainComparisonType", -1f);
        if (compTmp != -1)
        {
            trainCompType = (TrainComparisonType)compTmp;
        }

        if (trainCompType == TrainComparisonType.GROUP && taskType != TaskType.TRAIN)
        {
            string Str = "UNITY >> Comparison Type [GROUP] can only work with Task Type [TRAIN].";
            sendDebugLogChannel.SendEnvInfoToPython(Str);
            taskType = TaskType.TRAIN;
            Assert.IsTrue(false);
        }

        sendDebugLogChannel.SendEnvInfoToPython("UNITY>> taskType: " + taskType.ToString());
        sendDebugLogChannel.SendEnvInfoToPython("UNITY>> trainComparisonType: " + trainCompType.ToString());


        Random.InitState(2);

    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            //Gizmos.color = new Vector4(0.7f, 0.5f, 0.2F, 0.5F);

            //Gizmos.DrawSphere(bb.center, 0.1f); ;
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

            for (int k = 0; k < K; k++)
            {
                Gizmos.color = new Vector4(153 / 255F, 51 / 255F, 1F, 0.2F);
                Gizmos.DrawWireSphere(gizmoTrainingObj[k].transform.position, simParams.distance);
                Gizmos.DrawSphere(gizmoTrainingObj[k].transform.position, simParams.distance);
                Gizmos.DrawWireSphere(gizmoCandidateObjs[k].transform.position, simParams.distance);
                Gizmos.DrawSphere(gizmoCandidateObjs[k].transform.position, simParams.distance);
            }

            if (SeeGizmoCameraHistory)
            {

                index = 0;
                foreach (Vector3 vec in gizmoMiddlePointsSequenceC)
                {

                    Gizmos.color = new Vector4(0F, 1F, 0F, 0.5F);
                    Gizmos.DrawSphere(vec, 0.1F);
                    Gizmos.DrawWireSphere(vec, 0.1F);
                    //Gizmos.DrawLine(positions[(int)(index / numSc) % C], vec);
                    index += 1;
                }
                index = 0;
                foreach (Vector3 vec in gizmoMiddlePointsSequenceT)
                {

                    Gizmos.color = new Vector4(0F, 0F, 1F, 0.5F);
                    Gizmos.DrawSphere(vec, 0.1F);
                    Gizmos.DrawWireSphere(vec, 0.1F);
                    //Gizmos.DrawLine(positions[(int)(index / Q) % C], vec);
                    index += 1;
                }


            }
            if (SeeGizmoCameraMidpoint)
            {
                foreach (var mp in gizmoMiddlePointsAreaC)
                {
                    Gizmos.color = new Vector4(0F, 1F, 0F, 0.5F);
                    Gizmos.DrawSphere(mp, 0.2F);
                }
                foreach (var mp in gizmoMiddlePointsAreaT)
                {
                    Gizmos.color = new Vector4(0F, 0F, 1F, 0.5F);

                    Gizmos.DrawSphere(mp,  0.3F);
                }
            }
            if (SeeCamHistoryRelative)
            {
                for (int k = 0; k < K; k++)
                {
                    foreach (Vector3 vec in gizmoPointHistoryCenterRelativeCT[k])
                    {
                        Gizmos.color = new Vector4(1F, 0F, 0f, 1f);
                        Gizmos.DrawSphere(vec, 0.1F);
                        Gizmos.DrawLine(gizmoTrainingObj[k].transform.position, Vector3.forward * simParams.distance + gizmoTrainingObj[k].transform.position);

                        Gizmos.DrawWireSphere(vec, 0.1F);

                    }
                }
                //for (int i = 0; i < gizmoPointHistoryCenterRelativeCT.Count; i++)
                //{
                //    Gizmos.color = new Vector4(1F, 1F, 0f, 1f);
                //    Gizmos.DrawLine(gizmoTrainingObj[i].transform.position, gizmoPointHistoryCenterRelativeCT[i][gizmoPointHistoryCenterRelativeCT[i].Count - 1]);
                //}
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

    protected Vector3 GetPositionAroundSphere(float inclinationDeg, float azimuthDeg, Vector3 aroundPosition)
    {

        // direction in DEGREES

        float azimuthRad = azimuthDeg * Mathf.Deg2Rad; // * 2.0F * UnityEngine.Mathf.PI;
        float cosDistFromZenith = Mathf.Cos(inclinationDeg * Mathf.Deg2Rad); //Random.Range(Mathf.Min(a, b), Mathf.Max(a, b));
        float sinDistFromZenith = Mathf.Sqrt(1.0F - cosDistFromZenith * cosDistFromZenith);
        Vector3 pqr = new Vector3(Mathf.Cos(azimuthRad) * sinDistFromZenith, UnityEngine.Mathf.Sin(azimuthRad) * sinDistFromZenith, cosDistFromZenith);
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
        public float distance = 4f;
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
        public float areaInclTcameras = 120;
        public float areaAziTcameras = 359;

        public float areaInclCcameras = 120; 
        public float areaAziCcameras = 359;

        // DISTANCE
        // This goes from -180 to +180 (where 0 is the position of the first training camera)
        // this + areaDegC is the maximum distance between the 2 centers
        public float minInclTCcameras = 0; public float maxInclTCcameras = 0;
        public float minAziTCcameras = 0; public float maxAziTCcameras = 0;

        // THIS SHOULD BE POSITIVE! Distance across frames
        public float minDegreeFrames = 14; public float maxDegreeFrames = 15;
        public float probMatching = 0.5f;
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

    public void ClearVarForEpisode()
    {
        gizmoTrainingObj.Clear();
        gizmoCandidateObjs.Clear();
        cameraPositions.Clear();
        indexSelectedObjects.Clear();

        //debugRotation.Clear();

        if (numEpisodes % 300 == 0)
        {
            gizmoMiddlePointsSequenceT.Clear();
            gizmoMiddlePointsSequenceC.Clear();
            for (int k = 0; k < K; k++)
            {
                gizmoPointHistoryCenterRelativeCT[k].Clear();
            }
        }

        gizmoMiddlePointsAreaC.Clear();
        gizmoMiddlePointsAreaT.Clear();
    }

    public (int idxTraining, int idxCandidate) GetObjIdxRandom()
    {
        var trainingObjIdx = Random.Range(0, datasetList.Count);
        int candidateObjIdx = trainingObjIdx;
        if (Random.Range(0f, 1f) > simParams.probMatching)
        {
            do
            {
                switch (trainCompType)
                {
                    case TrainComparisonType.ALL:
                        candidateObjIdx = Random.Range(0, datasetList.Count);
                        break;
                    case TrainComparisonType.GROUP:
                        switch (nameDataset)
                        {
                            case var ss when nameDataset.Contains("ShapeNet"):
                                var nameGroup = datasetList[trainingObjIdx].name.Split('.')[0];
                                List<int> allIndices = new List<int>(datasetList.Select((go, i) => new { GameObj = go, Index = i })
                                                                                 .Where(x => x.GameObj.name.Split('.')[0] == nameGroup)
                                                                                 .Select(x => x.Index));
                                candidateObjIdx = allIndices[Random.Range(0, allIndices.Count)];
                                UnityEngine.Debug.Log("TRAIN OBJ: " + datasetList[trainingObjIdx].name + " CAND OBJ: " + datasetList[candidateObjIdx].name);
                                break;
                        }
                        break;

                    default:
                        Assert.IsTrue(false, "COMPARISON TYPE NOT IMPLEMENTED");
                        break;
                }
             
            } while (candidateObjIdx == trainingObjIdx);
        }
        return (trainingObjIdx, candidateObjIdx);
    }

    public void SetCameraPositionsRandom(int k, int trainingObjIdx, int candidateObjIdx)
    {
        Vector3 middlePointSequenceT;
        Vector3 middlePointSequenceC;

        var objPosT = datasetList[trainingObjIdx].transform.position;
        var objToLookAtT = datasetList[trainingObjIdx].transform;

        var objPosC = datasetList[candidateObjIdx].transform.position;
        var objToLookAtC = datasetList[candidateObjIdx].transform;

        float azimuthCenterPointT = RandomAngle(simParams.minCenterPointAziTcameras, simParams.maxCenterPointAziTcameras, simParams.minCenterPointAziTcameras, simParams.maxCenterPointAziTcameras);
        float inclinationCenterPointT = RandomAngle(simParams.minCenterPointInclTcameras, simParams.maxCenterPointInclTcameras, simParams.minCenterPointInclTcameras, simParams.maxCenterPointInclTcameras);
        var middlePointTarea = GetPositionAroundSphere(inclinationCenterPointT, azimuthCenterPointT, Vector3.up) * simParams.distance;
        gizmoMiddlePointsAreaT.Add(middlePointTarea + objPosT);

        float azimuthSequence = 0f;
        float inclinationSequence = 0f;
        var delta = Random.Range(simParams.minDegreeFrames, simParams.maxDegreeFrames);




        float azimuthCenterPointC = RandomAngle(azimuthCenterPointT + simParams.minAziTCcameras,
                                           azimuthCenterPointT + simParams.maxAziTCcameras,
                                           simParams.minCenterPointAziCcameras, simParams.maxCenterPointAziCcameras);
        float inclinationCenterPointC = RandomAngle(inclinationCenterPointT + simParams.minInclTCcameras,
                                                    inclinationCenterPointT + simParams.maxInclTCcameras,
                                                    simParams.minCenterPointInclCcameras, simParams.maxCenterPointInclCcameras);

        var middlePointCarea = GetPositionAroundSphere(inclinationCenterPointC, azimuthCenterPointC, Vector3.up) * simParams.distance;
        gizmoMiddlePointsAreaC.Add(middlePointCarea + objPosC);

        for (int sc = 0; sc < numSc; sc++)
        {
            azimuthSequence = RandomAngle(azimuthCenterPointC - simParams.areaAziCcameras / 2F, azimuthCenterPointC + simParams.areaAziCcameras / 2F, simParams.minCenterPointAziCcameras, simParams.maxCenterPointAziCcameras);
            inclinationSequence = RandomAngle(inclinationCenterPointC - simParams.areaInclCcameras / 2F, inclinationCenterPointC + simParams.areaInclCcameras / 2F, simParams.minCenterPointInclCcameras, simParams.maxCenterPointInclCcameras);

            middlePointSequenceC = GetPositionAroundSphere(inclinationSequence, azimuthSequence, Vector3.up) * simParams.distance;

            var randomDirection = Vector3.Normalize(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(1f, 1f)));

            gizmoMiddlePointsSequenceC.Add(middlePointSequenceC + objPosC);


            for (int fsc = 0; fsc < numFc; fsc++)
            {
                candidates[k].sequences[sc].frames[fsc].transform.position = objPosC + Quaternion.AngleAxis(delta * (fsc - numFc / 2), randomDirection) * middlePointSequenceC;
                candidates[k].sequences[sc].frames[fsc].transform.LookAt(objToLookAtC, Vector3.up);
                //candidates[k].sequences[sc].frames[fsc].GetComponent<Camera>().cullingMask = 1 << (8 + candidateObjIdx);
                cameraPositions.Add(candidates[k].sequences[sc].frames[fsc].transform.position - objPosC);
            }
        }

        for (int st = 0; st < numSt; st++)
        {
            //UnityEngine.Debug.Log("DELTA: " + delta);
            azimuthSequence = RandomAngle(azimuthCenterPointT - simParams.areaAziTcameras / 2F, azimuthCenterPointT + simParams.areaAziTcameras / 2F, simParams.minCenterPointAziTcameras, simParams.maxCenterPointAziTcameras);
            inclinationSequence = RandomAngle(inclinationCenterPointT - simParams.areaInclTcameras / 2F, inclinationCenterPointT + simParams.areaInclTcameras / 2F, simParams.minCenterPointInclTcameras, simParams.maxCenterPointInclTcameras);

            middlePointSequenceT = GetPositionAroundSphere(inclinationSequence, azimuthSequence, Vector3.up) * simParams.distance;
            gizmoMiddlePointsSequenceT.Add(middlePointSequenceT + objPosT);

            var randomDirection = Vector3.Normalize(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
        
            for (int fst = 0; fst < numFt; fst++)
            {
                training[k].sequences[st].frames[fst].transform.position = objPosT + Quaternion.AngleAxis(delta * (fst - numFt / 2), randomDirection) * middlePointSequenceT;
                training[k].sequences[st].frames[fst].transform.LookAt(objToLookAtT);
                //training[k].sequences[st].frames[fst].GetComponent<Camera>().cullingMask = 1 << (8 + trainingObjIdx);
                cameraPositions.Add(training[k].sequences[st].frames[fst].transform.position - objPosT);
            }
        }
        var diffAzi = azimuthCenterPointT - azimuthSequence;
        var diffIncl = inclinationCenterPointT - inclinationSequence;
        var diffPos = simParams.distance * GetPositionAroundSphere(diffIncl + 90f, diffAzi - 90, Vector3.up);

        gizmoPointHistoryCenterRelativeCT[k].Add(diffPos + objPosT);
    }

    public void SetCameraPositionEdelman(int k, int trainingObjIdx, int candidateObjIdx)
    {
        int candidateCamDegree = (int)envParams.GetWithDefault("degree", (numEpisodes-1)* 10);
        int candidateCamRotation = (int)envParams.GetWithDefault("rotation", (numEpisodes-1) * 10);
        
        var objPosT = datasetList[trainingObjIdx].transform.position;
        var objToLookAtT = datasetList[trainingObjIdx].transform;

        var objPosC = datasetList[candidateObjIdx].transform.position;
        var objToLookAtC = datasetList[candidateObjIdx].transform;

        List<Vector3> positionsTcameras = new List<Vector3>();

        int sc = 0;
        int fsc = 0;
        Vector3 positionObj = new Vector3(0f, 0f, 0f);
        if (taskType == TaskType.TEST_EDELMAN_SAME_AXIS)
        {
            positionObj = GetPositionAroundSphere(90, candidateCamRotation + candidateCamDegree, Vector3.up) * simParams.distance;

        }
        if (taskType == TaskType.TEST_EDELMAN_ORTHOGONAL)
        {
            int rotation_ortho = candidateCamRotation; 
            if (candidateCamDegree > 90 && candidateCamDegree < 270)
            {
                rotation_ortho += 180;
            }
            UnityEngine.Debug.Log("D: " + (90 + candidateCamDegree).ToString() + "R: " + candidateCamRotation);
            positionObj = GetPositionAroundSphere(90 + candidateCamDegree, rotation_ortho, Vector3.up) * simParams.distance;

        }


        candidates[k].sequences[sc].frames[fsc].transform.position = objPosC + positionObj;
        candidates[k].sequences[sc].frames[fsc].transform.LookAt(objToLookAtC, Vector3.up);
        //candidates[k].sequences[sc].frames[fsc].GetComponent<Camera>().cullingMask = 1 << (8 + candidateObjIdx);
        cameraPositions.Add(candidates[k].sequences[sc].frames[fsc].transform.position - objPosC);

        for (int st = 0; st < numSt; st++)
        {
            if (st == 0)
            {
                positionsTcameras.Clear();
                positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation + 15, Vector3.up) * simParams.distance);
                positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation + 0, Vector3.up) * simParams.distance);
                positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation - 15, Vector3.up) * simParams.distance);

            }
            if (st == 1)
            {
                positionsTcameras.Clear();
                positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation - 60, Vector3.up) * simParams.distance);
                positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation - 75, Vector3.up) * simParams.distance);
                positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation - 90, Vector3.up) * simParams.distance);

            }

            for (int fst = 0; fst < numFt; fst++)
            {
                training[k].sequences[st].frames[fst].transform.position = objPosT + positionsTcameras[fst];
                training[k].sequences[st].frames[fst].transform.LookAt(objToLookAtT);
                //training[k].sequences[st].frames[fst].GetComponent<Camera>().cullingMask = 1 << (8 + trainingObjIdx);
                //debugPointsTraining.Add(training[k].sequences[st].frames[fst].transform.position);
                //trainingCameraPosRelativeToObj[indexTraining] = positionsTcameras[fst];
                //indexTraining += 1;
                cameraPositions.Add(training[k].sequences[st].frames[fst].transform.position - objPosT);

            }
        }       
    }

    public void SetCameraPositionGoker(int k, int trainingObjIdx, int candidateObjIdx)
    {
        //int candidateCamDegree = (int)envParams.GetWithDefault("degree", (numEpisodes - 1) * 10);
        int trainingCamAzimuth = (int)envParams.GetWithDefault("azimuthT", (numEpisodes - 1) * 2);
        int candidateCamAzimuth = (int)envParams.GetWithDefault("azimuthC", (numEpisodes - 1) * 10);


        var objPosT = datasetList[trainingObjIdx].transform.position;
        var objToLookAtT = datasetList[trainingObjIdx].transform;

        var objPosC = datasetList[candidateObjIdx].transform.position;
        var objToLookAtC = datasetList[candidateObjIdx].transform;

        var positionCamC = GetPositionAroundSphere(45, candidateCamAzimuth, Vector3.up) * simParams.distance;
        var positionCamT = GetPositionAroundSphere(45, trainingCamAzimuth, Vector3.up) * simParams.distance;


        candidates[0].sequences[0].frames[0].transform.position = objPosC + positionCamC;
        candidates[0].sequences[0].frames[0].transform.LookAt(objToLookAtC, Vector3.up);
        cameraPositions.Add(candidates[0].sequences[0].frames[0].transform.position - objPosC);

        training[0].sequences[0].frames[0].transform.position = objPosT + positionCamT;
        training[0].sequences[0].frames[0].transform.LookAt(objToLookAtT, Vector3.up);
        cameraPositions.Add(training[0].sequences[0].frames[0].transform.position - objPosT);
    }

    public (int t, int c) GetObjIdxFromChannel()
    {
        int trainingObjIdx = (int)envParams.GetWithDefault("objT", 0f);
        int candidateObjIdx = (int)envParams.GetWithDefault("objC", 2f);
        return (trainingObjIdx, candidateObjIdx);

    }

    public override void OnEpisodeBegin()
    {

        newLevel = envParams.GetWithDefault("newLevel", newLevel);
        // This default is used ONLY IF THE "newLevel" IS NEVER RECEIVED! Otherwise the default is the PREVIOUS one!!!

        if (newLevel == 1f)
        {
            simParams.UpdateParameters(envParams);
            sendDebugLogChannel.SendEnvInfoToPython("UNITY >> Parameters Updated: \nDistance [" + simParams.distance.ToString() + "]" +
            "\nCenterPointInclTcameras: [" + simParams.minCenterPointInclTcameras + "," + simParams.maxCenterPointInclTcameras + "]" +
            "\nCenterPointInclCcameras: [" + simParams.minCenterPointInclCcameras + "," + simParams.maxCenterPointInclCcameras + "]" +
            "\nareaInclTcameras: [" + simParams.areaInclTcameras + "]" +
            "\nareaAziTcameras: [" + simParams.areaAziTcameras + "]" +
            "\nDegreeFrames: [" + simParams.minDegreeFrames + "," + simParams.maxDegreeFrames + "]" +
            "\nareaInclCcameras: [" + simParams.areaInclCcameras + "]" +
            "\nareaAziCcameras: [" + simParams.areaAziCcameras + "]" +
            "\nprobMatching: [" + simParams.probMatching + "]" +
            "\n<< UNITY.");
        }
        //Assert.IsTrue(minDegreeQueryCamera + maxDegreeSQcameras > minDegreeCandidatesCamera && maxDegreeQueriesCamera + minDegreeSQcameras < maxDegreeCandidatesCamera);
        //if (!(minDegreeQueryCamera + maxDegreeSQcameras > minDegreeCandidatesCamera && maxDegreeQueriesCamera + minDegreeSQcameras < maxDegreeCandidatesCamera))
        //{
        //    UnityEngine.Debug.Log("CAMERAS ARE NOT PLACED PROPERLY.");
        //    Application.Quit(1);

        //}
        ClearVarForEpisode();

        for (int k = 0; k < K; k++)
        {
            int trainingObjIdx;
            int candidateObjIdx;
            if (taskType == TaskType.TRAIN)
            {
                (trainingObjIdx, candidateObjIdx) = GetObjIdxRandom();
            }
            else
            {
                (trainingObjIdx, candidateObjIdx) = GetObjIdxFromChannel();
            }

            indexSelectedObjects.Add(candidateObjIdx);
            indexSelectedObjects.Add(trainingObjIdx);

            gizmoTrainingObj.Add(datasetList[trainingObjIdx]);
            gizmoCandidateObjs.Add(datasetList[candidateObjIdx]);
            switch (taskType)
            {
                case TaskType.TRAIN:
                    SetCameraPositionsRandom(k, trainingObjIdx, candidateObjIdx);
                    break;
                case TaskType.TEST_EDELMAN_ORTHOGONAL:
                case TaskType.TEST_EDELMAN_SAME_AXIS:
                    SetCameraPositionEdelman(k, trainingObjIdx, candidateObjIdx);
                    break;
                case TaskType.TEST_GOKER:
                    SetCameraPositionGoker(k, trainingObjIdx, candidateObjIdx);
                    break;

            }

            numEpisodes += 1;
            //UnityEngine.Debug.Break();
            this.RequestDecision();
        }
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
            OnEpisodeBegin();
        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
    
        for (int i = 0; i < indexSelectedObjects.Count; i++)
        {
            sensor.AddObservation(indexSelectedObjects[i]);
        }

        //Support Camera Position, X, Y and Z
        foreach (Vector3 pos in cameraPositions)
        {
            sensor.AddObservation(pos);
        }
    }


    public override void OnActionReceived(float[] vectorAction)
    {
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
    int grayscale;

    string nameDataset;
    public Object source;
    public Object dataset_to_adjust;
    void OnEnable()
    {
        var mt = (SequenceLearningTask)target;
        if (mt.runEnable)
        {
            mt.agent = GameObject.Find("Agent");
            mt.runEnable = false;
        }

    }

    void ScaleAndMovePivotObj(GameObject gm)
    {
        // Assume the hierarchy is gm -> Obj1 (to be left untouched) -> ObjA, ObjB, etc. (with renderer)
        Bounds bb = new Bounds();
         
        int children = gm.transform.GetChild(0).transform.childCount;

        for (int i = 0; i < children; i++)
        {
            var obj = gm.transform.GetChild(0).transform.GetChild(i);

            if (i == 0)
            {
                bb = obj.GetComponent<Renderer>().bounds;
            }
            else
            {
                bb.Encapsulate(obj.GetComponent<Renderer>().bounds);
            }
        }
        //GameObject.Find("Sphere").transform.position = bb.center;
     
        gm.transform.position = bb.center;
        float maxSize = 3f;
        gm.transform.localScale = gm.transform.localScale / (Mathf.Max(Mathf.Max(bb.size.x, bb.size.y), bb.size.z) / maxSize);
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
            grayscale = int.Parse(tmp[7].Split(':')[1]);

            GameObject infoDTO = info.transform.GetChild(1).gameObject;
            string infoDTOstr = infoDTO.name;
            dataset_to_adjust = GameObject.Find(infoDTOstr.Split(':')[1]);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Size Canvas"), GUILayout.Width(80));
            int currentSC = EditorGUILayout.IntSlider(sizeCanvas, 10, 250);
            EditorGUILayout.LabelField(new GUIContent("Grayscale"), GUILayout.Width(80));
            int currentG = EditorGUILayout.Toggle(grayscale == 1) ? 1 : 0;
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

            EditorGUILayout.BeginHorizontal();
            //EditorGUILayout.LabelField(new GUIContent("->"), GUILayout.Width(70));
            dataset_to_adjust = EditorGUILayout.ObjectField(dataset_to_adjust, typeof(Object), true);
            //UnityEngine.Debug.Log(dataset_to_adjust);
            if (dataset_to_adjust != null)
            {
                infoDTO.name = "DTA:" + dataset_to_adjust.name;
            }

            if (GUILayout.Button("Adjust Dataset"))
            {
                GameObject adjusted = (GameObject)GameObject.Instantiate(dataset_to_adjust);
                adjusted.name = dataset_to_adjust.name + "ADJ";

                adjusted.transform.parent = GameObject.Find(dataset_to_adjust.name).transform.parent;
                adjusted.transform.localPosition = new Vector3(0f, 0f, 0f);
                int children = adjusted.transform.childCount;

                for (int i = children - 1; i >= 0; i--)
                {
                    UnityEngine.Debug.Log("HERE");
                    // The hierarchy must be DATASET NAME -> Obj1 (with changed transform) -> Obj1 (just a container with default values) -> ObjA, ObjB, etc with Renderer
                    // If it's not like the try to fix it
                    var cc = adjusted.transform.GetChild(i).GetChild(0);
                    if (cc.GetComponent<Renderer>() != null)
                    {
                        int meshChildren = adjusted.transform.GetChild(i).transform.childCount;
                        GameObject parent = new GameObject(adjusted.transform.GetChild(i).name);
                        parent.transform.parent = adjusted.transform.GetChild(i);
                        for (int m = meshChildren - 1; m >= 0; m--)
                        {
                            adjusted.transform.GetChild(i).GetChild(m).parent = parent.transform;
                        }
                        
                    }
                    ScaleAndMovePivotObj(adjusted.transform.GetChild(i).gameObject);
                }

            }
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
                    currentFc != numFc || currentFt != numFt || currentSC != sizeCanvas || 
                    currentDS != nameDataset || currentG != grayscale)
                {
                    SequenceBuildSceneCLI.numSt = currentSt;
                    SequenceBuildSceneCLI.numFc = currentFc;
                    SequenceBuildSceneCLI.numFt = currentFt;
                    SequenceBuildSceneCLI.numSc = currentSc;
                    SequenceBuildSceneCLI.K = currentK;
                    SequenceBuildSceneCLI.sizeCanvas = currentSC;
                    UnityEngine.Debug.Log("GRAYSCALE" + grayscale);
                    SequenceBuildSceneCLI.grayscale = currentG;


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
