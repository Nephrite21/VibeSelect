using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class SpatialPanel : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        
        // Grab 시 위치/회전 추적 비활성화 (컨트롤러를 따라 움직이지 않음)
        grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;
        
        // 초기 위치/회전 저장
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        
        // Grab 이벤트 리스너 등록
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // Grab 시점의 위치와 회전을 고정
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Release 시에도 위치 유지 (이미 고정되어 있음)
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}

