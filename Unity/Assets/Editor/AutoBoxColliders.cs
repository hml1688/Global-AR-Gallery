#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AutoBoxColliders   // 类必须是 static
{
    [MenuItem("Tools/Generate Wall BoxColliders")]
    static void Generate()
    {
        int count = 0;

        foreach (MeshRenderer mr in Selection.GetFiltered<MeshRenderer>(SelectionMode.Editable | SelectionMode.ExcludePrefab))
        {
            BoxCollider bc = mr.GetComponent<BoxCollider>();
            if (bc == null) bc = mr.gameObject.AddComponent<BoxCollider>();

            Bounds b = mr.bounds;
            Vector3 worldCenter = b.center;
            Vector3 worldSize   = b.size;

            Vector3 localCenter = mr.transform.InverseTransformPoint(worldCenter);
            Vector3 localSize   = mr.transform.InverseTransformVector(worldSize);

            localSize.z = Mathf.Max(localSize.z + 0.1f, 0.1f);   // 0.1 m 厚度

            bc.center = localCenter;
            bc.size   = localSize;

            count++;
        }

        Debug.Log($"✅ AutoBoxColliders: 生成/更新 {count} 个 BoxCollider");
    }
}
#endif
