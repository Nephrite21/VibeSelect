using UnityEngine;

/// <summary>
/// VRControllerMovementTracker 사용 예제
/// 이 스크립트를 참고하여 원하는 기능을 구현하세요
/// </summary>
public class VRControllerUsageExample : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VRControllerMovementTracker controllerTracker;

    [Header("Test Objects")]
    [SerializeField] private GameObject rotationTestObject;
    [SerializeField] private GameObject thrustTestObject;

    void Start()
    {
        // 스크립트가 연결되지 않았다면 자동으로 찾기
        if (controllerTracker == null)
        {
            controllerTracker = FindObjectOfType<VRControllerMovementTracker>();
        }

        if (controllerTracker != null)
        {
            // 이벤트 리스너 등록
            controllerTracker.OnTwisted.AddListener(OnRotationStarted);
            controllerTracker.WhileTwisted.AddListener(OnRotating);
            controllerTracker.OnZAxisMoved.AddListener(OnThrust);
        }
        else
        {
            Debug.LogError("VRControllerMovementTracker를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 회전이 시작되었을 때 호출
    /// </summary>
    private void OnRotationStarted(Vector3 axis, float magnitude)
    {
        Debug.Log($"<color=green>회전 시작!</color> 주요 축: {axis}, 각도: {magnitude:F2}도");

        // 예제 1: 단일 축 회전 처리 (이제 정확히 한 축만 전달됨)
        if (axis == Vector3.right || axis == -Vector3.right) // X축 (Roll)
        {
            Debug.Log("X축 회전 (Roll) 감지!");
            // 여기에 Roll 동작 처리
        }
        else if (axis == Vector3.up || axis == -Vector3.up) // Y축 (Yaw)
        {
            Debug.Log("Y축 회전 (Yaw - 손목 비틀기) 감지!");
            // 여기에 손목 비틀기 동작 처리
        }
        else if (axis == Vector3.forward || axis == -Vector3.forward) // Z축 (Pitch)
        {
            Debug.Log("Z축 회전 (Pitch) 감지!");
            // 여기에 Pitch 동작 처리
        }

        // 예제 2: 회전 방향 판별
        if (axis.y > 0)
        {
            Debug.Log("시계방향 회전");
        }
        else if (axis.y < 0)
        {
            Debug.Log("반시계방향 회전");
        }

        // 예제 3: 사운드 재생
        // AudioSource.PlayOneShot(rotationSound);
    }

    /// <summary>
    /// 회전 중일 때 지속적으로 호출
    /// </summary>
    private void OnRotating(Vector3 axis, float magnitude)
    {
        // Debug.Log($"회전 중... 주요 축: {axis}, 각도: {magnitude:F2}도");

        // 예제 1: 오브젝트를 주요 회전 축을 따라 회전
        if (rotationTestObject != null)
        {
            // 이제 axis는 정확히 (1,0,0), (0,1,0), (0,0,1) 중 하나
            rotationTestObject.transform.Rotate(axis, magnitude * 2f, Space.World);
        }

        // 예제 2: 축별로 다른 효과
        if (axis.y != 0) // Y축 회전
        {
            // Y축 회전 시 특별한 효과
            // PlayYAxisEffect();
        }

        // 예제 3: UI 업데이트
        // UpdateRotationUI(axis, magnitude);
    }

    /// <summary>
    /// Z축 방향으로 이동(찌르기)했을 때 호출
    /// </summary>
    private void OnThrust(float movement)
    {
        string direction = movement > 0 ? "앞으로" : "뒤로";
        Debug.Log($"<color=cyan>찌르기 감지!</color> {direction} {Mathf.Abs(movement):F4}m");

        // 예제 1: 오브젝트를 앞뒤로 이동
        if (thrustTestObject != null)
        {
            thrustTestObject.transform.Translate(Vector3.forward * movement * 10f, Space.Self);
        }

        // 예제 2: 앞으로 찌를 때만 동작
        if (movement > 0)
        {
            Debug.Log("전방 찌르기 - 공격 실행!");
            // 공격 로직
            // ExecuteAttack();
        }

        // 예제 3: 뒤로 당길 때 동작
        else if (movement < 0)
        {
            Debug.Log("후방 당기기 - 물건 잡기!");
            // 잡기 로직
            // GrabObject();
        }

        // 예제 4: 이동 거리에 따른 강도 조절
        float intensity = Mathf.Abs(movement) * 100f;
        Debug.Log($"동작 강도: {intensity:F1}%");
    }

    void OnDestroy()
    {
        // 이벤트 리스너 제거 (메모리 누수 방지)
        if (controllerTracker != null)
        {
            controllerTracker.OnTwisted.RemoveListener(OnRotationStarted);
            controllerTracker.WhileTwisted.RemoveListener(OnRotating);
            controllerTracker.OnZAxisMoved.RemoveListener(OnThrust);
        }
    }

    // ========== 추가 유틸리티 함수 예제 ==========

    /// <summary>
    /// 특정 축 회전인지 확인
    /// </summary>
    private bool IsAxisRotation(Vector3 axis, Vector3 targetAxis, float threshold = 0.7f)
    {
        return Mathf.Abs(Vector3.Dot(axis, targetAxis)) > threshold;
    }

    /// <summary>
    /// 런타임에서 설정 변경 예제
    /// </summary>
    private void ChangeSettings()
    {
        // 회전 민감도 변경
        controllerTracker.SetRotationThreshold(70f);

        // 찌르기 민감도 변경
        controllerTracker.SetZMovementThreshold(0.02f);

        // 컨트롤러 변경 (오른손 <-> 왼손)
        controllerTracker.SetControllerNode(UnityEngine.XR.XRNode.LeftHand);
    }
}