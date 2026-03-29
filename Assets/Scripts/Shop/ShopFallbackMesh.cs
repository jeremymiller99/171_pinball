using UnityEngine;

/// <summary>
/// Creates a simple primitive cube at runtime to stand in for
/// missing prefabs on BallDefinition / BoardComponentDefinition.
/// </summary>
public static class ShopFallbackMesh
{
    private static readonly Color BallColor = new Color(0.3f, 0.6f, 1f, 1f);
    private static readonly Color ComponentColor = new Color(1f, 0.5f, 0.2f, 1f);

    /// <summary>
    /// Spawns a primitive cube with a tinted Unlit material.
    /// </summary>
    public static GameObject CreateFallbackCube(
        string label,
        bool isBall,
        Vector3 position,
        Quaternion rotation,
        float scale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = label;
        cube.transform.SetPositionAndRotation(position, rotation);
        cube.transform.localScale = Vector3.one * scale;

        Renderer rend = cube.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat = new Material(Shader.Find("Unlit/Color"));
            }
            mat.color = isBall ? BallColor : ComponentColor;
            rend.sharedMaterial = mat;
        }

        return cube;
    }
}
