////using System.Drawing;
//using System.Drawing.Imaging;
using System.IO;
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
using UnityEngine.Assertions;
using Unity.MLAgents.SideChannels;
using System.Linq;

public class SequenceLearningTask : Agent
{

    [HideInInspector]
    public bool runEnable = true;

    [HideInInspector]
    public GameObject cameraContainer;
    [HideInInspector]
    public GameObject agent;
    GameObject info;

    List<(GameObject gm, int classNumber)> datasetList = new List<(GameObject gm, int classNumber)>();

    List<int> indexSelectedObjects = new List<int>();
    List<Vector3> cameraPositions = new List<Vector3>();

    [HideInInspector]
    public bool GizmoCamHistory = true;
    [HideInInspector]
    public bool GizmoCamMidpoint = true;

    [HideInInspector]
    public bool changeLightsEachTrial = false;
    
    StringLogSideChannel sendEnvParamsChannel;
    StringLogSideChannel sendDebugLogChannel;
    
    [HideInInspector]
    public BatchProvider batchProvider;
    
    [HideInInspector]
    public bool useBatchProvider = false;

    [HideInInspector]
    public int repeatSameBatch = -1;
    private int batchRepeated = 0;

    [HideInInspector]
    public bool saveFramesOnDisk;
    
    [HideInInspector]
    public string savingDatasetDir = "../dataset/"; 

    int numSt = 1;
    int numSc = 1;
    int numFt = 4;
    int numFc = 1;
    int K = 2;
    int sizeCanvas = 64;
    [HideInInspector]
    public string nameDataset = "none";
    float newLevel = 0f;

    public event System.Action AfterObservationCollected; 

    public class SequenceCameras
    {
        public List<GameObject> cameraObjs = new List<GameObject>();
    }

    public class ObjectCameras
    {
        public List<SequenceCameras> sequences = new List<SequenceCameras>();
    }
    public class ClassIdxDummy
    {
        public int classIdx;
        public ClassIdxDummy(int idx)
        {
            classIdx = idx; 
        }
    }
    List<(ObjectCameras objCamera, ClassIdxDummy classIdx)> candidates = new List<(ObjectCameras objCamera, ClassIdxDummy classIdx)>();
    List<(ObjectCameras objCamera, ClassIdxDummy classIdx)> training = new List<(ObjectCameras objCamera, ClassIdxDummy classIdx)>();


    List<Vector3> gizmoMiddlePointsSequenceC = new List<Vector3>();
    List<Vector3> gizmoMiddlePointsSequenceT = new List<Vector3>();
    List<GameObject> gizmoTrainingObj = new List<GameObject>();
    List<GameObject> gizmoCandidateObjs = new List<GameObject>();
    List<List<Vector3>> gizmoPointHistoryCenterRelativeCT = new List<List<Vector3>>();
    List<Vector3> gizmoMiddlePointsAreaT = new List<Vector3>();
    List<Vector3> gizmoMiddlePointsAreaC = new List<Vector3>();
    List<GameObject> lights = new List<GameObject>();


    int numEpisodes = 0;
    EnvironmentParameters envParams;
    public SimulationParameters simParams = new SimulationParameters();
    
    [HideInInspector]
    public TrialType trialType = TrialType.RND_TRIAL;
    [HideInInspector]
    public TrainComparisonType trainCompType = TrainComparisonType.ALL;

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

    public enum TrialType
    {
        RND_TRIAL,
        DET_TRIAL_EDELMAN_SAME_AXIS_HOR,
        DET_TRIAL_EDELMAN_ORTHO_HOR,
        DET_TRIAL_EDELMAN_SAME_AXIS_VER,
        DET_TRIAL_EDELMAN_ORTHO_VER,
        DET_TRIAL_STATIC
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
            {
                string logmsg = "UNITY >> Dataset " + nameDataset + " or some part of it not found!";
                UnityEngine.Debug.Log(logmsg);
                sendDebugLogChannel.SendEnvInfoToPython(logmsg);
                Assert.IsTrue(false, logmsg);

            }
            int children = thisDataset.transform.childCount;

