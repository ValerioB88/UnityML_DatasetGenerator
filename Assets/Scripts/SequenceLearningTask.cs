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

    List<(GameObject gm, int classIdx, int objIdx)> batchDatasetList = new List<(GameObject gm, int classIdx, int objIdx)>();

    List<Vector3> cameraPositions = new List<Vector3>();

    [HideInInspector]
    public bool GizmoCamHistory = true;
    [HideInInspector]
    public bool GizmoAreaMidPoints = true;
    
    [HideInInspector]
    public bool changeLightsEachIteration = false;
    
    StringLogSideChannel sendEnvParamsChannel;
    StringLogSideChannel sendDebugLogChannel;
    
    [HideInInspector]
    public BatchProvider batchProvider;
    
    [HideInInspector]
    public bool useBatchProvider = false;

    [HideInInspector]
    public int repeatSameBatch = -1;

    int imgsSaved = 0;
    private int batchRepeated = 0;

    [HideInInspector]
    public bool saveFramesOnDisk;
    
    [HideInInspector]
    public string saveDatasetDir = "../generated_datasets/mydat/"; 

    int numSt = 1;
    int numSc = 1;
    int numFt = 4;
    int numFc = 1;
    int numCameraSets = 2;
    int sizeCanvas = 64;

    int indexBatch = -1;
    [HideInInspector]
    public string nameDataset = "none";
    float newLevel = 0f;

    public event System.Action AfterObservationCollected; 

    public class SequenceCameras
    {
        public List<GameObject> cameraObjs = new List<GameObject>();
    }

    public class ObjectCameraSet
    {
        public List<SequenceCameras> sequences = new List<SequenceCameras>();
        public int classIdx = 0;
        public int objIdx = 0;
        public int batchIdx = 0;
        public ObjectCameraSet(int cI, int oI)
        {
            classIdx = cI;
            objIdx = oI; 
        }
    }
    public class ClassIdxDummy
    {
        public int classIdx;
        public ClassIdxDummy(int idx)
        {
            classIdx = idx; 
        }
    }
    List<ObjectCameraSet> candidates = new List<ObjectCameraSet >();
    List<ObjectCameraSet> training = new List<ObjectCameraSet>();


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

    [HideInInspector]
    public PlaceCamerasMode placeCameraMode = PlaceCamerasMode.RND;
    [HideInInspector]
    public int numGridPointAzi = 10;
    [HideInInspector]
    public int numGridPointIncl = 10;

    [HideInInspector]
    public GetLabelsMode getLabelsMode = GetLabelsMode.RND;
    [HideInInspector]
    public TrainComparisonType trainCompType = TrainComparisonType.ALL;
    Stopwatch stopWatchBatches = new Stopwatch();

    [HideInInspector]
    public List<float> aziGridPoints = new List<float>();
    [HideInInspector]
    public List<float> inclGridPoints = new List<float>();

    public int seed = 3;
    public CameraSphereParams cameraSphereParams = new CameraSphereParams();

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
        var cmlSeed = Helper.GetArg("-seed");
        if (cmlSeed != null)
        {
            seed = int.Parse(cmlSeed);
            Random.InitState(seed);
        }
        else
        {
            Random.InitState(seed);
        }
        UnityEngine.Debug.Log("Seed set at " + seed);

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

    public enum GetLabelsMode
    {
        RND,
        SEQUENTIAL,
        FROM_CHANNEL
    }
    public enum PlaceCamerasMode
    {
        RND,
        FROM_CHANNEL,
        REPEAT_SEQUENTIAL
    }

    public enum TrainComparisonType
    {
        ALL,
        GROUP
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
                batchDatasetList.Add((newObj.gameObject, totChildren, totChildren));
                batchDatasetList[batchDatasetList.Count - 1].gm.transform.position = new Vector3(15 * totChildren, 0, 0);
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


    void Start()
    {
        stopWatchBatches.Start();
         GameObject cameraContainer = GameObject.Find("CameraContainer");
        info = GameObject.Find("Info");

        string infostr = info.transform.GetChild(0).name;
        var tmp = infostr.Split('_');
        numCameraSets = int.Parse(tmp[0].Split(':')[1]);
        numSt = int.Parse(tmp[1].Split(':')[1]);
        numSc = int.Parse(tmp[2].Split(':')[1]);
        numFt = int.Parse(tmp[3].Split(':')[1]);
        numFc = int.Parse(tmp[4].Split(':')[1]);
        sizeCanvas = int.Parse(tmp[5].Split(':')[1]);

        sendDebugLogChannel.SendEnvInfoToPython("SLT From Unity Info: \nK: " + numCameraSets.ToString() + " St: " + numSt.ToString() + " Sc: " + numSc.ToString() + " Ft: " + numFt.ToString() + " Fc: " + numFc.ToString() + " size_canvas: " + sizeCanvas.ToString());
        int totIndex = 0;
        string tmpName;
        for (int k = 0; k < numCameraSets; k++)
        {
            gizmoPointHistoryCenterRelativeCT.Add(new List<Vector3>());
            training.Add(new ObjectCameraSet(0, 0));
           
            for (int sq = 0; sq < numSt; sq++)
            {
                training[k].sequences.Add(new SequenceCameras());

                for (int fsq = 0; fsq < numFt; fsq++)
                {
                    tmpName = "T" + k.ToString("D3") + "S" + sq.ToString("D3") + "F" + fsq.ToString("D3");
                    training[k].sequences[sq].cameraObjs.Add(cameraContainer.transform.Find(tmpName).gameObject);
                    totIndex += 1;
                }
            }
        }

        totIndex = 0;
        if (numSc > 0)
        {
            for (int k = 0; k < numCameraSets; k++)
            {
                candidates.Add(new ObjectCameraSet(0, 0));
                for (int sc = 0; sc < numSc; sc++)
                {
                    candidates[k].sequences.Add(new SequenceCameras());

                    for (int fsc = 0; fsc < numFc; fsc++)
                    {
                        tmpName = "C" + k.ToString("D3") + "S" + sc.ToString("D3") + "F" + fsc.ToString("D3");
                        candidates[k].sequences[sc].cameraObjs.Add(cameraContainer.transform.Find(tmpName).gameObject);
                        totIndex += 1;
                    }
                }
            }
        }
        string cmlNameDataset = null;
      
        var cmlUseBatchProv = Helper.GetArg("-use_batch_provider");
        if (cmlUseBatchProv != null)
        {
            useBatchProvider = int.Parse(cmlUseBatchProv) == 1;
            UnityEngine.Debug.Log("Using batch provider from the command line");
        }


        cmlNameDataset = Helper.GetArg("-dataset");
        if (cmlNameDataset != null)
        {
            nameDataset = cmlNameDataset;
            UnityEngine.Debug.Log("New Dataset from command line: " + nameDataset);
        }

        var cmlSaveFramesPath = Helper.GetArg("-save_frames_path");
        if (cmlSaveFramesPath != null)
        {
            saveFramesOnDisk = true;
            saveDatasetDir = cmlSaveFramesPath;
        }
        //else
        //{
        //    saveFramesOnDisk = false;
        //}

        var cmlRepeatBatch = Helper.GetArg("-repeat_same_batch");
        if (cmlRepeatBatch != null)
        {
            repeatSameBatch = int.Parse(cmlRepeatBatch);
        }
        var cmlPlaceCamera = Helper.GetArg("-place_camera_mode");
        if (cmlPlaceCamera != null)
        {
            placeCameraMode = (PlaceCamerasMode)int.Parse(cmlPlaceCamera);
            if (placeCameraMode == PlaceCamerasMode.REPEAT_SEQUENTIAL)
            {
                var cmlAziGridPoints = Helper.GetArg("-azi_n");
                if (cmlAziGridPoints != null)
                {
                    numGridPointAzi = int.Parse(cmlAziGridPoints);
                }
                var cmlInclGridPoints = Helper.GetArg("-incl_n");
                if (cmlInclGridPoints != null)
                {
                    numGridPointIncl = int.Parse(cmlInclGridPoints);
                }
                repeatSameBatch = numGridPointAzi * numGridPointIncl;
            }
        }
        var cmlGetLabels = Helper.GetArg("-get_labels_mode");
        if (cmlGetLabels != null)
        {
            getLabelsMode = (GetLabelsMode)int.Parse(cmlGetLabels);
        }
        var cmlTrainComparisonType = Helper.GetArg("-train_comparison_type");
        if (cmlTrainComparisonType != null)
        {
            trainCompType = (TrainComparisonType)int.Parse(cmlTrainComparisonType);
        }
        var cmlChangeLights = Helper.GetArg("-change_lights");
        if (cmlChangeLights != null)
        {
            changeLightsEachIteration = int.Parse(cmlChangeLights) == 1;
        }
    
        
        if (useBatchProvider)
        {
            if (cmlNameDataset != null)
            {
                batchProvider.filePathDataset = nameDataset;
            }
            batchProvider.InitBatchProvider();
            batchProvider.ActionReady += PrepareAndStartEpisode;
            batchProvider.RequestedExhaustedDataset += QuitApplication;
            sendEnvParamsChannel.SendEnvInfoToPython((numCameraSets).ToString());  // this is not totally accurate, but it's what needed most of the time
            if (GameObject.Find("DATASET") != null)
             GameObject.Find("DATASETS").SetActive(false);
        }
        else
        { 
            FillDataset();
            sendEnvParamsChannel.SendEnvInfoToPython((batchDatasetList.Count).ToString());
        }
        envParams = Academy.Instance.EnvironmentParameters;


        
        // TEST GOKER
        if ((placeCameraMode == PlaceCamerasMode.FROM_CHANNEL || getLabelsMode == GetLabelsMode.FROM_CHANNEL) && (numCameraSets != 1 || numSt != 1 || numFt != 1 || numSc != 1 || numFc != 1))
        {
            string Str = "UNITY >> PlaceCameraMode/GetLabelsMode [FROM CHANNEL] is designed to work only for K = 1, numSt = 1, numFt = 1. Build the scene again!";
            sendDebugLogChannel.SendEnvInfoToPython(Str);
            //Assert.IsTrue(false, Str);
            numCameraSets = 1;
        }       


        if (trainCompType == TrainComparisonType.GROUP && placeCameraMode != PlaceCamerasMode.RND)
        {
            string Str = "UNITY >> Comparison Type [GROUP] can only work with Place Camera Mode [RND].";
            sendDebugLogChannel.SendEnvInfoToPython(Str);
            placeCameraMode = PlaceCamerasMode.RND;
            //Assert.IsTrue(false);
        }
        
        if (placeCameraMode == PlaceCamerasMode.REPEAT_SEQUENTIAL)
        {
            for (int i = 0; i < numGridPointAzi; i++)
            {
                for (int j = 0; j < numGridPointIncl; j++)
                {
                    aziGridPoints.Add(cameraSphereParams.minCenterPointAziTcameras + (cameraSphereParams.maxCenterPointAziTcameras - cameraSphereParams.minCenterPointAziTcameras) / numGridPointAzi * i);

                    inclGridPoints.Add(cameraSphereParams.minCenterPointInclTcameras + (cameraSphereParams.maxCenterPointInclTcameras - cameraSphereParams.minCenterPointInclTcameras) / numGridPointIncl * j);
                }
            }
        }

        sendDebugLogChannel.SendEnvInfoToPython("UNITY>> placeCameraMode: " + placeCameraMode.ToString());
        sendDebugLogChannel.SendEnvInfoToPython("UNITY>> getLabelsMode: " + getLabelsMode.ToString());
        sendDebugLogChannel.SendEnvInfoToPython("UNITY>> changeLightsEachTrials: " + changeLightsEachIteration.ToString());
        sendDebugLogChannel.SendEnvInfoToPython("UNITY>> trainComparisonType: " + trainCompType.ToString());

        GameObject tmpL = GameObject.Find("Lights").gameObject;
        for (int i = 0; i < tmpL.transform.childCount; i++)
        {
            lights.Add(tmpL.transform.GetChild(i).gameObject);
        }

        if (saveFramesOnDisk)
        {
            AfterObservationCollected += SaveFrames;
            if (batchProvider.startFromObjectIdx == 0)
            {
                UnityEngine.Debug.Log("DELETING FOLDER");
                var d = new DirectoryInfo(Path.Combine(new string[] { Application.dataPath, "..", saveDatasetDir }));
                if (d.Exists)
                {
                    d.Delete(true);
                }
                if (useBatchProvider)
                {
                    foreach (var className in batchProvider.idxClassToName)
                    {
                        new DirectoryInfo(Path.Combine(new string[] { Application.dataPath, "..", saveDatasetDir, className.Value })).Create();
                    }
                }
                else
                {
                    foreach (var item in batchDatasetList)
                    {
                        new DirectoryInfo(Path.Combine(new string[] { Application.dataPath, "..", saveDatasetDir, item.classIdx.ToString() })).Create();

                    }
                }
            }
        }
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
        indexBatch += 1;
        GameObject datasetObj = GameObject.Find("ActiveDataset");
        //UnityEngine.Debug.Log("Prepare and start batched episode.");
        //GameObject datasetsStr = GameObject.Find(nameDataset);
        if (datasetObj == null)
        {
            datasetObj = new GameObject("ActiveDataset");
        }
        foreach (var (gm, clsIdx, objIdx) in batchDatasetList)
        {
            Destroy(gm);
        }

        batchDatasetList.Clear();
        int idxObjs = 0;
        foreach (KeyValuePair<string, (GameObject gm, int classIdx, int objIdx)> entry in batch.pathToGm)
        {
            var gm = entry.Value.gm;
            gm.SetActive(true);
            gm.transform.position = new Vector3(0f, 0f, 0f);
            gm.transform.parent = datasetObj.transform;
            var v = batch.pathToGm[entry.Key];
            batchDatasetList.Add((gm, entry.Value.classIdx, entry.Value.objIdx));
            batchDatasetList[batchDatasetList.Count - 1].gm.transform.position = new Vector3(15 * idxObjs, 0, 0);

            idxObjs += 1;
        }
        Destroy(batch.batchContainer);
        setScene();
        //UnityEngine.Debug.Break();
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
            foreach (ObjectCameraSet t in training)
            {
                foreach (SequenceCameras s in t.sequences)
                {
                    foreach (var f in s.cameraObjs)
                    {
                        Gizmos.DrawSphere(f.transform.position, 0.5F);
                        Gizmos.DrawWireSphere(f.transform.position, 0.5F);

                    }
                }

            }
            Gizmos.color = new Vector4(0, 1F, 0F, 0.5F);
            foreach (ObjectCameraSet c in candidates)
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
                Gizmos.DrawWireSphere(obj.transform.position, cameraSphereParams.distance);
                Gizmos.DrawSphere(obj.transform.position, cameraSphereParams.distance);

            }
            foreach (var obj in gizmoCandidateObjs)
            {
                Gizmos.color = new Vector4(153 / 255F, 51 / 255F, 1F, 0.2F);
                Gizmos.DrawWireSphere(obj.transform.position, cameraSphereParams.distance);
                Gizmos.DrawSphere(obj.transform.position, cameraSphereParams.distance);

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
            if (GizmoAreaMidPoints)
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

    [System.Serializable]

    public class CameraSphereParams
    {
        public float distance = 4f;
        // TRAINING
        // Min and Max for Training Cameras, from 0 to 180 to cover the whole sphere (relative to the world)
        public float minCenterPointInclTcameras = 30;
        public float maxCenterPointInclTcameras = 120;

        // This is almost always 0-360
        public float minCenterPointAziTcameras = 0;
        public float maxCenterPointAziTcameras = 360;

        // CANDIDATE 
        public float minCenterPointInclCcameras = 30;
        public float maxCenterPointInclCcameras = 120;

        public float minCenterPointAziCcameras = 0;
        public float maxCenterPointAziCcameras = 360;

        // from 0 to 180 / 360, it's degree and direction of the area around the center point for training cameras.
        public float areaInclTcameras = 0;
        public float areaAziTcameras = 360;

        public float areaInclCcameras = 0;
        public float areaAziCcameras = 360;

        // DISTANCE BETWEEN TRAINING AND CANDIDATE CAMREAS
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
        //candidateIndexSelectedObjects.Clear();
        //trainingIndexSelectedObjects.Clear();
        //debugRotation.Clear();

        if (numEpisodes % 100 == 0)
        {
            gizmoMiddlePointsSequenceT.Clear();
            gizmoMiddlePointsSequenceC.Clear();
            for (int k = 0; k < numCameraSets; k++)
            {
                gizmoPointHistoryCenterRelativeCT[k].Clear();
            }
        }

        gizmoMiddlePointsAreaC.Clear();
        gizmoMiddlePointsAreaT.Clear();
    }

    public (int idxTraining, int idxCandidate) GetObjIdxRandom()
    {
        var trainingObjIdx = Random.Range(0, batchDatasetList.Count);
        if (numSc == 0)
        {
            return (trainingObjIdx, -1);
        }
        int candidateObjIdx = 0;

        candidateObjIdx = trainingObjIdx;

        if (Random.Range(0f, 1f) > cameraSphereParams.probMatching && batchDatasetList.Count > 1)
        {
            do
            {
                switch (trainCompType)
                {
                    case TrainComparisonType.ALL:
                        candidateObjIdx = Random.Range(0, batchDatasetList.Count);
                        break;
                    case TrainComparisonType.GROUP:
                        switch (nameDataset)
                        {
                            case var ss when nameDataset.Contains("ShapeNet"):
                                var nameGroup = batchDatasetList[trainingObjIdx].gm.name.Split('.')[0];
                                List<int> allIndices = new List<int>(batchDatasetList.Select((go, i) => new { GameObj = go, Index = i })
                                                                                 .Where(x => x.GameObj.gm.name.Split('.')[0] == nameGroup)
                                                                                 .Select(x => x.Index));
                                candidateObjIdx = allIndices[Random.Range(0, allIndices.Count)];
                                //UnityEngine.Debug.Log("TRAIN OBJ: " + datasetList[trainingObjIdx].name + " CAND OBJ: " + datasetList[candidateObjIdx].name);
                                break;
                            case var ss when nameDataset.Contains("GokerCuboids"):
                                var nameGroupC = batchDatasetList[trainingObjIdx].gm.name.Split('_')[0];
                                List<int> allIndicesC = new List<int>(batchDatasetList.Select((go, i) => new { GameObj = go, Index = i })
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

        var objPosT = batchDatasetList[trainingObjIdx].gm.transform.position;
        var objToLookAtT = batchDatasetList[trainingObjIdx].gm.transform;


        float azimuthCenterPointT = RandomAngle(cameraSphereParams.minCenterPointAziTcameras, cameraSphereParams.maxCenterPointAziTcameras, cameraSphereParams.minCenterPointAziTcameras, cameraSphereParams.maxCenterPointAziTcameras);
        float inclinationCenterPointT = RandomAngle(cameraSphereParams.minCenterPointInclTcameras, cameraSphereParams.maxCenterPointInclTcameras, cameraSphereParams.minCenterPointInclTcameras, cameraSphereParams.maxCenterPointInclTcameras);
        var middlePointTarea = GetPositionAroundSphere(inclinationCenterPointT, azimuthCenterPointT, Vector3.up) * cameraSphereParams.distance;
        gizmoMiddlePointsAreaT.Add(middlePointTarea + objPosT);

        float azimuthSequence = 0f;
        float inclinationSequence = 0f;
        var delta = Random.Range(cameraSphereParams.minDegreeFrames, cameraSphereParams.maxDegreeFrames);


        if (numSc > 0)
        {
            float azimuthCenterPointC = RandomAngle(azimuthCenterPointT + cameraSphereParams.minAziTCcameras,
                                               azimuthCenterPointT + cameraSphereParams.maxAziTCcameras,
                                               cameraSphereParams.minCenterPointAziCcameras, cameraSphereParams.maxCenterPointAziCcameras);
            float inclinationCenterPointC = RandomAngle(inclinationCenterPointT + cameraSphereParams.minInclTCcameras,
                                                        inclinationCenterPointT + cameraSphereParams.maxInclTCcameras,
                                                        cameraSphereParams.minCenterPointInclCcameras, cameraSphereParams.maxCenterPointInclCcameras);
            var objPosC = batchDatasetList[candidateObjIdx].gm.transform.position;
            var objToLookAtC = batchDatasetList[candidateObjIdx].gm.transform;

            var middlePointCarea = GetPositionAroundSphere(inclinationCenterPointC, azimuthCenterPointC, Vector3.up) * cameraSphereParams.distance;
            gizmoMiddlePointsAreaC.Add(middlePointCarea + objPosC);

            for (int sc = 0; sc < numSc; sc++)
            {
                azimuthSequence = RandomAngle(azimuthCenterPointC - cameraSphereParams.areaAziCcameras / 2F, azimuthCenterPointC + cameraSphereParams.areaAziCcameras / 2F, cameraSphereParams.minCenterPointAziCcameras, cameraSphereParams.maxCenterPointAziCcameras);
                inclinationSequence = RandomAngle(inclinationCenterPointC - cameraSphereParams.areaInclCcameras / 2F, inclinationCenterPointC + cameraSphereParams.areaInclCcameras / 2F, cameraSphereParams.minCenterPointInclCcameras, cameraSphereParams.maxCenterPointInclCcameras);

                middlePointSequenceC = GetPositionAroundSphere(inclinationSequence, azimuthSequence, Vector3.up) * cameraSphereParams.distance;

                var randomDirection = Vector3.Normalize(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(1f, 1f)));

                gizmoMiddlePointsSequenceC.Add(middlePointSequenceC + objPosC);


                for (int fsc = 0; fsc < numFc; fsc++)
                {
                    candidates[k].sequences[sc].cameraObjs[fsc].transform.position = objPosC + Quaternion.AngleAxis(delta * (fsc - numFc / 2), randomDirection) * middlePointSequenceC;
                    candidates[k].sequences[sc].cameraObjs[fsc].transform.LookAt(objToLookAtC, Vector3.up);
                    //candidates[k].sequences[sc].frames[fsc].GetComponent<Camera>().cullingMask = 1 << (8 + candidateObjIdx);
                    cameraPositions.Add(candidates[k].sequences[sc].cameraObjs[fsc].transform.position - objPosC);
                }
            }
        }
        for (int st = 0; st < numSt; st++)
        {
            //UnityEngine.Debug.Log("DELTA: " + delta);
            azimuthSequence = RandomAngle(azimuthCenterPointT - cameraSphereParams.areaAziTcameras / 2F, azimuthCenterPointT + cameraSphereParams.areaAziTcameras / 2F, cameraSphereParams.minCenterPointAziTcameras, cameraSphereParams.maxCenterPointAziTcameras);
            inclinationSequence = RandomAngle(inclinationCenterPointT - cameraSphereParams.areaInclTcameras / 2F, inclinationCenterPointT + cameraSphereParams.areaInclTcameras / 2F, cameraSphereParams.minCenterPointInclTcameras, cameraSphereParams.maxCenterPointInclTcameras);
            inclGridPoints.Add((int)inclinationSequence);
            aziGridPoints.Add((int)azimuthSequence);

            middlePointSequenceT = GetPositionAroundSphere(inclinationSequence, azimuthSequence, Vector3.up) * cameraSphereParams.distance;
            gizmoMiddlePointsSequenceT.Add(middlePointSequenceT + objPosT);

            var randomDirection = Vector3.Normalize(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)));

            for (int fst = 0; fst < numFt; fst++)
            {
                training[k].sequences[st].cameraObjs[fst].transform.position = objPosT + Quaternion.AngleAxis(delta * (fst - numFt / 2), randomDirection) * middlePointSequenceT;
                training[k].sequences[st].cameraObjs[fst].transform.LookAt(objToLookAtT);
                //training[k].sequences[st].frames[fst].GetComponent<Camera>().cullingMask = 1 << (8 + trainingObjIdx);
                cameraPositions.Add(training[k].sequences[st].cameraObjs[fst].transform.position - objPosT);
            }
        }
        var diffAzi = azimuthCenterPointT - azimuthSequence;
        var diffIncl = inclinationCenterPointT - inclinationSequence;
        var diffPos = cameraSphereParams.distance * GetPositionAroundSphere(diffIncl + 90f, diffAzi - 90, Vector3.up);

        gizmoPointHistoryCenterRelativeCT[k].Add(diffPos + objPosT);
    }

   
    public void SetRepeatedCameraSequence(int k, int trainingObjIdx, int candidateObjIdx)
    {
        var objPosT = batchDatasetList[trainingObjIdx].gm.transform.position;
        var objToLookAtT = batchDatasetList[trainingObjIdx].gm.transform;
        var positionCamT = GetPositionAroundSphere(inclGridPoints[batchRepeated - 1], aziGridPoints[batchRepeated - 1], Vector3.up) * cameraSphereParams.distance;
        training[k].sequences[0].cameraObjs[0].transform.position = objPosT + positionCamT;
        training[k].sequences[0].cameraObjs[0].transform.LookAt(objToLookAtT, Vector3.up);
        cameraPositions.Add(training[k].sequences[0].cameraObjs[0].transform.position - objPosT);
        gizmoMiddlePointsSequenceC.Add(objPosT + positionCamT);


    }
    public void SetStaticCameraPositionFromPython(int k, int trainingObjIdx, int candidateObjIdx)
    {
        //int candidateCamDegree = (int)envParams.GetWithDefault("degree", (numEpisodes - 1) * 10);
        if (numSt > 0)
        {
            int trainingCamAzimuth = (int)envParams.GetWithDefault("azimuthT", (numEpisodes - 1) * 2);
            int trainingCamInclination = (int)envParams.GetWithDefault("inclinationT", (numEpisodes - 1) * 2);
            var objPosT = batchDatasetList[trainingObjIdx].gm.transform.position;
            var objToLookAtT = batchDatasetList[trainingObjIdx].gm.transform;
            var positionCamT = GetPositionAroundSphere(trainingCamInclination, trainingCamAzimuth, Vector3.up) * cameraSphereParams.distance;
            training[0].sequences[0].cameraObjs[0].transform.position = objPosT + positionCamT;
            training[0].sequences[0].cameraObjs[0].transform.LookAt(objToLookAtT, Vector3.up);
            cameraPositions.Add(training[0].sequences[0].cameraObjs[0].transform.position - objPosT);
        }

        if (numSc > 0)
        {
            var objPosC = batchDatasetList[candidateObjIdx].gm.transform.position;
            var objToLookAtC = batchDatasetList[candidateObjIdx].gm.transform;
            int candidateCamInclination = (int)envParams.GetWithDefault("inclinationC", (numEpisodes - 1) * 10);
            int candidateCamAzimuth = (int)envParams.GetWithDefault("azimuthC", (numEpisodes - 1) * 10);
            var positionCamC = GetPositionAroundSphere(candidateCamInclination, candidateCamAzimuth, Vector3.up) * cameraSphereParams.distance;

            candidates[0].sequences[0].cameraObjs[0].transform.position = objPosC + positionCamC;
            candidates[0].sequences[0].cameraObjs[0].transform.LookAt(objToLookAtC, Vector3.up);
            cameraPositions.Add(candidates[0].sequences[0].cameraObjs[0].transform.position - objPosC);
        }

    }

    public (int t, int c) GetObjIdxFromChannel()
    {
        int trainingObjIdx = (int)envParams.GetWithDefault("objT", 0f);
        int candidateObjIdx = (int)envParams.GetWithDefault("objC", 0f);
        return (trainingObjIdx, candidateObjIdx);

    }

    private (int, int) GetSequentialIdx(int k)
    {
        if (k >= batchDatasetList.Count)
        {
            (int t, int c) = (Random.Range(0, batchDatasetList.Count), Random.Range(0, batchDatasetList.Count));
            return (t, c);
        }
        else
            return (k, k);
    }
    public void setScene()
    {
        //UnityEngine.Debug.Log("SET SCENE" + batchRepeated);
        if (changeLightsEachIteration)
        {
            for (int i = 0; i < lights.Count; i++)
            {
                lights[i].GetComponent<Light>().intensity = Random.Range(0.0f, 1f);
            }
        }
        newLevel = envParams.GetWithDefault("newLevel", newLevel);
        // This default is used ONLY IF THE "newLevel" IS NEVER RECEIVED! Otherwise the default is the PREVIOUS one!!!

        if (newLevel == 1f)
        {
            cameraSphereParams.UpdateParameters(envParams);
            sendDebugLogChannel.SendEnvInfoToPython("UNITY >> Parameters Updated: \nDistance [" + cameraSphereParams.distance.ToString() + "]" +
            "\nCenterPointInclTcameras: [" + cameraSphereParams.minCenterPointInclTcameras + "," + cameraSphereParams.maxCenterPointInclTcameras + "]" +
            "\nCenterPointInclCcameras: [" + cameraSphereParams.minCenterPointInclCcameras + "," + cameraSphereParams.maxCenterPointInclCcameras + "]" +
            "\nareaInclTcameras: [" + cameraSphereParams.areaInclTcameras + "]" +
            "\nareaAziTcameras: [" + cameraSphereParams.areaAziTcameras + "]" +
            "\nDegreeFrames: [" + cameraSphereParams.minDegreeFrames + "," + cameraSphereParams.maxDegreeFrames + "]" +
            "\nareaInclCcameras: [" + cameraSphereParams.areaInclCcameras + "]" +
            "\nareaAziCcameras: [" + cameraSphereParams.areaAziCcameras + "]" +
            "\nprobMatching: [" + cameraSphereParams.probMatching + "]" +
            "\n<< UNITY.");
        }

        ClearVarForEpisode();

        for (int k = 0; k < numCameraSets; k++)
        {
            int trainingObjIdx = -1;
            int candidateObjIdx = -1;
            switch (getLabelsMode)
            {
                case GetLabelsMode.RND:
                    (trainingObjIdx, candidateObjIdx) = GetObjIdxRandom();
                    break;
                case GetLabelsMode.SEQUENTIAL:
                    (trainingObjIdx, candidateObjIdx) = GetSequentialIdx(k);
                    break;
                case GetLabelsMode.FROM_CHANNEL:
                    (trainingObjIdx, candidateObjIdx) = GetObjIdxFromChannel();
                    break;
                default:
                    Assert.IsTrue(false);
                    break;
            }

            training[k].classIdx = batchDatasetList[trainingObjIdx].classIdx;
            training[k].objIdx = batchDatasetList[trainingObjIdx].objIdx;
            training[k].batchIdx = trainingObjIdx;

            
            if (numSc > 0)
            {
                gizmoCandidateObjs.Add(batchDatasetList[candidateObjIdx].gm);
                //candidateIndexSelectedObjects.Add(candidateObjIdx);
                candidates[k].classIdx = batchDatasetList[candidateObjIdx].classIdx;
                candidates[k].objIdx = batchDatasetList[candidateObjIdx].objIdx;
                candidates[k].batchIdx = candidateObjIdx;
            }
            //trainingIndexSelectedObjects.Add(trainingObjIdx);

            gizmoTrainingObj.Add(batchDatasetList[trainingObjIdx].gm);
            switch (placeCameraMode)
            {
                case PlaceCamerasMode.RND:
                    SetCameraPositionsRandom(k, trainingObjIdx, candidateObjIdx);
                    break;
                case PlaceCamerasMode.FROM_CHANNEL:
                    SetStaticCameraPositionFromPython(k, trainingObjIdx, candidateObjIdx);
                    break;
                case PlaceCamerasMode.REPEAT_SEQUENTIAL:
                    SetRepeatedCameraSequence(k, trainingObjIdx, candidateObjIdx);
                    break;

            }

            RequestDecision();
        }
        numEpisodes += 1;
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
                    batchRepeated = 1;
                    StartCoroutine(batchProvider.StartWhenReady());
                    //StartCoroutine(waitForBatch());

                    

                }
            }
        }
        else
        {
            if (batchRepeated < repeatSameBatch || repeatSameBatch == -1)
            {
                batchRepeated += 1;
                setScene();
            }
            else 
            {
                QuitApplication();
            }
        }
    }
    
    public void SaveFrames()
    {
        void SaveObjCameraSet(List<ObjectCameraSet> objCameraSet, string append)
        {
            int idxCameraSet = 0;
            foreach (ObjectCameraSet objCamera in objCameraSet)
            {
                int indexSeq = 0;
                foreach (var sequence in objCamera.sequences)
                {
                    int indexCam = 0;
                    foreach (var cameraObj in sequence.cameraObjs)
                    {
                        var camera = cameraObj.GetComponent<Camera>();
                        var texture = CameraSensor.ObservationToTexture(camera, sizeCanvas, sizeCanvas);
                        var compressed = texture.EncodeToPNG();
                        
                        string imageName = "O" + objCamera.objIdx + "_I" + inclGridPoints[batchRepeated - 1] + "_A" + aziGridPoints[batchRepeated - 1];
                        File.WriteAllBytes(Path.Combine(new string[] { Application.dataPath, "..", saveDatasetDir, 
                            useBatchProvider? batchProvider.idxClassToName[objCamera.classIdx]:objCamera.classIdx.ToString(),
                            imageName + ".png" }), compressed);
                        indexCam += 1;
                        imgsSaved += 1;

                    }
                    indexSeq += 1;
                }
                idxCameraSet += 1;
            }
        }

        SaveObjCameraSet(training, "t");
        SaveObjCameraSet(candidates, "c");

        string msg = System.DateTime.Now.ToString("HH:mm:ss") + " ";
        if (indexBatch % 10 == 0 && (batchRepeated -1) == 0)  // Change to ... batchRepeated == RepeatSameBatch
        {
            double elps = stopWatchBatches.Elapsed.TotalSeconds;
            msg += "10 Batches: " + elps + " sec, " + elps/((float)batchDatasetList.Count * 10) + "sec x obj \n";
            stopWatchBatches.Restart();
        }
        
        if (useBatchProvider && batchRepeated - 1 == 0)
        {
            msg += "Image Saved: " + imgsSaved + ", ObjSaved:" + batchProvider.batchProvided * numCameraSets + "/" + batchProvider.TotObjects;
            UnityEngine.Debug.Log(msg);
            Helper.FileLog(msg, filename: "debugLog" + seed + ".txt");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        for (int i = 0; i < Mathf.Max(candidates.Count, training.Count); i++)
        {
            if (i < candidates.Count)
            {
                sensor.AddObservation(candidates[i].batchIdx);
            }
            if (i < training.Count)
            {
                sensor.AddObservation(training[i].batchIdx);
            }

        }

        //Support Camera Position, X, Y and Z
        foreach (Vector3 pos in cameraPositions)
        {
            sensor.AddObservation(pos);
        }
        if (AfterObservationCollected != null)
        {
            AfterObservationCollected();
        }
    }


    public override void Heuristic(float[] actionsOut)
    {
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        OnEpisodeBegin();
    }

}

#if UNITY_EDITOR

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

            //advancedCameraGrouping = EditorGUILayout.Foldout(advancedCameraGrouping, "Advanced Option for Cameras Grouping");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Gizmo Camera History"), GUILayout.Width(180));
            slt.GizmoCamHistory = EditorGUILayout.Toggle(slt.GizmoCamHistory) == true;
            EditorGUILayout.EndHorizontal();

            //EditorGUILayout.BeginHorizontal();
            //EditorGUILayout.LabelField(new GUIContent("Gizmo Midpoint History"), GUILayout.Width(180));
            //slt.GizmoAreaMidPoints = EditorGUILayout.Toggle(slt.GizmoAreaMidPoints) == true;
            //EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Change Lights Each Trial"), GUILayout.Width(180));
            slt.changeLightsEachIteration = EditorGUILayout.Toggle(slt.changeLightsEachIteration) == true;
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Get Labels Mode"), GUILayout.Width(120));
            //slt.trialType = (SequenceLearningTask.TrialType)((int)(SequenceLearningTask.TrialType)EditorGUILayout.EnumPopup((SequenceLearningTask.TrialType)((int)(slt.trialType) << 1)) >> 1);
            slt.getLabelsMode = (SequenceLearningTask.GetLabelsMode)EditorGUILayout.EnumPopup(slt.getLabelsMode, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent("Place Camera Mode"), GUILayout.Width(120));
            //slt.trialType = (SequenceLearningTask.TrialType)((int)(SequenceLearningTask.TrialType)EditorGUILayout.EnumPopup((SequenceLearningTask.TrialType)((int)(slt.trialType) << 1)) >> 1);
            SequenceLearningTask.PlaceCamerasMode tmp = (SequenceLearningTask.PlaceCamerasMode)EditorGUILayout.EnumPopup(slt.placeCameraMode, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            if (tmp != slt.placeCameraMode)
            {
                slt.placeCameraMode = tmp;
                if (slt.placeCameraMode == SequenceLearningTask.PlaceCamerasMode.REPEAT_SEQUENTIAL)
                {
                    SequenceBuildSceneCLI.numFc = 0;
                    SequenceBuildSceneCLI.numFt = 1;
                    SequenceBuildSceneCLI.numSt = 1;
                    SequenceBuildSceneCLI.numSc = 0;
                    SequenceBuildSceneCLI.UpdateComponents();
                }
            }

            if (slt.placeCameraMode == SequenceLearningTask.PlaceCamerasMode.RND)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Comparison Type"), GUILayout.Width(120));
                slt.trainCompType = (SequenceLearningTask.TrainComparisonType)EditorGUILayout.EnumPopup(slt.trainCompType, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }
            if (slt.placeCameraMode == SequenceLearningTask.PlaceCamerasMode.REPEAT_SEQUENTIAL)
            {

                EditorGUILayout.LabelField(new GUIContent("Num Grid Points"), GUILayout.Width(120));
                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(new GUIContent("H: "), GUILayout.Width(30));
                slt.numGridPointAzi = EditorGUILayout.IntField(slt.numGridPointAzi, GUILayout.Width(40));

                EditorGUILayout.LabelField(new GUIContent("V: "), GUILayout.Width(30));
                slt.numGridPointIncl = EditorGUILayout.IntField(slt.numGridPointIncl, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                if (SequenceBuildSceneCLI.numSt != 1 || SequenceBuildSceneCLI.numFt != 1 || SequenceBuildSceneCLI.numSc != 0 || SequenceBuildSceneCLI.numFc != 0)
                {
                    GUIStyle s = new GUIStyle(EditorStyles.label);
                    s.normal.textColor = Color.red;
                    EditorGUI.indentLevel--;

                    GUILayout.Label("WATCH OUT! REPEAT SEQUENTIAL SELECTED!\nThis only works with default parameters!\nPlease set numSt=1, numFt=1, numSc=0, numFc=0\nand regenerate.", s);
                }
                slt.repeatSameBatch = slt.numGridPointIncl * slt.numGridPointAzi;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Repeat Same Batch " + slt.repeatSameBatch + " times (HxV)"), GUILayout.Width(300));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Repeat Same Batch:", "Set to -1 to repeat just one batch forever"), GUILayout.Width(150));
                slt.repeatSameBatch = EditorGUILayout.IntField(slt.repeatSameBatch, GUILayout.Width(50));
                if (slt.repeatSameBatch == -1)
                {
                    EditorGUILayout.LabelField(new GUIContent("Repeat Forever"), GUILayout.Width(120));

                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Save Frames"), GUILayout.Width(120));
            slt.saveFramesOnDisk = EditorGUILayout.Toggle(slt.saveFramesOnDisk) == true;

            if (slt.saveFramesOnDisk)
            {;
                //[Tooltip("Accepts combinations of datasets with + e.g. LeekSS+LeekSD")]
                EditorGUILayout.LabelField(new GUIContent("Here: "), GUILayout.Width(50));
                slt.saveDatasetDir = EditorGUILayout.TextField(slt.saveDatasetDir, GUILayout.Width(200));
            }
            EditorGUILayout.EndHorizontal();




            EditorGUILayout.BeginHorizontal(); 
            EditorGUILayout.LabelField(new GUIContent("Use BatchProvider"), GUILayout.Width(120));
            slt.useBatchProvider = EditorGUILayout.Toggle(slt.useBatchProvider, GUILayout.Width(20)) == true;

            if (slt.useBatchProvider)
            {
                slt.batchProvider = (BatchProvider)EditorGUILayout.ObjectField(slt.batchProvider, typeof(BatchProvider), true);
                EditorGUILayout.EndHorizontal();

            }
            else
            {
                EditorGUILayout.EndHorizontal();
            }

            if (!slt.useBatchProvider)
            {
                EditorGUILayout.BeginHorizontal();
                //[Tooltip("Accepts combinations of datasets with + e.g. LeekSS+LeekSD")]
                EditorGUILayout.LabelField(new GUIContent("Dataset Name"), GUILayout.Width(120));
                slt.nameDataset = EditorGUILayout.TextField(slt.nameDataset, GUILayout.Width(200));
                EditorGUILayout.EndHorizontal();
                if (GameObject.Find(slt.nameDataset) == null)
                { 
                    GUIStyle s = new GUIStyle(EditorStyles.label);
                    s.normal.textColor = Color.red;
                    EditorGUI.indentLevel--;
                    GUILayout.Label("WATCH OUT! Dataset doesn't exist in /DATASETS", s);

                }
                else
                {
                    int numObjects = GameObject.Find(slt.nameDataset).transform.childCount;
                    if (numObjects != SequenceBuildSceneCLI.numCameraSets)
                    {
                        SequenceBuildSceneCLI.numCameraSets = numObjects;
                        SequenceBuildSceneCLI.UpdateComponents();
                    }
                }   
            }
        }
    }
}
#endif