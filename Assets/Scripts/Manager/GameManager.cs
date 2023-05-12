﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using System;

public enum E_GAMESTATE  // 현재 게임 방의 진행 상태
{
    Wait,       // 둘 이상의 유저를 기다리는 상태 (1명일때만 이 상태 유지)
    Prepare,    // 충분한 수의 유저(둘 이상)가 모여, 게임 시작을 기다리는 상태(카운트다운)
    Play,       // 게임이 시작되고 진행중인 상태(카운트다운)
    Cooling     // 모든 게임이 끝나고 새 라운드를 기다리는 상태(카운트다운)
}

public class GameManager : SingletonPunCallbacks<GameManager>
{
    private E_GAMESTATE m_eGameState;
    public E_GAMESTATE a_eGameState { get { return m_eGameState; } }

    double m_dProcessTimer;
    double m_dStartTime;
    double m_dEndTime;

    /* 방 속성(유저(방장)가 설정 가능) */
    double m_dPropertyTimeForPrepare;   // 준비 시간
    double m_dPropertyTimeForPlay;      // 플레이 시간
    double m_dPropertyBonusTimeForKill;      // 사람 한명 죽을때마다 추가시간
    double m_dPropertyTimeForCooling;   // 게임 끝나고 기다리는 시간

    int m_iPropertyNumberOfMafia;       // 마피아 수
    int m_iPropertyNumberOfDetective;   // 탐정 수
    /***************************/
    private Dictionary<int, E_PlayerRole> m_dicPlayerRoles = new Dictionary<int, E_PlayerRole>();    // 플레이어 역할 데이터, int = 플레이어의 Actor number

    public PhotonView m_vPhotonView;

    Hashtable m_htCustomValue;

    void Start()
    {
        m_eGameState = E_GAMESTATE.Wait;
        m_dPropertyTimeForPrepare = 5f;
        m_dPropertyTimeForPlay = 300f;
        m_dPropertyBonusTimeForKill = 30f;
        m_dPropertyTimeForCooling = 5f;
        m_iPropertyNumberOfMafia = 1;
        m_iPropertyNumberOfDetective = 0;

        m_dicPlayerRoles = new Dictionary<int, E_PlayerRole>();

        if (PhotonNetwork.IsMasterClient)
        {
            m_htCustomValue = new ExitGames.Client.Photon.Hashtable();
            m_dStartTime = PhotonNetwork.Time;
            m_htCustomValue.Add("StartTime", m_dStartTime);
            PhotonNetwork.CurrentRoom.SetCustomProperties(m_htCustomValue);
        }
        else    // 게스트에겐 이미 진행되고있는 시간을 표시
        {
            m_dStartTime = double.Parse(PhotonNetwork.CurrentRoom.CustomProperties["StartTime"].ToString());
        }
    }

    void Update()
    {
        //if(PhotonNetwork.IsMasterClient)
        {
            switch(m_eGameState)
            {
                case E_GAMESTATE.Wait:
                    UpdateWaitProcess();
                    break;
                case E_GAMESTATE.Prepare:
                    UpdatePrepareProcess();
                    break;
                case E_GAMESTATE.Play:
                    UpdatePlayProcess();
                    break;
                case E_GAMESTATE.Cooling:
                    UpdateCoolingProcess();
                    break;
            }
        }
        // Debug.Log(gameState + " " + timer);
    }

    void UpdateWaitProcess()
    {
        if(PhotonNetwork.CurrentRoom.PlayerCount >= 1)
        {
            //startTime = PhotonNetwork.Time;
            //endTime = timeForPrepare;
            //gameState = E_GAMESTATE.Prepare;
            if (PhotonNetwork.IsMasterClient)
            {
                m_vPhotonView.RPC(nameof(SetGameStateRPC), RpcTarget.AllBuffered, PhotonNetwork.Time, m_dPropertyTimeForPrepare, E_GAMESTATE.Prepare);
                MapManager.I.SpawnWeapons();
            }
        }
    }

