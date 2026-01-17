using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 앱 전체 상태를 중앙에서 관리하는 싱글톤 매니저.
/// 상태 전이 테이블 기반으로 동작하며, 로딩/씬 전환을 조율합니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }
    #endregion

    #region State
    public GameState CurrentState { get; private set; } = GameState.None;
    public event Action<GameState, GameState> OnStateChanged; // (oldState, newState)
    #endregion

    #region Transition Table
    private struct Transition
    {
        public GameState NextState;
        public string SceneName; // null이면 씬 전환 없음
        public Action OnTransition; // 전이 시 추가 동작
    }

    private Dictionary<(GameState, StateEvent), Transition> _transitionTable;
    #endregion

    #region Mismatch Tracking
    private Dictionary<int, int> _mismatchCounter = new Dictionary<int, int>(); // PacketId -> Count
    private const int MISMATCH_WARN_THRESHOLD = 3;
    private const int MISMATCH_TERMINATE_THRESHOLD = 5;
    #endregion

    #region Loading
    private Coroutine _loadingCoroutine;
    private const float LOADING_TIMEOUT = 10f;
    #endregion

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeTransitionTable();
            Debug.Log("[GameManager] Initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 앱 시작 시 Login 상태로 전이
        TriggerEvent(StateEvent.AppStart);
    }

    #region Transition Table Initialization
    private void InitializeTransitionTable()
    {
        _transitionTable = new Dictionary<(GameState, StateEvent), Transition>
        {
            // None -> Login
            {
                (GameState.None, StateEvent.AppStart),
                new Transition { NextState = GameState.Login }
            },
            // Login -> Lobby
            {
                (GameState.Login, StateEvent.LoginSuccess),
                new Transition { NextState = GameState.Lobby, SceneName = "LobbyScene" }
            },
            {
                (GameState.Login, StateEvent.LoginFail),
                new Transition { NextState = GameState.Login }
            }, // Stay
            // Lobby -> Loading -> InGame
            {
                (GameState.Lobby, StateEvent.JoinRoomSuccess),
                new Transition { NextState = GameState.Loading, SceneName = "GameScene" }
            },
            {
                (GameState.Lobby, StateEvent.Disconnect),
                new Transition { NextState = GameState.Login, SceneName = "LoginScene" }
            },
            // Loading -> InGame or Fallback
            {
                (GameState.Loading, StateEvent.SceneLoadComplete),
                new Transition { NextState = GameState.InGame }
            },
            {
                (GameState.Loading, StateEvent.Timeout),
                new Transition { NextState = GameState.Lobby, SceneName = "LobbyScene" }
            },
            {
                (GameState.Loading, StateEvent.Disconnect),
                new Transition { NextState = GameState.Login, SceneName = "LoginScene" }
            },
            // InGame -> Lobby
            {
                (GameState.InGame, StateEvent.LeaveRoom),
                new Transition { NextState = GameState.Lobby, SceneName = "LobbyScene" }
            },
            {
                (GameState.InGame, StateEvent.Disconnect),
                new Transition { NextState = GameState.Login, SceneName = "LoginScene" }
            },
            // Any -> Login (Kick)
            {
                (GameState.Lobby, StateEvent.Kick),
                new Transition { NextState = GameState.Login, SceneName = "LoginScene" }
            },
            {
                (GameState.InGame, StateEvent.Kick),
                new Transition { NextState = GameState.Login, SceneName = "LoginScene" }
            },
        };
    }
    #endregion

    #region Public API
    /// <summary>
    /// 이벤트를 트리거하여 상태 전이를 시도합니다.
    /// </summary>
    public bool TriggerEvent(StateEvent evt)
    {
        var key = (CurrentState, evt);

        if (_transitionTable.TryGetValue(key, out Transition transition))
        {
            Debug.Log(
                $"[GameManager] State Transition: {CurrentState} --[{evt}]--> {transition.NextState}"
            );

            GameState oldState = CurrentState;

            // 씬 전환이 필요한 경우
            if (!string.IsNullOrEmpty(transition.SceneName))
            {
                StartSceneTransition(transition.NextState, transition.SceneName);
            }
            else
            {
                // 즉시 상태 전환
                SetState(transition.NextState);
            }

            transition.OnTransition?.Invoke();
            return true;
        }
        else
        {
            Debug.LogWarning($"[GameManager] Invalid Transition: {CurrentState} --[{evt}]--> ???");
            return false;
        }
    }

    /// <summary>
    /// 현재 상태에서 특정 패킷 처리가 허용되는지 확인합니다.
    /// </summary>
    public bool IsPacketAllowed(int packetId, GameState[] allowedStates)
    {
        foreach (var state in allowedStates)
        {
            if (CurrentState == state)
                return true;
        }

        HandleMismatch(packetId);
        return false;
    }

    /// <summary>
    /// 상태를 강제로 설정합니다 (내부 또는 씬 로드 완료 시 사용).
    /// </summary>
    public void SetState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        GameState oldState = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] State Changed: {oldState} -> {newState}");
        OnStateChanged?.Invoke(oldState, newState);
    }
    #endregion

    #region Scene Transition
    private void StartSceneTransition(GameState targetState, string sceneName)
    {
        if (_loadingCoroutine != null)
        {
            StopCoroutine(_loadingCoroutine);
        }

        _loadingCoroutine = StartCoroutine(CoLoadScene(targetState, sceneName));
    }

    private IEnumerator CoLoadScene(GameState targetState, string sceneName)
    {
        // 1. Loading 상태로 전환 (무조건)
        SetState(GameState.Loading);

        // 2. 로딩 UI 표시
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.Show($"Loading {sceneName}...");
        }

        // 3. 비동기 씬 로드
        float startTime = Time.time;
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            // 타임아웃 체크
            if (Time.time - startTime > LOADING_TIMEOUT)
            {
                Debug.LogError($"[GameManager] Scene load timeout: {sceneName}");
                TriggerEvent(StateEvent.Timeout);
                yield break;
            }

            // 진행률 업데이트
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.SetProgress(progress);
            }

            // 로드 완료 시 씬 활성화
            if (asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        // 4. 로딩 UI 숨김
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.Hide();
        }

        // 5. 목표 상태로 전환
        SetState(targetState);

        // 6. 씬 로드 완료 이벤트 (Loading -> InGame 전이용)
        if (CurrentState == GameState.Loading)
        {
            TriggerEvent(StateEvent.SceneLoadComplete);
        }

        _loadingCoroutine = null;
    }
    #endregion

    #region Mismatch Handling
    private void HandleMismatch(int packetId)
    {
        if (!_mismatchCounter.ContainsKey(packetId))
            _mismatchCounter[packetId] = 0;

        _mismatchCounter[packetId]++;
        int count = _mismatchCounter[packetId];

        if (count >= MISMATCH_TERMINATE_THRESHOLD)
        {
            // [Fix] LoadingState에서는 패킷 늦게 도착할 수 있으므로 강제 종료하지 않음
            if (CurrentState == GameState.Loading)
            {
                Debug.LogWarning(
                    $"[GameManager] Packet {packetId} mismatch in LOADING state. Ignoring (Count: {count})."
                );
                return;
            }

            Debug.LogError(
                $"[GameManager] Packet {packetId} mismatch exceeded threshold. Terminating session."
            );
            // NetworkManager.Instance.Disconnect();
            TriggerEvent(StateEvent.Disconnect);
        }
        else if (count >= MISMATCH_WARN_THRESHOLD)
        {
            Debug.LogWarning($"[GameManager] Packet {packetId} mismatch count: {count} (SUSPECT)");
        }
        else
        {
            Debug.LogWarning($"[GameManager] Packet {packetId} ignored in state {CurrentState}");
        }
    }

    /// <summary>
    /// 불일치 카운터 초기화 (정상 처리 시 호출)
    /// </summary>
    public void ResetMismatchCounter(int packetId)
    {
        if (_mismatchCounter.ContainsKey(packetId))
            _mismatchCounter[packetId] = 0;
    }
    #endregion
}
