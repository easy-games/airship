using System.Collections.Generic;
using UnityEngine;

namespace Airship
{
    public class SpatialGrid
    {
        private Dictionary<Vector3Int, HashSet<GameObject>> grid = new Dictionary<Vector3Int, HashSet<GameObject>>();
        private Dictionary<GameObject, ObjectData> objectData = new Dictionary<GameObject, ObjectData>();

        public float cellSize;

        public SpatialGrid(float cellSize)
        {
            this.cellSize = cellSize;
        }

        private struct ObjectData
        {
            public Vector3 position;
            public float radius;
        }

        private Vector3Int CalculateCellKey(Vector3 position)
        {
            return new Vector3Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize),
                Mathf.FloorToInt(position.z / cellSize));
        }

        private HashSet<Vector3Int> CalculateOverlapCells(Vector3 position, float radius)
        {
            var minCorner = position - Vector3.one * radius;
            var maxCorner = position + Vector3.one * radius;
            var minKey = CalculateCellKey(minCorner);
            var maxKey = CalculateCellKey(maxCorner);

            var cells = new HashSet<Vector3Int>();

            for (int x = minKey.x; x <= maxKey.x; x++)
            {
                for (int y = minKey.y; y <= maxKey.y; y++)
                {
                    for (int z = minKey.z; z <= maxKey.z; z++)
                    {
                        cells.Add(new Vector3Int(x, y, z));
                    }
                }
            }

            return cells;
        }

        public void AddObject(GameObject obj, Vector3 position, float radius)
        {
            var cells = CalculateOverlapCells(position, radius);
            foreach (var cellKey in cells)
            {
                if (!grid.ContainsKey(cellKey))
                    grid[cellKey] = new HashSet<GameObject>();

                grid[cellKey].Add(obj);
            }

            objectData[obj] = new ObjectData { position = position, radius = radius };
        }

        public void RemoveObject(GameObject obj)
        {
            if (!objectData.TryGetValue(obj, out var data)) return;

            foreach (var cellKey in CalculateOverlapCells(data.position, data.radius))
            {
                if (grid.ContainsKey(cellKey))
                {
                    grid[cellKey].Remove(obj);

                    if (grid[cellKey].Count == 0)
                        grid.Remove(cellKey);
                }
            }

            objectData.Remove(obj);
        }

        public void MoveObject(GameObject obj, Vector3 newPosition)
        {
            // Remove object from its current cells
            RemoveObject(obj);

            // Retrieve object radius from stored data
            if (!objectData.TryGetValue(obj, out var data)) return;

            // Add object back with new position
            AddObject(obj, newPosition, data.radius);
        }

        public HashSet<GameObject> GetObjectsInBounds(Vector3 position, float radius)
        {
            var objectsInBounds = new HashSet<GameObject>();

            foreach (var cellKey in CalculateOverlapCells(position, radius))
            {
                if (grid.ContainsKey(cellKey))
                {
                    foreach (var obj in grid[cellKey])
                    {
                        objectsInBounds.Add(obj);
                    }
                }
            }

            return objectsInBounds;
        }
    }
}
