using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public bool AutoRotate = false;
    // ここを中心にカメラを回す
    Vector3 lookatPosition = new Vector3(0, 0, 0);
    Vector2 prevPosition;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (AutoRotate)
        {
            transform.RotateAround(lookatPosition, new Vector3(0, 1, 0), 0.1f);
            return;
        }

        // プレイヤーの操作
        if (Input.GetMouseButtonDown(0))
        {
            prevPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0))
        {
            float val = prevPosition.x - Input.mousePosition.x;
            val *= -0.1f;

            // 場所、回転軸、移動量を指定
            transform.RotateAround(lookatPosition, new Vector3(0, 1, 0), val);
            prevPosition = Input.mousePosition;
        }
    }
}
