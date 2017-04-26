using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplanationClose : MonoBehaviour
{
    //★Explanation Panel의 자식 비활성화
    public void Close()
    {
        GameObject.Find("Explanation Panel").transform.FindChild("Image Panel").gameObject.SetActive(false);
        GameObject.Find("Explanation Panel").transform.FindChild("Content Panel").gameObject.SetActive(false);
        GameObject.Find("Explanation Panel").transform.FindChild("Close Button").gameObject.SetActive(false);

        GameObject.Find("Input Panel").transform.FindChild("Image Panel").gameObject.SetActive(false);
        GameObject.Find("Input Panel").transform.FindChild("Content Panel").gameObject.SetActive(false);
        GameObject.Find("Input Panel").transform.FindChild("Close Button").gameObject.SetActive(false);
    }
}