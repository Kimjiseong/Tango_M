using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MuseumManagement : MonoBehaviour {

    public void LoadManagerMode()
    {
        SceneManager.LoadScene("ManagerMode");
    }
    
    public void LoadUserMode()
    {
        SceneManager.LoadScene("UserMode");
    }

}
