using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Oculus Quest 2 컨트롤러의 손목 회전 및 Z축 이동을 감지하는 컴포넌트
/// </summary>
public class WristRotationDetector : MonoBehaviour
{
    [Header("Controller Settings")]
    [Tooltip("감지할 컨트롤러 (Left/Right)")]
    public XRNode controllerNode = XRNode.RightHand;
    
    [Header("Rotation Detection Settings")]
    [Tooltip("회전 감지를 위한 이전 프레임 수")]
    [Range(5, 30)]
    public int rotationHistorySize = 10;
    
    [Tooltip("회전으로 인식하기 위한 최소 각도 (도)")]
    [Range(5f, 45f)]
    public float rotationThreshold = 15f;
    
    [Tooltip("회전 중으로 판단하기 위한 최소 각속도 (도/초)")]
    [Range(10f, 200f)]
    public float angularVelocityThreshold = 30f;
    
    [Tooltip("회전이 끝났다고 판단하는 각속도 임계값 (도/초)")]
    [Range(5f, 50f)]
    public float rotationEndThreshold = 15f;
    
    [Header("Z-Axis Movement Settings")]
    [Tooltip("Z축 이동 감지를 위한 이전 프레임 수")]
    [Range(5, 30)]
    public int zMovementHistorySize = 10;
    
    [Tooltip("Z축 이동으로 인식하기 위한 최소 거리 (미터)")]
    [Range(0.05f, 0.5f)]
    public float zMovementThreshold = 0.1f;
    
    [Tooltip("Z축 이동 속도 임계값 (미터/초)")]
    [Range(0.1f, 2f)]
    public float zVelocityThreshold = 0.3f;
    
    [Header("Smoothing Settings")]
    [Tooltip("회전 축 스무딩 강도 (0=없음, 1=최대)")]
    [Range(0f, 1f)]
    public float axisSmoothing = 0.3f;
    
    [Header("Events")]
    [Tooltip("회전이 시작되었을 때 발생 (주요 축, 각도)")]
    public UnityEvent<Vector3, float> OnTwisted;
    
    [Tooltip("회전 중일 때 지속적으로 발생 (주요 축, 각도)")]
    public UnityEvent<Vector3, float> WhileTwisted;
    
    [Tooltip("Z축 방향으로 이동(찌르기)했을 때 발생 (이동 거리)")]
    public UnityEvent<float> OnZAxisMoved;
    
    // 내부 상태 변수
    private Queue<Quaternion> rotationHistory = new Queue<Quaternion>();
    private Queue<Vector3> positionHistory = new Queue<Vector3>();
    private bool isRotating = false;
    private Vector3 currentRotationAxis = Vector3.zero;
    private Vector3 smoothedRotationAxis = Vector3.zero;
    private float accumulatedRotation = 0f;
    
    // 컨트롤러 입력 장치
    private InputDevice targetDevice;
    
    // 이전 프레임 데이터
    private Quaternion previousRotation;
    private Vector3 previousPosition;
    private bool isInitialized = false;

    void Start()
    {
        InitializeDevice();
    }

