using System.Collections.Generic;

namespace VoxelWorldStuff
{
    public class SingletonClassManager<T>
    {
        // Static singleton instance
        private static SingletonClassManager<T> instance;

        // Public property to access the instance
        public static SingletonClassManager<T> Instance
        {
            get
            {
                // Create the instance if it doesn't exist
                if (instance == null)
                {
                    instance = new SingletonClassManager<T>();
                }
                return instance;
            }
        }

        // HashSet to store active items of type T
        private HashSet<T> activeItems = new HashSet<T>();

        // List to store active items of type T for faster access
        private List<T> activeItemsList = new List<T>();

        // Method to register items of type T
        public void RegisterItem(T item)
        {
            // Check if the item is already registered
            if (!activeItems.Contains(item))
            {
                activeItems.Add(item);
                activeItemsList.Add(item);  // add to the list
            }
        }

        // Method to unregister items of type T
        public void UnregisterItem(T item)
        {
            if (activeItems.Remove(item))
            {
                activeItemsList.Remove(item);  // remove from the list
            }
        }

        // Method to get all the active items of type T
        public List<T> GetAllActiveItems()
        {
            return activeItemsList;  // return the precomputed list
        }
    }
}

