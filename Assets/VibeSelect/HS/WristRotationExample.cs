using UnityEngine;

/// <summary>
/// WristRotationDetector 사용 예제
/// </summary>
public class WristRotationExample : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WristRotationDetector wristDetector;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject rotationIndicator;
    [SerializeField] private LineRenderer rotationAxisLine;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rotationStartSound;
    [SerializeField] private AudioClip zMovementSound;

    void Start()
    {
        if (wristDetector == null)
        {
            wristDetector = GetComponent<WristRotationDetector>();
        }

        // 이벤트 구독
        wristDetector.OnTwisted.AddListener(HandleRotationStart);
        wristDetector.WhileTwisted.AddListener(HandleRotating);
        wristDetector.OnZAxisMoved.AddListener(HandleZAxisMovement);
    }

    /// <summary>
    /// 회전 시작 이벤트 핸들러
    /// </summary>
    void HandleRotationStart(Vector3 axis, float angle)
    {
        Debug.Log($"<color=green>회전 시작</color> 축: {axis}, 각도: {angle:F2}°");

        // 사운드 재생
        if (audioSource && rotationStartSound)
        {
            audioSource.PlayOneShot(rotationStartSound);
        }

        // 시각적 피드백
        if (rotationIndicator)
        {
            rotationIndicator.SetActive(true);
        }

        // 회전 축 표시
        ShowRotationAxis(axis);

        // 실제 게임 로직 예시
        ApplyRotationLogic(axis, angle);
    }

    /// <summary>
    /// 회전 중 이벤트 핸들러
    /// </summary>
    void HandleRotating(Vector3 axis, float angle)
    {
        // 실시간 회전 반영 (예: 오브젝트 회전)
        UpdateRotatingObject(axis, angle);
    }

    /// <summary>
    /// Z축 이동 이벤트 핸들러
    /// </summary>
    void HandleZAxisMovement(float distance)
    {
        Debug.Log($"<color=cyan>Z축 이동 감지/color> 거리: {distance:F3}m");
    }

    /// <summary>
    /// 회전 축 시각화
    /// </summary>
    void ShowRotationAxis(Vector3 localAxis)
    {
        if (rotationAxisLine == null) return;

        Vector3 worldAxis = wristDetector.transform.TransformDirection(localAxis);

        rotationAxisLine.positionCount = 2;
        rotationAxisLine.SetPosition(0, wristDetector.transform.position);
        rotationAxisLine.SetPosition(1, wristDetector.transform.position + worldAxis * 0.3f);

        // 축에 따라 색상 변경
        if (Mathf.Abs(localAxis.x) > 0.5f)
            rotationAxisLine.startColor = rotationAxisLine.endColor = Color.red;
        else if (Mathf.Abs(localAxis.y) > 0.5f)
            rotationAxisLine.startColor = rotationAxisLine.endColor = Color.green;
        else
            rotationAxisLine.startColor = rotationAxisLine.endColor = Color.blue;
    }

    /// <summary>
    /// 회전 로직 적용 예시
    /// </summary>
    void ApplyRotationLogic(Vector3 axis, float angle)
    {
        if (Mathf.Abs(axis.z) > 0.7f) // Z축 회전 (손목 비틀기)
        {
            if (angle > 30f)
            {
                Debug.Log("Z축 시계방향 회전");
            }
            else if (angle < -30f)
            {
                Debug.Log("Z축 반시계방향 회전");
            }
        }

        else if (Mathf.Abs(axis.y) > 0.7f) // Y축 회전
        {
            Debug.Log("Y축 회전");
        }
    }

    /// <summary>
    /// 실시간 회전 업데이트 예시
    /// </summary>
    void UpdateRotatingObject(Vector3 axis, float angle)
    {
        // 예시: 회전 중인 오브젝트를 실시간으로 회전
        if (rotationIndicator)
        {
            Vector3 worldAxis = wristDetector.transform.TransformDirection(axis);
            rotationIndicator.transform.Rotate(worldAxis, angle, Space.World);
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (wristDetector != null)
        {
            wristDetector.OnTwisted.RemoveListener(HandleRotationStart);
            wristDetector.WhileTwisted.RemoveListener(HandleRotating);
            wristDetector.OnZAxisMoved.RemoveListener(HandleZAxisMovement);
        }
    }
}