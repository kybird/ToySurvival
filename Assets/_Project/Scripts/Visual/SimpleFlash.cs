using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleFlash : MonoBehaviour
{
    private List<SpriteRenderer> _spriteRenderers = new List<SpriteRenderer>();
    private List<MeshRenderer> _meshRenderers = new List<MeshRenderer>();

    private Dictionary<SpriteRenderer, Color> _originalSpriteColors =
        new Dictionary<SpriteRenderer, Color>();
    private Dictionary<MeshRenderer, Material> _originalMaterials =
        new Dictionary<MeshRenderer, Material>();

    private Coroutine _flashRoutine;
    private bool _isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized)
            return;

        // 모든 자식에서 렌더러를 찾습니다 (복합 오브젝트 지원)
        GetComponentsInChildren(true, _spriteRenderers);
        GetComponentsInChildren(true, _meshRenderers);

        foreach (var sr in _spriteRenderers)
        {
            _originalSpriteColors[sr] = sr.color;
        }

        foreach (var mr in _meshRenderers)
        {
            _originalMaterials[mr] = mr.sharedMaterial;
        }

        _isInitialized = true;
    }

    public void Flash(Color color, Material flashMat, float duration)
    {
        if (!_isInitialized)
            Initialize();

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(CoFlash(color, flashMat, duration));
    }

    private IEnumerator CoFlash(Color color, Material flashMat, float duration)
    {
        // Apply Flash
        foreach (var sr in _spriteRenderers)
        {
            if (sr != null)
                sr.color = color;
        }

        foreach (var mr in _meshRenderers)
        {
            if (mr != null && flashMat != null)
                mr.sharedMaterial = flashMat;
        }

        yield return new WaitForSeconds(duration);

        // Revert
        foreach (var sr in _spriteRenderers)
        {
            if (sr != null && _originalSpriteColors.ContainsKey(sr))
                sr.color = _originalSpriteColors[sr];
        }

        foreach (var mr in _meshRenderers)
        {
            if (mr != null && _originalMaterials.ContainsKey(mr))
                mr.sharedMaterial = _originalMaterials[mr];
        }

        _flashRoutine = null;
    }
}
