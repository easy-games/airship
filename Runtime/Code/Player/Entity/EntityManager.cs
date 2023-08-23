using System.Collections.Generic;

[LuauAPI]
public class EntityManager : Singleton<EntityManager> {
    private HashSet<EntityDriver> entities = new();

    public void AddEntity(EntityDriver entity) {
        this.entities.Add(entity);
    }

    public void RemoveEntity(EntityDriver entity) {
        this.entities.Remove(entity);
    }
}