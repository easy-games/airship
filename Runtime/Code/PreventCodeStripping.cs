using Code.Player.Character;
using UnityEngine;

public class PreventCodeStripping : MonoBehaviour
{
    private void Start()
    {
        GetComponent("");
        gameObject.GetComponent("");
        Instantiate(gameObject);
        Instantiate(gameObject, gameObject.transform);
        Instantiate(gameObject, Vector3.back, Quaternion.identity);
        Instantiate(gameObject, Vector3.back, Quaternion.identity, gameObject.transform);
        Instantiate(gameObject, gameObject.transform);
        Instantiate(gameObject, gameObject.transform, true);

        // var dict = new DictionaryAsset();
        // dict.ContainsKey("test");

        CharacterAnimationHelper animationHelper = null;
        animationHelper.PlayAnimation(null, CharacterAnimationHelper.CharacterAnimationLayer.OVERRIDE_1, 1f);

        ReflectionCameraScript cam = null;
    }
}