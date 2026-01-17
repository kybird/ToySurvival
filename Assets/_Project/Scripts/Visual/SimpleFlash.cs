using UnityEngine;
using System.Collections;

public class SimpleFlash : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private MeshRenderer _meshRenderer;
    
    private Color _originalSpriteColor;
    private Material _originalMaterial;
    
    private Coroutine _flashRoutine;
    private bool _isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized) return;

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer) _originalSpriteColor = _spriteRenderer.color;
        
        _meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (_meshRenderer) _originalMaterial = _meshRenderer.sharedMaterial;

        _isInitialized = true;
    }

    public void Flash(Color color, Material flashMat, float duration)
    {
        if (!_isInitialized) Initialize();

        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(CoFlash(color, flashMat, duration));
    }

    private IEnumerator CoFlash(Color color, Material flashMat, float duration)
    {
        // Apply Flash
        if (_spriteRenderer != null) _spriteRenderer.color = color;
        if (_meshRenderer != null && flashMat != null) _meshRenderer.sharedMaterial = flashMat;

        yield return new WaitForSeconds(duration);

        // Revert
        if (_spriteRenderer != null) _spriteRenderer.color = _originalSpriteColor;
        if (_meshRenderer != null && _originalMaterial != null) _meshRenderer.sharedMaterial = _originalMaterial;
        
        _flashRoutine = null;
    }
}
