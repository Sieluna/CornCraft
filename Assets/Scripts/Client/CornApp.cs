#nullable enable
using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

using CraftSharp.Event;
using CraftSharp.Control;
using CraftSharp.Resource;
using CraftSharp.UI;

namespace CraftSharp
{
    public class CornApp : MonoBehaviour
    {
        public const int WINDOWED_APP_WIDTH = 1280, WINDOWED_APP_HEIGHT = 720;
        public const int EDITOR_FPS_LIMIT = 60;

        private BaseCornClient? client = null;
        public BaseCornClient? Client => client;

        public static BaseCornClient? CurrentClient => Instance.Client;
        public static void SetCurrentClient(BaseCornClient c) => Instance.client = c;

        private static CornApp? instance;
        public static CornApp Instance
        {
            get
            {
                if (instance != null)
                    return instance;
                
                var magic = new GameObject("Corn Craft");
                GameObject.DontDestroyOnLoad(magic);
                return instance = magic.AddComponent<CornApp>();
            }
        }

        // Runs before a scene gets loaded
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeApp()
        {
            Loom.Initialize();

            #if UNITY_EDITOR
            // Limit framerate in editor
            Application.targetFrameRate = EDITOR_FPS_LIMIT;
            #endif

            // Ensure CornApp instance is created
            _ = Instance;
        }

