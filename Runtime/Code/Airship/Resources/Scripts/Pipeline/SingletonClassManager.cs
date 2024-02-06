#if UNITY_EDITOR
using UnityEditor; // Required for EditorApplication
#endif
using System.Collections.Generic;
using UnityEngine;

namespace Airship
{
    public class SingletonClassManager<T> where T : UnityEngine.Object
    {
        private static SingletonClassManager<T> instance;
        private static int lastCleanupTime = 0;
         
        public static SingletonClassManager<T> Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SingletonClassManager<T>();
                    instance.Initialize();
                }
                return instance;
            }
        }

        private HashSet<T> activeItems = new HashSet<T>();
        private List<T> activeItemsList = new List<T>();

        // Constructor made private to prevent instantiation
        private SingletonClassManager() { }

        // Initialization method
        private void Initialize()
        {
#if UNITY_EDITOR
            EditorApplication.hierarchyWindowChanged += CleanupDeletedItemsInEditor;
            EditorApplication.projectWindowChanged += CleanupDeletedItemsInEditor;
#endif
        }

        public void RegisterItem(T item)
        {
            if (!activeItems.Contains(item))
            {
                activeItems.Add(item);
                activeItemsList.Add(item);
            }
        }

        public void UnregisterItem(T item)
        {
            if (activeItems.Remove(item))
            {
                activeItemsList.Remove(item);
            }
        }

        public List<T> GetAllActiveItems()
        {
#if UNITY_EDITOR            
            CleanupDeletedItemsInEditor();
#endif           
            return activeItemsList;
        }

#if UNITY_EDITOR
        private void CleanupDeletedItemsInEditor()
        {
            //See if we've already done this, this frame
            int frameCount = Time.frameCount;
            if (frameCount == lastCleanupTime)
            {
                return;
            }
            lastCleanupTime = frameCount;


            //Check all items for nil and remove them
            List<UnityEngine.Object> removeList = new();
            foreach (T item in activeItems)
            {
                Object x = (Object)item;

                if (x == null)
                {
                    removeList.Add(x);
                }
            }
            foreach (T item in removeList)
            {
                activeItems.Remove(item);
                activeItemsList.Remove(item);
            }
        }
#endif

        // Ensure to unsubscribe from the event when the instance is destroyed or no longer needed
#if UNITY_EDITOR
        ~SingletonClassManager()
        {
            //EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }
#endif
    }
}
