using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using System.Linq;
using System;

using AsImpL;
using System.Linq;

public class DatasetEnum : IEnumerator
{
    List<(string path, int classIdx)> _samples = new List<(string path, int classIdx)>();

    // Enumerators are positioned before the first element
    // until the first MoveNext() call.
    public int position = -1;

    public DatasetEnum(List<(string path, int classIdx)> samples)
    {
        _samples = samples;
    }

    public bool MoveNext()
    {
        position++;
        return (position < _samples.Count);
    }

    public void Reset()
    {
        position = -1;
    }

    object IEnumerator.Current
    {
        get
        {
            return Current;
        }
    }

    public (string path, int classIdx) Current
    {
        get
        {
            try
            {
                return _samples[position];
            }
            catch (IndexOutOfRangeException)
            {
                throw new InvalidOperationException();
            }
        }
    }
}

public static class Helper
{
    public static string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && args.Length > i + 1)
            {
                UnityEngine.Debug.Log(args[i] + ": " + args[i + 1]);
                return args[i + 1];
            }
        }
        return null;
    }
    public static void Shuffle<T>(this IList<T> ts)
    {
        var count = ts.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i)
        {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = ts[i];
            ts[i] = ts[r];
            ts[r] = tmp;
        }
    }

    public static void FileLog(string debugMsg, string filename = "debugLog.txt")
    {
        string path = Application.dataPath + "/" + filename;
        StreamWriter sw = new StreamWriter(path, true);
        sw.WriteLine(debugMsg + "\n");
        sw.Close();
    }

}

public class Dataset : IEnumerable
{
    List<(string path, int classIdx)> samples = new List<(string path, int classIdx)>();
    public void Add(string path, int classIdx)
    {
        string absolutePath = path.Contains("//") ? path : Path.GetFullPath(path);
        absolutePath = absolutePath.Replace('\\', '/');
        samples.Add((absolutePath, classIdx));
    }

    public void RemoveRange(int start, int count)
    {
        samples.RemoveRange(start, count);
    }

    public int Size
    {
        get
        {
            return samples.Count;
        }
    }

    public int Count
    {
        get
        {
            return samples.Count;
        }
    }
    public void Shuffle()
    {
        samples.Shuffle();
    }

    // Implementation for the GetEnumerator method.
    IEnumerator IEnumerable.GetEnumerator()
    {
        return (IEnumerator)GetEnumerator();
    }

    public DatasetEnum GetEnumerator()
    {
        return new DatasetEnum(samples);
    }
}


public class Batch
{
    public ObjectImporter batchImporter;
    public Dictionary<string, (GameObject gm, int classIdx, int objIdx)> pathToGm = new Dictionary<string, (GameObject, int, int)>(); // GameObject, class Index, Obj Index
    Dictionary<string, (int classIdx, int objIdx)> tmpMapPathIdx = new Dictionary<string, (int, int)>();

    public bool ready = false;
    public event Action<Batch> BatchImportComplete;
    public event Action<Batch> NoObjectsLoaded;
    public GameObject batchContainer;
    DatasetEnum datasetEnum;
    int objectsLoading = 0;
    public int size = 0;
    ImportOptions importOptions;
    BatchProvider btp;

    protected virtual void OnBatchImportComplete()
    {
        ready = true;
        if (BatchImportComplete != null)
        {
            BatchImportComplete(this);
        }
    }

    public void OnModelImported(GameObject gm, string path)
    {
        DatasetUtils.AdjustObject(gm);
        var v = tmpMapPathIdx[path];
        pathToGm.Add(path, (gm, v.classIdx, v.objIdx));
        gm.name += "_ADJ";
        objectsLoading--;
        btp.objsReady++;
        btp.objsLoading--;
    }

    public void OnImportError(string path)
    {
        UnityEngine.Debug.Log("<color=red>Error: FAILED TO LOAD: " + path + " </color>");
        objectsLoading--;
        btp.objsLoading--;
        btp.totExcluded++;
        TryImportObject();

    }
    public Batch(DatasetEnum dataEnum, int bSize, ImportOptions importOpt, GameObject gm, BatchProvider batchP)
    {
        btp = batchP;
        size = bSize;

        BatchImportComplete += btp.BatchReady;
        NoObjectsLoaded += btp.BatchFailedToLoad;
        datasetEnum = dataEnum;
        importOptions = importOpt;
        batchContainer = new GameObject("Batch");
        batchImporter = gm.AddComponent<ObjectImporter>();
        batchImporter.ImportingComplete += OnBatchImportComplete;
        batchImporter.ImportedModel += OnModelImported;
        batchImporter.ImportError += OnImportError;
        for (int i = 0; i < size; i++)
        {
            bool next = TryImportObject();
            if (!next)
            {
                break;
            }
        }
    }