    void UpdatePrepareProcess()
    {
        m_dProcessTimer = PhotonNetwork.Time - m_dStartTime;

        if (m_dProcessTimer >= m_dPropertyTimeForPrepare)
        {
            //foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
            //{
            //    Debug.Log(player.NickName);
            //}
            //startTime = PhotonNetwork.Time;
            //endTime = timeForPlay;
            //gameState = E_GAMESTATE.Play;
            if (PhotonNetwork.IsMasterClient)
            {
                SetPlayerRole();
                m_vPhotonView.RPC(nameof(SetGameStateRPC), RpcTarget.AllBuffered, PhotonNetwork.Time, m_dPropertyTimeForPlay, E_GAMESTATE.Play);
            }
            ////else
            //{
            //    //.Parse(PhotonNetwork.CurrentRoom.CustomProperties["PlayerRoles"].ToString());

            //    //playerRoles.Clear();
            //    //Hashtable ht = PhotonNetwork.CurrentRoom.CustomProperties;
            //    //ht.TryGetValue("PlayerRoles", out var pr);
            //    //playerRoles = (Dictionary<int, E_PlayerRole>)pr;
            //    //playerRoles = (Dictionary<int, E_PlayerRole>)PhotonNetwork.CurrentRoom.CustomProperties["PlayerRoles"];
            //}
        }
    }

    // 마스터 클라이언트가 모든 플레이어의 역할을 정해주고 나머지 클라이언트에게 알려준다
    void SetPlayerRole()
    {
        m_dicPlayerRoles.Clear();
        Player[] vSortedPlayers = PhotonNetwork.PlayerList;

        for (int i = 0; i < vSortedPlayers.Length; i ++)   // 전부 시민으로 초기화
        {
            m_dicPlayerRoles.Add(vSortedPlayers[i].ActorNumber, E_PlayerRole.Civil);
        }

        for (int i = 0; i < m_iPropertyNumberOfMafia; i++)   // 마피아 뽑기
        {
            int index = UnityEngine.Random.Range(0, vSortedPlayers.Length);
            if (GetPlayerRole(vSortedPlayers[index].ActorNumber) != E_PlayerRole.Civil)  // 랜덤으로 뽑은 플레이어가 이미 마피아나 경찰이면 다시뽑음
            {
                i--;
                continue;
            }
            m_dicPlayerRoles[vSortedPlayers[index].ActorNumber] = E_PlayerRole.Mafia;

        }

        for (int i = 0; i < m_iPropertyNumberOfDetective; i++)   // 탐정 뽑기
        {
            int index = UnityEngine.Random.Range(0, vSortedPlayers.Length);
            if (GetPlayerRole(vSortedPlayers[index].ActorNumber) != E_PlayerRole.Civil)  // 랜덤으로 뽑은 플레이어가 이미 마피아나 경찰이면 다시뽑음
            {
                i--;
                continue;
            }
            m_dicPlayerRoles[vSortedPlayers[index].ActorNumber] = E_PlayerRole.Detective;

        }

        // RPC는 dictionary를 받지 못하므로, playerRoles dictionary를 string으로 변환하여 파라미터로 준다.
        string strPlayerRoles = StringConverter.I.ConvertDictionaryToString<int, E_PlayerRole>(m_dicPlayerRoles);
        m_vPhotonView.RPC(nameof(SetPlayerRoleRPC), RpcTarget.AllBuffered, strPlayerRoles);

        for (int i = 0; i < vSortedPlayers.Length; i ++)
        {
            Debug.Log(vSortedPlayers[i].NickName + " : " + m_dicPlayerRoles[vSortedPlayers[i].ActorNumber].ToString());
        }

    }

    // playerRoles를 string으로 받아 int, E_PlayerRole형 dictionary로 변환하여 모든 클라이언트에게 전해준다.
    [PunRPC]
    void SetPlayerRoleRPC(string strPlayerRoles)
    {
        Dictionary<string, string> dicPlayerRolesString = StringConverter.I.ConvertStringToDictionary(strPlayerRoles);

        m_dicPlayerRoles.Clear();
        foreach (KeyValuePair<string, string> kvPair in dicPlayerRolesString)
        {
            m_dicPlayerRoles.Add(int.Parse(kvPair.Key), (E_PlayerRole)Enum.Parse(typeof(E_PlayerRole), kvPair.Value));
        }


    }

    public E_PlayerRole GetPlayerRole(int iActorNumber)  // 특정 유저의 역할 받기
    {
        m_dicPlayerRoles.TryGetValue(iActorNumber, out E_PlayerRole eRoleToGet);

        return eRoleToGet;
    }
    public E_PlayerRole GetPlayerRole() // 자기자신의 역할 받기
    {
        m_dicPlayerRoles.TryGetValue(PhotonNetwork.LocalPlayer.ActorNumber, out E_PlayerRole roleToGet);

        return roleToGet;
    }

