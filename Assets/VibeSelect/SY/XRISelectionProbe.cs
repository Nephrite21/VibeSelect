using UnityEngine;


public class XRISelectionProbe : MonoBehaviour
{
    UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor[] interactors;

    void Awake()
    {
        interactors = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(true);
        Debug.Log($"[Probe] Found {interactors.Length} interactors under {name}");
        foreach (var i in interactors)
            Debug.Log($"[Probe] - {i.name} ({i.GetType().Name})");
    }

    void Update()
    {
        if (Time.frameCount % 30 != 0) return;

        foreach (var i in interactors)
        {
            Debug.Log($"[Probe] {i.name} hovered={i.interactablesHovered.Count} selected={i.interactablesSelected.Count}");
        }
    }
}
