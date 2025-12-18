using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class VSRotatingUIbywrist : MonoBehaviour
{
    [SerializeField] private WristRotationDetector wristDetector;
    [SerializeField] private VSRotatingUI vSRotatingUI;

    [Header("Target 설정")]
    [Tooltip("OnTwisted로 받은 angle(도)에 곱해서 목표 회전량을 결정")]
    public float angleMultiplier = 10f;

    [Header("회전 속도")]
    [Tooltip("목표까지 도달하는 회전 속도 (도/초)")]
    public float rotateSpeedDegPerSec = 120f;

    [Tooltip("목표 도달 판정(도)")]
    public float snapEpsilonDeg = 0.2f;

    private float _targetZ;       // 목표 local Z 각도(서명각 -180~180)
    private bool _hasTarget;

    private void OnEnable()
    {
        if (!wristDetector) wristDetector = GetComponent<WristRotationDetector>();
        if (wristDetector) wristDetector.OnTwisted.AddListener(SetTargetFromTwist);
    }

    private void OnDisable()
    {
        if (wristDetector) wristDetector.OnTwisted.RemoveListener(SetTargetFromTwist);
    }

    // OnTwisted에서 "목표 회전값"만 설정
    private void SetTargetFromTwist(Vector3 axis, float angleDeg)
    {
        //Z축 회전일 때만 반응하고 싶으면 아래 주석 해제
        // if (axis != Vector3.forward) return;
        Debug.Log(angleDeg);

        if (angleDeg < 15 && angleDeg > -15) return;

        int dir = angleDeg > 0f ? 1 : (angleDeg < 0f ? -1 : 0);

        float currentZ = GetLocalZSigned();
        //float delta = angleDeg * angleMultiplier;
        float delta = dir * 100;

        _targetZ = NormalizeSignedAngle(currentZ + delta);
        _hasTarget = true;
    }

    private void Update()
    {
        if (!_hasTarget) return;

        float currentZ = GetLocalZSigned();
        float nextZ = Mathf.MoveTowardsAngle(currentZ, _targetZ, rotateSpeedDegPerSec * Time.deltaTime);

        var e = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(e.x, e.y, nextZ);

        if (Mathf.Abs(Mathf.DeltaAngle(nextZ, _targetZ)) <= snapEpsilonDeg)
        {
            transform.localEulerAngles = new Vector3(e.x, e.y, _targetZ);

            vSRotatingUI.SnapToNearestSegment(); // 기준점으로 돌아가게
            _hasTarget = false;
        }
    }

    // localEulerAngles.z(0~360)을 -180~180로 변환
    private float GetLocalZSigned()
    {
        return NormalizeSignedAngle(transform.localEulerAngles.z);
    }

    private float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

}
