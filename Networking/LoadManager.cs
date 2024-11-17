using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Managing.Scened;
using TMPro;

/// <summary>
/// Manages scene loading and unloading, including UI updates and scene transitions.
/// </summary>
public class LoadManager : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject _loadUI;
    [SerializeField] private TMP_Text _loadingTxt;
    [SerializeField] private GameObject _canvas;

    [Header("Scene Data")]
    [SerializeField] private string _sceneToLoad;
    public string SceneToLoad
    { get => _sceneToLoad; set => _sceneToLoad = value; }

    [SerializeField] private bool _isLoading;
    public bool IsLoading
    { get => _isLoading; set => _isLoading = value; }

    [SerializeField] private string _sceneToUnload;
    public string SceneToUnload
    { get => _sceneToUnload; set => _sceneToUnload = value; }

    [SerializeField] private bool _startLoading;
    public bool StartLoading
    { get => _startLoading; set => _startLoading = value; }

    private bool _startedLoad;

    /// <summary>
    /// Registers to scene load percentage change events when the object is enabled.
    /// </summary>
    private void OnEnable()
    {
        if (SceneManager != null)
        {
            SceneManager.OnLoadPercentChange += HandleLoadProgress;
        }
    }

    /// <summary>
    /// Unregisters from events to avoid memory leaks when the object is disabled.
    /// </summary>
    private void OnDisable()
    {
        if (SceneManager != null)
        {
            SceneManager.OnLoadPercentChange -= HandleLoadProgress;
        }
    }

    /// <summary>
    /// Coroutine to update the loading text animation.
    /// </summary>
    private IEnumerator UpdateLoadingText()
    {
        yield return new WaitForSeconds(0.5f);

        // Alternates between different loading text formats
        if (_loadingTxt.text.Contains("Loading..."))
        {
            _loadingTxt.text = "Loading.";
        }
        else
        {
            _loadingTxt.text += ".";
        }

        _startedLoad = false;
    }

    /// <summary>
    /// Handles scene load progress and updates UI accordingly.
    /// </summary>
    /// <param name="obj">Data containing the loading progress percentage.</param>
    private void HandleLoadProgress(SceneLoadPercentEventArgs obj)
    {
        if (_isLoading)
        {
            Debug.Log($"Loading progress: {obj.Percent * 100}%");

            if (obj.Percent < 1)
            {
                // Display the loading UI and hide the main canvas
                _loadUI?.SetActive(true);
                _canvas?.SetActive(false);
            }
            else
            {
                // Loading completed; reset and update UI
                _startLoading = false;
                _isLoading = false;

                _loadUI?.SetActive(false);
                _canvas?.SetActive(true);

                // Unload the previous scene if specified
                if (!string.IsNullOrEmpty(_sceneToUnload))
                {
                    SceneUnloadData unloadData = new SceneUnloadData(_sceneToUnload);
                    SceneManager.UnloadGlobalScenes(unloadData);
                }
            }
        }
    }

    /// <summary>
    /// Starts the scene loading process.
    /// </summary>
    private void StartLoadingScene()
    {
        SceneLoadData loadData = new SceneLoadData(_sceneToLoad);
        SceneManager.LoadGlobalScenes(loadData);
        _isLoading = true;
    }

    /// <summary>
    /// Updates the state of the loading process and triggers the loading coroutine.
    /// </summary>
    private void Update()
    {
        if (_startLoading && !_startedLoad)
        {
            _startedLoad = true;
            StartLoadingScene();
            StartCoroutine(UpdateLoadingText());
        }
    }
}