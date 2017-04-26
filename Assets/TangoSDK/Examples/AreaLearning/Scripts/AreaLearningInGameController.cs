//-----------------------------------------------------------------------
// <copyright file="AreaLearningInGameController.cs" company="Google">
//
// Copyright 2016 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Tango;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// AreaLearningGUIController is responsible for the main game interaction.
/// 
/// This class also takes care of loading / save persistent data(marker), and loop closure handling.
/// </summary>
public class AreaLearningInGameController : MonoBehaviour, ITangoPose, ITangoEvent, ITangoDepth
{
    /// <summary>
    /// Prefabs of different colored markers.
    /// </summary>
    public GameObject[] m_markPrefabs;

    /// <summary>
    /// The point cloud object in the scene.
    /// </summary>
    public TangoPointCloud m_pointCloud;

    /// <summary>
    /// The canvas to place 2D game objects under.
    /// </summary>
    public Canvas m_canvas;

    /// <summary>
    /// The touch effect to place on taps.
    /// </summary>
    public RectTransform m_prefabTouchEffect;

    /// <summary>
    /// Saving progress UI text.
    /// </summary>
    public UnityEngine.UI.Text m_savingText;

    /// <summary>
    /// The Area Description currently loaded in the Tango Service.
    /// </summary>
    [HideInInspector]
    public AreaDescription m_curAreaDescription;

#if UNITY_EDITOR
    /// <summary>
    /// Handles GUI text input in Editor where there is no device keyboard.
    /// If true, text input for naming new saved Area Description is displayed.
    /// ��ġ Ű���尡���� �����⿡�� GUI �ؽ�Ʈ �Է��� ó���մϴ�.
    /// true�̸� ���� ����� ���� ������ �̸� �Է������� �ؽ�Ʈ �Է��� ǥ�õ˴ϴ�.
    /// </summary>
    private bool m_displayGuiTextInput;

    /// <summary>
    /// Handles GUI text input in Editor where there is no device keyboard.
    /// Contains text data for naming new saved Area Descriptions.
    /// ���� ����� ���� ������ �̸��� �����ϱ����� �ؽ�Ʈ �����Ͱ� ��� �ֽ��ϴ�.
    /// </summary>
    private string m_guiTextInputContents;

    /// <summary>
    /// Handles GUI text input in Editor where there is no device keyboard.
    /// Indicates whether last text input was ended with confirmation or cancellation.
    /// ������ �ؽ�Ʈ �Է��� Ȯ�� �Ǵ� ��ҷ� ����Ǿ����� ���θ� ��Ÿ���ϴ�.
    /// </summary>
    private bool m_guiTextInputResult;
#endif

    /// <summary>
    /// If set, then the depth camera is on and we are waiting for the next depth update.
    /// �����Ǹ� ���� ī�޶� ������ ���� ���� ������Ʈ�� ��ٸ��� �ֽ��ϴ�.
    /// </summary>
    private bool m_findPlaneWaitingForDepth;

    /// <summary>
    /// A reference to TangoARPoseController instance.
    ///TangoARPoseController �ν��Ͻ��� ���� �����Դϴ�.
    /// In this class, we need TangoARPoseController reference to get the timestamp and pose when we place a marker.
    /// The timestamp and pose is used for later loop closure position correction. 
    /// �� Ŭ�������� ��Ŀ�� ��ġ �� �� Ÿ�� �������� ��� �������� TangoARPoseController ������ �ʿ��մϴ�.
    /// Ÿ�� �������� ����� ���߿� �������� ��ġ ������ ���˴ϴ�.
    /// </summary>
    private TangoARPoseController m_poseController;

    /// <summary>
    /// List of markers placed in the scene.
    /// ��鿡 ��ġ �� ��Ŀ ����Դϴ�.
    /// </summary>
    private List<GameObject> m_markerList = new List<GameObject>();

    /// <summary>
    /// Reference to the newly placed marker.
    /// ���Ӱ� ��ġ �� ��Ŀ�� ���� ����.
    /// </summary>
    private GameObject newMarkObject = null;

    /// <summary>
    /// Current marker type.
    /// ���� ��Ŀ ����.
    /// </summary>
    private int m_currentMarkType = 0;

