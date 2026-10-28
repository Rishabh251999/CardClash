using UnityEngine;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using Mirror;
using CardClash;

namespace CardClash
{
    public static class BuildAll
    {
        private const string ServerOutput =
            @"D:\UNO\Server Build\GameServer.exe";

        private const string AndroidOutput =
            @"D:\UNO\Build\Game.apk";

        [MenuItem("Build/Build Both (Server + Android)")]
        public static void BuildBoth()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                Debug.LogError(
                    "Unity is busy. Wait for compilation/import to finish.");
                return;
            }

            var scenes = GetScenes();

            if (scenes.Length == 0)
            {
                Debug.LogError(
                    "No enabled scenes found in Build Profiles / Build Settings.");
                return;
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(ServerOutput));
            Directory.CreateDirectory(
                Path.GetDirectoryName(AndroidOutput));

            // Windows Dedicated Server
            var serverOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                locationPathName = ServerOutput,
                options = BuildOptions.None
            };

            if (!SetHeadlessStartModeInScenes(
                    scenes, HeadlessStartOptions.AutoStartServer))
                return;

            bool serverSucceeded = Build(serverOptions, "Windows Server");

            if (!serverSucceeded)
                return;

            // Android
            var androidOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                locationPathName = AndroidOutput,
                options = BuildOptions.None
            };

            if (!SetHeadlessStartModeInScenes(
                    scenes, HeadlessStartOptions.AutoStartClient))
                return;

            Build(androidOptions, "Android");
        }

        private static string[] GetScenes()
        {
            var result = new System.Collections.Generic.List<string>();

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    result.Add(scene.path);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Opens each scene, finds the CardNetworkManager (if present),
        /// sets its Headless Start Mode, and saves the scene.
        /// </summary>
        private static bool SetHeadlessStartModeInScenes(
            string[] scenePaths,
            HeadlessStartOptions mode)
        {
            string activeScenePath =
                EditorSceneManager.GetActiveScene().path;

            bool foundAny = false;

            foreach (var scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(
                    scenePath, OpenSceneMode.Single);

                var manager = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<CardNetworkManager>(true))
                    .FirstOrDefault();

                if (manager == null)
                    continue;

                foundAny = true;

                if (manager.headlessStartMode == mode)
                    continue;

                Undo.RecordObject(manager, "Set Headless Start Mode");
                manager.headlessStartMode = mode;
                EditorUtility.SetDirty(manager);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    Debug.LogError(
                        $"Failed to save scene '{scenePath}' after updating headlessStartMode.");
                    return false;
                }

                Debug.Log(
                    $"Set CardNetworkManager.headlessStartMode to {mode} in scene '{scenePath}'.");
            }

            // Restore originally active scene.
            if (!string.IsNullOrEmpty(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

            if (!foundAny)
            {
                Debug.LogWarning(
                    "No CardNetworkManager found in the build scenes; headlessStartMode was not set.");
            }

            return true;
        }

        private static bool Build(
            BuildPlayerOptions options,
            string label)
        {
            Debug.Log("Starting " + label + " build...");

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    label + " build failed: " + report.summary.result);
                return false;
            }

            Debug.Log(label + " build completed: " +
                      options.locationPathName);

            return true;
        }
    }

}