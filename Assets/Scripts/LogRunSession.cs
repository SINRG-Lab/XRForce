using System;
using System.IO;
using UnityEngine;

public static class LogRunSession
{
    static string _runStamp;
    static string _runFolderPath;

    public static string RunStamp
    {
        get
        {
            EnsureRunFolder();
            return _runStamp;
        }
    }

    public static string RunFolderPath
    {
        get
        {
            EnsureRunFolder();
            return _runFolderPath;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _runStamp = null;
        _runFolderPath = null;
    }

    public static string GetSubfolderPath(string subfolderName)
    {
        EnsureRunFolder();

        string resolvedSubfolder = string.IsNullOrWhiteSpace(subfolderName)
            ? "Logs"
            : subfolderName.Trim();

        string folderPath = Path.Combine(_runFolderPath, resolvedSubfolder);
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    static void EnsureRunFolder()
    {
        if (!string.IsNullOrEmpty(_runFolderPath))
            return;

        _runStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _runFolderPath = Path.Combine(Application.persistentDataPath, $"Run_{_runStamp}");
        Directory.CreateDirectory(_runFolderPath);
        Debug.Log("XRForce log run folder: " + _runFolderPath);
    }
}
