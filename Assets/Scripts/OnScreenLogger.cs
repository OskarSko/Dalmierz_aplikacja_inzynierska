using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class OnScreenLogger : MonoBehaviour
{
    [Header("Gdzie wyświetlać")]
    public TextMeshProUGUI logText;

    [Header("Ustawienia")]
    public int maxLines = 12;
    public string filter = "";

    private readonly Queue<string> lines = new Queue<string>();
    private readonly List<string> pending = new List<string>();
    private readonly object lockObj = new object();
    private bool dirty = false;

    void OnEnable()
    {
        Application.logMessageReceivedThreaded += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceivedThreaded -= HandleLog;
    }

    void HandleLog(string message, string stackTrace, LogType type)
    {
        if (!string.IsNullOrEmpty(filter) && !message.Contains(filter)) return;

        string color = (type == LogType.Error || type == LogType.Exception) ? "#FF6B6B"
                     : (type == LogType.Warning) ? "#FFD166"
                     : "#22D3EE";

        if (message.Length > 120) message = message.Substring(0, 120) + "...";

        lock (lockObj)
        {
            pending.Add($"<color={color}>{message}</color>");
            dirty = true;
        }
    }

    void Update()
    {
        if (!dirty) return;

        lock (lockObj)
        {
            foreach (var l in pending)
            {
                lines.Enqueue(l);
                while (lines.Count > maxLines) lines.Dequeue();
            }
            pending.Clear();
            dirty = false;
        }

        if (logText != null) logText.text = string.Join("\n", lines);
    }

    public void ClearLog()
    {
        lines.Clear();
        if (logText != null) logText.text = "";
    }
}