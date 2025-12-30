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
            CS_Move move = new CS_Move()
            {
                X = transform.position.x + x * Time.deltaTime * 5.0f,
                Y = transform.position.y + y * Time.deltaTime * 5.0f
            };
            
            // Move locally first (Client-side prediction would go here)
            transform.position = new Vector3(move.X, move.Y, 0);

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