    private bool TryImportObject()
    {
        bool next = datasetEnum.MoveNext();
        if (!next)
        {

            size = objectsLoading + pathToGm.Count;
            if (size == 0)
            {
                NoObjectsLoaded(this);
                return false;
            }

            if (objectsLoading > 0)
            {
                // The batch is still loading objects
                return false;
            }

            else
            {
                //// if  (objectLoading == 0 && batchSize>0)
                // The batch has finished
                OnBatchImportComplete();
                return true;
            }
        }
        else
        {
            objectsLoading++;
            btp.objsLoading++;
            btp.objsExtracted++;
            var v = datasetEnum.Current;
            tmpMapPathIdx[v.path] = (v.classIdx, datasetEnum.position);

            var nameObj = btp.idxClassToName[v.classIdx] + "_" + objectsLoading;
            batchImporter.ImportModelAsync(nameObj, v.path, batchContainer.transform, importOptions);
            return true;
        }
    }
}




public class BatchProvider : MonoBehaviour
{
    [HideInInspector]
    public int totExcluded = 0;
    [HideInInspector]
    public int objsLoading = 0;
    [HideInInspector]
    public int objsReady = 0;
    [HideInInspector]
    public int objsExtracted = 0;
    [HideInInspector]
    public int objsProvided = 0;
    [HideInInspector]
    public int batchProvided = 0;
    public Queue<Batch> ready;
    int counterLoadingBatches = 0;

    [Tooltip("Leave list empty to use all classes")]
    public List<string> selectClasses = new List<string> { };

    [SerializeField]
    public string filePathDataset = "../data/3D/ShapeNetLimited";


    public bool shuffleData = true;

    public Dictionary<int, string> idxClassToName = new Dictionary<int, string>();
    [HideInInspector]
    public int indexObjDataset = 0;
    [HideInInspector]
    public int startFromObjectIdx = 0;

    public int batchSize; // Batch size of -1 indicates to load all the dataset
    bool waitingForReady = false;
    string info = "";
    public event Action<Batch> ActionReady;

    public event Action RequestedExhaustedDataset;

    [HideInInspector]
    public int batchesToHaveReady = 3;
    private bool isInit = false;

    [Tooltip("Set to -1 to use all objects")]
    [HideInInspector]
    public int maxObjsPerClass = -1;

    [SerializeField]
    private ImportOptions importOptions = new ImportOptions();

    Dataset dataset;
    DatasetEnum dataEnum;

    private void Start()
    {
    }

    public int NumObjects
    {
        get
        {
            return dataset.Count;

        }
    }

    public void InitBatchProvider()
    {
        var cmlShuffle = Helper.GetArg("-shuffle_data");
        if (cmlShuffle != null)
        {
            shuffleData = int.Parse(cmlShuffle) == 1;
        }
        var cmlImportMat = Helper.GetArg("-import_material");
        if (cmlImportMat != null)
        {
            importOptions.importMaterial = int.Parse(cmlImportMat) == 1;
        }
        var cmlBatchSize = Helper.GetArg("-batch_size");
        if (cmlBatchSize != null)
        {
            batchSize = int.Parse(cmlBatchSize);
        }
        var cmlBatchesReady = Helper.GetArg("-batches_ready");
        if (cmlBatchesReady != null)
        {
            batchesToHaveReady = int.Parse(cmlBatchesReady);
        }
        var cmlStartFromObjIdx = Helper.GetArg("-start_from_object_idx");
        if (cmlStartFromObjIdx != null)
        {
            startFromObjectIdx = int.Parse(cmlStartFromObjIdx);
        }
        var cmlMaxObjPerClass = Helper.GetArg("-max_objs_per_class");
        if (cmlMaxObjPerClass != null)
        {
            maxObjsPerClass = int.Parse(cmlMaxObjPerClass);
        }
        var cmlSelectedClasses = Helper.GetArg("-selected_classes");
        if (cmlSelectedClasses != null)
        {
            selectClasses = cmlSelectedClasses.Split('_').ToList();
        }


        filePathDataset = Path.Combine(new string[] { Application.dataPath, "..", filePathDataset });
        ActionReady += CountProvided;
        ready = new Queue<Batch>();
        dataset = new Dataset();
        // Look in the dataset to create a DatasetList
        int classIdx = 0;
        var datasetDir = new DirectoryInfo(filePathDataset);
        UnityEngine.Debug.Log(datasetDir.FullName);

        foreach (var classdir in datasetDir.GetDirectories())
        {
            bool foundClass = false;
            int idxObjPerClass = 0;
            if (selectClasses.Count > 0)
            {
                foreach (var name in selectClasses)
                {
                    if (classdir.Name == name)
                    {
                        idxClassToName[classIdx] = classdir.Name;
                        foundClass = true;
                        break;
                    }
                }
            }
            else
            {
                foundClass = true;
            }
            if (!foundClass)
            {
                continue;
            }
            else
            {
                idxClassToName[classIdx] = classdir.Name;

                List<string> tmpCat = new List<string>();
                foreach (var obj in classdir.GetDirectories())
                {

                    tmpCat.Add(obj.FullName + "/models/model_normalized.obj");
                }
                tmpCat.Shuffle();

                for (int i = 0; i < Mathf.Min(maxObjsPerClass == -1 ? tmpCat.Count : maxObjsPerClass, tmpCat.Count); i++)
                {
                    dataset.Add(tmpCat[i], classIdx);
                }
                classIdx += 1;
            }
        }


        if (shuffleData)
        {
            dataset.Shuffle();
        }

        dataset.RemoveRange(0, startFromObjectIdx);
        dataEnum = dataset.GetEnumerator();

        if (batchSize == -1)
        {
            batchSize = dataset.Count;
            batchesToHaveReady = 1;
        }


        for (int i = 0; i < batchesToHaveReady; i++)
        {
            AddBatch();
        }
        isInit = true;
    }

