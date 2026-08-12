using System;
using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace OneJourney.Tests.EditMode
{
    /// <summary>临时测试运行辅助（A2-18 验证用，运行后删除）。结果写入文件避免域重载丢失。</summary>
    public static class TestRunnerHelper
    {
        private const string ResultFile = "Library/Locus/tmp/a2-18-test-result.txt";

        public static string StartRun()
        {
            try { File.Delete(ResultFile); } catch { }

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks
            {
                OnFinished = (result) =>
                {
                    using (var w = new StreamWriter(ResultFile, true))
                    {
                        w.WriteLine("RUN_FINISHED pass=" + result.PassCount
                            + " fail=" + result.FailCount
                            + " skip=" + result.SkipCount
                            + " inconclusive=" + result.InconclusiveCount);
                        WriteResult(w, result, 0);
                    }
                }
            });

            var settings = new ExecutionSettings();
            settings.filters = new[] { new Filter { testMode = TestMode.EditMode } };
            return api.Execute(settings);
        }

        private static void WriteResult(StreamWriter w, ITestResultAdaptor r, int depth)
        {
            if (r.TestStatus == TestStatus.Failed)
            {
                w.WriteLine(new string(' ', depth * 2) + "FAIL " + r.FullName);
                if (!string.IsNullOrEmpty(r.Message))
                    w.WriteLine(new string(' ', depth * 2 + 2) + "MSG: " + r.Message.Replace("\n", " | "));
            }
            foreach (var child in r.Children) WriteResult(w, child, depth + 1);
        }

        private class Callbacks : ICallbacks
        {
            public Action<ITestResultAdaptor> OnFinished;
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) { OnFinished?.Invoke(result); }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
