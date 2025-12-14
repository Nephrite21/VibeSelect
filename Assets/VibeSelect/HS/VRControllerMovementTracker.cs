using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

/// <summary>
/// VR 컨트롤러의 회전 및 Z축 이동을 감지하는 스크립트
/// OpenXR을 사용하여 Meta Quest 2 컨트롤러를 추적합니다
/// </summary>
public class VRControllerMovementTracker : MonoBehaviour
{
    [Header("Controller Settings")]
    [Tooltip("추적할 컨트롤러 선택 (Right Hand 또는 Left Hand)")]
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;

    [Header("Rotation Detection Settings")]
    [Tooltip("회전 감지 임계값 (도/초) - 값이 클수록 큰 회전만 감지")]
    [SerializeField] private float rotationThreshold = 50f;

    [Tooltip("회전 중지 판정 시간 (초) - 이 시간 동안 회전이 없으면 중지로 판정")]
    [SerializeField] private float rotationStopDelay = 0.15f;

    [Header("Z-Axis Movement Detection Settings")]
    [Tooltip("Z축 이동 감지 임계값 (미터) - 찌르기 동작 민감도")]
    [SerializeField] private float zMovementThreshold = 0.015f;

    [Header("Debug Settings")]
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool showDebugLogs = false;

    [Header("Events")]
    [Tooltip("회전이 시작되었을 때 발생 (주요 축, 각도)")]
    public UnityEvent<Vector3, float> OnTwisted;

    [Tooltip("회전 중일 때 지속적으로 발생 (주요 축, 각도)")]
    public UnityEvent<Vector3, float> WhileTwisted;

    [Tooltip("Z축 방향으로 이동(찌르기)했을 때 발생 (이동 거리)")]
    public UnityEvent<float> OnZAxisMoved;

    // Private variables
    private InputDevice controllerDevice;
    private Quaternion previousRotation;
    private Vector3 previousPosition;
    private bool isCurrentlyTwisting = false;
    private float lastRotationTime = 0f;
    private Vector3 currentRotationAxis = Vector3.zero;
    private float currentRotationMagnitude = 0f;

    void Start()
    {
        InitializeController();

        if (controllerDevice.isValid)
        {
            controllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out previousRotation);
            controllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out previousPosition);

