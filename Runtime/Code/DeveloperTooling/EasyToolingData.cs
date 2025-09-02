using UnityEngine;

public enum EngineRunMode {
    NONE = -1,
    EDITOR = 0,
    PLAY,
    BOTH,
}

public enum EasyAxis {
    None = -1,
    X = 0, 
    Y,
    Z,
}

public static class EasyTooling{
    public static bool IsValidRunMode(EngineRunMode runMode){
        return !(runMode == EngineRunMode.NONE ||
        (runMode == EngineRunMode.EDITOR && Application.isPlaying) ||
        (runMode == EngineRunMode.PLAY && !Application.isPlaying));
    }
}
