namespace Core
{
    /// <summary>
    /// 앱 전체 상태 정의
    /// </summary>
    public enum GameState
    {
        None,       // 앱 시작 전
        Login,      // 로그인 화면
        Lobby,      // 로비 화면
        Loading,    // 씬 전환 중
        InGame      // 게임 플레이 중
    }

    /// <summary>
    /// 상태 전이를 유발하는 이벤트
    /// </summary>
    public enum StateEvent
    {
        AppStart,           // 앱 시작
        LoginSuccess,       // 로그인 성공
        LoginFail,          // 로그인 실패
        JoinRoomSuccess,    // 방 입장 성공
        JoinRoomFail,       // 방 입장 실패
        SceneLoadComplete,  // 씬 로드 완료
        LeaveRoom,          // 방 퇴장
        Disconnect,         // 서버 연결 끊김
        Kick,               // 강제 퇴장
        Timeout             // 타임아웃
    }

    /// <summary>
    /// 패킷 불일치 정책
    /// </summary>
    public enum MismatchPolicy
    {
        Warn,       // 경고 로그만
        Suspect,    // 의심 (카운터 증가)
        Terminate,  // 세션 종료
        Critical,   // 즉시 Disconnect
        Sync        // 상태 동기화 시도
    }
}
