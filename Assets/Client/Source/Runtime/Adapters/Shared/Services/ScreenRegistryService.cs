using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Adapters.Services
{
    /// <summary>
    /// Holds the live screen component of each additively loaded demo scene, so systems built
    /// at boot can reach views that do not exist yet when the pipeline is wired.
    /// </summary>
    /// <remarks>
    /// Populated from <see cref="SceneManager.sceneLoaded"/>. A destroyed screen reads as absent
    /// through Unity's fake-null, so no <c>sceneUnloaded</c> bookkeeping is needed.
    /// </remarks>
    public sealed class ScreenRegistryService : IDisposable
    {
        private readonly Type[] _trackedTypes;
        private readonly Dictionary<Type, Component> _screens = new();

        public ScreenRegistryService(params Type[] trackedTypes)
        {
            _trackedTypes = trackedTypes;
            SceneManager.sceneLoaded += _OnSceneLoaded;

            // Scenes that were already open when the registry was built: re-entry into play mode
            // without a domain reload, or tests loading Boot into a live editor.
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                _Scan(SceneManager.GetSceneAt(sceneIndex));
        }

        public bool TryGet<T>(out T screen) where T : Component
        {
            // A destroyed component stays in the dictionary but compares equal to null.
            if (_screens.TryGetValue(typeof(T), out var found) && found != null)
            {
                screen = (T)found;
                return true;
            }

            screen = null;
            return false;
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= _OnSceneLoaded;
        }

        private void _OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _Scan(scene);
        }

        private void _Scan(Scene scene)
        {
            if (scene.isLoaded == false)
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var type in _trackedTypes)
                {
                    var found = root.GetComponentInChildren(type, true);

                    if (found != null)
                        _screens[type] = found;
                }
            }
        }
    }
}
