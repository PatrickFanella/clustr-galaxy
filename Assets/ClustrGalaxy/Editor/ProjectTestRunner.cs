using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ClustrGalaxy.Editor
{
    public static class ProjectTestRunner
    {
        [MenuItem("Clustr Galaxy/Run Edit Mode Tests %#g")]
        public static void RunEditModeTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new TestCallbacks());
            api.Execute(new ExecutionSettings(new Filter
            {
                assemblyNames = new[] { "ClustrGalaxy.EditModeTests" },
                testMode = TestMode.EditMode
            }));
        }

        private sealed class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) =>
                Debug.Log($"[ClustrGalaxy.Tests] Running {testsToRun.TestCaseCount} edit-mode tests.");

            public void RunFinished(ITestResultAdaptor result) =>
                Debug.Log($"[ClustrGalaxy.Tests] {result.TestStatus}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");

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
