using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
public class RebindCancelButton : MonoBehaviour
{
    public UnityEvent cancelRebindCalled;
    public void CancelRebind()
    {
        cancelRebindCalled?.Invoke();
    }
}