            for (int i = 0; i < children; i++)
            {
                var obj = thisDataset.transform.GetChild(i);
                //var newObj = ScaleAndMovePivotObj(obj.gameObject);
                var newObj = GameObject.Instantiate(obj);
                newObj.name = obj.name;
                newObj.transform.position = new Vector3(0f, 0f, 0f);
                newObj.transform.parent = datasetObj.transform;
                datasetList.Add((newObj.gameObject, 0));
                datasetList[datasetList.Count - 1].gm.transform.position = new Vector3(15 * totChildren, 0, 0);
                totChildren += 1;
                //mapNameToNum.Add(datasetList[i].name, i);
                //SetLayerRecursively(datasetList[i], 8 + i);
                //var newSeparator = GameObject.Instantiate(separator);
                //newSeparator.transform.position = new Vector3(10 * i + 5, 0, 0);
            }
        }
        //Assert.IsTrue(datasetList.Count >= K, "The elements in the datasetList are less than K!");
        GameObject.Find("DATASETS").SetActive(false);
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
        //sendDebugLogChannel.SendEnvInfoToPython("UNITIY COMPTMP : " + trainCompType);

        sendDebugLogChannel.SendEnvInfoToPython("SLT From Unity Info: \nK: " + K.ToString() + " St: " + numSt.ToString() + " Sc: " + numSc.ToString() + " Ft: " + numFt.ToString() + " Fc: " + numFc.ToString() + " size_canvas: " + sizeCanvas.ToString());
        int totIndex = 0;
        string tmpName;
        for (int k = 0; k < K; k++)
        {
            gizmoPointHistoryCenterRelativeCT.Add(new List<Vector3>());
            training.Add((new ObjectCameras(), new ClassIdxDummy(k)));
           
            for (int sq = 0; sq < numSt; sq++)
            {
                training[k].objCamera.sequences.Add(new SequenceCameras());

                for (int fsq = 0; fsq < numFt; fsq++)
                {
                    tmpName = "T" + k.ToString("D2") + "S" + sq + "F" + fsq;
                    training[k].objCamera.sequences[sq].cameraObjs.Add(cameraContainer.transform.Find(tmpName).gameObject);
                    //trainingCameraPosRelativeToObj.Add(new Vector3(0f, 0f, 0f));
                    //cameraPositions.Add(new Vector3(0f, 0f, 0f));

                    totIndex += 1;
                }
            }
        }

        totIndex = 0;
        for (int k = 0; k < K; k++)
        {
            candidates.Add((new ObjectCameras(), new ClassIdxDummy(k)));
            for (int sc = 0; sc < numSc; sc++)
            {
                candidates[k].objCamera.sequences.Add(new SequenceCameras());

                for (int fsc = 0; fsc < numFc; fsc++)
                {
                    tmpName = "C" + k.ToString("D2") + "S" + sc + "F" + fsc;
                    candidates[k].objCamera.sequences[sc].cameraObjs.Add(cameraContainer.transform.Find(tmpName).gameObject);
                    //candidateCameraPosRelativeToObj.Add(new Vector3(0f, 0f, 0f));
                    //cameraPositions.Add(new Vector3(0f, 0f, 0f));

                    totIndex += 1;
                }
            }
        }

        if (useBatchProvider)
        {
            batchProvider.InitBatchProvider();
            batchProvider.ActionReady += PrepareAndStartEpisode;
            batchProvider.RequestedExhaustedDataset += QuitApplication; 
            GameObject.Find("DATASETS").SetActive(false);

        }
        else
        {

            UnityEngine.Debug.Log("ATTENZIONE");
            var cmlNameDataset = GetArg("-name_dataset");
            if (!(cmlNameDataset == null))
            {
                nameDataset = cmlNameDataset;
                UnityEngine.Debug.Log("New Dataset from command line: " + nameDataset);
            }
 
            FillDataset();
            sendEnvParamsChannel.SendEnvInfoToPython((datasetList.Count).ToString());
        }
        envParams = Academy.Instance.EnvironmentParameters;

