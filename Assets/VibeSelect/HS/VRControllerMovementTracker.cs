using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

public class VRControllerMovementTracker : MonoBehaviour
{
    [Header("Controller Settings")]
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;

    [Header("Rotation Detection")]
    [SerializeField] private float rotationThreshold = 10f; // 회전 감지 임계값 (도)

    [Header("Z-Axis Movement Detection")]
    [SerializeField] private float zMovementThreshold = 0.01f; // Z축 이동 감지 임계값

    // Events
    public UnityEvent<Vector3, float> OnTwisted;
    public UnityEvent<Vector3, float> WhileTwisted;
    public UnityEvent<float> OnZAxisMoved;

    // Private variables
    private Quaternion previousRotation;
    private Vector3 previousPosition;
    private bool isCurrentlyTwisting = false;
    private Vector3 currentRotationAxis = Vector3.zero;
    private float currentRotationMagnitude = 0f;

    // Input devices
    private InputDevice controllerDevice;

    void Start()
    {
        // 컨트롤러 디바이스 초기화
        InitializeController();

        // 초기 회전 및 위치 설정
        if (controllerDevice.isValid)
        {
            controllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out previousRotation);
            controllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out previousPosition);
        }
    }

    void Update()
    {
        // 컨트롤러가 유효하지 않으면 재초기화 시도
        if (!controllerDevice.isValid)
        {
            InitializeController();
            return;
        }

        DetectRotation();
        DetectZAxisMovement();
    }

    /// <summary>
    /// 컨트롤러 디바이스 초기화
    /// </summary>
    private void InitializeController()
    {
        controllerDevice = InputDevices.GetDeviceAtXRNode(controllerNode);

        if (controllerDevice.isValid)
        {
            Debug.Log($"컨트롤러 연결됨: {controllerDevice.name}");
        }
    }

    /// <summary>
    /// 컨트롤러 회전 감지
    /// </summary>
    private void DetectRotation()
    {
        // 현재 회전 가져오기
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

        // 회전이 임계값을 넘었는지 확인
        if (absoluteAngle > rotationThreshold * Time.deltaTime)
        {
            // 축을 정규화
            if (axis.sqrMagnitude > 0.001f)
            {
                axis.Normalize();
                currentRotationAxis = axis;
                currentRotationMagnitude = absoluteAngle;

                // 회전이 시작되었을 때
                if (!isCurrentlyTwisting)
                {
                    isCurrentlyTwisting = true;
                    OnTwisted?.Invoke(axis, absoluteAngle);
                }

                // 회전 중
                WhileTwisted?.Invoke(axis, absoluteAngle);
            }
        }
        else
        {
            // 회전이 멈췄을 때
            if (isCurrentlyTwisting)
            {
                isCurrentlyTwisting = false;
            }
        }

        previousRotation = currentRotation;
    }

    /// <summary>
    /// Z축 방향 이동 감지
    /// </summary>
    private void DetectZAxisMovement()
    {
        // 현재 위치 가져오기
        if (!controllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 currentPosition))
            return;

        // 위치 변화 계산
        Vector3 deltaPosition = currentPosition - previousPosition;

        // 컨트롤러의 로컬 Z축 방향으로 투영
        if (controllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion controllerRotation))
        {
            Vector3 localZAxis = controllerRotation * Vector3.forward;

            // Z축 방향 이동 계산
            float zMovement = Vector3.Dot(deltaPosition, localZAxis);

            // 임계값을 넘는 Z축 이동이 감지되면
            if (Mathf.Abs(zMovement) > zMovementThreshold)
            {
                OnZAxisMoved?.Invoke(zMovement);
            }
        }

        previousPosition = currentPosition;
    }

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
    }
}