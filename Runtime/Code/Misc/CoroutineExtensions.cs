using System.Collections;
using System.Linq;
using UnityEngine;

public static class CoroutineExtensions
{
    public static IEnumerator WaitAll(this MonoBehaviour mono, params IEnumerator[] ienumerators)
    {
        return ienumerators.Select(mono.StartCoroutine).ToArray().GetEnumerator();
    }
}