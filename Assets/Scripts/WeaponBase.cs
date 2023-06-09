﻿using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponBase : MonoBehaviour, IPunInstantiateMagicCallback
{
    [SerializeField]
    private WeaponData m_vWeaponData;
    public WeaponData a_vWeaponData { get { return m_vWeaponData; } }

    [SerializeField]
    private PhotonView m_vPhotonView;
    [SerializeField]
    private Transform m_vMuzzlePosition;
    [SerializeField]
    private GameObject m_vBulletObject;
    [SerializeField]
    private GameObject m_vWeaponSkinObject;
    [SerializeField]
    private Rigidbody2D m_vRigidbody;

    private Collider2D m_vWeaponSkinCollider;

    int m_iCurrentAmmo;    // 현재 장전된 총알
    int m_iRemainAmmo;     // 남은 총알
    public int a_iCurrentAmmo { get { return m_iCurrentAmmo; } }
    public int a_iRemainAmmo { get { return m_iRemainAmmo; } }

    int m_iOwnerPlayerActorNumber;  // 이 무기를 들고있는 플레이어
    public int a_iOwnerPlayerActorNumber { get { return m_iOwnerPlayerActorNumber; } set { m_iOwnerPlayerActorNumber = value; } }

    int m_iWeaponID;
    public int a_iWeaponID { get { return m_iWeaponID; } set { m_iWeaponID = value; } }

    DateTime m_vLastShootTime = DateTime.MinValue;

    private void Awake()    // Start로 하면 RPC의 allbuffered로 호출된 함수가 먼저 발동돼서 초기화가 제대로 안되므로 awake를 사용
    {
        //InitWeaponData();
    }

    public void InitWeaponData()
    {
        InitCommonData();

        m_iCurrentAmmo = m_vWeaponData.a_iAmmoCapacity;
        m_iRemainAmmo = m_vWeaponData.a_iMaxAmmo;
    }
    public void InitWeaponData(int _iCurrentAmmo, int _iRemainAmmo)
    {
        InitCommonData();

        m_iCurrentAmmo = _iCurrentAmmo;
        m_iRemainAmmo = _iRemainAmmo;
    }

    private void InitCommonData()
    {
        SetWeaponSkin();
        m_iOwnerPlayerActorNumber = -1;
    }

    private void SetWeaponSkin()
    {
        if (m_vWeaponSkinObject == null)
        {
            m_vWeaponSkinObject = Instantiate(m_vWeaponData.a_vWeaponPrefab);
            m_vWeaponSkinObject.transform.parent = transform;
            m_vWeaponSkinObject.transform.localPosition = new Vector3(0f, 0f, 0f);
            m_vWeaponSkinObject.transform.rotation = Quaternion.identity;

            m_vMuzzlePosition = m_vWeaponSkinObject.transform.Find("ShootPosition");

            m_vWeaponSkinCollider = m_vWeaponSkinObject.GetComponent<Collider2D>();
        }
    }

    public void Shoot(float fAngle, int iShooterActorNumber)
    {
        if (m_iCurrentAmmo <= 0) return;

        if (DateTime.Now.Subtract(m_vLastShootTime).TotalSeconds >= m_vWeaponData.a_fRateOfFire)
        {
            m_vLastShootTime = DateTime.Now;

            //PhotonNetwork.Instantiate("Bullet", muzzlePosition.position, Quaternion.Euler(0f, 0f, angle));
            /* 꿀팁 
            PhotonNetwork.Instantiate("Bullet", muzzlePosition.position, Quaternion.Euler(0f, 0f, angle))
                .GetComponent<PhotonView>().RPC("RPCfunction",RpcTarget,RPCparameter)
            를 쓰면 instantiate 한 오브젝트의 rpc를 호출할 수 있다.
            */

            m_vPhotonView.RPC(nameof(ShootRPC), RpcTarget.All, m_vMuzzlePosition.position, Quaternion.Euler(0f, 0f, fAngle), iShooterActorNumber, m_vWeaponData.a_iWeaponId);

            m_iCurrentAmmo -= 1;
            SetAmmoUI();
        }
    }

    [PunRPC]
    public void ShootRPC(Vector3 vPosition, Quaternion vRotation, int iShooterActorNumber, int iWeaponID)
    {
        Instantiate(m_vBulletObject, vPosition, vRotation).GetComponent<BulletController>().SetBulletData(iShooterActorNumber, iWeaponID);
    }

    public void Reload()
    {
        if (m_iCurrentAmmo >= m_vWeaponData.a_iAmmoCapacity || m_iRemainAmmo <= 0) return;

        int iAmmoNumberToReload = Mathf.Min(m_iRemainAmmo, m_vWeaponData.a_iAmmoCapacity - m_iCurrentAmmo);

        m_iRemainAmmo -= iAmmoNumberToReload;
        m_iCurrentAmmo += iAmmoNumberToReload;

        SetAmmoUI();
    }

    public void SetAmmoUI()
    {
        GameUIManager.I.SetAmmo(m_vWeaponData.a_iAmmoCapacity, m_iCurrentAmmo, m_iRemainAmmo);
    }

    public void DropWeapon(Quaternion _WeaponRoration)
    {
        //StartCoroutine(ColliderOnCoroutine());
        StartCoroutine(SmoothLerp(0.2f, _WeaponRoration));
    }

    public void SetWeaponCollider(bool _bIsEnable)
    {
        m_vWeaponSkinCollider.enabled = _bIsEnable;
    }

    private IEnumerator SmoothLerp(float time, Quaternion _WeaponRoration)
    {
        Vector3 startingPos = transform.position;
        Vector3 finalPos = transform.position + (_WeaponRoration.normalized * Vector3.right * 0.4f);
        float elapsedTime = 0;

        while (elapsedTime < time)
        {
            transform.position = Vector3.Lerp(startingPos, finalPos, (elapsedTime / time));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(time);
        SetWeaponCollider(true);
    }

    //private IEnumerator ColliderOnCoroutine()
    //{
    //    yield return new WaitForSeconds(1.5f);

    //    SetWeaponCollider(true);
    //}

    // 게임 도중 관전자 플레이어가 들어왔을 때 게임 내 무기들의 위치를 바로잡는다.
    private void SetPosition()
    {
        m_vPhotonView.RPC(nameof(SetPositionRPC), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    public void SetPositionRPC(int _iLocalPlayerActorNumber)
    {
        m_vPhotonView.RPC(nameof(ReturnPositionRPC), PhotonNetwork.CurrentRoom.GetPlayer(_iLocalPlayerActorNumber), m_iOwnerPlayerActorNumber, transform.position);
    }

    [PunRPC]
    public void ReturnPositionRPC(int _iOwnerPlayerActorNumber, Vector3 _vPosition)
    {
        if(_iOwnerPlayerActorNumber==-1)
        {
            transform.parent = MapManager.I.a_vDroppedItem;
            transform.position = _vPosition;
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            gameObject.SetActive(true);
        }
        else
        {

        }
    }


    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] vInstantiationData = info.photonView.InstantiationData;

        if (vInstantiationData != null && vInstantiationData.Length == 1)
        {
            m_vWeaponData = DataManager.I.GetWeaponDataWithID((int)vInstantiationData[0]);
            InitWeaponData();

            SetPosition();
        }

    }

    public int GetPhotonViewID()
    {
        return m_vPhotonView.ViewID;
    }
}
