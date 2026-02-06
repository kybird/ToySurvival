using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 내 효과음(SFX) 재생을 관리하는 싱글톤 매니저
/// </summary>
public class SoundManager : MonoBehaviour
{
    private static SoundManager _instance;
    public static SoundManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에 없을 경우 자동으로 생성 (편의성)
                GameObject go = new GameObject("@SoundSystem");
                _instance = go.AddComponent<SoundManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private AudioSource _audioSource;
    private Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _audioSource = gameObject.GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    /// <summary>
    /// 효과음을 재생합니다.
    /// </summary>
    /// <param name="name">Resources/Sounds 폴더 내의 파일 이름</param>
    public void Play(string name, float volume = 1.0f)
    {
        AudioClip clip = GetOrLoadClip(name);
        if (clip != null)
        {
            _audioSource.PlayOneShot(clip, volume);
        }
        else
        {
            // 방어적 코딩: 리소스가 없을 경우 경고 메시지 출력 (Rule 8)
            Debug.LogWarning($"[SoundManager] 효과음을 찾을 수 없어 재생에 실패했습니다: {name}");
        }
    }

    private AudioClip GetOrLoadClip(string name)
    {
        if (_clips.TryGetValue(name, out AudioClip clip))
            return clip;

        // Resources/Sounds/ 폴더에서 로드 시도
        clip = Resources.Load<AudioClip>($"Sounds/{name}");
        if (clip != null)
        {
            _clips.Add(name, clip);
        }

        return clip;
    }
}
