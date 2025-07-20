using UnityEngine;
using Datra.Interfaces;
using Datra.Unity.Serialization;

namespace Datra.Unity
{
    /// <summary>
    /// Initializes Datra for use in Unity
    /// </summary>
    public class DatraUnityInitializer : MonoBehaviour
    {
        private static DatraUnityInitializer _instance;
        private static IDataContext _dataContext;
        private static UnityDataRefResolver _resolver;
        
        /// <summary>
        /// Get the current DataContext
        /// </summary>
        public static IDataContext DataContext => _dataContext;
        
        /// <summary>
        /// Get the DataRef resolver
        /// </summary>
        public static UnityDataRefResolver Resolver => _resolver;
        
        /// <summary>
        /// Initialize Datra with a DataContext
        /// </summary>
        public static void Initialize(BaseDataContext dataContext)
        {
            if (_instance == null)
            {
                var go = new GameObject("[Datra]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<DatraUnityInitializer>();
            }
            
            _dataContext = dataContext;
            _resolver = new UnityDataRefResolver(dataContext);
            
            Debug.Log("[Datra] Initialized successfully");
        }
        
        /// <summary>
        /// Check if Datra is initialized
        /// </summary>
        public static bool IsInitialized => _dataContext != null;
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                _dataContext = null;
                _resolver = null;
            }
        }
    }
}