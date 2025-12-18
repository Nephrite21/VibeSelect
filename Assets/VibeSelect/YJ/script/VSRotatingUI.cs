using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class VSRotatingUI : MonoBehaviour
{
    private XRBaseInteractor interactor;
    private bool isHeld = false;

    private Vector3 previousInteractorDirection;

    [SerializeField] private Transform wheelCenter;
    [SerializeField] private float autoRotateSpeed = 90f; // 회전 속도 (도/초)


    [SerializeField] private GameObject Button; // 버튼
    [SerializeField] private TMP_Text txt; // 업데이트할 텍스트 - 씬전환

    private void Start()
    {
        var grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);

        // 자동 위치/회전 방지
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.trackPosition = false;
        grab.trackRotation = false;

        Button.SetActive(false);
    }

    private void Update()
    {
        //Grab 중일 때만 회전
        if (isHeld)
        {
            // Z축 기준으로 일정 속도로 회전
            transform.Rotate(Vector3.forward, autoRotateSpeed * Time.deltaTime);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (Button.activeSelf) Button.SetActive(false);

        interactor = args.interactorObject.transform.GetComponent<XRBaseInteractor>();
        isHeld = true;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        isHeld = false;
        interactor = null;

        SnapToNearestSegment();
    }

    public void SnapToNearestSegment()
    {
        isHeld = false;
        interactor = null;
        //float zAngle = transform.eulerAngles.z % 360f;
        //if (zAngle < 0) zAngle += 360f;

        //float topAngle = (360f - zAngle) % 360f;

        float z360 = transform.eulerAngles.z;           // 0..360
        float topAngle = Mathf.DeltaAngle(0f, z360);   //-180~180
        int selectedSegment = GetSelectedSegment(topAngle);

        if (!Button.activeSelf) Button.SetActive(true);//벝은 활성화

        UpdateSelectedText(selectedSegment); // 텍스트 업데이트


        float targetAngle = 0f;
        switch (selectedSegment)
        {
            case 0: targetAngle = 60f; break;
            case 1: targetAngle = 180f; break;
            case 2: targetAngle = 300f; break;
        }

        StartCoroutine(SmoothRotateTo(targetAngle));
        Debug.Log($"[VSRotatingUI] 선택된 구역: {selectedSegment}, 목표 회전: {targetAngle}");
    }

    private int GetSelectedSegment(float topAngle)
    {
        // [0, 360] 구간
        if (topAngle >= 0f && topAngle <= 120f)
            return 0;
        else if (topAngle > 120f && topAngle <= 240f)
            return 1;
        else if (topAngle > 240f && topAngle < 360f) // 240~360
            return 2;

        // 음수 구간 ([-360, 0) 가정)
        if (topAngle < 0f && topAngle >= -120f)       // -120~0  => 240~360
            return 2;
        else if (topAngle < -120f && topAngle >= -240f) // -240~-120 => 120~240
            return 1;
        else                                          // -360~-240 => 0~120
            return 0;
    }
    private void UpdateSelectedText(int segment)
    {
        if (txt == null) return;

        string s = segment switch
        {
            0 => "Multi_Selection",
            1 => "Through_Selection",
            2 => "Axis_Roation",
            _ => "?"
        };

        txt.text = s;
    }

    private IEnumerator SmoothRotateTo(float targetAngle)
    {
        float duration = 0.5f;
        float elapsed = 0f;

        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = Quaternion.Euler(0f, 0f, targetAngle);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }

        transform.rotation = endRotation;
    }
}







