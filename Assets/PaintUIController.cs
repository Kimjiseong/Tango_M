using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PaintUIController : MonoBehaviour
{
    public GameObject m_circle0;
    public GameObject m_circle1;
    public GameObject m_circle2;
    public GameObject m_circle3;
    public Dropdown m_dropDown;
    private TangoPointCloud m_pointCloud;

    void Start()
    {
        m_pointCloud = FindObjectOfType<TangoPointCloud>();
    }

    void Update()
    {
        if (Input.touchCount == 1)
        {
            // Trigger place function when single touch.
            Touch t = Input.GetTouch(0);
            PlaceCircle(t.position);
        }
    }

    void PlaceCircle(Vector2 touchPosition)
    {
        // Find the plane.
        Camera cam = Camera.main;
        Vector3 planeCenter;
        Plane plane;
        if (!m_pointCloud.FindPlane(cam, touchPosition, out planeCenter, out plane))
        {
            Debug.Log("cannot find plane.");
            return;
        }
        Vector3 up = plane.normal;
        Vector3 right = Vector3.Cross(plane.normal, cam.transform.forward).normalized;
        Vector3 forward = Vector3.Cross(right, plane.normal).normalized;
        if (m_dropDown.value == 0)
            Instantiate(m_circle0, planeCenter, Quaternion.LookRotation(forward, up));
        else if (m_dropDown.value == 1)
            Instantiate(m_circle1, planeCenter, Quaternion.LookRotation(forward, up));
        else if (m_dropDown.value == 2)
            Instantiate(m_circle2, planeCenter, Quaternion.LookRotation(forward, up));
        else if (m_dropDown.value == 3)
            Instantiate(m_circle3, planeCenter, Quaternion.LookRotation(forward, up));
    }
}