using UnityEngine;
using UnityEngine.XR;

public class VRControllerExample : MonoBehaviour
{
    private VRControllerMovementTracker tracker;

    void Start()
    {
        // VRControllerMovementTracker 컴포넌트 가져오기
        tracker = GetComponent<VRControllerMovementTracker>();

        // 이벤트 리스너 등록
        tracker.OnTwisted.AddListener(HandleOnTwisted);
        tracker.WhileTwisted.AddListener(HandleWhileTwisted);
        tracker.OnZAxisMoved.AddListener(HandleOnZAxisMoved);
    }

    void Update()
    {
        // 예제: 버튼을 눌러서 왼손/오른손 전환
        // A 버튼으로 오른손, B 버튼으로 왼손으로 전환
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool aButton) && aButton)
        {
            tracker.SetControllerNode(XRNode.RightHand);
            Debug.Log("오른손 컨트롤러로 전환");
        }

        if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bButton) && bButton)
        {
            tracker.SetControllerNode(XRNode.LeftHand);
            Debug.Log("왼손 컨트롤러로 전환");
        }
    }

    /// <summary>
    /// 회전이 시작되었을 때 호출
    /// </summary>
    private void HandleOnTwisted(Vector3 axis, float magnitude)
    {
        Debug.Log($"회전 시작! 축: {axis}, 크기: {magnitude}도");

        // 주요 축 판별
        string axisName = GetAxisName(axis);
        Debug.Log($"주요 회전 축: {axisName}");

        // 회전 시작 시 수행할 액션
        // 예: 사운드 재생, 햅틱 피드백 등
    }

    /// <summary>
    /// 회전 중일 때 계속 호출
    /// </summary>
    private void HandleWhileTwisted(Vector3 axis, float magnitude)
    {
        // Debug.Log($"회전 중... 축: {axis}, 크기: {magnitude}도");

        // 회전에 따른 액션 수행
        // 예: 오브젝트 회전, UI 업데이트 등

        // 예제: 이 오브젝트를 회전 축을 따라 회전
        transform.Rotate(axis, magnitude * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// Z축 방향으로 이동했을 때 호출
    /// </summary>
    private void HandleOnZAxisMoved(float movement)
    {
        string direction = movement > 0 ? "앞으로" : "뒤로";
        Debug.Log($"Z축 이동: {direction} {Mathf.Abs(movement)}m");

        // Z축 이동에 따른 액션 수행
        // 예: 오브젝트 밀기/당기기, 줌 인/아웃 등

        // 예제: 이 오브젝트를 Z축 방향으로 이동
        transform.Translate(Vector3.forward * movement, Space.Self);
    }

    /// <summary>
    /// 회전 축의 주요 방향 판별
    /// </summary>
    private string GetAxisName(Vector3 axis)
    {
        float absX = Mathf.Abs(axis.x);
        float absY = Mathf.Abs(axis.y);
        float absZ = Mathf.Abs(axis.z);

        if (absX > absY && absX > absZ)
            return axis.x > 0 ? "+X (Roll Right)" : "-X (Roll Left)";
        else if (absY > absX && absY > absZ)
            return axis.y > 0 ? "+Y (Yaw Right)" : "-Y (Yaw Left)";
        else
            return axis.z > 0 ? "+Z (Pitch Up)" : "-Z (Pitch Down)";
    }

    void OnDestroy()
    {
        // 이벤트 리스너 제거
        if (tracker != null)
        {
            tracker.OnTwisted.RemoveListener(HandleOnTwisted);
            tracker.WhileTwisted.RemoveListener(HandleWhileTwisted);
            tracker.OnZAxisMoved.RemoveListener(HandleOnZAxisMoved);
        }
    }
}