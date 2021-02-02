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
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif 

class SequenceBuildSceneCLI : MonoBehaviour
{

    public static int numSt = 1;
    public static int numSc = 1;
    public static int numFt = 4;
    public static int numFc = 1;
    public static int grayscale = 1;
    public static int K = 2;
    public static int Q = 1; // always 1
    public static int sizeCanvas = 64;

    public static string nameDataset = "DatasetDebug";
    public static string nameScene = "SequenceMetaLearning";
    public static string outputPath = "";

    public static List<GameObject> candidatesCameras = new List<GameObject>();
    public static List<GameObject> trainingCameras = new List<GameObject>();

    public static string buildOs;

    static public void UpdateComponents()
    {
        trainingCameras.Clear();
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
        //for (int i = childs - 1; i >= 0; i--)
        //{
        //    DestroyImmediate(info.transform.GetChild(i).gameObject);
        //}

        string infoTxt = "K:" + K.ToString() + "_St:" + numSt.ToString() + "_Sc:" + numSc.ToString() + "_Ft:" + numFt.ToString() + "_Fc:" + numFc.ToString() + "_SC:" + sizeCanvas.ToString() + "_d:" + nameDataset.ToString() + "_g:" + grayscale.ToString();
        UnityEngine.Debug.Log("BUILDING INFO TXT " + infoTxt);
        info.transform.GetChild(0).name = infoTxt;

        //var infoObj = new GameObject(infoTxt);
        //infoObj.transform.parent = info.transform;
        GameObject.Find("DebugText").GetComponent<Text>().text = infoTxt;
        List<CameraSensorComponent> camComp = new List<CameraSensorComponent>(agent.GetComponents<CameraSensorComponent>());
        foreach (CameraSensorComponent cam in camComp)
        {
            DestroyImmediate(cam);
        }

        //    UnityEngine.Debug.Log("CIAO");

        int totIndexT = 0;
        int totIndexC = 0;

        for (int k = 0; k < K; k++)
        {
            GameObject candidateCamera = GameObject.Find("CandidateCamera"); // remember to take this out from the sensorlist
            for (int sc = 0; sc < numSc; sc++)
            {
                for (int fsc = 0; fsc < numFc; fsc++)
                {
                    candidatesCameras.Add(Instantiate(candidateCamera));
                    candidatesCameras[totIndexC].transform.parent = cameraContainer.transform;
                    candidatesCameras[totIndexC].name = "C" + k.ToString("D2") + "S" + sc + "F" + fsc;
                    candidatesCameras[totIndexC].SetActive(false);
                    totIndexC += 1;
                }
            }
           
        }

        for (int k = 0; k < K; k++)
        {
            GameObject trainingCamera = GameObject.Find("TrainingCamera"); // remember to take this out from the sensorlist
            for (int st = 0; st < numSt; st++)
            {
                for (int fst = 0; fst < numFt; fst++)
                {
                    trainingCameras.Add(Instantiate(trainingCamera));
                    trainingCameras[totIndexT].transform.parent = cameraContainer.transform;
                    trainingCameras[totIndexT].name = "T" + k.ToString("D2") + "S" + st + "F" + fst;
                    trainingCameras[totIndexT].SetActive(false);
                    totIndexT += 1;
                }
            }
        }

        //Add Cameras to Agent
        Assert.IsTrue(candidatesCameras.Count == K * numFc * numSc);
        Assert.IsTrue(trainingCameras.Count == K * numFt * numSt);


        foreach (GameObject traingCam in trainingCameras)
        {
            CameraSensorComponent cmtmp = agent.AddComponent<CameraSensorComponent>();
            cmtmp.Camera = traingCam.GetComponent<Camera>();
            cmtmp.SensorName = traingCam.name;
            cmtmp.Width = sizeCanvas;
            cmtmp.Height = sizeCanvas;
            cmtmp.Grayscale = grayscale == 1;

        }

        foreach (GameObject candidCam in candidatesCameras)  // N * K 
        {
            CameraSensorComponent cmtmp = agent.AddComponent<CameraSensorComponent>();
            cmtmp.Camera = candidCam.GetComponent<Camera>();
            cmtmp.SensorName = candidCam.name;
            cmtmp.Width = sizeCanvas;
            cmtmp.Height = sizeCanvas;

            cmtmp.Grayscale = grayscale == 1;

        }

        // Includes labels, and N*K+K*Q vector3s
        //UnityEngine.Debug.Log((N * K + K * Q) + 3 * (N * K + K * Q));
        // If numSc is 0, then we only send 1 set of labels (the training ones), otherwise we send two (training and the paired one)
        agent.GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize = (K * (numSc>0?2:1)) + (numFt * numSt + numFc * numSc) * K * 3;

#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
#endif

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
        grayscale = int.Parse(GetArg("-grayscale"));

        K = int.Parse(GetArg("-k"));
        sizeCanvas = int.Parse(GetArg("-size_canvas"));
        nameScene = GetArg("-name_scene");
        outputPath = GetArg("-output_path");
        buildOs = GetArg("-build_os"); // 'win' or 'linux'

#if UNITY_EDITOR
        UnityEngine.Debug.Log("NAME SCENE: " + nameScene);
        EditorSceneManager.OpenScene("Assets/Scenes/" + nameScene + ".unity", OpenSceneMode.Single );
#endif

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
