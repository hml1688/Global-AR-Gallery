using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;   // 判断是否点在 UI 上
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;  // ✅ 别名简化


/// <summary>
/// 双指捏合 ⇒ 在水平面上沿相机 forward 方向“步行”
/// 挂在 XR Origin (Mobile AR) 节点即可
/// </summary>
public class PinchWalkController : MonoBehaviour
{
    [Tooltip("每个像素的捏合差对应前进的米数")]
    public float moveFactor = 0.01f;   // 0.002–0.005 之间自己调

    Camera arCam;

    void Start()
    {
        arCam = GetComponentInChildren<Camera>();
        EnhancedTouch.EnhancedTouchSupport.Enable();  // ✅ 开启新输入系统的触摸支持
    }

    void Update()
    {
        // ① 必须是两指
        if (EnhancedTouch.Touch.activeTouches.Count != 2) return;

        var t0 = EnhancedTouch.Touch.activeTouches[0];
        var t1 = EnhancedTouch.Touch.activeTouches[1];

        // ② 若手指在 UI 区域上方就忽略（防止捏合网页等）
        if (EventSystem.current != null &&
            (EventSystem.current.IsPointerOverGameObject(t0.finger.index) ||
            EventSystem.current.IsPointerOverGameObject(t1.finger.index)))
            return;


        // ③ 只在移动阶段计算
        if (t0.phase != UnityEngine.InputSystem.TouchPhase.Moved &&
            t1.phase != UnityEngine.InputSystem.TouchPhase.Moved) return;

        // ✅ 捏合距离变化计算（像素）
        Vector2 prevT0 = t0.screenPosition - t0.delta;
        Vector2 prevT1 = t1.screenPosition - t1.delta;
        float prevMag = (prevT0 - prevT1).magnitude;
        float curMag  = (t0.screenPosition - t1.screenPosition).magnitude;
        float delta   = curMag - prevMag;

        if (Mathf.Abs(delta) < 0.01f) return;

        Vector3 fwd = arCam.transform.forward;
        fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) return;
        fwd.Normalize();

        //（Zoom In） → delta < 0 → Camera 向前走 → 看起来“靠近”
        //（Zoom Out） → delta > 0 → Camera 向后退
        transform.position += fwd * delta * moveFactor;
    }
}

