using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System;

using AsImpL;

public static class Helper
{
    public static string GetArg(string name)
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

    public static void FileLog(string debugMsg)
    {
        string path = Application.dataPath + "/debugLog.txt";
        StreamWriter sw = new StreamWriter(path, true);
        sw.WriteLine(debugMsg + "\n");
        sw.Close();
    }

}

public class Batch
{
    public Dictionary<string, int> pathToClassIdx = new Dictionary<string, int>();
    ObjectImporter batchImporter;
    public Dictionary<string, GameObject> pathToGm = new Dictionary<string, GameObject>();
    public bool ready = false;
    public event Action<Batch> BatchImportComplete;
    public GameObject batchContainer; 
    
    

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
    }
    public Batch(List<(string path, int classIdx)> samples, ImportOptions importOptions, GameObject gm)
    {
        batchContainer = new GameObject("Batch");
        batchImporter = gm.AddComponent<ObjectImporter>();
        batchImporter.CreatedModel += AddGameObject;
        batchImporter.ImportingComplete += OnBatchImportComplete;
        batchImporter.ImportedModel += OnModelImported;
        int index = 0;
        foreach (var v in samples)
        {
            batchImporter.ImportModelAsync("objC" + v.classIdx, v.Item1, batchContainer.transform, importOptions);
            index += 1;
        }
    }

    // This is called when the object is created, not when it has finished loading
    protected virtual void AddGameObject(GameObject obj, string absolutePath)
    {
        pathToClassIdx.Add(absolutePath, Int32.Parse(obj.name.Split('C')[1]));
        pathToGm.Add(absolutePath, obj);
    }
}


public class BatchProvider : MonoBehaviour
{

    Queue<Batch> ready;
    int counterLoadingBatches = 0;

    [SerializeField]
    public string filePathDataset = "ShapeNetTry";

    [SerializeField]
    private ImportOptions importOptions = new ImportOptions();

    public bool shuffleData = true;

    List<(string path, int classIdx)> samples = new List<(string path, int classIdx)>();
    public Dictionary<int, string> idxClassToName = new Dictionary<int, string>();
    [HideInInspector]
    public int indexObjDataset = 0;

    public int batchSize = 6; // Batch size of -1 indicates to load all the dataset
    bool waitingForReady = false;
    string info = "";
    public event Action<Batch> ActionReady;

    public event Action RequestedExhaustedDataset;

    public int batchesToHaveReady = 3;
    private bool isInit = false;

    public int getSamplesCount()
    {
        return samples.Count;
    }
    private void Start()
    {
    }

    public void InitBatchProvider()
    {
        filePathDataset = Path.Combine(new string[] { Application.dataPath, "..", filePathDataset });

        ready = new Queue<Batch>();

        // Look in the dataset to create a DatasetList
        int classIdx = 0;
        var datasetDir = new DirectoryInfo(filePathDataset);
        foreach (var classdir in datasetDir.GetDirectories())
        {
            idxClassToName[classIdx] = classdir.Name;
            foreach (var obj in classdir.GetDirectories())
            {
                samples.Add((obj.FullName + "/models/model_normalized.obj", classIdx));
            }
            classIdx += 1;
        }

        var cmlShuffle = Helper.GetArg("-shuffle_data");
        if (cmlShuffle != null)
        {
            shuffleData = int.Parse(cmlShuffle) == 1;
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

        if (shuffleData)
        {
            Helper.Shuffle(samples);
        }
        if (batchSize == -1)
        {
            batchSize = samples.Count;
            batchesToHaveReady = 1;
        }
        for (int i = 0; i < batchesToHaveReady; i++)
        {
            AddBatch();
        }
        isInit = true;
    }

    private void SetBatchReady(Batch batch)
    {
        counterLoadingBatches -= 1;
        ready.Enqueue(batch);
    }
    private bool AddBatch()
    {
        if (indexObjDataset >= samples.Count)
        {
            Debug.Log("Dataset Exhausted");

            return false;
        }
        var batch = new Batch(samples.GetRange(indexObjDataset, Mathf.Min(batchSize, samples.Count - indexObjDataset)), importOptions, gameObject);
        indexObjDataset = Mathf.Min(samples.Count, indexObjDataset + batchSize);
        if (batch.ready)
        {
            ready.Enqueue(batch);
        }
        else
        {
            counterLoadingBatches += 1;
            batch.BatchImportComplete += SetBatchReady;
        }
        return true;
    }


    public IEnumerator StartWhenReady()
    {
        if (waitingForReady)
        {
            Debug.Log("Already waiting for a batch");
            yield break;
        }
        if (ready.Count > 0)
        {
            AddBatch();
            ActionReady(ready.Dequeue());
            yield break;
        }
        if (ready.Count == 0 && counterLoadingBatches == 0)
        {
            Debug.Log("No batch loading - Dataset is probably exhausted and you are asking for a new batch!");
            if (RequestedExhaustedDataset != null)
            {
                RequestedExhaustedDataset();
            }
            yield break;
        }

        Debug.Log("Waiting for loading batch");
        waitingForReady = true;
        yield return new WaitUntil(() => ready.Count > 0);
        waitingForReady = false;
        AddBatch();
        ActionReady(ready.Dequeue());

    }

    private void printInfo()
    {
        string tmpInfo = "Loading: " + counterLoadingBatches + "; Ready: " + ready.Count + ";  ObjsUsed: " + indexObjDataset + "; ObjsTot: " + samples.Count;
        if (tmpInfo != info)
        {
            info = tmpInfo;
            Debug.Log(info);
        }

    }


    private void Update()
    {
        if (isInit)
            printInfo();

        if (Input.GetKeyDown("space"))
        {
            //UnityEngine.Debug.Log("REQUESTED START AS SOON AS READY");
            StartCoroutine(StartWhenReady());

        }
        if (Input.GetKeyDown("b"))
        {
            AddBatch();
        }
    }


}
