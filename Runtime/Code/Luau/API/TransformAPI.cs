using System;
using UnityEngine;

public class TransformAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Transform);
    }
}