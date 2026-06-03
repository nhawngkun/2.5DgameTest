using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class WaterZone : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Tốc độ di chuyển của Player khi ở dưới nước (nhân với tốc độ gốc)")]
    public float WaterSpeedMultiplier = 0.6f;

    [Header("Submersion Settings")]
    [Tooltip("Độ chìm của sprite nhân vật dưới nước (âm = đi xuống)")]
    public float SubmersionOffset = -0.25f;
    [Tooltip("Tốc độ chuyển tiếp khi chìm/nổi")]
    public float SubmersionSpeed = 5.0f;

    [Header("Ripple Settings")]
    [Tooltip("Khoảng thời gian giữa mỗi lần tạo sóng gợn nước khi di chuyển")]
    public float RippleInterval = 0.45f;
    [Tooltip("Kích thước tối đa của sóng gợn nước")]
    public float RippleMaxRadius = 0.6f;
    [Tooltip("Thời gian tồn tại của sóng gợn nước")]
    public float RippleDuration = 0.5f;
    [Tooltip("Màu sắc gợn sóng")]
    public Color RippleColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("Concentric Ripple Settings")]
    [Tooltip("Số lượng vòng tròn đồng tâm khi tạo sóng")]
    [Range(1, 6)]
    public int RippleRingCount = 3;
    [Tooltip("Khoảng thời gian trễ giữa các vòng gợn nước")]
    public float RippleRingDelay = 0.15f;

    private struct WadingPlayer
    {
        public PlayerController controller;
        public Transform spriteTransform;
        public float currentOffset;
        public float rippleTimer;
    }

    private List<WadingPlayer> _wadingPlayers = new List<WadingPlayer>();
    private List<WadingPlayer> _exitingPlayers = new List<WadingPlayer>();

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            if (_wadingPlayers.Exists(wp => wp.controller == player))
                return;

            int exitIdx = _exitingPlayers.FindIndex(ep => ep.controller == player);
            float startOffset = 0f;
            if (exitIdx >= 0)
            {
                startOffset = _exitingPlayers[exitIdx].currentOffset;
                _exitingPlayers.RemoveAt(exitIdx);
            }

            player.SpeedMultiplier = WaterSpeedMultiplier;

            Transform spriteTransform = player._Animator != null ? player._Animator.transform : player.transform;

            WadingPlayer wp = new WadingPlayer
            {
                controller = player,
                spriteTransform = spriteTransform,
                currentOffset = startOffset,
                rippleTimer = 0f
            };

            _wadingPlayers.Add(wp);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            int index = _wadingPlayers.FindIndex(wp => wp.controller == player);
            if (index >= 0)
            {
                var wp = _wadingPlayers[index];
                
                wp.controller.SpeedMultiplier = 1f;

                _exitingPlayers.Add(wp);
                _wadingPlayers.RemoveAt(index);
            }
        }
    }

    private void Update()
    {
        for (int i = 0; i < _wadingPlayers.Count; i++)
        {
            var wp = _wadingPlayers[i];
            
            wp.currentOffset = Mathf.MoveTowards(wp.currentOffset, SubmersionOffset, SubmersionSpeed * Time.deltaTime);
            if (wp.spriteTransform != null)
            {
                wp.spriteTransform.localPosition = new Vector3(
                    wp.spriteTransform.localPosition.x,
                    wp.currentOffset,
                    wp.spriteTransform.localPosition.z
                );
            }

            Rigidbody rb = wp.controller.GetComponent<Rigidbody>();
            bool isMoving = (rb != null && rb.linearVelocity.sqrMagnitude > 0.05f);

            if (isMoving)
            {
                wp.rippleTimer += Time.deltaTime;
                if (wp.rippleTimer >= RippleInterval)
                {
                    StartCoroutine(SpawnConcentricRipplesCoroutine(wp.controller.transform.position));
                    wp.rippleTimer = 0f;
                }
            }
            else
            {
                wp.rippleTimer = RippleInterval; 
            }

            _wadingPlayers[i] = wp; 
        }

        for (int i = _exitingPlayers.Count - 1; i >= 0; i--)
        {
            var ep = _exitingPlayers[i];
            
            ep.currentOffset = Mathf.MoveTowards(ep.currentOffset, 0f, SubmersionSpeed * Time.deltaTime);
            if (ep.spriteTransform != null)
            {
                ep.spriteTransform.localPosition = new Vector3(
                    ep.spriteTransform.localPosition.x,
                    ep.currentOffset,
                    ep.spriteTransform.localPosition.z
                );
            }

            if (Mathf.Approximately(ep.currentOffset, 0f))
            {
                _exitingPlayers.RemoveAt(i);
            }
            else
            {
                _exitingPlayers[i] = ep;
            }
        }
    }

    private IEnumerator SpawnConcentricRipplesCoroutine(Vector3 position)
    {
        for (int r = 0; r < RippleRingCount; r++)
        {
            float ringScale = 1.0f - (r * 0.2f);
            SpawnSingleRipple(position, RippleMaxRadius * ringScale);

            if (r < RippleRingCount - 1)
            {
                yield return new WaitForSeconds(RippleRingDelay);
            }
        }
    }

    private void SpawnSingleRipple(Vector3 position, float radius)
    {
        GameObject rippleGo = new GameObject("WaterRipple_Procedural");
        rippleGo.transform.position = position;
        
        WaterRipple ripple = rippleGo.AddComponent<WaterRipple>();
        ripple.MaxRadius = radius;
        ripple.Duration = RippleDuration;
        ripple.RippleColor = RippleColor;
    }

    private void OnDisable()
    {
        foreach (var wp in _wadingPlayers)
        {
            if (wp.controller != null)
            {
                wp.controller.SpeedMultiplier = 1f;
            }
            if (wp.spriteTransform != null)
            {
                wp.spriteTransform.localPosition = new Vector3(
                    wp.spriteTransform.localPosition.x,
                    0f,
                    wp.spriteTransform.localPosition.z
                );
            }
        }
        foreach (var ep in _exitingPlayers)
        {
            if (ep.controller != null)
            {
                ep.controller.SpeedMultiplier = 1f;
            }
            if (ep.spriteTransform != null)
            {
                ep.spriteTransform.localPosition = new Vector3(
                    ep.spriteTransform.localPosition.x,
                    0f,
                    ep.spriteTransform.localPosition.z
                );
            }
        }
        _wadingPlayers.Clear();
        _exitingPlayers.Clear();
    }
}
