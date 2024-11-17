using FishNet.Managing.Scened;
using FishNet;
using FishNet.Managing.Logging;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles scene stacking and transition logic for networked objects
/// </summary>
public class SceneStack : MonoBehaviour
{
    public const string scene_name = "Game";
    [SerializeField] private int _stackedSceneHandle = 0;

    [Server(Logging = LoggingType.Off)]
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Object entered trigger.");
        NetworkObject nob = other.GetComponent<NetworkObject>();

        if (nob != null)
        {
            LoadScene(nob);
        }
    }

    public void OnStartGame()
    {
        Debug.Log("Starting Game");
    }

    /// <summary>
    /// Loads a new scene and assigns the entering NetworkObject to it.
    /// </summary>
    /// <param name="nob"></param>
    private void LoadScene(NetworkObject nob)
    {
        // Ensure the object's owner is active in the network.
        if (!nob.Owner.IsActive)
        {
            return;
        }

        SceneLookupData lookup;

        // Decide whether to load by scene handle or name.
        Debug.Log("Loading scene by handle? " + (_stackedSceneHandle != 0));
        if (_stackedSceneHandle != 0)
        {
            lookup = new SceneLookupData(_stackedSceneHandle);
        }
        else
        {
            lookup = new SceneLookupData(scene_name);
        }

        // Configure scene load data.
        SceneLoadData sld = new SceneLoadData(lookup)
        {
            Options =
            {
                AllowStacking = true, // Allows multiple scenes to stack without unloading previous ones
                LocalPhysics = LocalPhysicsMode.Physics3D // Enables 3D physics for the new scene
            },
            MovedNetworkObjects = new NetworkObject[] { nob }, // Assign the entering object to the new scene
            ReplaceScenes = ReplaceOption.All // Replaces all currently active scenes
        };

        // Trigger the scene load for the object's owner.
        InstanceFinder.SceneManager.LoadConnectionScenes(nob.Owner, sld);
    }

    public bool sceneStack = false; // Whether scene stacking is enabled
    private SceneUnloadData sud; // Data related to unloading scenes

    /// <summary>
    /// Subscribes to scene load completion events at the start.
    /// </summary>
    private void Start()
    {
        InstanceFinder.SceneManager.OnLoadEnd += SceneManager_OnloadEnd;
    }

    /// <summary>
    /// Unsubscribes from scene load completion events to prevent memory leaks.
    /// </summary>
    private void OnDestroy()
    {
        if (InstanceFinder.SceneManager != null)
        {
            InstanceFinder.SceneManager.OnLoadEnd -= SceneManager_OnloadEnd;
        }
    }

    /// <summary>
    /// Handles post-load logic, including updating the scene handle and managing unused scenes.
    /// </summary>
    /// <param name="obj">Event data for the scene load completion.</param>
    private void SceneManager_OnloadEnd(SceneLoadEndEventArgs obj)
    {
        // Only process server-side load events.
        if (!obj.QueueData.AsServer)
        {
            return;
        }

        // Skip processing if stacking is disabled or a scene handle already exists.
        if (!sceneStack || _stackedSceneHandle != 0)
        {
            return;
        }

        // Store the handle of the newly loaded scene.
        if (obj.LoadedScenes.Length > 0)
        {
            _stackedSceneHandle = obj.LoadedScenes[0].handle;
        }

        // Configure unload options to clean up unused scenes.
        sud.Options.Mode = UnloadOptions.ServerUnloadMode.UnloadUnused;
    }
}