        var tmpChange = (int)envParams.GetWithDefault("changeLights", -1f);
        if (tmpChange != -1)
        {
            changeLightsEachTrial = tmpChange == 1;
        }
        var tmpTrialType = (int)envParams.GetWithDefault("trialType", -1f);
        if (tmpTrialType != -1)
        {
            trialType = (TrialType)tmpTrialType;
        }

  
        // TEST EDELMAN
        if ((this.trialType == TrialType.DET_TRIAL_EDELMAN_ORTHO_HOR || this.trialType == TrialType.DET_TRIAL_EDELMAN_SAME_AXIS_HOR) && (K != 1 || numSt != 2 || numFt != 3 || numSc != 1 || numFc != 1))
        {
            string Str = "UNITY >> TrialType [TEST_EDELMAN] is designed to work only for K = 1, numSt = 2, numFt = 3. Build the scene again!";
            sendDebugLogChannel.SendEnvInfoToPython(Str);
            //Assert.IsTrue(false, Str);
            K = 1;
        }
        // TEST GOKER
        if ((this.trialType == TrialType.DET_TRIAL_STATIC) && (K != 1 || numSt != 1 || numFt != 1 || numSc != 1 || numFc != 1))
        {
            string Str = "UNITY >> TrialType [TEST_STATIC] is designed to work only for K = 1, numSt = 1, numFt = 1. Build the scene again!";
            sendDebugLogChannel.SendEnvInfoToPython(Str);
            //Assert.IsTrue(false, Str);
            K = 1;
        }

        var compTmp = (int)envParams.GetWithDefault("trainComparisonType", -1f);
        if (compTmp != -1)
        {
            //sendDebugLogChannel.SendEnvInfoToPython("HERE");

            trainCompType = (TrainComparisonType)compTmp;
        }
        


        if (trainCompType == TrainComparisonType.GROUP && this.trialType != TrialType.RND_TRIAL)
        {
            string Str = "UNITY >> Comparison Type [GROUP] can only work with Trial Type [TRAIN].";
            sendDebugLogChannel.SendEnvInfoToPython(Str);
            this.trialType = TrialType.RND_TRIAL;
            //Assert.IsTrue(false);
        }

        sendDebugLogChannel.SendEnvInfoToPython("UNITY>> TrialType: " + this.trialType.ToString());
        sendDebugLogChannel.SendEnvInfoToPython("UNITY>> trainComparisonType: " + trainCompType.ToString());

        GameObject tmpL = GameObject.Find("Lights").gameObject;
        for (int i = 0; i < tmpL.transform.childCount; i++)
        {
            lights.Add(tmpL.transform.GetChild(i).gameObject);
        }

        if (saveFramesOnDisk)
        {
            AfterObservationCollected += SaveFrames;
            var d = new DirectoryInfo(Application.dataPath + savingDatasetDir);
            d.Delete(true);
            d.Create();

        }

