using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ClustrGalaxy.Data
{
    public sealed class SpatialSceneClient : IDisposable
    {
        private readonly int timeoutSeconds;
        private readonly int maximumAttempts;
        private UnityWebRequest activeRequest = null!;
        private int generation;

        public SpatialSceneClient(int timeoutSeconds = 15, int maximumAttempts = 3)
        {
            this.timeoutSeconds = Math.Max(1, timeoutSeconds);
            this.maximumAttempts = Math.Max(1, maximumAttempts);
        }

        public IEnumerator GetJson(
            string uri,
            Action<string> onSuccess,
            Action<SpatialRequestFailure> onFailure)
        {
            var requestGeneration = ++generation;
            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                using var request = UnityWebRequest.Get(uri);
                activeRequest = request;
                request.timeout = timeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");
                yield return request.SendWebRequest();

                if (requestGeneration != generation)
                {
                    yield break;
                }
                if (request.result == UnityWebRequest.Result.Success)
                {
                    activeRequest = null!;
                    onSuccess?.Invoke(request.downloadHandler.text);
                    yield break;
                }

                if (attempt < maximumAttempts && ShouldRetry(request.result, request.responseCode))
                {
                    yield return new WaitForSecondsRealtime(0.5f * Mathf.Pow(2f, attempt - 1));
                    continue;
                }

                activeRequest = null!;
                onFailure?.Invoke(new SpatialRequestFailure(
                    request.responseCode,
                    request.result,
                    string.IsNullOrWhiteSpace(request.error) ? "request failed" : request.error));
                yield break;
            }
        }

        public void Cancel()
        {
            generation++;
            if (activeRequest != null && !activeRequest.isDone)
            {
                activeRequest.Abort();
            }
            activeRequest = null!;
        }

        public static bool ShouldRetry(UnityWebRequest.Result result, long responseCode) =>
            result == UnityWebRequest.Result.ConnectionError ||
            responseCode == 429 ||
            responseCode >= 500;

        public void Dispose() => Cancel();
    }

    public readonly struct SpatialRequestFailure
    {
        public SpatialRequestFailure(long responseCode, UnityWebRequest.Result result, string message)
        {
            ResponseCode = responseCode;
            Result = result;
            Message = message;
        }

        public long ResponseCode { get; }
        public UnityWebRequest.Result Result { get; }
        public string Message { get; }

        public override string ToString() =>
            ResponseCode > 0 ? $"HTTP {ResponseCode}: {Message}" : $"{Result}: {Message}";
    }
}
