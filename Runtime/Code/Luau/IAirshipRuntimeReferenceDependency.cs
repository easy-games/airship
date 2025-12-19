/// <summary>
/// Used to handle kickstarting references to other objects before initializing properties 
/// </summary>
interface IAirshipRuntimeReferenceDependency {
    /// <summary>
    /// Initializes the dependency (create references)
    /// </summary>
    public void Init();
}