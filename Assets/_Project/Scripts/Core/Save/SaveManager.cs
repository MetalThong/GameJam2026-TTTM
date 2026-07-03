using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SaveManager : MonoBehaviour
{
    [SerializeField] private string fileName = "save.json";
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool saveOnApplicationQuit = true;

    private SaveData _saveData;
    private SaveFileHandler _fileHandler;
    private List<ISaveable> _saveables = new();

    public static SaveManager Instance { get; private set; }
    public SaveData CurrentData => _saveData;
    public bool HasSaveFile => _fileHandler != null && _fileHandler.HasSaveFile();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _fileHandler = new SaveFileHandler(fileName);
    }

    private void Start()
    {
        if (loadOnStart)
        {
            LoadGame();
        }
        else
        {
            NewGame();
        }
    }

    private void OnApplicationQuit()
    {
        if (saveOnApplicationQuit)
        {
            SaveGame();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void NewGame()
    {
        _saveData = new SaveData();
        LoadSaveables();
    }

    public void SaveGame()
    {
        if (_saveData == null)
        {
            _saveData = new SaveData();
        }

        FindSaveables();

        foreach (ISaveable saveable in _saveables)
        {
            saveable.Save(_saveData);
        }

        _fileHandler.Save(_saveData);
    }

    public void LoadGame()
    {
        _saveData = _fileHandler.Load();

        if (_saveData == null)
        {
            NewGame();
            return;
        }

        LoadSaveables();
    }

    public void DeleteSave()
    {
        _fileHandler.DeleteSaveFile();
        NewGame();
    }

    private void LoadSaveables()
    {
        FindSaveables();

        foreach (ISaveable saveable in _saveables)
        {
            saveable.Load(_saveData);
        }
    }

    private void FindSaveables()
    {
        _saveables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .ToList();
    }
}
