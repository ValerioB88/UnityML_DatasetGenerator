using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System;

using AsImpL;

public static class Helper
{
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
}

public class Batch
{
    public Dictionary<string, int> path_to_class = new Dictionary<string, int>();
    ObjectImporter batchImporter;
    public Dictionary<string, GameObject> path_to_gm = new Dictionary<string, GameObject>();
    public bool ready = false;
    public event Action<Batch> BatchImportComplete;
    protected virtual void OnBatchImportComplete()
    {
        ready = true;
        if (BatchImportComplete != null)
        {
            BatchImportComplete(this);
        }
    }
    public Batch(List<(string path, int classIdx)> samples, ImportOptions importOptions, GameObject gm)
    {
        batchImporter = gm.AddComponent<ObjectImporter>();
        batchImporter.CreatedModel += AddGameObject;
        batchImporter.ImportingComplete += OnBatchImportComplete;
        int index = 0;
        foreach (var v in samples)
        {
            batchImporter.ImportModelAsync("objC" + v.classIdx, v.Item1, null, importOptions);
            index += 1;
        }
    }

    // This is called when the object is created, not when it has finished loading
    protected virtual void AddGameObject(GameObject obj, string absolutePath)
    {
        path_to_class.Add(absolutePath, Int32.Parse(obj.name.Split('C')[1]));
        path_to_gm.Add(absolutePath, obj);
    }
}


public class BatchProvider : MonoBehaviour
{

    Queue<Batch> ready;
    int counterLoadingBatches = 0;

    [SerializeField]
    private string filePathDataset = "ShapeNetTry";

    [SerializeField]
    private ImportOptions importOptions = new ImportOptions();

    [SerializeField]
    private PathSettings pathSettings;

    public bool shuffleData = true;

    List<(string path, int classIdx)> samples = new List<(string path, int classIdx)>();
    List<string> classNames = new List<string>();
    int indexDataset = 0;

    public int batchSize = 6; // Batch size of -1 indicates to load all the dataset
    bool waitingForReady = false;
    string info = "";
    public event Action<Batch> ActionReady;

    public event Action RequestedExhaustedDataset;

    public int batchToHaveReady = 3;
    private bool isInit = false; 

    private void Start()
    {
    }

    public void InitBatchProvider()
    {
        filePathDataset = pathSettings.RootPath + filePathDataset;

        ready = new Queue<Batch>();

        // Look in the dataset to create a DatasetList
        int class_idx = 0;
        var datasetDir = new DirectoryInfo(filePathDataset);
        foreach (var classdir in datasetDir.GetDirectories())
        {
            classNames.Add(classdir.Name);
            foreach (var obj in classdir.GetDirectories())
            {
                samples.Add((obj.FullName + "/models/model_normalized.obj", class_idx));
            }
            class_idx += 1;

        }
        if (shuffleData)
        {
            Helper.Shuffle(samples);
        }
        if (batchSize == -1)
        {
            batchSize = samples.Count;
        }
        for (int i = 0; i < batchToHaveReady; i++)
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
        if (indexDataset >= samples.Count)
        {
            Debug.Log("Dataset Exhausted");
            
            return false;
        }
        var batch = new Batch(samples.GetRange(indexDataset, Mathf.Min(batchSize, samples.Count - indexDataset )), importOptions, gameObject);
        indexDataset = Mathf.Min(samples.Count, indexDataset + batchSize); 
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
        string tmpInfo = "Loading: " + counterLoadingBatches + "; Ready: " + ready.Count + ";  ImgsUsed: " + indexDataset + "; ImgsTot: " + samples.Count;
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
