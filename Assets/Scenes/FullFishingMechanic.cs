using System.Collections.Generic;
using UnityEngine;

public class FullFishingMechanic : MonoBehaviour
{
    [Header("Components")]
    public Transform rodTip;          // Đầu cần câu
    public Rigidbody bobberRb;        // Rigidbody của phao câu
    public LineRenderer lineRenderer; // Dùng để vẽ sợi dây câu

    [Header("Rope Settings (Verlet)")]
    public int segmentCount = 30;     // Số lượng mắt xích của dây (càng cao dây càng mượt)
    public float ropeLength = 5f;     // Chiều dài tối đa của dây câu
    public float gravity = -9.81f;    // Trọng lực tác động lên dây
    public int constraintIterations = 5; // Độ chính xác của vòng lặp vật lý

    [Header("Casting & Reeling")]
    public float castForceForward = 20f;
    public float castForceUp = 10f;
    public float reelSpeed = 5f;      // Tốc độ thu dây

    private List<RopeSegment> ropeSegments = new List<RopeSegment>();
    private float targetLength;       // Chiều dài dây hiện tại (thay đổi khi thu/thả)
    private bool isCasted = false;

    // Cấu trúc quản lý từng điểm nút trên dây câu
    private struct RopeSegment
    {
        public Vector3 currentPosition;
        public Vector3 oldPosition;

        public RopeSegment(Vector3 pos)
        {
            currentPosition = pos;
            oldPosition = pos;
        }
    }

    void Start()
    {
        targetLength = ropeLength;
        lineRenderer.positionCount = segmentCount;

        // Khởi tạo các điểm trên dây ban đầu xếp ngay tại đầu cần câu
        for (int i = 0; i < segmentCount; i++)
        {
            ropeSegments.Add(new RopeSegment(rodTip.position));
        }

        // Ban đầu đóng băng phao câu lại tại đầu cần
        bobberRb.isKinematic = true;
    }

    void Update()
    {
        // 1. CLICK CHUỘT TRÁI: Quăng câu
        if (Input.GetMouseButtonDown(0) && !isCasted)
        {
            CastLine();
        }

        // 2. GIỮ CHUỘT PHẢI: Thu dây về (Y hệt hành động quay máy câu trong video)
        if (Input.GetMouseButton(1) && isCasted)
        {
            ReelIn();
        }

        // Nếu chưa quăng, phao câu luôn dính chặt vào đầu cần
        if (!isCasted)
        {
            bobberRb.transform.position = rodTip.position;
        }

        // Vẽ dây câu lên màn hình
        DrawRope();
    }

    void FixedUpdate()
    {
        if (!isCasted) return;
        // Làm cần câu tự động hướng và cong nhẹ về phía phao câu khi chịu lực kéo
        if (isCasted)
        {
            Vector3 directionToBobber = (bobberRb.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(directionToBobber);

            // Tạo độ mượt bám theo (Lấy mốc uốn cong nhẹ)
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 2f);
        }

        SimulateRope();
        ApplyConstraints();
    }

    // Cơ chế quăng phao câu bay đi
    void CastLine()
    {
        isCasted = true;
        bobberRb.isKinematic = false;
        targetLength = ropeLength;

        // Reset vị trí các mắt xích dây theo vị trí hiện tại
        for (int i = 0; i < segmentCount; i++)
        {
            ropeSegments[i] = new RopeSegment(rodTip.position);
        }

        // Tính hướng quăng dựa theo góc nhìn của đầu cần
        Vector3 forceDirection = rodTip.forward * castForceForward + rodTip.up * castForceUp;
        bobberRb.AddForce(forceDirection, ForceMode.Impulse);
    }

    // Cơ chế thu dây (Thu ngắn chiều dài dây và kéo phao về)
    void ReelIn()
    {
        // Thu ngắn khoảng cách dây dần dần
        targetLength -= reelSpeed * Time.deltaTime;
        if (targetLength < 0.2f) targetLength = 0.2f;

        // Tác dụng lực kéo phao câu về phía đầu cần câu
        Vector3 directionToRod = (rodTip.position - bobberRb.position).normalized;
        float distance = Vector3.Distance(rodTip.position, bobberRb.position);

        // Lực kéo mạnh dần nếu phao ở càng xa so với độ dài dây hiện tại
        if (distance > targetLength)
        {
            bobberRb.AddForce(directionToRod * reelSpeed * 2f, ForceMode.Acceleration);
        }

        // Nếu phao đã sát đầu cần câu thì reset trạng thái quăng
        if (distance < 0.5f && targetLength <= 0.5f)
        {
            isCasted = false;
            bobberRb.isKinematic = true;
        }
    }

    // Mô phỏng vật lý dây bằng thuật toán Verlet Integration
    void SimulateRope()
    {
        Vector3 forceGravity = new Vector3(0f, gravity, 0f);

        for (int i = 0; i < segmentCount; i++)
        {
            RopeSegment segment = ropeSegments[i];
            Vector3 velocity = segment.currentPosition - segment.oldPosition;
            segment.oldPosition = segment.currentPosition;
            segment.currentPosition += velocity;
            segment.currentPosition += forceGravity * Time.fixedDeltaTime;
            ropeSegments[i] = segment;
        }
    }

    // Giữ khoảng cách cố định giữa các mắt xích dây
    void ApplyConstraints()
    {
        // Điểm đầu tiên luôn dính vào đầu cần câu
        RopeSegment firstSegment = ropeSegments[0];
        firstSegment.currentPosition = rodTip.position;
        ropeSegments[0] = firstSegment;

        // Điểm cuối cùng luôn dính vào Phao câu
        RopeSegment lastSegment = ropeSegments[segmentCount - 1];
        lastSegment.currentPosition = bobberRb.position;
        ropeSegments[segmentCount - 1] = lastSegment;

        // Tính khoảng cách tối đa giữa mỗi mắt xích dựa trên chiều dài dây hiện tại
        float targetSegmentLength = targetLength / (segmentCount - 1);

        for (int k = 0; k < constraintIterations; k++)
        {
            for (int i = 0; i < segmentCount - 1; i++)
            {
                RopeSegment segmentA = ropeSegments[i];
                RopeSegment segmentB = ropeSegments[i + 1];

                float dist = Vector3.Distance(segmentA.currentPosition, segmentB.currentPosition);
                float error = Mathf.Abs(dist - targetSegmentLength);
                Vector3 changeDir = Vector3.zero;

                if (dist > targetSegmentLength)
                {
                    changeDir = (segmentA.currentPosition - segmentB.currentPosition).normalized;
                }
                else if (dist < targetSegmentLength)
                {
                    changeDir = (segmentB.currentPosition - segmentA.currentPosition).normalized;
                }

                Vector3 changeAmount = changeDir * error;
                if (i != 0)
                {
                    segmentA.currentPosition -= changeAmount * 0.5f;
                    ropeSegments[i] = segmentA;
                    segmentB.currentPosition += changeAmount * 0.5f;
                    ropeSegments[i + 1] = segmentB;
                }
                else
                {
                    // Điểm đầu cố định nên chỉ dịch chuyển điểm thứ 2
                    segmentB.currentPosition += changeAmount;
                    ropeSegments[i + 1] = segmentB;
                }
            }
        }
    }

    // Cập nhật tọa độ hiển thị cho LineRenderer
    void DrawRope()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            lineRenderer.SetPosition(i, ropeSegments[i].currentPosition);
        }
    }
}