        Random.InitState(2);

    }

    private void QuitApplication()
    {
        UnityEngine.Debug.Log("Quitting application");
        // save any game data here
#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif
    }

    private void PrepareAndStartEpisode(Batch batch)
    {
        GameObject datasetObj = GameObject.Find("ActiveDataset");
        UnityEngine.Debug.Log("Prepare and start batched episode.");
        //GameObject datasetsStr = GameObject.Find(nameDataset);
        if (datasetObj == null)
        {
            datasetObj = new GameObject("ActiveDataset");
        }
        //GameObject separator = GameObject.Find("Separator");
        foreach (var (gm, clsIdx) in datasetList)
        {
            Destroy(gm);
        }
        datasetList.Clear();
        int idxObjs = 0;
        foreach (KeyValuePair<string, GameObject> entry in batch.path_to_gm)
        {
            var gm = entry.Value;
            gm.SetActive(true);
            gm.transform.position = new Vector3(0f, 0f, 0f);
            gm.transform.parent = datasetObj.transform;

            datasetList.Add((gm, batch.path_to_class[entry.Key]));
            datasetList[datasetList.Count - 1].gm.transform.position = new Vector3(15 * idxObjs, 0, 0);

            idxObjs += 1;
        }
        setScene();
        UnityEngine.Debug.Break();
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
            foreach ((ObjectCameras q,  ClassIdxDummy x) in training)
            {
                foreach (SequenceCameras s in q.sequences)
                {
                    foreach (var f in s.cameraObjs)
                    {
                        Gizmos.DrawSphere(f.transform.position, 0.5F);
                        Gizmos.DrawWireSphere(f.transform.position, 0.5F);

                    }
                }

            }
            Gizmos.color = new Vector4(0, 1F, 0F, 0.5F);
            foreach ((ObjectCameras c, ClassIdxDummy x) in candidates)
            {
                foreach (SequenceCameras s in c.sequences)
                {
                    foreach (var f in s.cameraObjs)
                    {
                        Gizmos.DrawSphere(f.transform.position, 0.5F);
                        Gizmos.DrawWireSphere(f.transform.position, 0.5F);

                    }
                }

            }
            Gizmos.color = new Vector4(1, 0F, 0F, 0.5F);

            foreach (var obj in gizmoTrainingObj)
            {
                Gizmos.color = new Vector4(153 / 255F, 51 / 255F, 1F, 0.2F);
                Gizmos.DrawWireSphere(obj.transform.position, simParams.distance);
                Gizmos.DrawSphere(obj.transform.position, simParams.distance);

            }
            foreach (var obj in gizmoCandidateObjs)
            {
                Gizmos.color = new Vector4(153 / 255F, 51 / 255F, 1F, 0.2F);
                Gizmos.DrawWireSphere(obj.transform.position, simParams.distance);
                Gizmos.DrawSphere(obj.transform.position, simParams.distance);

            }

            if (GizmoCamHistory)
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
            if (GizmoCamMidpoint)
            {
                foreach (var mp in gizmoMiddlePointsAreaC)
                {
                    Gizmos.color = new Vector4(0F, 1F, 0F, 0.5F);
                    Gizmos.DrawSphere(mp, 0.2F);
                }
                foreach (var mp in gizmoMiddlePointsAreaT)
                {
                    Gizmos.color = new Vector4(0F, 0F, 1F, 0.5F);

                    Gizmos.DrawSphere(mp, 0.3F);
                }
            }
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
        public float areaInclTcameras = 1;
        public float areaAziTcameras = 359;

        public float areaInclCcameras = 1;
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
        if (numSc == 0)
        {
            return (trainingObjIdx, -1);
        }
        int candidateObjIdx = 0;

        candidateObjIdx = trainingObjIdx;

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
                                var nameGroup = datasetList[trainingObjIdx].gm.name.Split('.')[0];
                                List<int> allIndices = new List<int>(datasetList.Select((go, i) => new { GameObj = go, Index = i })
                                                                                 .Where(x => x.GameObj.gm.name.Split('.')[0] == nameGroup)
                                                                                 .Select(x => x.Index));
                                candidateObjIdx = allIndices[Random.Range(0, allIndices.Count)];
                                //UnityEngine.Debug.Log("TRAIN OBJ: " + datasetList[trainingObjIdx].name + " CAND OBJ: " + datasetList[candidateObjIdx].name);
                                break;
                            case var ss when nameDataset.Contains("GokerCuboids"):
                                var nameGroupC = datasetList[trainingObjIdx].gm.name.Split('_')[0];
                                List<int> allIndicesC = new List<int>(datasetList.Select((go, i) => new { GameObj = go, Index = i })
                                                                                 .Where(x => x.GameObj.gm.name.Split('_')[0] == nameGroupC)
                                                                                 .Select(x => x.Index));
                                candidateObjIdx = allIndicesC[Random.Range(0, allIndicesC.Count)];
                                //UnityEngine.Debug.Log("TRAIN OBJ: " + datasetList[trainingObjIdx].name + " CAND OBJ: " + datasetList[candidateObjIdx].name);
                                break;
                            default:
                                Assert.IsTrue(false);
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

        var objPosT = datasetList[trainingObjIdx].gm.transform.position;
        var objToLookAtT = datasetList[trainingObjIdx].gm.transform;


        float azimuthCenterPointT = RandomAngle(simParams.minCenterPointAziTcameras, simParams.maxCenterPointAziTcameras, simParams.minCenterPointAziTcameras, simParams.maxCenterPointAziTcameras);
        float inclinationCenterPointT = RandomAngle(simParams.minCenterPointInclTcameras, simParams.maxCenterPointInclTcameras, simParams.minCenterPointInclTcameras, simParams.maxCenterPointInclTcameras);
        var middlePointTarea = GetPositionAroundSphere(inclinationCenterPointT, azimuthCenterPointT, Vector3.up) * simParams.distance;
        gizmoMiddlePointsAreaT.Add(middlePointTarea + objPosT);

        float azimuthSequence = 0f;
        float inclinationSequence = 0f;
        var delta = Random.Range(simParams.minDegreeFrames, simParams.maxDegreeFrames);


        if (numSc > 0)
        {
            float azimuthCenterPointC = RandomAngle(azimuthCenterPointT + simParams.minAziTCcameras,
                                               azimuthCenterPointT + simParams.maxAziTCcameras,
                                               simParams.minCenterPointAziCcameras, simParams.maxCenterPointAziCcameras);
            float inclinationCenterPointC = RandomAngle(inclinationCenterPointT + simParams.minInclTCcameras,
                                                        inclinationCenterPointT + simParams.maxInclTCcameras,
                                                        simParams.minCenterPointInclCcameras, simParams.maxCenterPointInclCcameras);
            var objPosC = datasetList[candidateObjIdx].gm.transform.position;
            var objToLookAtC = datasetList[candidateObjIdx].gm.transform;

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
                    candidates[k].objCamera.sequences[sc].cameraObjs[fsc].transform.position = objPosC + Quaternion.AngleAxis(delta * (fsc - numFc / 2), randomDirection) * middlePointSequenceC;
                    candidates[k].objCamera.sequences[sc].cameraObjs[fsc].transform.LookAt(objToLookAtC, Vector3.up);
                    //candidates[k].sequences[sc].frames[fsc].GetComponent<Camera>().cullingMask = 1 << (8 + candidateObjIdx);
                    cameraPositions.Add(candidates[k].objCamera.sequences[sc].cameraObjs[fsc].transform.position - objPosC);
                }
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
                training[k].objCamera.sequences[st].cameraObjs[fst].transform.position = objPosT + Quaternion.AngleAxis(delta * (fst - numFt / 2), randomDirection) * middlePointSequenceT;
                training[k].objCamera.sequences[st].cameraObjs[fst].transform.LookAt(objToLookAtT);
                //training[k].sequences[st].frames[fst].GetComponent<Camera>().cullingMask = 1 << (8 + trainingObjIdx);
                cameraPositions.Add(training[k].objCamera.sequences[st].cameraObjs[fst].transform.position - objPosT);
            }
        }
        var diffAzi = azimuthCenterPointT - azimuthSequence;
        var diffIncl = inclinationCenterPointT - inclinationSequence;
        var diffPos = simParams.distance * GetPositionAroundSphere(diffIncl + 90f, diffAzi - 90, Vector3.up);

        gizmoPointHistoryCenterRelativeCT[k].Add(diffPos + objPosT);
    }

    public void SetCameraPositionEdelman(int k, int trainingObjIdx, int candidateObjIdx)
    {
        int candidateCamDegree = (int)envParams.GetWithDefault("degree", (numEpisodes - 1) * 10); //(numEpisodes - 1) * 10);
        int candidateCamRotation = (int)envParams.GetWithDefault("rotation", 10 * (((numEpisodes - 1) * 10) / 360));

        candidateCamRotation = candidateCamRotation % 360;
        candidateCamDegree = candidateCamDegree % 360;

        var objPosT = datasetList[trainingObjIdx].gm.transform.position;
        var objToLookAtT = datasetList[trainingObjIdx].gm.transform;

        var objPosC = datasetList[candidateObjIdx].gm.transform.position;
        var objToLookAtC = datasetList[candidateObjIdx].gm.transform;

        List<Vector3> positionsTcameras = new List<Vector3>();

        int sc = 0;
        int fsc = 0;
        Vector3 positionObj = new Vector3(0f, 0f, 0f);
        if (trialType == TrialType.DET_TRIAL_EDELMAN_SAME_AXIS_HOR)
        {
            positionObj = GetPositionAroundSphere(90, candidateCamRotation + candidateCamDegree, Vector3.up) * simParams.distance;
        }

        if (trialType == TrialType.DET_TRIAL_EDELMAN_ORTHO_VER)
        {
            positionObj = GetPositionAroundSphere(90 + candidateCamRotation, (90 + candidateCamRotation) % 360 > 180 ? candidateCamDegree + 180 : candidateCamDegree, Vector3.up) * simParams.distance;
        }
        if (trialType == TrialType.DET_TRIAL_EDELMAN_ORTHO_HOR)
        {
            int rotation_ortho = candidateCamRotation;
            if (candidateCamDegree > 90 && candidateCamDegree < 270)
            {
                rotation_ortho += 180;
            }
            UnityEngine.Debug.Log("D: " + (90 + candidateCamDegree).ToString() + "R: " + candidateCamRotation);
            positionObj = GetPositionAroundSphere(90 + candidateCamDegree, rotation_ortho, Vector3.up) * simParams.distance;

        }

        if (trialType == TrialType.DET_TRIAL_EDELMAN_SAME_AXIS_VER)
        {
            positionObj = GetPositionAroundSphere(90 + candidateCamRotation + candidateCamDegree, (90 + candidateCamRotation + candidateCamDegree) % 360 > 180 ? 180 : 0, Vector3.up) * simParams.distance;

        }


        candidates[k].objCamera.sequences[sc].cameraObjs[fsc].transform.position = objPosC + positionObj;
        candidates[k].objCamera.sequences[sc].cameraObjs[fsc].transform.LookAt(objToLookAtC, Vector3.up);
        //candidates[k].sequences[sc].frames[fsc].GetComponent<Camera>().cullingMask = 1 << (8 + candidateObjIdx);
        cameraPositions.Add(candidates[k].objCamera.sequences[sc].cameraObjs[fsc].transform.position - objPosC);

        for (int st = 0; st < numSt; st++)
        {
            if (trialType == TrialType.DET_TRIAL_EDELMAN_ORTHO_HOR || trialType == TrialType.DET_TRIAL_EDELMAN_SAME_AXIS_HOR)
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
                    positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation + 60, Vector3.up) * simParams.distance);
                    positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation + 75, Vector3.up) * simParams.distance);
                    positionsTcameras.Add(GetPositionAroundSphere(90, candidateCamRotation + 90, Vector3.up) * simParams.distance);

                }
            }

            if (trialType == TrialType.DET_TRIAL_EDELMAN_ORTHO_VER || trialType == TrialType.DET_TRIAL_EDELMAN_SAME_AXIS_VER)
            {
                if (st == 0)
                {

                    positionsTcameras.Clear();
                    positionsTcameras.Add(GetPositionAroundSphere(candidateCamRotation + 75, (candidateCamRotation + 75) % 360 < 180 ? 0 : 180, Vector3.up) * simParams.distance);
                    positionsTcameras.Add(GetPositionAroundSphere(candidateCamRotation + 90, (candidateCamRotation + 90) % 360 < 180 ? 0 : 180, Vector3.up) * simParams.distance);
                    positionsTcameras.Add(GetPositionAroundSphere(candidateCamRotation + 105, (candidateCamRotation + 105) % 360 < 180 ? 0 : 180, Vector3.up) * simParams.distance);

                }
                if (st == 1)
                {
                    positionsTcameras.Clear();
                    positionsTcameras.Add(GetPositionAroundSphere(candidateCamRotation + 0, (candidateCamRotation + 0) % 360 < 180 ? 0 : 180, Vector3.up) * simParams.distance);
                    positionsTcameras.Add(GetPositionAroundSphere(candidateCamRotation + 15, (candidateCamRotation + 15) % 360 < 180 ? 0 : 180, Vector3.up) * simParams.distance);
                    positionsTcameras.Add(GetPositionAroundSphere(candidateCamRotation + 30, (candidateCamRotation + 30) % 360 < 180 ? 0 : 180, Vector3.up) * simParams.distance);
                    //UnityEngine.Debug.Log("ROTATION: " + candidateCamRotation);

                }
            }

            for (int fst = 0; fst < numFt; fst++)
            {
                training[k].objCamera.sequences[st].cameraObjs[fst].transform.position = objPosT + positionsTcameras[fst];
                training[k].objCamera.sequences[st].cameraObjs[fst].transform.LookAt(objToLookAtT);
                //training[k].sequences[st].frames[fst].GetComponent<Camera>().cullingMask = 1 << (8 + trainingObjIdx);
                //debugPointsTraining.Add(training[k].sequences[st].frames[fst].transform.position);
                //trainingCameraPosRelativeToObj[indexTraining] = positionsTcameras[fst];
                //indexTraining += 1;
                cameraPositions.Add(training[k].objCamera.sequences[st].cameraObjs[fst].transform.position - objPosT);

            }
        }
    }

    public void SetStaticCameraPositionFromPython(int k, int trainingObjIdx, int candidateObjIdx)
    {
        //int candidateCamDegree = (int)envParams.GetWithDefault("degree", (numEpisodes - 1) * 10);
        if (numSt > 0)
        {
            int trainingCamAzimuth = (int)envParams.GetWithDefault("azimuthT", (numEpisodes - 1) * 2);
            int trainingCamInclination = (int)envParams.GetWithDefault("inclinationT", (numEpisodes - 1) * 2);
            var objPosT = datasetList[trainingObjIdx].gm.transform.position;
            var objToLookAtT = datasetList[trainingObjIdx].gm.transform;
            var positionCamT = GetPositionAroundSphere(trainingCamInclination, trainingCamAzimuth, Vector3.up) * simParams.distance;
            training[0].objCamera.sequences[0].cameraObjs[0].transform.position = objPosT + positionCamT;
            training[0].objCamera.sequences[0].cameraObjs[0].transform.LookAt(objToLookAtT, Vector3.up);
            cameraPositions.Add(training[0].objCamera.sequences[0].cameraObjs[0].transform.position - objPosT);
        }

        if (numSc > 0)
        {
            var objPosC = datasetList[candidateObjIdx].gm.transform.position;
            var objToLookAtC = datasetList[candidateObjIdx].gm.transform;
            int candidateCamInclination = (int)envParams.GetWithDefault("inclinationC", (numEpisodes - 1) * 10);
            int candidateCamAzimuth = (int)envParams.GetWithDefault("azimuthC", (numEpisodes - 1) * 10);
            var positionCamC = GetPositionAroundSphere(candidateCamInclination, candidateCamAzimuth, Vector3.up) * simParams.distance;

            candidates[0].objCamera.sequences[0].cameraObjs[0].transform.position = objPosC + positionCamC;
            candidates[0].objCamera.sequences[0].cameraObjs[0].transform.LookAt(objToLookAtC, Vector3.up);
            cameraPositions.Add(candidates[0].objCamera.sequences[0].cameraObjs[0].transform.position - objPosC);
        }

    }

    public (int t, int c) GetObjIdxFromChannel()
    {
        int trainingObjIdx = (int)envParams.GetWithDefault("objT", 0f);
        int candidateObjIdx = (int)envParams.GetWithDefault("objC", 1f);
        return (trainingObjIdx, candidateObjIdx);

    }

    public void setScene()
    {
        if (changeLightsEachTrial)
        {
            for (int i = 0; i < lights.Count; i++)
            {
                lights[i].GetComponent<Light>().intensity = Random.Range(0.0f, 0.6f);
            }
        }
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

        ClearVarForEpisode();

        for (int k = 0; k < K; k++)
        {
            int trainingObjIdx;
            int candidateObjIdx;
            if (trialType == TrialType.RND_TRIAL)
            {
                (trainingObjIdx, candidateObjIdx) = GetObjIdxRandom();
            }
            else
            {
                (trainingObjIdx, candidateObjIdx) = GetObjIdxFromChannel();
            }

            training[k].classIdx.classIdx = trainingObjIdx;

            if (numSc > 0)
            {
                gizmoCandidateObjs.Add(datasetList[candidateObjIdx].gm);
                indexSelectedObjects.Add(candidateObjIdx);
                candidates[k].classIdx.classIdx = candidateObjIdx;
            }
            indexSelectedObjects.Add(trainingObjIdx);

            gizmoTrainingObj.Add(datasetList[trainingObjIdx].gm);
            switch (trialType)
            {
                case TrialType.RND_TRIAL:
                    SetCameraPositionsRandom(k, trainingObjIdx, candidateObjIdx);
                    break;
                case TrialType.DET_TRIAL_EDELMAN_ORTHO_HOR:
                case TrialType.DET_TRIAL_EDELMAN_SAME_AXIS_HOR:
                case TrialType.DET_TRIAL_EDELMAN_SAME_AXIS_VER:
                case TrialType.DET_TRIAL_EDELMAN_ORTHO_VER:
                    SetCameraPositionEdelman(k, trainingObjIdx, candidateObjIdx);
                    break;
                case TrialType.DET_TRIAL_STATIC:
                    SetStaticCameraPositionFromPython(k, trainingObjIdx, candidateObjIdx);
                    break;

            }

            numEpisodes += 1;
            this.RequestDecision();
        }
    }
    public override void OnEpisodeBegin()
    {
        if (useBatchProvider)
        {
            if (numEpisodes == 0)
            {
                StartCoroutine(batchProvider.StartWhenReady());
                batchRepeated += 1;

            }
            else
            {
                if (batchRepeated < repeatSameBatch || repeatSameBatch == -1)
                {
                    //UnityEngine.Debug.Log(batchRepeated);
                    batchRepeated += 1;
                    setScene();
                }
                else
                {
                    StartCoroutine(batchProvider.StartWhenReady());
                    batchRepeated = 1;

                }
            }
        }
        else
        {
            setScene();
        }
    }
    
    public void SaveFrames()
    {
        foreach ((ObjectCameras objCamera, ClassIdxDummy classIdx) in training)
        {
            foreach (var sequence in objCamera.sequences)
            {
                foreach (var cameraObj in sequence.cameraObjs)
                {
                    var camera = cameraObj.GetComponent<Camera>();
                    var texture = CameraSensor.ObservationToTexture(camera, sizeCanvas, sizeCanvas);
                    var compressed = texture.EncodeToPNG();
                    
                    //ByteArrayToFile("C:/prova.png", compressed);
                    File.WriteAllBytes(Application.dataPath + savingDatasetDir + "//prova.png", compressed);
                    //using (Image image = Image.FromStream(new MemoryStream(compressed)))
                    //{
                    //    image.Save("C://Users//valer//Desktop//prova.png", ImageFormat.Png);  // Or Png
                    //}
                }
            }
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
        AfterObservationCollected();
    }



    public override void OnActionReceived(float[] vectorAction)
    {
        OnEpisodeBegin();
    }

}