            if (showDebugLogs)
            {
                Debug.Log($"[VRController] 컨트롤러 초기화 완료: {controllerDevice.name} ({controllerNode})");
            }
        }
        else
        {
            Debug.LogWarning($"[VRController] 컨트롤러를 찾을 수 없습니다: {controllerNode}");
        }
    }

    void Update()
    {
        if (!controllerDevice.isValid)
        {
            InitializeController();
            return;
        }

        DetectRotation();
        DetectZAxisMovement();
        CheckRotationStop();
    }

    /// <summary>
    /// 컨트롤러 디바이스 초기화
    /// </summary>
    private void InitializeController()
    {
        controllerDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
    }

    /// <summary>
    /// 컨트롤러 회전 감지 (컨트롤러 로컬 공간 기준)
    /// </summary>
    private void DetectRotation()
    {
        if (!controllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion currentRotation))
            return;

        // 회전 차이 계산
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);

        // 회전을 축-각도로 변환
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

        // 각도를 -180 ~ 180 범위로 정규화
        if (angle > 180f)
            angle -= 360f;

        float absoluteAngle = Mathf.Abs(angle);

        // 초당 회전 속도 계산
        float rotationSpeed = absoluteAngle / Time.deltaTime;

        // 회전이 임계값을 넘었는지 확인
        if (rotationSpeed > rotationThreshold && axis.sqrMagnitude > 0.001f)
        {
            // 축을 정규화 (길이를 1로 만들어 방향만 유지)
            axis.Normalize();

            // 컨트롤러 로컬 공간 기준으로 변환
            Vector3 localAxis = currentRotation * axis;

            currentRotationAxis = localAxis;
            currentRotationMagnitude = absoluteAngle;
            lastRotationTime = Time.time;

            // 회전이 시작되었을 때
            if (!isCurrentlyTwisting)
            {
                isCurrentlyTwisting = true;
                OnTwisted?.Invoke(localAxis, absoluteAngle);

                if (showDebugLogs)
                {
                    string axisName = GetAxisName(localAxis);
                    Debug.Log($"[VRController] 회전 시작! 축: {axisName}, 각도: {absoluteAngle:F2}도");
                }
            }

            // 회전 중
            WhileTwisted?.Invoke(localAxis, absoluteAngle);
        }

        previousRotation = currentRotation;
    }

    /// <summary>
    /// 회전 중지 확인 (일정 시간 동안 회전이 없으면 중지로 판정)
    /// </summary>
    private void CheckRotationStop()
    {
        if (isCurrentlyTwisting && (Time.time - lastRotationTime) > rotationStopDelay)
        {
            isCurrentlyTwisting = false;

            if (showDebugLogs)
            {
                Debug.Log("[VRController] 회전 중지");
            }
        }
    }

    /// <summary>
    /// Z축 방향 이동 감지 (찌르기 동작)
    /// </summary>
    private void DetectZAxisMovement()
    {
        if (!controllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 currentPosition))
            return;

        // 위치 변화 계산
        Vector3 deltaPosition = currentPosition - previousPosition;

        // 컨트롤러의 로컬 Z축 방향 (컨트롤러가 가리키는 방향)
        if (controllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion controllerRotation))
        {
            Vector3 forwardDirection = controllerRotation * Vector3.forward;

            // Z축 방향 이동량 계산 (컨트롤러가 가리키는 방향으로의 이동)
            float zMovement = Vector3.Dot(deltaPosition, forwardDirection);

            // 임계값을 넘는 Z축 이동이 감지되면
            if (Mathf.Abs(zMovement) > zMovementThreshold)
            {
                OnZAxisMoved?.Invoke(zMovement);

                if (showDebugLogs)
                {
                    string direction = zMovement > 0 ? "앞으로(찌르기)" : "뒤로";
                    Debug.Log($"[VRController] Z축 이동: {direction} {Mathf.Abs(zMovement):F4}m");
                }
            }
        }

        previousPosition = currentPosition;
    }

    /// <summary>
    /// 회전 축의 주요 방향 이름 반환
    /// </summary>
    private string GetAxisName(Vector3 axis)
    {
        float absX = Mathf.Abs(axis.x);
        float absY = Mathf.Abs(axis.y);
        float absZ = Mathf.Abs(axis.z);

        if (absX > absY && absX > absZ)
            return axis.x > 0 ? "X+ (Roll Right)" : "X- (Roll Left)";
        else if (absY > absX && absY > absZ)
            return axis.y > 0 ? "Y+ (Yaw Right)" : "Y- (Yaw Left)";
        else
            return axis.z > 0 ? "Z+ (Pitch Up)" : "Z- (Pitch Down)";
    }

    // ========== Public API ==========

    /// <summary>
    /// 현재 회전 중인지 확인
    /// </summary>
    public bool IsTwisting()
    {
        return isCurrentlyTwisting;
    }

    /// <summary>
    /// 현재 회전 축 가져오기
    /// </summary>
    public Vector3 GetCurrentRotationAxis()
    {
        return currentRotationAxis;
    }

    /// <summary>
    /// 현재 회전 크기 가져오기
    /// </summary>
    public float GetCurrentRotationMagnitude()
    {
        return currentRotationMagnitude;
    }

    /// <summary>
    /// 컨트롤러 노드 변경 (런타임에서 왼손/오른손 전환)
    /// </summary>
    public void SetControllerNode(XRNode node)
    {
        controllerNode = node;
        InitializeController();

        if (showDebugLogs)
        {
            Debug.Log($"[VRController] 컨트롤러 변경: {node}");
        }
    }

    /// <summary>
    /// 회전 임계값 변경
    /// </summary>
    public void SetRotationThreshold(float threshold)
    {
        rotationThreshold = threshold;

        if (showDebugLogs)
        {
            Debug.Log($"[VRController] 회전 임계값 변경: {threshold}");
        }
    }

    /// <summary>
    /// Z축 이동 임계값 변경
    /// </summary>
    public void SetZMovementThreshold(float threshold)
    {
        zMovementThreshold = threshold;

        if (showDebugLogs)
        {
            Debug.Log($"[VRController] Z축 이동 임계값 변경: {threshold}");
        }
    }
}