    public void BatchReady(Batch batch)
    {
        GameObject.Destroy(batch.batchImporter);
        counterLoadingBatches--;
        ready.Enqueue(batch);
    }
    public void BatchFailedToLoad(Batch batch)
    {
        GameObject.Destroy(batch.batchImporter);
        counterLoadingBatches--;
        StopIfNoneLeft();
    }
    private void AddBatch()
    {

        counterLoadingBatches++;
        var batch = new Batch(dataEnum, batchSize, importOptions, gameObject, this);
    }

    public void StopIfNoneLeft()
    {
        if (ready.Count == 0 && counterLoadingBatches == 0)
        {
            UnityEngine.Debug.Log("Dataset Exhausted");
            RequestedExhaustedDataset();
        }
    }


    public IEnumerator StartWhenReady()
    {

        Resources.UnloadUnusedAssets();
        if (waitingForReady)
        {
            UnityEngine.Debug.Log("Already waiting for a batch");
            yield break;
        }
        if (ready.Count > 0)
        {
            ProvideBatch();
        }
        else
        {
            StopIfNoneLeft();
            waitingForReady = true;
            yield return new WaitUntil(() => ready.Count > 0);
            waitingForReady = false;

            ProvideBatch();
        }
    }

    private void ProvideBatch()
    {
        AddBatch();
        batchProvided++;
        var batch = ready.Dequeue();
        objsReady -= batch.size;
        ActionReady(batch);
    }

    private void CountProvided(Batch batch)
    {
        objsProvided += batch.size;
    }



    public int TotObjects
    {
        get
        {
            return dataset.Count;
        }
    }
    private void printInfo()
    {
        string tmpInfo = "ProvidedB: " + batchProvided + "; LoadingB: " + counterLoadingBatches + "; ReadyB: " + ready.Count + "; LoadingO: " + objsLoading + ";  ReadyO: " + objsReady + " ; ObjsProvided: " + objsProvided + "; ObjsExtracted: " + objsExtracted + ";  ObjsTot: " + TotObjects + "; ObjExcluded: " + totExcluded + "\nPercCompleted: " + 100 * objsProvided / (float)TotObjects + "%";
        if (tmpInfo != info)
        {
            info = tmpInfo;
            UnityEngine.Debug.Log(info);
        }
    }

    private void LateUpdate()
    {
        if (isInit)
            printInfo();

        if (Input.GetKeyDown("space"))
        {
            //UnityEngine.Debug.Log("REQUESTED START AS SOON AS READY");
            //StartCoroutine(StartWhenReady());
            ;
        }
        if (Input.GetKeyDown("b"))
        {
            AddBatch();
        }
    }


}



#if UNITY_EDITOR
[ExecuteInEditMode]
[CustomEditor(typeof(BatchProvider))]
public class BatchProviderEditor : Editor
{
    bool advancedOptions = false;
    SequenceBuildSceneCLI seq; 
    private BatchProvider bp;
    public void OnEnable()
    {
        bp = (BatchProvider)target;
        seq = GameObject.Find("Agent").GetComponent<SequenceBuildSceneCLI>();
    }
    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying)
        {
            base.OnInspectorGUI();

            EditorGUILayout.BeginHorizontal();
            //EditorGUILayout.LabelField(new GUIContent("Batch Size"), GUILayout.Width(150));
            //int tmp = EditorGUILayout.IntField(SequenceBuildSceneCLI.numCameraSets, GUILayout.Width(40));
            //EditorGUILayout.EndHorizontal();

            //if (tmp != SequenceBuildSceneCLI.numCameraSets)
            //{
            //    SequenceBuildSceneCLI.numCameraSets = tmp;
            //    SequenceBuildSceneCLI.UpdateComponents();
            //}


            advancedOptions = EditorGUILayout.Foldout(advancedOptions, "Advanced Options");
            if (advancedOptions)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Start from Object Index"), GUILayout.Width(150));
                bp.startFromObjectIdx = EditorGUILayout.IntField(bp.startFromObjectIdx, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Batches to have ready"), GUILayout.Width(150));
                bp.batchesToHaveReady = EditorGUILayout.IntField(bp.batchesToHaveReady, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Max Objects per Class", "Set to -1 to use all objects in folder"), GUILayout.Width(150));
                bp.maxObjsPerClass = EditorGUILayout.IntField(bp.maxObjsPerClass, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}

#endif
