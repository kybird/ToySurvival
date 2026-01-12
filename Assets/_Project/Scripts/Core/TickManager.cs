using Network; // For NetworkManager reference
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 클라이언트 tick 관리 싱글톤
    /// 서버 tick과 동기화하여 tick 기반 시뮬레이션 제공
    /// </summary>
    public class TickManager : MonoBehaviour
    {
        public static TickManager Instance { get; private set; }

        // Configuration and Properties
        public int TickRate { get; private set; }
        public float TickInterval { get; private set; }

        #region Dual Timeline System

        // 1. Global Timeline (System/Lobby Time)
        // 앱 시작/로그인 시점부터 계속 흐르는 시간. 퀘스트, 상점, 채팅 등 로비 로직용.
        private uint _baseGlobalTick;
        private double _baseGlobalTime;
        private bool _globalInitialized;

        // 2. Game Timeline (Room Time)
        // 방 입장 후 첫 S_DebugServerTick 수신 시점부터 흐르는 시간. 물리, 이동 동기화용.
        private uint _baseGameTick;
        private double _baseGameTime;
        private bool _gameInitialized;
        private float _gameTickOffset = 0; // Soft Sync용 오프셋 (안전한 보정)

        // Current Tick State
        private int _clientTick;

        // Flag to prevent error logs during application quit
        private static bool _isAppQuitting = false;

        #endregion

        // ========================================
        // Unity Lifecycle
        // ========================================

        void Awake()
        {
            Debug.Log(
                $"[TickManager] Awake called on {gameObject.name}. Instance is {(Instance == null ? "NULL" : "SET")}"
            );

            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;

                // 만약 부모가 있다면 루트로 옮겨서 DontDestroyOnLoad가 보장되도록 함
                if (transform.parent != null)
                    transform.SetParent(null);

                DontDestroyOnLoad(gameObject);

                // 안전장치: Default GameConstants가 없을 경우를 대비
                TickRate = 30;
                TickInterval = 0.0333f;
                Time.fixedDeltaTime = TickInterval;

                Debug.Log(
                    $"[TickManager] Initialized. Default FixedDeltaTime: {Time.fixedDeltaTime:F4}s"
                );
            }
            else
            {
                Debug.LogWarning(
                    $"[TickManager] Duplicate instance detected on {gameObject.name}. Destroying this."
                );
                Destroy(gameObject);
            }
        }

        void OnApplicationQuit()
        {
            _isAppQuitting = true;
        }

        void OnDestroy()
        {
            if (_isAppQuitting)
                return;

            if (Instance == this)
            {
                Debug.LogError(
                    "[TickManager] Instance is being destroyed! This should not happen for a Persistent Manager."
                );
                Instance = null;
            }
        }

        // ========================================
        // Initialization
        // ========================================

        /// <summary>
        /// 로그인 시 호출. Global Timeline만 초기화합니다.
        /// </summary>
        public void InitializeGlobal(int tickRate, float tickInterval, uint currentGlobalTick)
        {
            TickRate = tickRate;
            TickInterval = tickInterval > 0 ? tickInterval : (1.0f / tickRate);
            Time.fixedDeltaTime = TickInterval;

            // Global Anchor 설정
            _baseGlobalTick = currentGlobalTick;
            _baseGlobalTime = Time.realtimeSinceStartupAsDouble;
            _globalInitialized = true;

            // Game Anchor는 아직 초기화하지 않음 (방 진입 전)
            _gameInitialized = false;

            Debug.Log(
                $"[TickManager] Global Init. Rate: {TickRate}, GlobalTick: {_baseGlobalTick}"
            );
        }

        /// <summary>
        /// 게임(룸) 진입 후 첫 틱 패킷 수신 시 호출. Game Timeline을 초기화(Anchor)합니다.
        /// 로비 대기 시간과 무관하게 방의 현재 틱을 기준으로 시간선을 새로 잡습니다.
        /// </summary>
        public void InitGameAnchor(uint roomTick, bool forceReset = false)
        {
            if (_gameInitialized && !forceReset)
                return;

            // RTT 보정 (Half RTT)
            float rttMs = (NetworkManager.Instance != null) ? NetworkManager.Instance.RTT : 0;
            int latencyTicks = Mathf.RoundToInt((rttMs / 2000f) * TickRate); // ms -> sec -> ticks

            _baseGameTick = roomTick + (uint)latencyTicks;
            _baseGameTime = Time.realtimeSinceStartupAsDouble;
            _gameTickOffset = 0; // 오프셋 리셋
            _gameInitialized = true;

            // 현재 틱 업데이트
            _clientTick = (int)_baseGameTick;

            Debug.Log(
                $"[TickManager] Game Anchor Set! ServerRoomTick: {roomTick}, LatencyBonus: {latencyTicks}, FinalBase: {_baseGameTick}, ForceReset: {forceReset}"
            );
        }

        /// <summary>
        /// 게임 종료/로비 복귀 시 호출하여 Game Timeline을 리셋합니다.
        /// </summary>
        public void ResetGameAnchor()
        {
            _gameInitialized = false;
            _gameTickOffset = 0;
            Debug.Log("[TickManager] Game Anchor Reset. Ready for new game.");
        }

        // ========================================
        // Estimation Logic
        // ========================================

        public uint EstimateGlobalTick()
        {
            if (!_globalInitialized || TickRate <= 0)
                return 0;
            double dt = Time.realtimeSinceStartupAsDouble - _baseGlobalTime;
            if (dt < 0)
                dt = 0;
            return _baseGlobalTick + (uint)(dt * TickRate);
        }

        public uint EstimateGameTick()
        {
            if (!_gameInitialized || TickRate <= 0)
                return 0;
            double dt = Time.realtimeSinceStartupAsDouble - _baseGameTime;
            if (dt < 0)
                dt = 0;
            float rawTick = _baseGameTick + (float)(dt * TickRate);
            return (uint)(rawTick + _gameTickOffset); // offset 적용
        }

        // 호환성용 (인게임 로직에서 사용)
        public uint EstimateServerTick() => EstimateGameTick();

        /// <summary>
        /// 소수점 정밀도로 현재 게임 틱을 반환 (보간용)
        /// </summary>
        public float EstimateServerTickFloat()
        {
            if (!_gameInitialized || TickRate <= 0)
                return 0f;
            double dt = Time.realtimeSinceStartupAsDouble - _baseGameTime;
            if (dt < 0)
                dt = 0;
            return _baseGameTick + (float)(dt * TickRate) + _gameTickOffset;
        }

        public bool IsInitialized() => _gameInitialized; // 인게임 기준

        void FixedUpdate()
        {
            if (!IsInitialized())
                return;

            // 인게임 틱 갱신
            _clientTick = (int)EstimateGameTick();

            if (_clientTick % 50 == 0)
            {
                // Debug.Log($"[Tick] Game: {_clientTick}, Global: {EstimateGlobalTick()}");
            }
        }

        public int GetCurrentTick() => _clientTick;

        // RTT 기반 예측 틱 (클라이언트 사이드 예측용)
        public int GetPredictionTick()
        {
            if (NetworkManager.Instance == null || NetworkManager.Instance.RTT == 0)
                return GetCurrentTick() + 2;

            float rttSeconds = NetworkManager.Instance.RTT / 1000f;
            int latencyTicks = Mathf.CeilToInt((rttSeconds / 2f) * TickRate);
            int bufferTicks = 2; // Jitter Buffer

            return GetCurrentTick() + latencyTicks + bufferTicks;
        }

        // 기존 레거시 메서드 및 필드 제거됨 (CheckAndCorrectAnchor 등은 필요 시 재구현 가능하나, Anchor 재설정 방식이므로 생략)
        // 혹시 모를 대규모 Drift 대비용으로 CheckAndCorrectGameAnchor를 남길 수도 있음.
        public void CheckAndCorrectGameAnchor(uint roomTick, float rttMs)
        {
            if (!_gameInitialized)
                return;

            // 1. RTT를 고려한 실제 서버 틱 추정
            float halfRttSeconds = (rttMs / 2000f);
            float rttTicks = halfRttSeconds * TickRate;
            float actualServerTick = roomTick + rttTicks;

            // 2. 클라이언트 추정 틱 (offset 제외한 raw 값)
            double dt = Time.realtimeSinceStartupAsDouble - _baseGameTime;
            float rawClientTick = _baseGameTick + (float)(dt * TickRate);
            float clientTick = rawClientTick + _gameTickOffset;

            // 3. 오차 계산 (서버 - 클라 = 양수면 클라가 느림)
            float diff = actualServerTick - clientTick;

            // 4. 단계별 보정
            if (Mathf.Abs(diff) < 10 && Mathf.Abs(diff) > 0.5f)
            {
                // Soft Sync: offset을 10%씩 조정 (빠르고 부드럽게)
                float adjustment = diff * 0.1f;
                _gameTickOffset += adjustment; // diff가 음수면 offset 감소, 양수면 증가
                Debug.Log(
                    $"[TickManager] Soft Sync: Diff {diff:F1} (RTT: {rttMs:F1}ms) → Offset {_gameTickOffset:F2}"
                );
            }
            else if (Mathf.Abs(diff) >= 10 && Mathf.Abs(diff) < 60)
            {
                // Medium Sync: offset을 5%씩 조정
                float adjustment = diff * 0.05f;
                _gameTickOffset += adjustment;
                Debug.Log(
                    $"[TickManager] Medium Sync: Diff {diff:F1} → Offset {_gameTickOffset:F2}"
                );
            }
            else if (Mathf.Abs(diff) >= 60)
            {
                // Hard Sync: 강제 재동기화 (offset 리셋)
                Debug.LogWarning($"[TickManager] Hard Sync! Diff: {diff:F1}");
                _gameTickOffset = 0;
                _gameInitialized = false;
                InitGameAnchor(roomTick);
            }
        }
    }
}