    /// <summary>
    /// If set, this is the selected marker.
    /// �����Ǿ��ִ� ��� �̰��� ���õ� ��Ŀ�Դϴ�.
    /// </summary>
    private ARMarker m_selectedMarker;

    /// <summary>
    /// If set, this is the rectangle bounding the selected marker.
    /// �����Ǿ��ִ� ���� ���õ� ��Ŀ�� ��谡 ǥ�õ˴ϴ�.
    /// </summary>
    private Rect m_selectedRect;

    /// <summary>
    /// If the interaction is initialized.
    /// ��ȣ �ۿ��� �ʱ�ȭ �� ���.
    /// Note that the initialization is triggered by the relocalization event. We don't want user to place object before
    /// �� �ʱ�ȭ �̺�Ʈ�� ���� �ʱ�ȭ�� Ʈ���ŵ˴ϴ�.
    /// ��Ⱑ �ٽ� ���� ȭ�Ǳ� ���� ����ڰ� ��ü�� ��ġ�ϴ� ���� ������ �ʽ��ϴ�.
    /// </summary>
    private bool m_initialized = false;

    /// <summary>
    /// A reference to TangoApplication instance.
    /// TangoApplication �ν��Ͻ��� ���� �����Դϴ�.
    /// </summary>
    private TangoApplication m_tangoApplication;

    private Thread m_saveThread;

    /// <summary>
    /// Unity Start function.
    /// ����Ƽ ��ŸƮ ���
    /// We find and assign pose controller and tango application, and register this class to callback events.
    /// �츮�� ���� ��Ʈ�ѷ��� �ʰ� ���ø����̼��� ã�� �Ҵ��ϰ��� Ŭ������ �ݹ� �̺�Ʈ�� ����մϴ�.
    /// </summary>
    public void Start()
    {
        m_poseController = FindObjectOfType<TangoARPoseController>();
        m_tangoApplication = FindObjectOfType<TangoApplication>();
        
        if (m_tangoApplication != null)
        {
            m_tangoApplication.Register(this);
        }
    }

