using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ClustrGalaxy.World
{
    public sealed class GalaxyVisualCatalogProvider : MonoBehaviour
    {
        private AsyncOperationHandle<GalaxyVisualCatalog> handle;
        private AsyncOperationHandle<IList<IResourceLocation>> locations;
        private Task loading;
        private bool disposed;
        public GalaxyVisualCatalog Catalog { get; private set; }
        public Task LoadAsync() => loading ??= LoadInternalAsync();
        private async Task LoadInternalAsync()
        {
            if (disposed) return;
            try
            {
                locations = Addressables.LoadResourceLocationsAsync("galaxy-visual-catalog", typeof(GalaxyVisualCatalog));
                await locations.Task;
                if (disposed || !locations.IsValid() || locations.Status != AsyncOperationStatus.Succeeded || locations.Result.Count == 0) return;
                handle = Addressables.LoadAssetAsync<GalaxyVisualCatalog>(locations.Result[0]);
                await handle.Task;
                if (!disposed && handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded) Catalog = handle.Result;
            }
            catch (Exception exception)
            {
                if (!disposed) Debug.LogWarning("[ClustrGalaxy.Assets] Primitive fallback: " + exception.Message);
            }
            finally
            {
                if (locations.IsValid()) Addressables.Release(locations);
            }
        }
        private void OnDestroy()
        {
            disposed = true;
            Catalog = null;
            if (locations.IsValid()) Addressables.Release(locations);
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }
}
