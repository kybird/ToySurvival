using UnityEngine;

/// <summary>
/// 배경 격자무늬(Grid)를 동적으로 생성하고 관리하는 스크립트입니다.
/// </summary>
public class GridVisualizer : MonoBehaviour
{
    [Header("Settings")]
    public float gridSize = 1.0f;
    public Color backgroundColor = new Color(0.45f, 0.47f, 0.5f, 1.0f); // 확연히 밝은 회색 배경
    public Color lineColor = new Color(0.35f, 0.35f, 0.35f, 1.0f); // 선은 상대적으로 진하게
    public Color majorLineColor = new Color(0.25f, 0.25f, 0.25f, 1.0f); // 격자 무늬가 뚜렷하게 보이도록

    private GameObject _gridObject;
    private Material _gridMaterial;

    void Start()
    {
        CreateGrid();
    }

    void CreateGrid()
    {
        // 1. 거대한 Plane 생성 (실제 맵은 매우 크므로 충분한 크기로 설정)
        _gridObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        _gridObject.name = "BackgroundGrid";
        _gridObject.transform.SetParent(this.transform);
        // 2D 게임이므로 XY 평면을 바라보도록 회전 (Plane은 기본적으로 XZ 평면)
        _gridObject.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        _gridObject.transform.localPosition = new Vector3(0, 0, 10); // 플레이어 뒤쪽 (Z축)
        _gridObject.transform.localScale = new Vector3(100, 1, 100); // 1000m x 1000m

        // 2. 콜라이더 제거 (성능 및 충돌 방지)
        var collider = _gridObject.GetComponent<MeshCollider>();
        if (collider != null)
            Destroy(collider);

        // 3. 쉐이더 및 머티리얼 설정
        Shader gridShader = Shader.Find("Custom/BackgroundGrid");
        if (gridShader == null)
        {
            Debug.LogWarning(
                "[GridVisualizer] Shader 'Custom/BackgroundGrid' not found. Make sure it's included in the project."
            );
            // 쉐이더가 로드되지 않았을 경우를 대비해 스탠다드 시각화라도 함
            _gridObject.GetComponent<MeshRenderer>().material.color = backgroundColor;
            return;
        }

        _gridMaterial = new Material(gridShader);
        _gridMaterial.SetColor("_GridColor", backgroundColor);
        _gridMaterial.SetColor("_LineColor", lineColor);
        _gridMaterial.SetColor("_MainLineColor", majorLineColor);
        _gridMaterial.SetFloat("_GridSize", gridSize);
        _gridMaterial.SetFloat("_LineWidth", 0.02f);

        var renderer = _gridObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _gridMaterial;
        renderer.sortingOrder = -100; // 배경은 가장 뒤에 위치하도록 설정
    }

    void Update()
    {
        // 카메라를 따라다니게 하여 항상 격자가 보이도록 할 수도 있지만,
        // 현재는 world position 기반 쉐이더이므로 Plane이 충분히 크면 문제없습니다.
        // 필요하다면 여기서 카메라의 X, Z 좌표를 따라가도록 설정 가능합니다.
        if (Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            // 카메라를 따라다니되, Z축은 고정하여 배경으로 유지
            _gridObject.transform.position = new Vector3(camPos.x, camPos.y, 10f);
        }
    }
}
