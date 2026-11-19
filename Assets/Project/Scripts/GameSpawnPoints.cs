using UnityEngine;

public sealed class GameSpawnPoints : MonoBehaviour
{
    [SerializeField] private Transform[] points;

    public Transform GetPoint(int index)
    {
        if (points == null || points.Length == 0)
            return null;

        return points[index % points.Length];
    }
}