    void Update()
    {
        if (!targetDevice.isValid)
        {
            InitializeDevice();
            return;
        }
        
        // 현재 컨트롤러의 위치와 회전 가져오기
        Vector3 currentPosition;
        Quaternion currentRotation;
        
        if (targetDevice.TryGetFeatureValue(CommonUsages.devicePosition, out currentPosition) &&
            targetDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out currentRotation))
        {
            if (!isInitialized)
            {
                previousRotation = currentRotation;
                previousPosition = currentPosition;
                isInitialized = true;
                return;
            }
            
            // 회전 감지 처리
            ProcessRotation(currentRotation);
            
            // Z축 이동 감지 처리
            ProcessZAxisMovement(currentPosition, currentRotation);
            
            // 이전 프레임 데이터 업데이트
            previousRotation = currentRotation;
            previousPosition = currentPosition;
        }
    }

    /// <summary>
    /// 컨트롤러 장치 초기화
    /// </summary>
    void InitializeDevice()
    {
        targetDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
        
        if (!targetDevice.isValid)
        {
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(controllerNode, devices);
            
            if (devices.Count > 0)
            {
                targetDevice = devices[0];
            }
        }
    }

    /// <summary>
    /// 회전 감지 및 이벤트 처리
    /// </summary>
    void ProcessRotation(Quaternion currentRotation)
    {
        // 회전 히스토리에 추가
        rotationHistory.Enqueue(currentRotation);
        if (rotationHistory.Count > rotationHistorySize)
        {
            rotationHistory.Dequeue();
        }
        
        if (rotationHistory.Count < rotationHistorySize)
        {
            return;
        }
        
        // 프레임 간 회전 차이 계산
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
        deltaRotation.ToAngleAxis(out float frameAngle, out Vector3 frameAxis);
        
        // -180 ~ 180 범위로 정규화
        if (frameAngle > 180f)
        {
            frameAngle -= 360f;
        }
        
        // 각속도 계산 (도/초)
        float angularVelocity = Mathf.Abs(frameAngle) / Time.deltaTime;
        
        // 히스토리의 첫 번째와 마지막 회전 간의 차이 계산
        Quaternion oldestRotation = rotationHistory.Peek();
        Quaternion totalDeltaRotation = currentRotation * Quaternion.Inverse(oldestRotation);
        totalDeltaRotation.ToAngleAxis(out float totalAngle, out Vector3 totalAxis);
        
        // -180 ~ 180 범위로 정규화
        if (totalAngle > 180f)
        {
            totalAngle -= 360f;
        }
        
        // 회전 감지 로직
        if (!isRotating)
        {
            // 회전 시작 감지: threshold 이상 회전 + 충분한 각속도
            if (Mathf.Abs(totalAngle) >= rotationThreshold && 
                angularVelocity >= angularVelocityThreshold)
            {
                isRotating = true;
                currentRotationAxis = totalAxis.normalized;
                smoothedRotationAxis = currentRotationAxis;
                accumulatedRotation = totalAngle;
                
                // 컨트롤러 로컬 공간으로 변환
                Vector3 localAxis = transform.InverseTransformDirection(currentRotationAxis);
                
                OnTwisted?.Invoke(localAxis, totalAngle);
                
                Debug.Log($"[Rotation Started] Axis: {localAxis}, Angle: {totalAngle:F2}°, Angular Velocity: {angularVelocity:F2}°/s");
            }
        }
        else
        {
            // 회전 중
            if (angularVelocity >= rotationEndThreshold)
            {
                // 현재 프레임의 회전 축 업데이트 (스무딩 적용)
                if (frameAngle > 1f) // 의미있는 회전만 고려
                {
                    currentRotationAxis = frameAxis.normalized;
                    smoothedRotationAxis = Vector3.Lerp(smoothedRotationAxis, currentRotationAxis, 1f - axisSmoothing).normalized;
                }
                
                accumulatedRotation += frameAngle;
                
                // 컨트롤러 로컬 공간으로 변환
                Vector3 localAxis = transform.InverseTransformDirection(smoothedRotationAxis);
                
                WhileTwisted?.Invoke(localAxis, frameAngle);
                
                // 디버그 정보 (매 프레임마다는 부담스러우므로 간헐적으로)
                if (Time.frameCount % 10 == 0)
                {
                    Debug.Log($"[Rotating] Axis: {localAxis}, Frame Angle: {frameAngle:F2}°, Total: {accumulatedRotation:F2}°");
                }
            }
            else
            {
                // 회전 종료
                isRotating = false;
                Debug.Log($"[Rotation Ended] Total Rotation: {accumulatedRotation:F2}°");
                accumulatedRotation = 0f;
            }
        }
    }

    /// <summary>
    /// Z축 이동 감지 및 이벤트 처리
    /// </summary>
    void ProcessZAxisMovement(Vector3 currentPosition, Quaternion currentRotation)
    {
        // 위치 히스토리에 추가
        positionHistory.Enqueue(currentPosition);
        if (positionHistory.Count > zMovementHistorySize)
        {
            positionHistory.Dequeue();
        }
        
        if (positionHistory.Count < zMovementHistorySize)
        {
            return;
        }
        
        // 컨트롤러의 로컬 Z축 방향 (월드 공간)
        Vector3 controllerForward = currentRotation * Vector3.forward;
        
        // 히스토리 기간 동안의 총 이동
        Vector3 oldestPosition = positionHistory.Peek();
        Vector3 totalMovement = currentPosition - oldestPosition;
        
        // Z축 방향으로의 투영 (컨트롤러 기준)
        float zMovement = Vector3.Dot(totalMovement, controllerForward);
        
        // 프레임 간 이동으로 속도 계산
        Vector3 frameMovement = currentPosition - previousPosition;
        float frameZMovement = Vector3.Dot(frameMovement, controllerForward);
        float zVelocity = Mathf.Abs(frameZMovement) / Time.deltaTime;
        
        // Z축 이동 감지
        if (Mathf.Abs(zMovement) >= zMovementThreshold && 
            zVelocity >= zVelocityThreshold)
        {
            OnZAxisMoved?.Invoke(zMovement);
            
            Debug.Log($"[Z-Axis Movement] Distance: {zMovement:F3}m, Velocity: {zVelocity:F2}m/s");
            
            // 이벤트 발생 후 히스토리 초기화 (중복 감지 방지)
            positionHistory.Clear();
        }
    }

    /// <summary>
    /// 주요 회전 축 결정 (X, Y, Z 중 가장 큰 성분)
    /// </summary>
    Vector3 GetDominantAxis(Vector3 axis)
    {
        Vector3 absAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        
        if (absAxis.x > absAxis.y && absAxis.x > absAxis.z)
        {
            return new Vector3(Mathf.Sign(axis.x), 0, 0);
        }
        else if (absAxis.y > absAxis.z)
        {
            return new Vector3(0, Mathf.Sign(axis.y), 0);
        }
        else
        {
            return new Vector3(0, 0, Mathf.Sign(axis.z));
        }
    }

    void OnValidate()
    {
        // Inspector에서 값 변경 시 히스토리 크기 조정
        if (Application.isPlaying)
        {
            while (rotationHistory.Count > rotationHistorySize)
            {
                rotationHistory.Dequeue();
            }
            while (positionHistory.Count > zMovementHistorySize)
            {
                positionHistory.Dequeue();
            }
        }
    }

    // 디버그용 Gizmos
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !isRotating)
        {
            return;
        }
        
        // 현재 회전 축 시각화
        Gizmos.color = Color.yellow;
        Vector3 worldAxis = transform.TransformDirection(smoothedRotationAxis);
        Gizmos.DrawRay(transform.position, worldAxis * 0.2f);
        
        // 컨트롤러 Z축 시각화
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 0.15f);
    }
}