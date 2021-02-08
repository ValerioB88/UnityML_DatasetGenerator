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

        string infoTxt = "K:" + K.ToString() + "_St:" + numSt.ToString() + "_Sc:" + numSc.ToString() + "_Ft:" + numFt.ToString() + "_Fc:" + numFc.ToString() + "_SC:" + sizeCanvas.ToString() + "_g:" + grayscale.ToString();
        UnityEngine.Debug.Log("BUILDING INFO TXT " + infoTxt);
        info.transform.GetChild(0).name = infoTxt;

        GameObject.Find("DebugText").GetComponent<Text>().text = infoTxt;
        List<CameraSensorComponent> camComp = new List<CameraSensorComponent>(agent.GetComponents<CameraSensorComponent>());
        foreach (CameraSensorComponent cam in camComp)
        {
            DestroyImmediate(cam);
        }

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
                    candidatesCameras[totIndexC].name = "C" + k.ToString("D3") + "S" + sc.ToString("D3") + "F" + fsc.ToString("D3");
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
                    trainingCameras[totIndexT].name = "T" + k.ToString("D3") + "S" + st.ToString("D3") + "F" + fst.ToString("D3");
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
        agent.GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize = (K * (numSc > 0 ? 2 : 1)) + (numFt * numSt + numFc * numSc) * K * 3;

#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
#endif

    }
    static void ParseAndBuild()
    {
        UnityEngine.Debug.Log("CIAO ZIO");
        numSt = int.Parse(Helper.GetArg("-nSt"));
        numSc = int.Parse(Helper.GetArg("-nSc"));
        numFt = int.Parse(Helper.GetArg("-nFt"));
        numFc = int.Parse(Helper.GetArg("-nFc"));
        grayscale = int.Parse(Helper.GetArg("-grayscale"));

        K = int.Parse(Helper.GetArg("-k"));
        sizeCanvas = int.Parse(Helper.GetArg("-size_canvas"));
        nameScene = Helper.GetArg("-name_scene");
        outputPath = Helper.GetArg("-output_path");
        buildOs = Helper.GetArg("-build_os"); // 'win' or 'linux'

#if UNITY_EDITOR
        UnityEngine.Debug.Log("NAME SCENE: " + nameScene);
        EditorSceneManager.OpenScene("Assets/Scenes/" + nameScene + ".unity", OpenSceneMode.Single);
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

#if UNITY_EDITOR

[ExecuteInEditMode]
[CustomEditor(typeof(SequenceBuildSceneCLI))]
public class BuildSettingsEditor : Editor
{
    // Start is called before the first frame update

    //[CanEditMultipleObjects]

    int K;
    int numSt;
    int numSc;
    int numFt;
    int numFc;
    int sizeCanvas;
    int grayscale;

    bool unsavedChanges = false;
    public Object source;
    public Object datasetToAdjust;

    GameObject infoDTA;
    void OnEnable()
    {
        GameObject info = GameObject.Find("Info");
        string infostr = info.transform.GetChild(0).name;
        var tmp = infostr.Split('_');
        K = int.Parse(tmp[0].Split(':')[1]);
        numSt = int.Parse(tmp[1].Split(':')[1]);
        numSc = int.Parse(tmp[2].Split(':')[1]);
        numFt = int.Parse(tmp[3].Split(':')[1]);
        numFc = int.Parse(tmp[4].Split(':')[1]);
        sizeCanvas = int.Parse(tmp[5].Split(':')[1]);
        grayscale = int.Parse(tmp[6].Split(':')[1]);
        infoDTA = info.transform.GetChild(1).gameObject;
        string infoDTAstr = infoDTA.name;
        datasetToAdjust = GameObject.Find(infoDTAstr.Split(':')[1]);
    }

    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying)
        {
            base.OnInspectorGUI();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Size Canvas"), GUILayout.Width(80));
            sizeCanvas = EditorGUILayout.IntSlider(sizeCanvas, 10, 250);
            // Grayscale works generally but not for BatchProvider. You need to convert the bytes to bitmap and convert manually to grayscale. It's a bit of a pain so not now.
            EditorGUILayout.LabelField(new GUIContent("Grayscale"), GUILayout.Width(80));
            grayscale = EditorGUILayout.Toggle(grayscale == 1) ? 1 : 0;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("K"), GUILayout.Width(20));
            K = EditorGUILayout.IntSlider(K, 1, 30);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("numSt"), GUILayout.Width(60));
            numSt = EditorGUILayout.IntSlider(numSt, 1, 5);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("numSc"), GUILayout.Width(60));
            numSc = EditorGUILayout.IntSlider(numSc, 0, 5);
            EditorGUILayout.EndHorizontal();

            if (numSt > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("numFt"), GUILayout.Width(60));
                numFt = EditorGUILayout.IntSlider(numFt, 1, 10);
                EditorGUILayout.EndHorizontal();
            }
            if (numSc > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("numFc"), GUILayout.Width(60));
                numFc = EditorGUILayout.IntSlider(numFc, 1, 10);
                EditorGUILayout.EndHorizontal();
            }


            if (unsavedChanges)
            {
                GUIStyle s = new GUIStyle(EditorStyles.label);
                s.normal.textColor = Color.red;
                GUILayout.Label("Changed.", s);
            }

            if (GUI.changed)
            {
                unsavedChanges = true;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("REGENERATE"))
            {
                SequenceBuildSceneCLI.numSt = numSt;
                SequenceBuildSceneCLI.numFc = numFc;
                SequenceBuildSceneCLI.numFt = numFt;
                SequenceBuildSceneCLI.numSc = numSc;
                SequenceBuildSceneCLI.K = K;
                SequenceBuildSceneCLI.sizeCanvas = sizeCanvas;
                SequenceBuildSceneCLI.grayscale = grayscale;
                SequenceBuildSceneCLI.UpdateComponents();
                unsavedChanges = false;
            }
            EditorGUILayout.EndHorizontal();
        }

    }

}
#endif


