using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class TestUI : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var doc = GetComponent<UIDocument>();
        var container = doc.rootVisualElement.Q("Container");
        container.RegisterCallback<MouseOverEvent>(OnMouseOver);
        container.RegisterCallback<MouseDownEvent>(OnMouseDown);
    }

    void OnMouseOver(MouseOverEvent e)
    {
        Debug.Log("mouse over!");
    }

    void OnMouseDown(MouseDownEvent e)
    {
        Debug.Log("mouse down!");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
