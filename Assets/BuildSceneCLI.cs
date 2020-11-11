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

class BuildSceneCLI : MonoBehaviour
{

    public static int N = 1;
    public static int K = 2;
    public static int Q = 1;
    public static int sizeCanvas = 64;
    public static string nameDataset = "DatasetDebug";
    public static string nameScene = "MetaLearning";
    public static string outputPath = "";

    public static List<GameObject> supportCameras = new List<GameObject>();
    public static List<GameObject> testingCameras = new List<GameObject>();

    public static string buildOs;

    static public void UpdateComponents()
    {
        testingCameras.Clear();
        supportCameras.Clear();
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

        string infoTxt = "N:" + N.ToString() + "_K:" + K.ToString() + "_Q:" + Q.ToString() + "_SC:" + sizeCanvas.ToString() + "_d:" + nameDataset.ToString();
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
            GameObject supportCamera = GameObject.Find("SupportCamera"); // remember to take this out from the sensorlist
            for (int n = 0; n < N; n++)
            {
                supportCameras.Add(Instantiate(supportCamera));
                supportCameras[totIndex].transform.parent = cameraContainer.transform;
                supportCameras[totIndex].name = (n + N * k).ToString("D3") + "_SupportCamera";
                supportCameras[totIndex].SetActive(false);
                totIndex += 1;
            }
        }

        int thisIndex = 0;
        for (int k = 0; k < K; k++)
        {
            GameObject testCamera = GameObject.Find("QueryCamera"); // remember to take this out from the sensorlist 
            for (int q = 0; q < Q; q++)
            {
                testingCameras.Add(Instantiate(testCamera));
                testingCameras[thisIndex].transform.parent = cameraContainer.transform;
                // we want the test Cameras to be sent at the end, and since it's sent to python in alph, we prefix with an increasing number
                testingCameras[thisIndex].name = (totIndex).ToString("D3") + "_QueryCamera";
                testingCameras[thisIndex].SetActive(false);
                totIndex += 1;
                thisIndex += 1;
            }
        }

        //Add Cameras to Agent
        Assert.IsTrue(supportCameras.Count == N * K);

        foreach (GameObject testingCam in testingCameras)
        {
            CameraSensorComponent cmtmp = agent.AddComponent<CameraSensorComponent>();
            cmtmp.Camera = testingCam.GetComponent<Camera>();
            cmtmp.SensorName = testingCam.name;
            cmtmp.Width = sizeCanvas;
            cmtmp.Height = sizeCanvas;

        }

        foreach (GameObject supportCam in supportCameras)  // N * K 
        {
            CameraSensorComponent cmtmp = agent.AddComponent<CameraSensorComponent>();
            cmtmp.Camera = supportCam.GetComponent<Camera>();
            cmtmp.SensorName = supportCam.name;
            cmtmp.Width = sizeCanvas;
            cmtmp.Height = sizeCanvas;
        }

        // Includes labels, and N*K+K*Q vector3s
        //UnityEngine.Debug.Log((N * K + K * Q) + 3 * (N * K + K * Q));
        agent.GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize =  ( N * K + K * Q ) + 3 * (N * K + K * Q);

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
        N = int.Parse(GetArg("-n_shot"));
        K = int.Parse(GetArg("-k_ways"));
        Q = int.Parse(GetArg("-q_queries"));
        sizeCanvas = int.Parse(GetArg("-size_canvas"));
        nameScene = GetArg("-name_scene");
        outputPath = GetArg("-output_path");
        buildOs = GetArg("-build_os"); // 'win' or 'linux'

        UnityEngine.Debug.Log("N :" + N.ToString() + " K: " + K.ToString());

        UpdateComponents();

#if UNITY_EDITOR
        // build 
        UnityEngine.Debug.Log("STARTING BUILDING");
        string[] scenes = { "Assets/Scenes/" + nameScene + ".unity" };
        //BuildPipeline.BuildPlayer(scenes, outputPath + "/scene.exe",
        //BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (string.Compare(buildOs, "win") == 0 )
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
