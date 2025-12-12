
using UnityEngine;
using Oculus.Interaction;

public class EnableGravityOnRelease : MonoBehaviour
{
    private Rigidbody rb;
    private Grabbable grabbable;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();

        grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            rb.isKinematic = true;
        }
        else if (evt.Type == PointerEventType.Unselect)
        {
            rb.isKinematic = false;
        }
    }
}