        public IEnumerator PrepareDataAndResource(int protocolVersion, DataLoadFlag startUpFlag, Action<string> updateStatus)
        {
            var versionDictPath = PathHelper.GetExtraDataFile("versions.json");

            var dataVersion     = string.Empty;
            var entityVersion   = string.Empty;
            var resourceVersion = string.Empty;

            try
            {
                // Read data version dictionary
                var versions = Json.ParseJson(File.ReadAllText(versionDictPath, Encoding.UTF8));
                var version = protocolVersion.ToString();

                if (versions.Properties.ContainsKey(version))
                {
                    var entries = versions.Properties[version].Properties;

                    dataVersion = entries["data"].StringValue;

                    // Check entity data version override
                    if (entries.ContainsKey("entity"))
                        entityVersion = entries["entity"].StringValue;
                    else
                        entityVersion = dataVersion;
                    
                    resourceVersion = entries["resource"].StringValue;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Data for protocol version {protocolVersion} is not available: {e.Message}");
                Notify(Translations.Get("login.data_not_availale"), Notification.Type.Error);
                updateStatus("login.result.login_failed");
                startUpFlag.Failed = true;
                yield break;
            }

            if (startUpFlag.Failed)
            {
                updateStatus("login.login_failed");
                yield break;
            }

            // First load all possible Block States...
            var loadFlag = new DataLoadFlag();
            Task.Run(() => BlockStatePalette.INSTANCE.PrepareData(dataVersion, loadFlag));
            while (!loadFlag.Finished) yield return null;

            // Then load all Items...
            loadFlag.Finished = false;
            Task.Run(() => ItemPalette.INSTANCE.PrepareData(dataVersion, loadFlag));
            while (!loadFlag.Finished)
                yield return null;
            
            loadFlag.Finished = false;
            Task.Run(() => BlockInteractionManager.INSTANCE.PrepareData(loadFlag));
            while (!loadFlag.Finished) yield return null;

            var packManager = ResourcePackManager.Instance;

            // Load resource packs...
            packManager.ClearPacks();
            // Download base pack if not present (Check pack.mcmeta because the
            // folder is present even if only language files are downloaded)
            if (!File.Exists(PathHelper.GetPackFile($"vanilla-{resourceVersion}", "pack.mcmeta"))) // Prepare resources first
            {
                Debug.Log($"Resources for {resourceVersion} not present. Downloading...");
                bool downloadSucceeded = false;
                yield return StartCoroutine(ResourceDownloader.DownloadResource(resourceVersion,
                        updateStatus, () => { },
                        (succeeded) => downloadSucceeded = succeeded));
                
                if (!downloadSucceeded)
                {
                    Notify(Translations.Get("resource.error.base_resource_download_failure"), Notification.Type.Error);
                    updateStatus("login.login_failed");
                    startUpFlag.Failed = true;
                    yield break;
                }
            }
            // First add base resources
            ResourcePack basePack = new($"vanilla-{resourceVersion}");
            packManager.AddPack(basePack);
            // Check base pack availability
            if (!basePack.IsValid)
            {
                Notify(Translations.Get("resource.error.base_resource_invalid"), Notification.Type.Error);
                updateStatus("login.login_failed");
                startUpFlag.Failed = true;
                yield break;
            }
            // Then append overrides
            packManager.AddPack(new("vanilla_fix"));
            packManager.AddPack(new("Greenfield.Texture.Pack.1.17"));
            //packManager.AddPack(new("VanillaBDCraft 64x MC116"));
            // Load valid packs...
            loadFlag.Finished = false;
            Task.Run(() => {
                // Set up thread locale for testing resource loading with different locales
                //System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                packManager.LoadPacks(loadFlag, (status) => Loom.QueueOnMainThread(() => updateStatus(status)));
            });
            while (!loadFlag.Finished) yield return null;
            
            // Load entity definitions
            loadFlag.Finished = false;
            Task.Run(() => EntityPalette.INSTANCE.PrepareData(entityVersion, loadFlag));
            while (!loadFlag.Finished) yield return null;

            // Load block entity definitions (after all blockstates are loaded)
            loadFlag.Finished = false;
            Task.Run(() => BlockEntityPalette.INSTANCE.PrepareData(dataVersion, loadFlag));
            while (!loadFlag.Finished) yield return null;

            // Load in-game translations (loaded AFTER resource files)
            var s = Path.DirectorySeparatorChar;
            var langFile = PathHelper.GetPackDirectoryNamed(
                    $"vanilla-{resourceVersion}{s}assets{s}minecraft{s}lang{s}{CornGlobal.Language}.json");
            
            if (!File.Exists(langFile)) // If translation file is not available, try downloading it
            {
                // IMPORTANT: en_us.json is not present in asset manifest, so it cannot
                // be downloaded with ResourceDownloader.DownloadLanguageJson()
                // Instead it must be downloaded along with vanilla resource files
                yield return StartCoroutine(ResourceDownloader.DownloadLanguageJson(
                        resourceVersion, CornGlobal.Language, updateStatus,
                        () => { },
                        (langJsonDownloaded) => {
                            if (!langJsonDownloaded)
                                startUpFlag.Failed = true;
                        }
                ));
            }

            Protocol.ChatParser.LoadTranslationRules(langFile);

            if (loadFlag.Failed) // Cancel login if resources are not properly loaded
            {
                Notify(Translations.Get("resource.error.resource_load_failure"), Notification.Type.Error);
                updateStatus("login.login_failed");
                startUpFlag.Failed = true;
                yield break;
            }
        }

        private IEnumerator EnterWorldScene(string sceneName)
        {
            // Prepare scene and unity objects
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;

            while (op.progress < 0.9F) yield return null;

            // Scene is loaded, activate it
            op.allowSceneActivation = true;
            bool fullyLoaded = false;

            // Wait till everything's ready
            op.completed += (operation) =>
            {
                client = Component.FindObjectOfType<BaseCornClient>();
                fullyLoaded = true;
            };

            while (!fullyLoaded) yield return null;
        }

        public void StartLoginCoroutine(StartLoginInfo info, Action<bool> callback, Action<string> updateStatus)
        {
            StartCoroutine(StartLogin(info, callback, updateStatus));
        }

        private IEnumerator StartLogin(StartLoginInfo info, Action<bool> callback, Action<string> updateStatus)
        {
            // Clear client value
            client = null;

            // Enter world scene, and find the client instance in that scene
            updateStatus("login.enter_world_scene");
            yield return EnterWorldScene(info.Online ? "World" : "World Offline");

            // Start client
            if (client != null)
            {
                var succeeded = client!.StartClient(info);
                callback(succeeded);
            }
            else // Failed to find client instance in scene
            {
                callback(false);
                BackToLogin();
            }
        }

        /// <summary>
        /// Should only be externally called by CornClient
        /// </summary>
        public void BackToLogin() => SceneManager.LoadScene("Login");

        // Should be called from the Unity thread only, not net read thread
        public static void Notify(string notification) => EventManager.Instance.Broadcast<NotificationEvent>(new(notification));

        public static void Notify(string notification, Notification.Type type) => EventManager.Instance.Broadcast<NotificationEvent>(new(notification, 6F, type));

        public static void Notify(string notification, float duration, Notification.Type type) => EventManager.Instance.Broadcast<NotificationEvent>(new(notification, duration, type));

        void Update()
        {
            if (Keyboard.current.f11Key.wasPressedThisFrame) // Toggle full screen
            {
                if (Screen.fullScreen)
                {
                    Screen.SetResolution(WINDOWED_APP_WIDTH, WINDOWED_APP_HEIGHT, false);
                    Screen.fullScreen = false;
                }
                else
                {
                    var maxRes = Screen.resolutions[Screen.resolutions.Length - 1];
                    Screen.SetResolution(maxRes.width, maxRes.height, true);
                    Screen.fullScreen = true;
                }
            }
        }
    }
}