[ExecuteInEditMode]
[CustomEditor(typeof(SequenceLearningTask))]
public class SequenceMLtaskEditor : Editor
{
    private SequenceLearningTask slt;
    public void OnEnable()
    {
        slt = (SequenceLearningTask)target;
    }
    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying)
        {
            base.OnInspectorGUI();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Gizmo Camera History"), GUILayout.Width(180));
            slt.GizmoCamHistory = EditorGUILayout.Toggle(slt.GizmoCamHistory) == true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Gizmo Midpoint History"), GUILayout.Width(180));
            slt.GizmoCamMidpoint = EditorGUILayout.Toggle(slt.GizmoCamMidpoint) == true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Change Lights Each Trial"), GUILayout.Width(180));
            slt.changeLightsEachTrial = EditorGUILayout.Toggle(slt.changeLightsEachTrial) == true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Trial Type"), GUILayout.Width(120));
            //slt.trialType = (SequenceLearningTask.TrialType)((int)(SequenceLearningTask.TrialType)EditorGUILayout.EnumPopup((SequenceLearningTask.TrialType)((int)(slt.trialType) << 1)) >> 1);
            slt.trialType = (SequenceLearningTask.TrialType)EditorGUILayout.EnumPopup(slt.trialType, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            if (slt.trialType == SequenceLearningTask.TrialType.RND_TRIAL)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Comparison Type"), GUILayout.Width(120));
                slt.trainCompType = (SequenceLearningTask.TrainComparisonType)EditorGUILayout.EnumPopup(slt.trainCompType, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }

            if (!slt.useBatchProvider)
            {
                EditorGUILayout.BeginHorizontal();
                //[Tooltip("Accepts combinations of datasets with + e.g. LeekSS+LeekSD")]
                EditorGUILayout.LabelField(new GUIContent("Dataset Name"), GUILayout.Width(120));
                slt.nameDataset = EditorGUILayout.TextField(slt.nameDataset, GUILayout.Width(200));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal(); 
            EditorGUILayout.LabelField(new GUIContent("Use BatchProvider"), GUILayout.Width(120));
            slt.useBatchProvider = EditorGUILayout.Toggle(slt.useBatchProvider) == true;
            EditorGUILayout.EndHorizontal();

            if (slt.useBatchProvider)
            {
                slt.batchProvider = (BatchProvider)EditorGUILayout.ObjectField(slt.batchProvider, typeof(BatchProvider), true);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Repeat Same Batch"), GUILayout.Width(150));
                slt.repeatSameBatch = EditorGUILayout.IntField(slt.repeatSameBatch, GUILayout.Width(50));
                if (slt.repeatSameBatch == -1)
                {
                EditorGUILayout.LabelField(new GUIContent("Repeat Forever"), GUILayout.Width(120));

                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Save Frames"), GUILayout.Width(120));
                slt.saveFramesOnDisk = EditorGUILayout.Toggle(slt.saveFramesOnDisk) == true;
                EditorGUILayout.EndHorizontal();

                if (slt.saveFramesOnDisk)
                {
                    EditorGUILayout.BeginHorizontal();
                    //[Tooltip("Accepts combinations of datasets with + e.g. LeekSS+LeekSD")]
                    EditorGUILayout.LabelField(new GUIContent("Saving Dir"), GUILayout.Width(120));
                    slt.savingDatasetDir = EditorGUILayout.TextField(slt.savingDatasetDir, GUILayout.Width(200));
                    EditorGUILayout.EndHorizontal();
                }



            }
        }
    }
}
