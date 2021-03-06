﻿using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace RWS
{
    public class MultiplayerGameManager : MonoBehaviour, IOnEventCallback
    {
        [SerializeField]
        GameObject orbitCamera = null;

        [SerializeField]
        WingSpawner wingSpawner = null;

        [SerializeField]
        WingLauncher wingLauncher = null;

        [SerializeField]
        WingTelemetry wingTelemetry = null;

        [SerializeField]
        BatteryTelemetry batteryTelemetry = null;

        [SerializeField]
        MotorTelemetry motorTelemetry = null;

        [SerializeField]
        RaceTrack raceTrack = null;

        [SerializeField]
        LapTime lapTime = null;

        [SerializeField]
        PlayerOverviewPanel playerOverviewPanel = null;
        
        [SerializeField]
        GameMenu gameMenu = null;

        [SerializeField]
        SettingsPanel settingsPanel = null;

        [SerializeField]
        int photonSendRate = 60; // Default 20

        [SerializeField]
        int photonSerializationRate = 60; // Default 10

        //----------------------------------------------------------------------------------------------------

        public void OnEvent( EventData photonEvent )
        {
            var eventCode = (RaiseEventCodes)photonEvent.Code;

            if( eventCode == RaiseEventCodes.SpawnRemotePlayerWing )
            {
                var data = (object[])photonEvent.CustomData;

                var wingPosition = (Vector3)data[ 0 ];
                var wingRotation = (Quaternion)data[ 1 ];
                var viewID = (int)data[ 2 ];

                var wingGameObject = wingSpawner.SpawnRemotePlayerWing( wingPosition, wingRotation );

                var photonView = wingGameObject.GetComponent<PhotonView>();
                photonView.ViewID = viewID;

                if( remoteWingDictionary == null )
                {
                    remoteWingDictionary = new Dictionary<int, GameObject>();
                }
                remoteWingDictionary.Add( viewID, wingGameObject );
            }
        }

        //----------------------------------------------------------------------------------------------------

        readonly string bestLapPropertyKey = "BestLapProperty";
        readonly string playerTag = "Player";

        enum RaiseEventCodes : byte
        {
            SpawnRemotePlayerWing = 1
        }

        GameObject localWingGameObject;
        Rigidbody localWingRigidbody;
        Dictionary<int, GameObject> remoteWingDictionary;
        int spawnPointIndex;
        bool gameStarted;


        void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget( this );
        }

        void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget( this );
        }

        void Awake()
        {
            PhotonNetwork.SendRate = photonSendRate;
            PhotonNetwork.SerializationRate = photonSerializationRate;

            lapTime.Init( 0f );
            lapTime.OnNewBestTime += newBestTime =>
            {
                // Receiving in PlayerOverviewPanel
                PhotonNetwork.LocalPlayer.SetCustomProperties( new Hashtable { { bestLapPropertyKey, newBestTime } } );
            };
            lapTime.Hide();

            raceTrack.OnStart.AddListener( craft =>
            {
                if( craft.CompareTag( playerTag ) )
                {
                    lapTime.StartNewTime();
                }
            } );
            raceTrack.OnFinish.AddListener( craft =>
            {
                if( craft.CompareTag( playerTag ) )
                {
                    lapTime.CompareTime();
                }
            } );

            playerOverviewPanel.Hide();
            settingsPanel.Hide();
            gameMenu.Show();

            gameMenu.OnStartButton += OnStartButton;
            gameMenu.OnResumeButton += OnResumeButton;
            gameMenu.OnSettingsButton += OnSettingsButton;
            gameMenu.OnExitButton += OnExitButton;

            InputManager.Instance.OnEscapeButton += OnEscapeButton;

            if( PhotonNetwork.IsConnected )
            {
                spawnPointIndex = PhotonNetwork.LocalPlayer.ActorNumber;
                localWingGameObject = wingSpawner.SpawnLocalPlayerWing( spawnPointIndex );

                var localWingTransform = localWingGameObject.transform;
                var wingPosition = localWingTransform.position;
                var wingRotation = localWingTransform.rotation;

                localWingRigidbody = localWingGameObject.GetComponent<Rigidbody>();

                var photonView = localWingGameObject.GetComponent<PhotonView>();
                PhotonNetwork.AllocateViewID( photonView );

                SendEventToSpawnRemotePlayerWing( wingPosition, wingRotation, photonView.ViewID );

                orbitCamera.transform.position = wingPosition - new Vector3( 0f, 0f, 0.1f );

                if( wingTelemetry )
                {
                    wingTelemetry.Init( localWingGameObject.GetComponent<FlyingWing>() );
                }
                if( batteryTelemetry )
                {
                    batteryTelemetry.Init( localWingGameObject.GetComponentInChildren<Battery>() );
                }
                if( motorTelemetry )
                {
                    motorTelemetry.Init( localWingGameObject.GetComponentInChildren<Motor>() );
                }
            }
        }


        void OnStartButton()
        {
            if( !PhotonNetwork.IsConnected )
            {
                return;
            }

            gameMenu.Hide();
            playerOverviewPanel.Show();
            
            orbitCamera.SetActive( false );

            var fpvCamera = localWingGameObject.GetComponentInChildren<Camera>( true );
            fpvCamera.gameObject.SetActive( true );

            var inputManager = InputManager.Instance;
            inputManager.LaunchControl.Performed += OnLaunchButton;
            inputManager.ResetControl.Performed += OnResetButton;

            Cursor.visible = false;

            gameStarted = true;
        }

        void OnResumeButton()
        {
            gameMenu.Hide();
            settingsPanel.Hide();
            playerOverviewPanel.Show();
            
            Cursor.visible = false;
        }

        void OnSettingsButton()
        {
            settingsPanel.Show();
        }

        void OnExitButton()
        {
            BlackScreen.Instance.StartToBlackScreenAnimation( () =>
            {
                PhotonNetwork.LeaveLobby();
                SceneManager.LoadSceneAsync( 0 );
            } );
        }

        void OnLaunchButton()
        {
            wingLauncher.Launch( localWingRigidbody );
        }

        void OnResetButton()
        {
            localWingRigidbody.isKinematic = true;
            localWingRigidbody.position = wingSpawner.GetSpawnPosition( spawnPointIndex );
            localWingRigidbody.rotation = wingSpawner.GetSpawnRotation();

            wingLauncher.Reset();

            lapTime.Reset();
            lapTime.Hide();
        }

        void OnEscapeButton()
        {
            if( !gameStarted )
            {
                return;
            }

            if( !gameMenu.IsOpen )
            {
                playerOverviewPanel.Hide();
                gameMenu.Show();
                Cursor.visible = true;
            }
        }


        void SendEventToSpawnRemotePlayerWing( Vector3 position, Quaternion rotation, int viewID )
        {
            var content = new object[] { position, rotation, viewID };
            var eventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache };
            var eventCode = (byte) RaiseEventCodes.SpawnRemotePlayerWing;

            PhotonNetwork.RaiseEvent( eventCode, content, eventOptions, SendOptions.SendReliable );
        }
    }
}