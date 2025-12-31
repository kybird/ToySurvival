using UnityEngine;
using Protocol;

public class GameController : MonoBehaviour
{
    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");

        if (x != 0 || y != 0)
        {
            C_Move move = new C_Move()
            {
                DirX = x,
                DirY = y
            };
            
            // Move locally first (Simple simulation)
            transform.Translate(new Vector3(x, 0, y) * Time.deltaTime * 5.0f);

            NetworkManager.Instance.Send(move);
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Label("--- Game Scene (Debug UI) ---", new GUIStyle(GUI.skin.label) { fontSize = 20 });
        GUILayout.Label($"My Position: {transform.position.x:F2}, {transform.position.y:F2}");
        GUILayout.Label("Controls: WASD or Arrow Keys");
        GUILayout.EndArea();
    }
}
