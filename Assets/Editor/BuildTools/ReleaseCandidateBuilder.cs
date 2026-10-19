#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.EditorTools
{
    // This editor-only entry never generates or saves scenes and never changes licensing.
    [InitializeOnLoad]
    public static class ReleaseCandidateBuilder
    {
        const string CommandPath = "Library/release-candidate-command.txt";
        const string VerificationRoot = "Builds/Verification/ReleaseCandidate_20261018";
        static readonly string[] Scenes = { "Assets/Scenes/MainMenu.scene", "Assets/Scenes/Residents.scene" };
        static bool busy;
        static double stableSince = -1;
        static double nextPoll;

        static ReleaseCandidateBuilder()
        {
            EditorApplication.update += Poll;
        }

        [MenuItem("Everyone Recommends/Build New Release Candidate")]
        public static void RequestBuild()
        {
            if (busy || File.Exists(CommandPath))
            {
                Debug.LogWarning("A release candidate request is already pending or running.");
                return;
            }
            File.WriteAllText(CommandPath, "build", Encoding.UTF8);
            Debug.Log("Release candidate requested; waiting for a stable editor in Edit Mode.");
        }

        static void Poll()
        {
            if (busy || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                stableSince = -1;
                return;
            }
            if (stableSince < 0) stableSince = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - stableSince < 2 || !File.Exists(CommandPath)) return;

            busy = true;
            string verification = null;
            var ownsVerification = false;
            try
            {
                var stamp = DateTime.Now.ToString("HHmmss");
                verification = VerificationRoot + "/" + stamp + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                if (Directory.Exists(verification))
                    throw new IOException("Verification directory already exists: " + verification);
                Directory.CreateDirectory(verification);
                ownsVerification = true;
                var command = File.ReadAllText(CommandPath).Trim();
                // Consume once before validation/build; a failed request is never automatically retried.
                File.Move(CommandPath, verification + "/ConsumedCommand.txt");
                if (command != "build") throw new InvalidOperationException("Expected the single command: build");
                Build(verification, stamp);
            }
            catch (Exception error)
            {
                if (ownsVerification)
                    File.WriteAllText(verification + "/BuildFailure.txt", error.ToString(), Encoding.UTF8);
                Debug.LogException(error);
            }
            finally { busy = false; }
        }

        static void Build(string verification, string stamp)
        {
            WriteWatermarkEvidence(verification);
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                    throw new InvalidOperationException("Build refused: unsaved scene changes in " + scene.path +
                        ". Save or discard them yourself before making a new request.");
            }
            foreach (var scene in Scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Required saved scene is missing.", scene);

            var output = "Builds/Windows/ReleaseCandidate_20261018_" + stamp;
            if (Directory.Exists(output) || File.Exists(output))
                throw new IOException("Build output already exists; refusing to overwrite: " + output);

            var options = BuildOptions.CompressWithLz4 | BuildOptions.CleanBuildCache;
            var started = DateTime.Now.ToString("O");
            File.WriteAllText(verification + "/BuildStarted.txt", "Started=" + started + "\nOutput=" + output +
                "\nUnityVersion=" + Application.unityVersion + "\nTarget=StandaloneWindows64\nOptions=" + options +
                "\nDevelopment=false\nScenes=" + string.Join(",", Scenes) + "\n", Encoding.UTF8);
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output + "/EveryoneRecommends.exe",
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            if (report == null) throw new InvalidOperationException("BuildPipeline returned no report.");

            var messages = new StringBuilder();
            foreach (var step in report.steps)
            {
                messages.AppendLine("STEP: " + step.name + " (" + step.duration + ")");
                foreach (var message in step.messages)
                    messages.AppendLine(message.type + ": " + message.content);
            }
            // Keep every BuildReport message, including ordinary log messages.
            File.WriteAllText(verification + "/BuildMessages.txt", messages.ToString(), Encoding.UTF8);
            var summary = report.summary;
            var result = "Result=" + summary.result + "\nErrors=" + summary.totalErrors +
                "\nWarnings=" + summary.totalWarnings + "\nStarted=" + started +
                "\nFinished=" + DateTime.Now.ToString("O") + "\nOutput=" + output +
                "\nOptions=" + summary.options + "\nBytes=" + summary.totalSize +
                "\nDuration=" + summary.totalTime + "\nWatermarkVisualCheck=Pending\n";
            File.WriteAllText(verification + "/BuildResult.txt", result, Encoding.UTF8);
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Release candidate build failed; see " + verification);
            Debug.Log("Release candidate built: " + output + ". Verify the running player's watermark before release.");
        }

        static void WriteWatermarkEvidence(string verification)
        {
            const string evidence =
                "# Watermark investigation - 2026-10-18\n\n" +
                "This is a sanitized record of the investigation before this candidate request; it is not a fresh account-status check.\n\n" +
                "- The delivered Windows Demo visibly displayed 'Not for release'.\n" +
                "- The existing Demo builder used CompressWithLz4 without Development.\n" +
                "- Installed editor: 2022.3.62t16 / Tuanjie 1.10.4; Windows file metadata reported IsPreRelease=false, IsDebug=false, IsPrivateBuild=false.\n" +
                "- Editor.log from the 2026-10-18 editor startup reported a successful license update and resolved entitlement details after a client-validation warning.\n" +
                "- Only public product/feature names were extracted: Unity Personal, TuanjieEditor, com.unity.editor.watermarks.forcedisable, com.unity.editor.unity.compliant.\n" +
                "- These observations do not establish the account's profile certification or prove that a rebuilt player will have no watermark.\n" +
                "- The prior Demo data.tj3d retained an earlier modification date. This alone does not prove stale data. This candidate uses a new output directory and CleanBuildCache to rule out reuse of cached player data.\n\n" +
                "Official sources checked on 2026-10-18:\n" +
                "- https://developer.unity.cn/projects/67f4fa33edbc2a001ec2d485 : official pricing-policy FAQ states that an unsuitable license or a not-yet-verified new profile can cause a watermark.\n" +
                "- https://unity.cn/tuanjie/releases?u_major=5_unity&u_release_type=full : official release list includes Tuanjie 1.10.4 as a full release.\n\n" +
                "Next check: launch this exact candidate, inspect a rendered frame, and record whether the watermark is present. If present, ask the account owner to verify profile/license status through official support. This builder does not read license files, change accounts or alter any license.\n";
            File.WriteAllText(verification + "/WatermarkEvidence.md", evidence, Encoding.UTF8);
        }
    }
}
#endif
