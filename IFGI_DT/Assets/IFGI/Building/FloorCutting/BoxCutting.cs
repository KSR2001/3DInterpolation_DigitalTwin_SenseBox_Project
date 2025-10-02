using System.Collections;
using UnityEngine;

public class BoxCutting : MonoBehaviour
{
    public GameObject hatchedBox;
    public Transform[] liftParts;
    public float minHeight = 0.0f;
    public float maxHeight = 100.0f;

    private void Start()
    {
        StartCoroutine(InitializeBoxAsync());
    }

    private IEnumerator InitializeBoxAsync()
    {
        if (liftParts.Length == 0 || hatchedBox == null)
        {
            yield break;
        }

        Bounds combinedBounds = new Bounds(liftParts[0].GetChild(0).position, Vector3.zero);

        foreach (Transform lift in liftParts)
        {
            foreach (Renderer r in lift.GetComponentsInChildren<Renderer>())
            {
                combinedBounds.Encapsulate(r.bounds);
                yield return null; // Yield to keep things responsive if needed
            }
        }

        Vector3 boxScale = new Vector3(combinedBounds.size.x, combinedBounds.size.z, maxHeight - minHeight);
        hatchedBox.transform.position = new Vector3(combinedBounds.center.x, (maxHeight + minHeight) / 2f, combinedBounds.center.z);
        hatchedBox.transform.localScale = boxScale;

        Matrix4x4 mx = Matrix4x4.TRS(hatchedBox.transform.position, this.transform.localRotation, Vector3.one);

        Shader.EnableKeyword("CLIP_CUBOID");
        Shader.SetGlobalMatrix("_WorldToObjectMatrix", mx.inverse);
        Shader.SetGlobalVector("_SectionScale", boxScale);
    }
}
