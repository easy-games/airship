using System.Collections.Generic;

namespace VoxelWorldStuff
{
    public class PointLightManager
    {
        // Static singleton instance
        private static PointLightManager instance;

        // Public property to access the instance
        public static PointLightManager Instance
        {
            get
            {
                // Create the instance if it doesn't exist
                if (instance == null)
                {
                    instance = new PointLightManager();
                }
                return instance;
            }
        }

        // HashSet to store active point lights
        private HashSet<PointLight> activePointLights = new HashSet<PointLight>();

        // List to store active point lights for faster access
        private List<PointLight> activePointLightsList = new List<PointLight>();

        // Method to register point lights
        public void RegisterPointLight(PointLight pointLight)
        {
            // Check if the light is already registered
            if (!activePointLights.Contains(pointLight))
            {
                activePointLights.Add(pointLight);
                activePointLightsList.Add(pointLight);  // add to the list
            }
        }

        // Method to unregister point lights
        public void UnregisterPointLight(PointLight pointLight)
        {
            if (activePointLights.Remove(pointLight))
            {
                activePointLightsList.Remove(pointLight);  // remove from the list
            }
        }

        // Method to get all the active point lights
        public List<PointLight> GetAllActivePointLights()
        {
            return activePointLightsList;  // return the precomputed list
        }

     
    }
}
