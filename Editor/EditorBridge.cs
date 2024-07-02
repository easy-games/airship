[LuauAPI]
public class EditorBridge {
    public static bool IsMainMenuInEditorEnabled() {
        return EditorIntegrationsConfig.instance.enableMainMenu;
    }
}