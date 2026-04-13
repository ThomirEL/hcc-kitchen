using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

public class Logging : MonoBehaviour
{
    /*
    Logging distribution:
    - Gaze: EyeTracking
    - Targets information - TargetController; Which object flickered, flickering object position, object being looked at (target id), position of object being looked at, object selected (targetid), object selected position.
    - Study information - StudyController; Trial number, scende index, target distractor combo, technique, flickering
    - Saccades - SaccadeController



    */

    public static Logging Logger { get; private set; }

    [SerializeField]
    private bool LoggingEnabled = true;

    public UnityEvent ParticipantIDSet;

    public int participantID;

    private Thread logThread;

    private Dictionary<string, Writer> writers = new Dictionary<string, Writer>();

    private bool running = true;

    private bool addingNew = false;

    private string prefix="";

    private void Awake()
    {
        // If there is an instance, and it's not me, delete myself.

        if (Logger != null && Logger != this)
        {
            Destroy(this);
        }
        else
        {
            Logger = this;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        logThread = new Thread(WritetoFile);
        logThread.Start();
    }

    public void CreateLogger(string For, string at)
    {
        if (!LoggingEnabled){
            Debug.LogWarning($"[Logging] Attempted to create logger for '{For}' at '{at}', but logging is disabled.");
            return;
        }
        if (participantID == 0)
        {
            Debug.LogError("[Logging] Participant ID not set. Cannot create logger.");
            return;
        }

        if(writers.ContainsKey(For))
        {
            return;
        }
        Writer nw = new Writer(For, prefix+at) ;
        addingNew = true;
        writers.Add(nw.Name, nw);
        addingNew = false;
    }

    void WritetoFile()
    {
        //will drop remaining data if closed early
        while (running)
        {
            if(addingNew)continue;
            //Could maybe put back to normal foreach
            string[] keys = new string[writers.Keys.Count];
            writers.Keys.CopyTo(keys,0);                // ????
            foreach(string K in keys)   
            {
                writers[K].write();
            }
        }
        foreach (Writer w in writers.Values)
        {
            w.close();
        }
    }

    public void RecordExperimentController(string[] msg)
    {
        if (!writers.ContainsKey("ExperimentController"))
        {
            DateTime now = DateTime.Now;
            string fileName_time = string.Format("{0}-{1:00}-{2:00}-{3:00}-{4:00}", now.Year, now.Month, now.Day, now.Hour, now.Minute);
            CreateLogger("ExperimentController", "ExperimentController-participant"+participantID+"-"+fileName_time);
        }
        Record("ExperimentController", msg); 
    }

    public void RecordItemHighlights(string[] msg)
    {
        if (!writers.ContainsKey("ItemHighlighter"))
        {
            DateTime now = DateTime.Now;
            string fileName_time = string.Format("{0}-{1:00}-{2:00}-{3:00}-{4:00}", now.Year, now.Month, now.Day, now.Hour, now.Minute);
            CreateLogger("ItemHighlighter", "ItemHighlighter-participant"+participantID+"-"+fileName_time);
        }
        Record("ItemHighlighter", msg); 
    }

    public void RecordVRStats(string[] msg)
    {
        if (!writers.ContainsKey("VRTracking"))
        {
            DateTime now = DateTime.Now;
            string fileName_time = string.Format("{0}-{1:00}-{2:00}-{3:00}-{4:00}", now.Year, now.Month, now.Day, now.Hour, now.Minute);
            CreateLogger("VRTracking", "VRTracking-participant"+participantID+"-"+fileName_time);
        }
        Record("VRTracking", msg); 
    }

    public void Record(string writer, string[] msg)
    {
        writers[writer].addRecord(msg);
    }

    public void AddPrefix(string s)
    {
        prefix = s;
    }

    class Writer
    {
        public string Name;
        private int ID;
        public StreamWriter writer;
        private Queue<string[]> records = new Queue<string[]>();
        public Writer(string name, string fileName)
        {
            Name = name;
            ID = name.GetHashCode();

            string folderPath;
            if (Application.isEditor)
            {
                folderPath = Application.dataPath + "/0_HCC Kitchen/Experiment Data/Logs";
            }
            else
            {
                folderPath = Application.persistentDataPath + "/0_HCC Kitchen/Experiment Data/Logs";
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filepath = Path.Combine(folderPath, fileName + ".csv");
            Debug.Log($"[Logging] Creating log file at: {filepath}");
            writer = new StreamWriter(filepath);
        }
        public void addRecord(string[] r)
        {
            records.Enqueue(r);
        }
        public override int GetHashCode()
        {
            return ID;
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as Writer);
        }
        public bool Equals(Writer obj)
        {
            return obj != null && obj.Name == this.Name;
        }
        public void write()
        {
            if(records.Count>0)
                writer.WriteLine(msg2Line(records.Dequeue()));
        }
        public void close()
        {
            if (writer != null)
            {
                writer.Flush();
                writer.Close();
                writer = null;
            }
        }
        string msg2Line(string[] msg)
        {
            if (msg == null) return String.Empty;
            string line = "";
            for (int i = 0; i < msg.Length; ++i)
            {
                if (msg[i] == null) msg[i] = "";
                
                msg[i] = msg[i].Replace("\r", "").Replace("\n", ""); // Remove new lines so they don't break csv
                line += msg[i] + (i == (msg.Length - 1) ? "" : ";"); // Do not add semicolon to last data string
            }
            return line;
        }
    }

    void OnDestroy()
    {
        running = false;
        logThread.Join();
        foreach(Writer w in writers.Values)
        {
            w.close();
        }
    }
}