    void UpdatePlayProcess()
    {
        m_dProcessTimer = PhotonNetwork.Time - m_dStartTime;

        if (m_dProcessTimer >= m_dPropertyTimeForPlay)
        {
            //startTime = PhotonNetwork.Time;
            //endTime = timeForCooling;
            //gameState = E_GAMESTATE.Cooling;
            if (PhotonNetwork.IsMasterClient)
                m_vPhotonView.RPC(nameof(SetGameStateRPC), RpcTarget.AllBuffered, PhotonNetwork.Time, m_dPropertyTimeForCooling, E_GAMESTATE.Cooling);
        }
    }

    void UpdateCoolingProcess()
    {
        m_dProcessTimer = PhotonNetwork.Time - m_dStartTime;

        if (m_dProcessTimer >= m_dPropertyTimeForCooling)
        {
            //gameState = E_GAMESTATE.Wait;
            if (PhotonNetwork.IsMasterClient)
                m_vPhotonView.RPC(nameof(SetGameStateRPC), RpcTarget.AllBuffered, PhotonNetwork.Time, m_dPropertyTimeForCooling, E_GAMESTATE.Wait);
        }
    }

    [PunRPC]
    void SetGameStateRPC(double _dStartTime, double _dEndTime, E_GAMESTATE _eGameState)
    {
        m_dStartTime = _dStartTime;
        m_dEndTime = _dEndTime;
        m_eGameState = _eGameState;
    }

    public double GetTime()
    {
        return m_dEndTime - m_dProcessTimer;
    }

    // 플레이어가 무기에 닿으면 서버를 통해 해당 무기를 얻을 수 있는지 확인받는다.
    public void CheckCanPlayerPickUpWeapon(int _iWeaponViewID, int _iPlayerActorNumber, int _iWeaponManagerViewID)
    {
        m_vPhotonView.RPC(nameof(CheckCanPlayerPickUpWeaponRPC), RpcTarget.MasterClient, _iWeaponViewID, _iPlayerActorNumber, _iWeaponManagerViewID);
    }

    // 하나의 무기를 여러 플레이어가 동시에 주울때 꼬이는걸 막기위해 서버가 한명에게만 무기를 줍도록 조절한다.
    [PunRPC]
    private void CheckCanPlayerPickUpWeaponRPC(int _iWeaponViewID, int _iPlayerActorNumber, int _iWeaponManagerViewID)
    {
        if(!PhotonNetwork.LocalPlayer.IsMasterClient)
        {
            Debug.LogError(nameof(CheckCanPlayerPickUpWeaponRPC) + "must be called on the master client, BUT "+ PhotonNetwork.LocalPlayer.NickName+" is not MASTER!!");
            return;
        }

        WeaponBase vWeaponBase = PhotonView.Find(_iWeaponViewID).gameObject.GetComponent<WeaponBase>();

        if (vWeaponBase == null)
        {
            Debug.LogError("Player(" + _iPlayerActorNumber + ") tried to get weapon(" + _iWeaponViewID + "), But the weaponBase is null");
            return;
        }

        if (vWeaponBase.a_iOwnerPlayerActorNumber == -1)
        {
            vWeaponBase.a_iOwnerPlayerActorNumber = _iPlayerActorNumber;
            m_vPhotonView.RPC(nameof(ReturnCanPlayerPickUpWeaponRPC), PhotonNetwork.CurrentRoom.GetPlayer(_iPlayerActorNumber), _iWeaponViewID, _iWeaponManagerViewID, true);
        }
        else
        {
            m_vPhotonView.RPC(nameof(ReturnCanPlayerPickUpWeaponRPC), PhotonNetwork.CurrentRoom.GetPlayer(_iPlayerActorNumber), _iWeaponViewID, _iWeaponManagerViewID, false);
        }

    }

    // 무기를 주우려는 플레이어에게 무기 획득 가능 여부를 알려준다.
    [PunRPC]
    public void ReturnCanPlayerPickUpWeaponRPC(int _iWeaponViewID, int _iWeaponManagerViewID, bool _bCanPickUp)
    {
        if(!_bCanPickUp)
        {
            Debug.LogWarning("Player(" + PhotonNetwork.LocalPlayer.NickName + ") tried to get weapon(" + _iWeaponViewID + "), But the weaponBase is not on the field");
            return;
        }

        WeaponManager vWeaponManager = PhotonView.Find(_iWeaponManagerViewID).gameObject.GetComponent<WeaponManager>();

        if (vWeaponManager == null)
        {
            Debug.LogError("Player(" + PhotonNetwork.LocalPlayer.NickName + ") tried to get weapon(" + _iWeaponViewID + "), But the weaponManager is null");
            return;
        }

        vWeaponManager.PickUpWeapon(_iWeaponViewID);

    }
}
