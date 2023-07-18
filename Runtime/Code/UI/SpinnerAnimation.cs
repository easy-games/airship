using UnityEngine;

public class SpinnerAnimation : MonoBehaviour
{
    private RectTransform rectComponent;
    public float rotateSpeed = -150f;
    
    private void Start()
    {
        rectComponent = GetComponent<RectTransform>();
    }

    private void Update()
    {
        rectComponent.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
    }
}
