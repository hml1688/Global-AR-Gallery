using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;

[RequireComponent(typeof(CharacterController))]
public class PinchWalkController : MonoBehaviour
{
    public float moveFactor = 0.01f;          // 位移灵敏度
    public LayerMask wallMask;                // 只勾 GalleryWall

    Camera arCam;
    CharacterController cc;

    void Start()
    {
        arCam = GetComponentInChildren<Camera>();
        cc    = GetComponent<CharacterController>();
        EnhancedTouch.EnhancedTouchSupport.Enable();
    }

    void Update()
    {
        // ① 双指检测 & UI 忽略（省略：同之前代码）
        if (EnhancedTouch.Touch.activeTouches.Count != 2) return;
        var t0 = EnhancedTouch.Touch.activeTouches[0];
        var t1 = EnhancedTouch.Touch.activeTouches[1];
        if (EventSystem.current != null &&
            (EventSystem.current.IsPointerOverGameObject(t0.finger.index) ||
             EventSystem.current.IsPointerOverGameObject(t1.finger.index)))
            return;
        if (t0.phase != UnityEngine.InputSystem.TouchPhase.Moved &&
            t1.phase != UnityEngine.InputSystem.TouchPhase.Moved) return;

        // ② 计算捏合 delta（像素差）
        Vector2 prevT0 = t0.screenPosition - t0.delta;
        Vector2 prevT1 = t1.screenPosition - t1.delta;
        float prevMag = (prevT0 - prevT1).magnitude;
        float curMag  = (t0.screenPosition - t1.screenPosition).magnitude;
        float delta   = curMag - prevMag;
        if (Mathf.Abs(delta) < 0.01f) return;

        // ③ 取水平 forward
        Vector3 dir = arCam.transform.forward;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();

        // ④ 期望位移
        Vector3 deltaMove = dir * delta * moveFactor;   // Zoom-In 前进 (delta<0)
        float   dist      = deltaMove.magnitude;
        Vector3 moveDir   = deltaMove.normalized;

        // ⑤ 计算本帧胶囊两端点（世界坐标）
        Vector3 p0 = transform.position + cc.center + Vector3.up * ( cc.height*0.5f - cc.radius);
        Vector3 p1 = transform.position + cc.center - Vector3.up * ( cc.height*0.5f - cc.radius);

        // ⑥ CapsuleCast 探路
        if (Physics.CapsuleCast(p0, p1, cc.radius, moveDir, out RaycastHit hit, dist, wallMask))
        {
            dist = Mathf.Max(0, hit.distance - 0.02f);   // 留 2 cm 安全距
        }

        // ⑦ 实际移动
        cc.Move(moveDir * dist);
    }
}