    /// <summary>
    /// Unity Update function.
    /// ����Ƽ ������Ʈ ���
    /// Mainly handle the touch event and place mark in place.
    /// �ַ� ��ġ �̺�Ʈ �� ��� ǥ�ø� ó���մϴ�.
    /// </summary>
    public void Update()
    {
        if (m_saveThread != null && m_saveThread.ThreadState != ThreadState.Running)
        {
            // After saving an Area Description or mark data, we reload the scene to restart the game.
            // ���� ������ �����ϰų� �����͸� ǥ�� �� �� ����� �ٽ÷ε��Ͽ� ������ �ٽ� �����մϴ�.
            _UpdateMarkersForLoopClosures();
            _SaveMarkerToDisk();
            #pragma warning disable 618
            Application.LoadLevel(Application.loadedLevel);
            #pragma warning restore 618
        }

        if (Input.GetKey(KeyCode.Escape))
        {
            #pragma warning disable 618
            Application.LoadLevel(Application.loadedLevel);
            #pragma warning restore 618
        }

        if (!m_initialized)
        {
            return;
        }

        if (EventSystem.current.IsPointerOverGameObject(0) || GUIUtility.hotControl != 0)
        {
            return;
        }

        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            Vector2 guiPosition = new Vector2(t.position.x, Screen.height - t.position.y);
            Camera cam = Camera.main;
            RaycastHit hitInfo;

            if (t.phase != TouchPhase.Began)
            {
                return;
            }

            if (m_selectedRect.Contains(guiPosition))
            {
                // do nothing, the button will handle it
                // �ƹ��͵����� �ʰ�, ��ư�� ó���մϴ�
            }
            else if (Physics.Raycast(cam.ScreenPointToRay(t.position), out hitInfo))
            {
                // Found a marker, select it (so long as it isn't disappearing)!
                // ��Ŀ�� ã�Ҵ���, �װ��� �����Ͻʽÿ� (������� �ʴ� ��)!
                GameObject tapped = hitInfo.collider.gameObject;
                if (!tapped.GetComponent<Animation>().isPlaying)
                {
                    m_selectedMarker = tapped.GetComponent<ARMarker>();
                }
            }
            else
            {
                // Place a new point at that location, clear selection
                // �ش� ��ġ�� �� ������ ���� ������ ����Ͻʽÿ�.
                m_selectedMarker = null;
                StartCoroutine(_WaitForDepthAndFindPlane(t.position));

                // Because we may wait a small amount of time, this is a good place to play a small
                // animation so the user knows that their input was received.
                // �츮�� ���� �ð� ��� �� �� �ֱ� ������, �̴� ����ڰ� �ڽ��� �Է��� ���ŵǾ����� �� �� �ֵ��� ���� �ִϸ��̼��� ����ϱ⿡ ���� ����̴�.
                RectTransform touchEffectRectTransform = Instantiate(m_prefabTouchEffect) as RectTransform;
                touchEffectRectTransform.transform.SetParent(m_canvas.transform, false);
                Vector2 normalizedPosition = t.position;
                normalizedPosition.x /= Screen.width;
                normalizedPosition.y /= Screen.height;
                touchEffectRectTransform.anchorMin = touchEffectRectTransform.anchorMax = normalizedPosition;
            }
        }
    }

    /// <summary>
    /// Application onPause / onResume callback.
    /// </summary>
    /// <param name="pauseStatus"><c>true</c> if the application about to pause, otherwise <c>false</c>.</param>
    public void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && m_initialized)
        {
            // When application is backgrounded, we reload the level because the Tango Service is disconected. All
            // learned area and placed marker should be discarded as they are not saved.
            // ���� ���α׷��� ��׶���� ��ȯ�Ǹ� Tango ���񽺰� ���� �����Ǿ� ������ �ٽ÷ε��մϴ�.
            // ��� �н� �� ������ ��ġ �� ��Ŀ�� ������� �����Ƿ� �����ؾ��մϴ�.
#pragma warning disable 618
            Application.LoadLevel(Application.loadedLevel);
            #pragma warning restore 618
        }
    }

    /// <summary>
    /// Unity OnGUI function.
    /// ����Ƽ onGUI ���
    /// Mainly for removing markers.
    /// �ַ� ��Ŀ�� �����մϴ�.
    /// </summary>
    public void OnGUI()
    {
        if (m_selectedMarker != null)
        {
            Renderer selectedRenderer = m_selectedMarker.GetComponent<Renderer>();
            
            // GUI's Y is flipped from the mouse's Y
            Rect screenRect = _WorldBoundsToScreen(Camera.main, selectedRenderer.bounds);
            float yMin = Screen.height - screenRect.yMin;
            float yMax = Screen.height - screenRect.yMax;
            screenRect.yMin = Mathf.Min(yMin, yMax);
            screenRect.yMax = Mathf.Max(yMin, yMax);

            //Hide UI ����

            // ���� ��ư�� View�� ��쿡�� ARMarker.cs�� Explanation ȣ��
            if (m_currentMarkType == 2)
            {
                if (GUI.Button(screenRect, "<size=50>Infomation</size>"))
                {
                    m_markerList.Remove(m_selectedMarker.gameObject);
                    m_selectedMarker.SendMessage("Explanation"); //Hide
                    m_selectedMarker = null;
                    m_selectedRect = new Rect();
                }
                else
                {
                    m_selectedRect = screenRect;
                }
            }

            // ���� ��ư�� Create �� ��� ������ ����
            else if (m_currentMarkType == 1)
            {
                if (GUI.Button(screenRect, "<size=30>Create</size>"))
                {
                    m_markerList.Remove(m_selectedMarker.gameObject);
                    m_selectedMarker.SendMessage("InputData"); //Hide
                    m_selectedMarker = null;
                    m_selectedRect = new Rect();
                }
                else
                {
                    m_selectedRect = screenRect;
                }
            }

            // ���� ��ư�� Delete �� ��� ��Ŀ����
            else
            {
                if (GUI.Button(screenRect, "<size=30>Hide</size>"))
                {
                    m_markerList.Remove(m_selectedMarker.gameObject);
                    m_selectedMarker.SendMessage("Hide"); //Hide
                    m_selectedMarker = null;
                    m_selectedRect = new Rect();
                }
                else
                {
                    m_selectedRect = screenRect;
                }
            }
        }
        else
        {
            m_selectedRect = new Rect();
        }


    }

    /// <summary>
    /// Set the marker type.
    /// </summary>
    /// <param name="type">Marker type.</param>
    /// ��Ŀ Ÿ�� ����
    public void SetCurrentMarkType(int type)
    {
        if (type != m_currentMarkType)
        {
            m_currentMarkType = type;
        }
    }

    /// <summary>
    /// Save the game.
    /// ���� ����
    /// Save will trigger 3 things:
    /// �����ϸ� 3������ �߻��մϴ�.
    /// 1. Save the Area Description if the learning mode is on.
    /// 2. Bundle adjustment for all marker positions, please see _UpdateMarkersForLoopClosures() function header for 
    ///     more details.
    /// 3. Save all markers to xml, save the Area Description if the learning mode is on.
    /// 4. Reload the scene.
    /// 
    /// ���� ����
    /// 
    /// �����ϸ� 3������ �߻��մϴ�.
    /// 1. �н� ��尡 �����ִ� ��� �����ν�(����������)�� �����մϴ�.
    /// 2. ��� ��Ŀ ��ġ�� ���� ���� ������ ���� �ڼ��� ������ _UpdateMarkersForLoopClosures () �Լ� ����� �����Ͻʽÿ�.
    /// 3. ��� ��Ŀ�� xml�� �����ϰ� �н� ��尡 �����ִ� ��� �����ν�(����������)�� �����մϴ�.
    /// 4. ���� �ҷ��´�.
    /// </summary>
    public void Save()
    {
        StartCoroutine(_DoSaveCurrentAreaDescription());
    }

    /// <summary>
    /// This is called each time a Tango event happens.
    /// �̰��� Tango �̺�Ʈ�� �߻��� ������ ȣ��˴ϴ�.
    /// </summary>
    /// <param name="tangoEvent">Tango event.</param>
    public void OnTangoEventAvailableEventHandler(Tango.TangoEvent tangoEvent)
    {
        // We will not have the saving progress when the learning mode is off.
        // �н� ��尡 �������� �� ���μ����� ���� ���� ���� ���Դϴ�.
        if (!m_tangoApplication.m_areaDescriptionLearningMode)
        {
            return;
        }

        if (tangoEvent.type == TangoEnums.TangoEventType.TANGO_EVENT_AREA_LEARNING
            && tangoEvent.event_key == "AreaDescriptionSaveProgress")
        {
            m_savingText.text = "Saving. " + (float.Parse(tangoEvent.event_value) * 100) + "%";
        }
    }

    /// �� ��������(Loop closure)
    /// ��Ⱑ ���� �� �̹� ����� ��ġ�� �ٽ� �湮�Ͽ��� ��, ���� ������ ���� �����Ϳ� ������ �����ϴ� ���� ������ 
    /// ������ ��ġ�� ã�Ƴ��� ���� �����͸� ���������ν� ������ ������ ���ҽ��� ������ ��Ȯ���� ����ϴ� ������ ���Ѵ�.
    /// �� Ÿ�ӽ�����(Timestamp)
    /// �Ϲ�������, � ���� �ð�(����, Epoch)���� ��� �ð��� ��ġ������ �ִ� ���ڿ�

    /// <summary>
    /// OnTangoPoseAvailable event from Tango.
    /// Tango�� OnTangoPoseAvailable �̺�Ʈ�Դϴ�.
    /// 
    /// In this function, we only listen to the Start-Of-Service with respect to Area-Description frame pair.
    /// This pair indicates a relocalization or loop closure event happened, base on that, we either start the initialize
    /// the interaction or do a bundle adjustment for all marker position.
    /// �� ��ɿ����� Area-Description ������ �ֿ� ���ؼ��� Start-Of-Service�� ����ϴ�.
    /// �� ���� relocalization �Ǵ� loop closure �̺�Ʈ�� �߻������� ��Ÿ���ϴ�.
    /// �⺻���� ��ȣ �ۿ��� �ʱ�ȭ�ϰų� ��� ��Ŀ ��ġ�� ���� ���� ������ �����մϴ�.
    /// </summary>
    /// <param name="poseData">Returned pose data from TangoService.</param>
    public void OnTangoPoseAvailable(Tango.TangoPoseData poseData)
    {
        // This frame pair's callback indicates that a loop closure or relocalization has happened. 
        //�� ������ ���� �ݹ��� �������� �Ǵ� relocalization�� �߻������� ��Ÿ���ϴ�.

        // When learning mode is on, this callback indicates the loop closure event. Loop closure will happen when the
        // system recognizes a pre-visited area, the loop closure operation will correct the previously saved pose 
        // to achieve more accurate result. (pose can be queried through GetPoseAtTime based on previously saved
        // timestamp).
        // Loop closure definition: https://en.wikipedia.org/wiki/Simultaneous_localization_and_mapping#Loop_closure
        //
        // �н� ��尡 ���� �������� �ݹ��� ���� ��� �̺�Ʈ�� ��Ÿ���ϴ�.
        // ���������� �ý����� �̸� �湮�� ������ �ν� �� �� �߻��մϴ�.
        // �������� �۾��� ������ ����� ��� �����Ͽ����� ��Ȯ�� ����� ����ϴ�.
        // (����� ������ ����� Ÿ�� �������� ������� GetPoseAtTime�� ���� ���� �� �� ����).

        // When learning mode is off, and an Area Description is loaded, this callback indicates a
        // relocalization event. Relocalization is when the device finds out where it is with respect to the loaded
        // Area Description. In our case, when the device is relocalized, the markers will be loaded because we
        // know the relatvie device location to the markers.
        //
        // �н� ��尡 �����Ǹ�, �����ν�(����������)�� �ε�Ǹ� �� �ݹ��� Relocalization �̺�Ʈ�� ��Ÿ���ϴ�.
        // Relocalization�� ��ġ�� �ε� �� �����ν�(����������)�� �����Ͽ� ��ġ�� ã�� ����Դϴ�.
        // �츮�� ��쿡, ��ġ�� relocalization �� ��, ��Ŀ�� ����� ��ġ ��ġ�� �˱� ������ ��Ŀ�� �ε� �� ���Դϴ�.
        if (poseData.framePair.baseFrame == 
            TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_AREA_DESCRIPTION &&
            poseData.framePair.targetFrame ==
            TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_START_OF_SERVICE &&
            poseData.status_code == TangoEnums.TangoPoseStatusType.TANGO_POSE_VALID)
        {
            // When we get the first loop closure/ relocalization event, we initialized all the in-game interactions.
            // ù ��° �������� / relocalization �̺�Ʈ�� �߻��ϸ� ��� ���� �� ��ȣ �ۿ��� �ʱ�ȭ�˴ϴ�.
            if (!m_initialized)
            {
                m_initialized = true;
                if (m_curAreaDescription == null)
                {
                    Debug.Log("AndroidInGameController.OnTangoPoseAvailable(): m_curAreaDescription is null");
                    return;
                }

                _LoadMarkerFromDisk();
            }
        }
    }

    /// <summary>
    /// This is called each time new depth data is available.
    /// �̰��� ���ο� ���� �����Ͱ� ��� ������ ������ ȣ��˴ϴ�.
    /// 
    /// On the Tango tablet, the depth callback occurs at 5 Hz.
    /// Tango �º������� 5Hz���� ���� �ݹ��� �߻��մϴ�.
    /// </summary>
    /// <param name="tangoDepth">Tango depth.</param>
    public void OnTangoDepthAvailable(TangoUnityDepth tangoDepth)
    {
        // Don't handle depth here because the PointCloud may not have been updated yet.  
        // Just tell the coroutine it can continue.
        // PointCloud�� ���� ������Ʈ���� �ʾұ� ������ ���⿡�� ���̸� �ٷ��� ���ʽÿ�.
        // coroutine�� ��� �����ϸ�˴ϴ�.
        m_findPlaneWaitingForDepth = false;
    }

    /// <summary>
    /// Actually do the Area Description save.
    /// ���� �����ν�(����������)�� �����մϴ�.
    /// </summary>
    /// <returns>Coroutine IEnumerator.</returns>
    private IEnumerator _DoSaveCurrentAreaDescription()
    {
#if UNITY_EDITOR
        // Work around lack of on-screen keyboard in editor:
        if (m_displayGuiTextInput || m_saveThread != null)
        {
            yield break;
        }

        m_displayGuiTextInput = true;
        m_guiTextInputContents = "Unnamed";
        while (m_displayGuiTextInput)
        {
            yield return null;
        }

        bool saveConfirmed = m_guiTextInputResult;
#else
        if (TouchScreenKeyboard.visible || m_saveThread != null)
        {
            yield break;
        }
        
        TouchScreenKeyboard kb = TouchScreenKeyboard.Open("Unnamed");
        while (!kb.done && !kb.wasCanceled)
        {
            yield return null;
        }

        bool saveConfirmed = kb.done;
#endif
        if (saveConfirmed)
        {
            // Disable interaction before saving.
            // �����ϱ� ���� ��ȣ �ۿ��� ��� �����մϴ�.
            m_initialized = false;
            m_savingText.gameObject.SetActive(true);
            if (m_tangoApplication.m_areaDescriptionLearningMode)
            {
                m_saveThread = new Thread(delegate()
                {
                    // Start saving process in another thread.
                    // �ٸ� �����忡�� ���μ��� ���� ����.
                    m_curAreaDescription = AreaDescription.SaveCurrent();
                    AreaDescription.Metadata metadata = m_curAreaDescription.GetMetadata();
#if UNITY_EDITOR
                    metadata.m_name = m_guiTextInputContents;
#else
                    metadata.m_name = kb.text;
#endif
                    m_curAreaDescription.SaveMetadata(metadata);
                });
                m_saveThread.Start();
            }
            else
            {
                _SaveMarkerToDisk();
                #pragma warning disable 618
                Application.LoadLevel(Application.loadedLevel);
                #pragma warning restore 618
            }
        }
    }

    /// <summary>
    /// Correct all saved marks when loop closure happens.
    /// ���������� �߻��ϸ� ����� ��ũ�� ��� �����Ͻʽÿ�.
    /// 
    /// When Tango Service is in learning mode, the drift will accumulate overtime, but when the system sees a
    /// preexisting area, it will do a operation to correct all previously saved poses
    /// (the pose you can query with GetPoseAtTime). This operation is called loop closure. When loop closure happens,
    /// we will need to re-query all previously saved marker position in order to achieve the best result.
    /// This function is doing the querying job based on timestamp.
    /// Tango Service�� �н� ��忡���� ��, drift�� �ð��� �þ���� �ý����� ���� ������ �� �� ������ �����
    /// ���� (GetPoseAtTime���� ���� �� ���ִ� ����)�� ��� �����ϴ� �۾��� �����մϴ�. �� �۾��� �������� �̶���մϴ�.
    /// �������վ� �߻��ϸ� �ֻ��� ����� �������� ������ ����� ��Ŀ ��ġ�� �ٽ� �����ؾ��մϴ�.
    /// �� �Լ��� Ÿ�� �������� ���� ���� �۾��� �����մϴ�.
    /// </summary>
    private void _UpdateMarkersForLoopClosures()
    {
        // Adjust mark's position each time we have a loop closure detected.
        // ���������� ���� �� ������ ��ũ�� ��ġ�� �����Ͻʽÿ�.
        foreach (GameObject obj in m_markerList)
        {
            ARMarker tempMarker = obj.GetComponent<ARMarker>();
            if (tempMarker.m_timestamp != -1.0f)
            {
                TangoCoordinateFramePair pair;
                TangoPoseData relocalizedPose = new TangoPoseData();

                pair.baseFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_AREA_DESCRIPTION;
                pair.targetFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_DEVICE;
                PoseProvider.GetPoseAtTime(relocalizedPose, tempMarker.m_timestamp, pair);

                Matrix4x4 uwTDevice = m_poseController.m_uwTss
                                      * relocalizedPose.ToMatrix4x4()
                                      * m_poseController.m_dTuc;

                Matrix4x4 uwTMarker = uwTDevice * tempMarker.m_deviceTMarker;

                obj.transform.position = uwTMarker.GetColumn(3);
                obj.transform.rotation = Quaternion.LookRotation(uwTMarker.GetColumn(2), uwTMarker.GetColumn(1));
            }
        }
    }

    /// <summary>
    /// XML I/O
    /// Write marker list to an xml file stored in application storage.
    /// ���� ���α׷� ����ҿ� ����� XML ���Ͽ� ��Ŀ ����� �ۼ��մϴ�.
    /// </summary>
    private void _SaveMarkerToDisk()
    {
        // Compose a XML data list.
        // XML ������ ����� �ۼ��Ͻʽÿ�.
        List<MarkerData> xmlDataList = new List<MarkerData>();
        foreach (GameObject obj in m_markerList)
        {
            // Add marks data to the list, we intentionally didn't add the timestamp, because the timestamp will not be
            // useful when the next time Tango Service is connected. The timestamp is only used for loop closure pose
            // correction in current Tango connection.
            // ��ũ �����͸� ��Ͽ� �߰��ϸ� �ǵ������� Ÿ�� �������� �߰����� �ʾҽ��ϴ�.
            // �ֳ��ϸ� Ÿ�ӽ������� ���� �ʰ� ���� �� ����ɶ� ���������ʽ��ϴ�.
            // Ÿ�� �������� ���� Tango ���ῡ�� �������� ���� �������� ���˴ϴ�.
            MarkerData temp = new MarkerData();
            temp.m_type = obj.GetComponent<ARMarker>().m_type;
            temp.m_position = obj.transform.position;
            temp.m_orientation = obj.transform.rotation;
            xmlDataList.Add(temp);
        }

        string path = Application.persistentDataPath + "/" + m_curAreaDescription.m_uuid + ".xml";
        var serializer = new XmlSerializer(typeof(List<MarkerData>));
        using (var stream = new FileStream(path, FileMode.Create))
        {
            serializer.Serialize(stream, xmlDataList);
        }
    }

    /// <summary>
    /// Load marker list xml from application storage.
    /// ���� ���α׷� ����ҿ��� ��Ŀ ��� xml���ε��մϴ�.
    /// </summary>
    private void _LoadMarkerFromDisk()
    {
        // Attempt to load the exsiting markers from storage.
        // ����ҿ��� ���� ��Ŀ���ε��Ϸ����մϴ�.
        string path = Application.persistentDataPath + "/" + m_curAreaDescription.m_uuid + ".xml";

        var serializer = new XmlSerializer(typeof(List<MarkerData>));
        var stream = new FileStream(path, FileMode.Open);

        List<MarkerData> xmlDataList = serializer.Deserialize(stream) as List<MarkerData>;

        if (xmlDataList == null)
        {
            Debug.Log("AndroidInGameController._LoadMarkerFromDisk(): xmlDataList is null");
            return;
        }

        m_markerList.Clear();
        foreach (MarkerData mark in xmlDataList)
        {
            // Instantiate all markers' gameobject.
            // ��� ��Ŀ�� ���� ��ü�� �ν��Ͻ�ȭ�մϴ�.
            GameObject temp = Instantiate(m_markPrefabs[mark.m_type],
                                          mark.m_position,
                                          mark.m_orientation) as GameObject;
            m_markerList.Add(temp);
        }
    }

    /// <summary>
    /// Convert a 3D bounding box represented by a <c>Bounds</c> object into a 2D 
    /// rectangle represented by a <c>Rect</c> object.
    /// <c>Bounds</c> ��ü�� ��Ÿ���� 3D ��� ���ڸ� <c>Rect</c> ��ü�� ��Ÿ���� 2D ����ü�� ��ȯ�մϴ�.
    /// </summary>
    /// <returns>The 2D rectangle in Screen coordinates.</returns>
    /// <param name="cam">Camera to use.</param>
    /// <param name="bounds">3D bounding box.</param>
    private Rect _WorldBoundsToScreen(Camera cam, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Bounds screenBounds = new Bounds(cam.WorldToScreenPoint(center), Vector3.zero);
        
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center + new Vector3(+extents.x, +extents.y, +extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center + new Vector3(+extents.x, +extents.y, -extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center + new Vector3(+extents.x, -extents.y, +extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center + new Vector3(+extents.x, -extents.y, -extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center + new Vector3(-extents.x, +extents.y, +extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center + new Vector3(-extents.x, +extents.y, -extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center + new Vector3(-extents.x, -extents.y, +extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center + new Vector3(-extents.x, -extents.y, -extents.z)));
        return Rect.MinMaxRect(screenBounds.min.x, screenBounds.min.y, screenBounds.max.x, screenBounds.max.y);
    }

    /// <summary>
    /// Wait for the next depth update, then find the plane at the touch position.
    /// ���� ���� ������Ʈ�� ��ٸ� ���� ��ġ ��ġ���� ����� ã���ϴ�.
    /// </summary>
    /// <returns>Coroutine IEnumerator.</returns>
    /// <param name="touchPosition">Touch position to find a plane at.</param>
    private IEnumerator _WaitForDepthAndFindPlane(Vector2 touchPosition)
    {
        m_findPlaneWaitingForDepth = true;

        // Turn on the camera and wait for a single depth update.
        // ī�޶� �Ѱ� ���� ���� ������Ʈ�� ��ٸ��ϴ�.
        m_tangoApplication.SetDepthCameraRate(TangoEnums.TangoDepthCameraRate.MAXIMUM);
        while (m_findPlaneWaitingForDepth)
        {
            yield return null;
        }

        m_tangoApplication.SetDepthCameraRate(TangoEnums.TangoDepthCameraRate.DISABLED);
        
        // Find the plane.
        // �ٴ��� ã�´�.
        Camera cam = Camera.main;
        Vector3 planeCenter;
        Plane plane;
        if (!m_pointCloud.FindPlane(cam, touchPosition, out planeCenter, out plane))
        {
            yield break;
        }

        // Ensure the location is always facing the camera.  This is like a LookRotation, but for the Y axis.
        // ��ġ�� �׻� ī�޶� ���ϰ� �ִ��� Ȯ���Ͻʽÿ�. �̰��� LookRotation�� ������ Y ���Դϴ�.
        Vector3 up = plane.normal;
        Vector3 forward;
        if (Vector3.Angle(plane.normal, cam.transform.forward) < 175)
        {
            Vector3 right = Vector3.Cross(up, cam.transform.forward).normalized;
            forward = Vector3.Cross(right, up).normalized;
        }
        else
        {
            // Normal is nearly parallel to camera look direction, the cross product would have too much
            // floating point error in it.
            // ������ ī�޶� ��� ����� ���� �����մϴ�. ���� ��ǰ���� �ʹ� ���� �ε� �Ҽ��� ������ �ֽ��ϴ�.
            forward = Vector3.Cross(up, cam.transform.right);
        }

        // Instantiate marker object.
        // ��Ŀ ��ü�� �ν��Ͻ�ȭ�մϴ�.
        newMarkObject = Instantiate(m_markPrefabs[m_currentMarkType],
                                    planeCenter,
                                    Quaternion.LookRotation(forward, up)) as GameObject;

        ARMarker markerScript = newMarkObject.GetComponent<ARMarker>();

        markerScript.m_type = m_currentMarkType;
        markerScript.m_timestamp = (float)m_poseController.m_poseTimestamp;
        
        Matrix4x4 uwTDevice = Matrix4x4.TRS(m_poseController.m_tangoPosition,
                                            m_poseController.m_tangoRotation,
                                            Vector3.one);
        Matrix4x4 uwTMarker = Matrix4x4.TRS(newMarkObject.transform.position,
                                            newMarkObject.transform.rotation,
                                            Vector3.one);
        markerScript.m_deviceTMarker = Matrix4x4.Inverse(uwTDevice) * uwTMarker;
        
        m_markerList.Add(newMarkObject);

        m_selectedMarker = null;
    }

    /// <summary>
    /// Data container for marker.
    /// ��Ŀ�� ������ �����̳�
    /// 
    /// Used for serializing/deserializing marker to xml.
    /// marker�� xml�� serialize / deserialize�ϴ� �� ���˴ϴ�.
    /// </summary>
    [System.Serializable]
    public class MarkerData
    {
        /// <summary>
        /// Marker's type.
        /// 
        /// Red, green or blue markers. In a real game scenario, this could be different game objects
        /// ������, ��� �Ǵ� �Ķ��� ��Ŀ.
        /// ���� ���� �ó���������, �̰��� �ٸ� ���� ��ü �� �� �ֽ��ϴ�.
        /// (e.g. banana, apple, watermelon, persimmons).
        /// </summary>
        [XmlElement("type")]
        public int m_type;

        /// <summary>
        /// Position of the this mark, with respect to the origin of the game world.
        /// ���ӿ����� �������� ��ġ�� ���õ� ��Ŀ�� ��ġ 
        /// </summary>
        [XmlElement("position")]
        public Vector3 m_position;
        
        /// <summary>
        /// Rotation of the this mark.
        /// ��Ŀ�� ȸ��
        /// </summary>
        [XmlElement("orientation")]
        public Quaternion m_orientation;
    }
}
