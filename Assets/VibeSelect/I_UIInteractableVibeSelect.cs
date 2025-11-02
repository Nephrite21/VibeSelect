using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle;

public enum VSAxes { X,Y,Z }
public interface I_UIInteractableVibeSelect
{
    void OnAxesMovedEnter(VSAxes axes);
    void OnAxesMoved(VSAxes axes, float distance);
    void OnTwistedEnter(VSAxes axes);
    void OnTwisted(VSAxes axes, float degree);
}
