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
    /// 장치 키보드가없는 편집기에서 GUI 텍스트 입력을 처리합니다.
    /// true이면 새로 저장된 영역 설명의 이름 입력을위한 텍스트 입력이 표시됩니다.
    /// </summary>
    private bool m_displayGuiTextInput;

    /// <summary>
    /// Handles GUI text input in Editor where there is no device keyboard.
    /// Contains text data for naming new saved Area Descriptions.
    /// 새로 저장된 영역 설명의 이름을 지정하기위한 텍스트 데이터가 들어 있습니다.
    /// </summary>
    private string m_guiTextInputContents;

    /// <summary>
    /// Handles GUI text input in Editor where there is no device keyboard.
    /// Indicates whether last text input was ended with confirmation or cancellation.
    /// 마지막 텍스트 입력이 확인 또는 취소로 종료되었는지 여부를 나타냅니다.
    /// </summary>
    private bool m_guiTextInputResult;
#endif

    /// <summary>
    /// If set, then the depth camera is on and we are waiting for the next depth update.
    /// 설정되면 깊이 카메라가 켜지고 다음 깊이 업데이트를 기다리고 있습니다.
    /// </summary>
    private bool m_findPlaneWaitingForDepth;

    /// <summary>
    /// A reference to TangoARPoseController instance.
    ///TangoARPoseController 인스턴스에 대한 참조입니다.
    /// In this class, we need TangoARPoseController reference to get the timestamp and pose when we place a marker.
    /// The timestamp and pose is used for later loop closure position correction. 
    /// 이 클래스에서 마커를 배치 할 때 타임 스탬프와 포즈를 얻으려면 TangoARPoseController 참조가 필요합니다.
    /// 타임 스탬프와 포즈는 나중에 루프결합 위치 수정에 사용됩니다.
    /// </summary>
    private TangoARPoseController m_poseController;

    /// <summary>
    /// List of markers placed in the scene.
    /// 장면에 배치 된 마커 목록입니다.
    /// </summary>
    private List<GameObject> m_markerList = new List<GameObject>();

    /// <summary>
    /// Reference to the newly placed marker.
    /// 새롭게 배치 된 마커에 대한 참조.
    /// </summary>
    private GameObject newMarkObject = null;

    /// <summary>
    /// Current marker type.
    /// 현재 마커 유형.
    /// </summary>
    private int m_currentMarkType = 0;

    /// <summary>
    /// If set, this is the selected marker.
    /// 설정되어있는 경우 이것은 선택된 마커입니다.
    /// </summary>
    private ARMarker m_selectedMarker;

    /// <summary>
    /// If set, this is the rectangle bounding the selected marker.
    /// 설정되어있는 경우는 선택된 마커의 경계가 표시됩니다.
    /// </summary>
    private Rect m_selectedRect;

    /// <summary>
    /// If the interaction is initialized.
    /// 상호 작용이 초기화 된 경우.
    /// Note that the initialization is triggered by the relocalization event. We don't want user to place object before
    /// 재 초기화 이벤트에 의해 초기화가 트리거됩니다.
    /// 기기가 다시 로컬 화되기 전에 사용자가 객체를 배치하는 것을 원하지 않습니다.
    /// </summary>
    private bool m_initialized = false;

    /// <summary>
    /// A reference to TangoApplication instance.
    /// TangoApplication 인스턴스에 대한 참조입니다.
    /// </summary>
    private TangoApplication m_tangoApplication;

    private Thread m_saveThread;

    /// <summary>
    /// Unity Start function.
    /// 유니티 스타트 기능
    /// We find and assign pose controller and tango application, and register this class to callback events.
    /// 우리는 포즈 컨트롤러와 탱고 어플리케이션을 찾아 할당하고이 클래스를 콜백 이벤트에 등록합니다.
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
    /// 유니티 업데이트 기능
    /// Mainly handle the touch event and place mark in place.
    /// 주로 터치 이벤트 및 장소 표시를 처리합니다.
    /// </summary>
    public void Update()
    {
        if (m_saveThread != null && m_saveThread.ThreadState != ThreadState.Running)
        {
            // After saving an Area Description or mark data, we reload the scene to restart the game.
            // 영역 설명을 저장하거나 데이터를 표시 한 후 장면을 다시로드하여 게임을 다시 시작합니다.
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
                // 아무것도하지 않고, 버튼을 처리합니다
            }
            else if (Physics.Raycast(cam.ScreenPointToRay(t.position), out hitInfo))
            {
                // Found a marker, select it (so long as it isn't disappearing)!
                // 마커를 찾았는지, 그것을 선택하십시오 (사라지지 않는 한)!
                GameObject tapped = hitInfo.collider.gameObject;
                if (!tapped.GetComponent<Animation>().isPlaying)
                {
                    m_selectedMarker = tapped.GetComponent<ARMarker>();
                }
            }
            else
            {
                // Place a new point at that location, clear selection
                // 해당 위치에 새 지점을 놓고 선택을 취소하십시오.
                m_selectedMarker = null;
                StartCoroutine(_WaitForDepthAndFindPlane(t.position));

                // Because we may wait a small amount of time, this is a good place to play a small
                // animation so the user knows that their input was received.
                // 우리는 작은 시간 대기 할 수 있기 때문에, 이는 사용자가 자신의 입력이 수신되었는지 알 수 있도록 작은 애니메이션을 재생하기에 좋은 장소이다.
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
            // 응용 프로그램이 백그라운드로 전환되면 Tango 서비스가 연결 해제되어 레벨을 다시로드합니다.
            // 모든 학습 된 영역과 배치 된 마커는 저장되지 않으므로 삭제해야합니다.
#pragma warning disable 618
            Application.LoadLevel(Application.loadedLevel);
            #pragma warning restore 618
        }
    }

    /// <summary>
    /// Unity OnGUI function.
    /// 유니티 onGUI 기능
    /// Mainly for removing markers.
    /// 주로 마커를 제거합니다.
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

            //Hide UI 생성

            // 라디오 버튼이 View일 경우에만 ARMarker.cs에 Explanation 호출
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

            // 라디오 버튼이 Create 일 경우 데이터 생성
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

            // 라디오 버튼이 Delete 일 경우 마커삭제
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
    /// 마커 타입 지정
    public void SetCurrentMarkType(int type)
    {
        if (type != m_currentMarkType)
        {
            m_currentMarkType = type;
        }
    }

    /// <summary>
    /// Save the game.
    /// 게임 저장
    /// Save will trigger 3 things:
    /// 저장하면 3가지가 발생합니다.
    /// 1. Save the Area Description if the learning mode is on.
    /// 2. Bundle adjustment for all marker positions, please see _UpdateMarkersForLoopClosures() function header for 
    ///     more details.
    /// 3. Save all markers to xml, save the Area Description if the learning mode is on.
    /// 4. Reload the scene.
    /// 
    /// 게임 저장
    /// 
    /// 저장하면 3가지가 발생합니다.
    /// 1. 학습 모드가 켜져있는 경우 공간인식(공간데이터)을 저장합니다.
    /// 2. 모든 마커 위치에 대한 번들 조정에 대한 자세한 내용은 _UpdateMarkersForLoopClosures () 함수 헤더를 참조하십시오.
    /// 3. 모든 마커를 xml에 저장하고 학습 모드가 켜져있는 경우 공간인식(공간데이터)을 저장합니다.
    /// 4. 씬을 불러온다.
    /// </summary>
    public void Save()
    {
        StartCoroutine(_DoSaveCurrentAreaDescription());
    }

    /// <summary>
    /// This is called each time a Tango event happens.
    /// 이것은 Tango 이벤트가 발생할 때마다 호출됩니다.
    /// </summary>
    /// <param name="tangoEvent">Tango event.</param>
    public void OnTangoEventAvailableEventHandler(Tango.TangoEvent tangoEvent)
    {
        // We will not have the saving progress when the learning mode is off.
        // 학습 모드가 꺼져있을 때 프로세스를 절약 하지 못할 것입니다.
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

    /// ※ 루프결합(Loop closure)
    /// 기기가 지도 상에 이미 등록한 위치를 다시 방문하였을 때, 현재 측정된 센서 데이터와 지도상에 존재하는 동일 데이터 
    /// 사이의 일치를 찾아내고 관련 데이터를 연결함으로써 누적된 에러를 감소시켜 지도의 정확도를 향상하는 과정을 말한다.
    /// ※ 타임스탬프(Timestamp)
    /// 일반적으로, 어떤 기준 시각(보통, Epoch)부터 경과 시간을 수치값으로 주는 문자열

    /// <summary>
    /// OnTangoPoseAvailable event from Tango.
    /// Tango의 OnTangoPoseAvailable 이벤트입니다.
    /// 
    /// In this function, we only listen to the Start-Of-Service with respect to Area-Description frame pair.
    /// This pair indicates a relocalization or loop closure event happened, base on that, we either start the initialize
    /// the interaction or do a bundle adjustment for all marker position.
    /// 이 기능에서는 Area-Description 프레임 쌍에 대해서만 Start-Of-Service를 듣습니다.
    /// 이 쌍은 relocalization 또는 loop closure 이벤트가 발생했음을 나타냅니다.
    /// 기본으로 상호 작용을 초기화하거나 모든 마커 위치에 대해 번들 조정을 시작합니다.
    /// </summary>
    /// <param name="poseData">Returned pose data from TangoService.</param>
    public void OnTangoPoseAvailable(Tango.TangoPoseData poseData)
    {
        // This frame pair's callback indicates that a loop closure or relocalization has happened. 
        //이 프레임 쌍의 콜백은 루프결합 또는 relocalization이 발생했음을 나타냅니다.

        // When learning mode is on, this callback indicates the loop closure event. Loop closure will happen when the
        // system recognizes a pre-visited area, the loop closure operation will correct the previously saved pose 
        // to achieve more accurate result. (pose can be queried through GetPoseAtTime based on previously saved
        // timestamp).
        // Loop closure definition: https://en.wikipedia.org/wiki/Simultaneous_localization_and_mapping#Loop_closure
        //
        // 학습 모드가 켜져 있으면이 콜백은 루프 폐쇄 이벤트를 나타냅니다.
        // 루프결합은 시스템이 미리 방문한 영역을 인식 할 때 발생합니다.
        // 루프결합 작업은 이전에 저장된 포즈를 수정하여보다 정확한 결과를 얻습니다.
        // (포즈는 이전에 저장된 타임 스탬프를 기반으로 GetPoseAtTime을 통해 쿼리 할 수 있음).

        // When learning mode is off, and an Area Description is loaded, this callback indicates a
        // relocalization event. Relocalization is when the device finds out where it is with respect to the loaded
        // Area Description. In our case, when the device is relocalized, the markers will be loaded because we
        // know the relatvie device location to the markers.
        //
        // 학습 모드가 해제되면, 공간인식(공간데이터)가 로드되면 이 콜백은 Relocalization 이벤트를 나타냅니다.
        // Relocalization은 장치가 로드 된 공간인식(공간데이터)과 관련하여 장치를 찾은 경우입니다.
        // 우리의 경우에, 장치가 relocalization 될 때, 마커에 상대적 장치 위치를 알기 때문에 마커가 로드 될 것입니다.
        if (poseData.framePair.baseFrame == 
            TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_AREA_DESCRIPTION &&
            poseData.framePair.targetFrame ==
            TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_START_OF_SERVICE &&
            poseData.status_code == TangoEnums.TangoPoseStatusType.TANGO_POSE_VALID)
        {
            // When we get the first loop closure/ relocalization event, we initialized all the in-game interactions.
            // 첫 번째 루프결합 / relocalization 이벤트가 발생하면 모든 게임 내 상호 작용이 초기화됩니다.
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
    /// 이것은 새로운 깊이 데이터가 사용 가능할 때마다 호출됩니다.
    /// 
    /// On the Tango tablet, the depth callback occurs at 5 Hz.
    /// Tango 태블릿에서는 5Hz에서 깊이 콜백이 발생합니다.
    /// </summary>
    /// <param name="tangoDepth">Tango depth.</param>
    public void OnTangoDepthAvailable(TangoUnityDepth tangoDepth)
    {
        // Don't handle depth here because the PointCloud may not have been updated yet.  
        // Just tell the coroutine it can continue.
        // PointCloud가 아직 업데이트되지 않았기 때문에 여기에서 깊이를 다루지 마십시오.
        // coroutine을 계속 진행하면됩니다.
        m_findPlaneWaitingForDepth = false;
    }

    /// <summary>
    /// Actually do the Area Description save.
    /// 실제 공간인식(공간데이터)를 저장합니다.
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
            // 저장하기 전에 상호 작용을 사용 중지합니다.
            m_initialized = false;
            m_savingText.gameObject.SetActive(true);
            if (m_tangoApplication.m_areaDescriptionLearningMode)
            {
                m_saveThread = new Thread(delegate()
                {
                    // Start saving process in another thread.
                    // 다른 스레드에서 프로세스 저장 시작.
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
    /// 루프결합이 발생하면 저장된 마크를 모두 수정하십시오.
    /// 
    /// When Tango Service is in learning mode, the drift will accumulate overtime, but when the system sees a
    /// preexisting area, it will do a operation to correct all previously saved poses
    /// (the pose you can query with GetPoseAtTime). This operation is called loop closure. When loop closure happens,
    /// we will need to re-query all previously saved marker position in order to achieve the best result.
    /// This function is doing the querying job based on timestamp.
    /// Tango Service가 학습 모드에있을 때, drift는 시간이 늘어나지만 시스템이 기존 영역을 볼 때 이전에 저장된
    /// 포즈 (GetPoseAtTime으로 쿼리 할 수있는 포즈)를 모두 수정하는 작업을 수행합니다. 이 작업을 루프결합 이라고합니다.
    /// 루프결합아 발생하면 최상의 결과를 얻으려면 이전에 저장된 마커 위치를 다시 쿼리해야합니다.
    /// 이 함수는 타임 스탬프에 따라 쿼리 작업을 수행합니다.
    /// </summary>
    private void _UpdateMarkersForLoopClosures()
    {
        // Adjust mark's position each time we have a loop closure detected.
        // 루프결합이 감지 될 때마다 마크의 위치를 조정하십시오.
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
    /// 응용 프로그램 저장소에 저장된 XML 파일에 마커 목록을 작성합니다.
    /// </summary>
    private void _SaveMarkerToDisk()
    {
        // Compose a XML data list.
        // XML 데이터 목록을 작성하십시오.
        List<MarkerData> xmlDataList = new List<MarkerData>();
        foreach (GameObject obj in m_markerList)
        {
            // Add marks data to the list, we intentionally didn't add the timestamp, because the timestamp will not be
            // useful when the next time Tango Service is connected. The timestamp is only used for loop closure pose
            // correction in current Tango connection.
            // 마크 데이터를 목록에 추가하면 의도적으로 타임 스탬프를 추가하지 않았습니다.
            // 왜냐하면 타임스탬프는 다음 탱고 서비스 가 연결될때 유용하지않습니다.
            // 타임 스탬프는 현재 Tango 연결에서 루프결합 포즈 수정에만 사용됩니다.
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
    /// 응용 프로그램 저장소에서 마커 목록 xml을로드합니다.
    /// </summary>
    private void _LoadMarkerFromDisk()
    {
        // Attempt to load the exsiting markers from storage.
        // 저장소에서 기존 마커를로드하려고합니다.
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
            // 모든 마커의 게임 객체를 인스턴스화합니다.
            GameObject temp = Instantiate(m_markPrefabs[mark.m_type],
                                          mark.m_position,
                                          mark.m_orientation) as GameObject;
            m_markerList.Add(temp);
        }
    }

    /// <summary>
    /// Convert a 3D bounding box represented by a <c>Bounds</c> object into a 2D 
    /// rectangle represented by a <c>Rect</c> object.
    /// <c>Bounds</c> 객체가 나타내는 3D 경계 상자를 <c>Rect</c> 객체가 나타내는 2D 구조체로 변환합니다.
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
    /// 다음 깊이 업데이트를 기다린 다음 터치 위치에서 평면을 찾습니다.
    /// </summary>
    /// <returns>Coroutine IEnumerator.</returns>
    /// <param name="touchPosition">Touch position to find a plane at.</param>
    private IEnumerator _WaitForDepthAndFindPlane(Vector2 touchPosition)
    {
        m_findPlaneWaitingForDepth = true;

        // Turn on the camera and wait for a single depth update.
        // 카메라를 켜고 단일 깊이 업데이트를 기다립니다.
        m_tangoApplication.SetDepthCameraRate(TangoEnums.TangoDepthCameraRate.MAXIMUM);
        while (m_findPlaneWaitingForDepth)
        {
            yield return null;
        }

        m_tangoApplication.SetDepthCameraRate(TangoEnums.TangoDepthCameraRate.DISABLED);
        
        // Find the plane.
        // 바닥을 찾는다.
        Camera cam = Camera.main;
        Vector3 planeCenter;
        Plane plane;
        if (!m_pointCloud.FindPlane(cam, touchPosition, out planeCenter, out plane))
        {
            yield break;
        }

        // Ensure the location is always facing the camera.  This is like a LookRotation, but for the Y axis.
        // 위치가 항상 카메라를 향하고 있는지 확인하십시오. 이것은 LookRotation과 같지만 Y 축입니다.
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
            // 보통은 카메라 모양 방향과 거의 평행합니다. 교차 제품에는 너무 많은 부동 소수점 오류가 있습니다.
            forward = Vector3.Cross(up, cam.transform.right);
        }

        // Instantiate marker object.
        // 마커 객체를 인스턴스화합니다.
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
    /// 마커용 데이터 컨테이너
    /// 
    /// Used for serializing/deserializing marker to xml.
    /// marker를 xml로 serialize / deserialize하는 데 사용됩니다.
    /// </summary>
    [System.Serializable]
    public class MarkerData
    {
        /// <summary>
        /// Marker's type.
        /// 
        /// Red, green or blue markers. In a real game scenario, this could be different game objects
        /// 빨간색, 녹색 또는 파란색 마커.
        /// 실제 게임 시나리오에서, 이것은 다른 게임 객체 일 수 있습니다.
        /// (e.g. banana, apple, watermelon, persimmons).
        /// </summary>
        [XmlElement("type")]
        public int m_type;

        /// <summary>
        /// Position of the this mark, with respect to the origin of the game world.
        /// 게임월드의 오리지널 위치에 관련된 마커의 위치 
        /// </summary>
        [XmlElement("position")]
        public Vector3 m_position;
        
        /// <summary>
        /// Rotation of the this mark.
        /// 마커의 회전
        /// </summary>
        [XmlElement("orientation")]
        public Quaternion m_orientation;
    }
}
