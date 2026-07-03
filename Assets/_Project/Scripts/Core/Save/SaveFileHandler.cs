using System;
using System.IO;
using UnityEngine;

public sealed class SaveFileHandler
{
    private readonly string _filePath;

    public SaveFileHandler(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Save file name cannot be empty.", nameof(fileName));
        }

        _filePath = Path.Combine(Application.persistentDataPath, fileName);
    }

    public void Save(SaveData data)
    {
        string directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(_filePath, json);
    }

    public SaveData Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        string json = File.ReadAllText(_filePath);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public bool HasSaveFile()
    {
        return File.Exists(_filePath);
    }

    public void DeleteSaveFile()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
