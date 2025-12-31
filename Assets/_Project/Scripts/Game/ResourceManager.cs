using System.Collections.Generic;
using UnityEngine;

public class ResourceManager
{
    // 리소스를 캐싱하여 중복 로드 방지
    private Dictionary<string, UnityEngine.Object> _resources = new Dictionary<string, UnityEngine.Object>();

    // 경로에 있는 리소스를 로드 (T 타입)
    public T Load<T>(string path) where T : UnityEngine.Object
    {
        if (_resources.TryGetValue(path, out UnityEngine.Object resource))
            return resource as T;

        T loaded = Resources.Load<T>(path);
        if (loaded != null)
        {
            _resources.Add(path, loaded);
        }
        else
        {
            Debug.LogWarning($"[ResourceManager] Failed to load resource at path: {path}");
        }

        return loaded;
    }

    // 프리팹을 로드하고 즉시 인스턴스화
    public GameObject Instantiate(string path, Transform parent = null)
    {
        GameObject prefab = Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[ResourceManager] Failed to load prefab: {path}");
            return null;
        }

        return UnityEngine.Object.Instantiate(prefab, parent);
    }
    
    public void Clear()
    {
        _resources.Clear();
    }
}
