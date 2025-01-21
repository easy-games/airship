using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InertiaScrollRect : ScrollRect, IScrollHandler, IDragHandler
{
    // The speed at which the scrolling will slow down when inertia is applied
    // public float scrollDecelerationRate = 5;
    //
    // // The current velocity of the scrolling
    // private float scrollVelocity;
    //
    // // A flag that indicates whether inertia is currently being applied
    // private bool isInertiaActive = true;

    // Stop inertia when you click on the list
    // public override void OnInitializePotentialDrag(PointerEventData eventData)
    // {
    //     StopInertia();
    // }
    //
    // public override void OnScroll(PointerEventData eventData)
    // {
    //     // Calculate the amount of scrolling that should be applied based on the mouse wheel delta
    //
    //     // I had to tone the regular scroll sensitivy of 1 way down for this to be not too fast
    //     // You can probaly mess around with the values here
    //     float scrollDelta = eventData.scrollDelta.y * (scrollSensitivity * 0.001f);
    //
    //     // If inertia is currently being applied, we need to stop it before we can apply
    //     // the new scroll delta.
    //     if (isInertiaActive)
    //     {
    //         StopInertia();
    //     }
    //
    //     float normalizedPosition = verticalNormalizedPosition;
    //
    //     // Apply the scroll delta to the current normalized position
    //     normalizedPosition += scrollDelta;
    //
    //     // Set the new normalized position of the scroll rect
    //      SetNormalizedPosition(normalizedPosition,1);
    //
    //     // Start inertia scrolling with the current scroll velocity
    //     StartInertia(scrollDelta / Time.deltaTime);
    // }
    //
    // // Update is called once per frame
    // void Update()
    // {
    //     if (isInertiaActive)
    //     {
    //
    //         // Calculate the new scroll position based on the current scroll velocity
    //         float scrollDelta = scrollVelocity * Time.deltaTime;
    //
    //         float normalizedPosition = verticalNormalizedPosition;
    //
    //         // Apply the scroll delta to the current normalized position
    //         normalizedPosition += scrollDelta;
    //
    //         if (normalizedPosition <= 0 || normalizedPosition >= 1)
    //         {
    //             // The content has reached the top or bottom, so stop applying inertia
    //             StopInertia();
    //         }
    //         else
    //         {
    //             // The content has not reached the top or bottom, so set the new normalized position
    //             // and continue applying inertia.
    //             SetNormalizedPosition(normalizedPosition, 1);
    //
    //             // Reduce the scroll velocity according to the deceleration rate
    //             scrollVelocity -= scrollVelocity * scrollDecelerationRate * Time.deltaTime;
    //
    //             // Check if the scroll velocity has reached 0
    //             if (Mathf.Abs(scrollVelocity) <= 0)
    //             {
    //                 // The scroll velocity has reached 0, so stop applying inertia
    //                 StopInertia();
    //             }
    //         }
    //     }
    // }
    //
    // // Start inertia scrolling with the specified scroll velocity
    // private void StartInertia(float velocity)
    // {
    //     scrollVelocity = velocity;
    //     isInertiaActive = true;
    // }
    //
    // // Stop applying inertia to the scrolling
    // private void StopInertia()
    // {
    //     scrollVelocity = 0;
    //     isInertiaActive = false;
    // }
}