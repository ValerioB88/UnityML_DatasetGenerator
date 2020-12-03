using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents.Policies;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Diagnostics;
using UnityEngine.Assertions;
using UnityEngine.UI;

class SequenceBuildSceneCLI : MonoBehaviour
{

    public static int numSt = 1;
    public static int numSc = 1;
    public static int numFt = 4;
    public static int numFc = 1;
    public static int K = 2;
    public static int Q = 1; // always 1
    public static int sizeCanvas = 64;

    public static string nameDataset = "DatasetDebug";
    public static string nameScene = "SequenceMetaLearning";
    public static string outputPath = "";

    public static List<GameObject> candidatesCameras = new List<GameObject>();
    public static List<GameObject> queriesCameras = new List<GameObject>();

    public static string buildOs;

    static public void UpdateComponents()
    {
        queriesCameras.Clear();
        candidatesCameras.Clear();
        GameObject cameraContainer = GameObject.Find("CameraContainer");
        GameObject agent = GameObject.Find("Agent");
        int childs = cameraContainer.transform.childCount;
        for (int i = childs - 1; i >= 0; i--)
        {
            DestroyImmediate(cameraContainer.transform.GetChild(i).gameObject);
        }

        GameObject info = GameObject.Find("Info");
        childs = info.transform.childCount;
        for (int i = childs - 1; i >= 0; i--)
        {
            DestroyImmediate(info.transform.GetChild(i).gameObject);
        }

        string infoTxt = "K:" + K.ToString() + "_St:" + numSt.ToString() + "_Sc:" + numSc.ToString() + "_Ft:" + numFt.ToString() + "_Fc:" + numFc.ToString() + "_SC:" + sizeCanvas.ToString() + "_d:" + nameDataset.ToString();
        var infoObj = new GameObject(infoTxt);
        infoObj.transform.parent = info.transform;
        GameObject.Find("DebugText").GetComponent<Text>().text = infoTxt;
        List<CameraSensorComponent> camComp = new List<CameraSensorComponent>(agent.GetComponents<CameraSensorComponent>());
        foreach (CameraSensorComponent cam in camComp)
        {
            DestroyImmediate(cam);
        }

        //    UnityEngine.Debug.Log("CIAO");

        int totIndex = 0;
        for (int k = 0; k < K; k++)
        {
            GameObject candidateCamera = GameObject.Find("CandidateCamera"); // remember to take this out from the sensorlist
            for (int sc = 0; sc < numSc; sc++)
            {
                for (int fsc = 0; fsc < numFc; fsc++)
                {
                    candidatesCameras.Add(Instantiate(candidateCamera));
                    candidatesCameras[totIndex].transform.parent = cameraContainer.transform;
                    candidatesCameras[totIndex].name = "C" + k + "S" + sc + "F" + fsc;
                    candidatesCameras[totIndex].SetActive(false);
                    totIndex += 1;
                }
            }
        }

        totIndex = 0;
        for (int k = 0; k < K; k++)
        {
            GameObject queryCamera = GameObject.Find("QueryCamera"); // remember to take this out from the sensorlist
            for (int st = 0; st < numSt; st++)
            {
                for (int fst = 0; fst < numFt; fst++)
                {
                    queriesCameras.Add(Instantiate(queryCamera));
                    queriesCameras[totIndex].transform.parent = cameraContainer.transform;
                    queriesCameras[totIndex].name = "Q" + k + "S" + st + "F" + fst;
                    queriesCameras[totIndex].SetActive(false);
                    totIndex += 1;
                }
            }
        }

        //Add Cameras to Agent
        Assert.IsTrue(candidatesCameras.Count == K * numFc * numSc);
        Assert.IsTrue(queriesCameras.Count == K * numFt * numSt);


        foreach (GameObject queryCam in queriesCameras)
        {
            CameraSensorComponent cmtmp = agent.AddComponent<CameraSensorComponent>();
            cmtmp.Camera = queryCam.GetComponent<Camera>();
            cmtmp.SensorName = queryCam.name;
            cmtmp.Width = sizeCanvas;
            cmtmp.Height = sizeCanvas;

        }

        foreach (GameObject candidCam in candidatesCameras)  // N * K 
        {
            CameraSensorComponent cmtmp = agent.AddComponent<CameraSensorComponent>();
            cmtmp.Camera = candidCam.GetComponent<Camera>();
            cmtmp.SensorName = candidCam.name;
            cmtmp.Width = sizeCanvas;
            cmtmp.Height = sizeCanvas;
        }

        // Includes labels, and N*K+K*Q vector3s
        //UnityEngine.Debug.Log((N * K + K * Q) + 3 * (N * K + K * Q));
        agent.GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize = K + (numFt * numSt + numFc * numSc) * K * 3;

    }

    // Helper function for getting the command line arguments
    private static string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }
        UnityEngine.Debug.Log("I AM HERE READING YOUR COMMAND LINE");
        return null;
    }

    static void ParseAndBuild()
    {
        UnityEngine.Debug.Log("CIAO ZIO");
        numSt = int.Parse(GetArg("-nSt"));
        numSc = int.Parse(GetArg("-nSc"));
        numFt = int.Parse(GetArg("-nFt"));
        numFc = int.Parse(GetArg("-nFc"));

        K = int.Parse(GetArg("-k"));
        sizeCanvas = int.Parse(GetArg("-size_canvas"));
        nameScene = GetArg("-name_scene");
        outputPath = GetArg("-output_path");
        buildOs = GetArg("-build_os"); // 'win' or 'linux'

       // UnityEngine.Debug.Log("N :" + N.ToString() + " K: " + K.ToString());

        UpdateComponents();

#if UNITY_EDITOR
        // build 
        UnityEngine.Debug.Log("STARTING BUILDING");
        string[] scenes = { "Assets/Scenes/" + nameScene + ".unity" };
        //BuildPipeline.BuildPlayer(scenes, outputPath + "/scene.exe",
        //BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (string.Compare(buildOs, "win") == 0)
        {
            UnityEngine.Debug.Log("WINDOWS!");
            BuildPipeline.BuildPlayer(scenes, outputPath + "/scene.exe",
               BuildTarget.StandaloneWindows, BuildOptions.None);
        }
        else
        {

            UnityEngine.Debug.Log("LINUX!");
            BuildPipeline.BuildPlayer(scenes, outputPath + "/scene.x86_64",
               BuildTarget.StandaloneLinux64, BuildOptions.None);

        }
        UnityEngine.Debug.Log("Build done");
#endif

    }
}
