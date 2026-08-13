using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ClustrGalaxy.Editor
{
    public static class ProjectTestRunner
    {
        private const string SuppressBootstrapKey = "ClustrGalaxy.SuppressAutomaticBootstrap";

        [InitializeOnLoadMethod]
        private static void RestorePlayModeTestLifecycle()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(SuppressBootstrapKey, false);
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(SuppressBootstrapKey, false);
            }
        }

        [MenuItem("Clustr Galaxy/Run Edit Mode Tests %#g")]
        public static void RunEditModeTests()
        {
            SessionState.SetBool(SuppressBootstrapKey, false);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new TestCallbacks(false, "edit-mode"));
            api.Execute(new ExecutionSettings(new Filter
            {
                assemblyNames = new[] { "ClustrGalaxy.EditModeTests" },
                testMode = TestMode.EditMode
            }));
        }

        [MenuItem("Clustr Galaxy/Run Play Mode Tests %#j")]
        public static void RunPlayModeTests()
        {
            SessionState.SetBool(SuppressBootstrapKey, true);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new TestCallbacks(true, "play-mode"));
            api.Execute(new ExecutionSettings(new Filter
            {
                assemblyNames = new[] { "ClustrGalaxy.PlayModeTests" },
                testMode = TestMode.PlayMode
            }));
        }

        private sealed class TestCallbacks : ICallbacks
        {
            private readonly bool clearBootstrapSuppression;
            private readonly string mode;

            public TestCallbacks(bool clearBootstrapSuppression, string mode)
            {
                this.clearBootstrapSuppression = clearBootstrapSuppression;
                this.mode = mode;
            }

            public void RunStarted(ITestAdaptor testsToRun) =>
                Debug.Log($"[ClustrGalaxy.Tests] Running {testsToRun.TestCaseCount} {mode} tests.");

            public void RunFinished(ITestResultAdaptor result)
            {
                if (clearBootstrapSuppression)
                {
                    SessionState.SetBool(SuppressBootstrapKey, false);
                }

                Debug.Log($"[ClustrGalaxy.Tests] {result.TestStatus}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"[ClustrGalaxy.Tests] {result.FullName}: {result.Message}");
                }
            }
        }
    }
}
