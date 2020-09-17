﻿using System.Collections;
using FPS;
using Rewired;
using Sirenix.OdinInspector;
using UnityEngine;
using TMPro;

public class Gun : MonoBehaviour
{
    #region |Variables
    // * //
    #region ||Base
    [TabGroup("Base")]
    public GunTypes gunType;
    [TabGroup("Base")]
    public float damage = 10f;
    [TabGroup("Base")]
    public float range = 100f;
    [TabGroup("Base")]
    public bool automatic = false;
    [ShowIf("automatic"), TabGroup("Base")]
    public float fireRate = 15f;
    [TabGroup("Base")]
    public float impactForce = 30;
    [TabGroup("Base")]
    public Camera mainCam;
    [TabGroup("Base")]
    public GameObject weaponCam;

    //*PRIVATE
    private Player _player;
    private FpsCustom _custom;
    private float _nextFire = 0;
    #endregion

    #region ||Effects
    [TabGroup("Effects")]
    public Transform muzzlePoint;
    [TabGroup("Effects")]
    public GameObject muzzleFlash;
    [TabGroup("Effects")]
    public GameObject[] impactEffects;
    #endregion

    #region ||Ammo & Reload
    [TabGroup("Ammo & Reload")]
    public int currentAmmo = 10;
    [TabGroup("Ammo & Reload")]
    public int rounds = 3;
    [TabGroup("Ammo & Reload")]
    public float reloadTime = 1;
    [TabGroup("Ammo & Reload")]
    public float cooldownTime = .25f;
    [TabGroup("Ammo & Reload")]
    public Animator animator;
    [TabGroup("Ammo & Reload")]
    public string reloadSound;
    [TabGroup("Ammo & Reload")]
    public string cooldownSound;

    //*PRIVATE
    private int _currentAmmo;
    private int _totalAmmo;
    private bool _isReloading;
    private bool _isCooling;
    #endregion

    #region ||HUD
    [TabGroup("HUD")]
    public TextMeshProUGUI currentAmmoText;
    [TabGroup("HUD")]
    public TextMeshProUGUI totalAmmoText;
    #endregion

    #endregion

    #region |Setup
    private void Awake()
    {
        //initial setup for guns 
        _custom = GetComponentInParent<FpsCustom>();
        _currentAmmo = currentAmmo;
        _totalAmmo = currentAmmo * rounds;

        if (mainCam == null)
            mainCam = Camera.main;
    }

    private void OnEnable()
    {
        //Bug fixes for changing weapons mid-animation
        _isReloading = false;
        _isCooling = false;
        animator.SetBool("Reloading", false);

        AmmoCountUpdate();
    }
    #endregion

    private void Update()
    {
        #region |Checks
        //Reloading return
        if (_isReloading)
            return;

        //Cooldown return
        if (_isCooling)
            return;

        //Checking for auto-reload
        if (_currentAmmo <= 0 && _totalAmmo > 0)
        {
            StartCoroutine(Reload());
            return;
        }

        //Input Debugging
        if (_player == null)
        {
            _player = _custom._player;
        }
        #endregion

        #region |Shooting Input
        //automatic
        if (automatic && (Input.GetButton("Fire1") || _player.GetButton("Shoot")) && Time.time >= _nextFire)
        {
            _nextFire = Time.time + 1 / fireRate;
            Shoot();
        }
        //manual
        else if (Input.GetButtonDown("Fire1") || _player.GetButtonDown("Shoot"))
        {
            Shoot();
        }
        #endregion

        #region |Manual Reload Input
        if (_player.GetButtonDown("Reload") && _currentAmmo < currentAmmo)
            StartCoroutine(Reload());
        #endregion
    }

    #region |Reloading
    IEnumerator Reload()
    {
        if (_totalAmmo <= 0)
            StopCoroutine(Reload());

        _isReloading = true;
        print("Reloading...");

        if (reloadSound != "")
            AudioManager.instance.Play(reloadSound);
        animator.SetBool("Reloading", true);

        yield return new WaitForSeconds(reloadTime - .25f);
        animator.SetBool("Reloading", false);
        yield return new WaitForSeconds(.25f);

        if (_totalAmmo >= currentAmmo)
        {
            _currentAmmo = currentAmmo;
        }
        else
        {
            _currentAmmo = _totalAmmo;
        }

        _isReloading = false;

        AmmoCountUpdate();
    }
    #endregion

    #region |Cooldown
    IEnumerator Cooldown()
    {
        _isCooling = true;
        if (cooldownSound != "")
            AudioManager.instance.Play(cooldownSound);
        animator.SetTrigger("Cooling");
        yield return new WaitForSeconds(cooldownTime + .25f);
        _isCooling = false;
    }
    #endregion

    #region |Shooting Functionality
    void Shoot()
    {
        if (_currentAmmo <= 0)
            return;

        //play muzzle flash effect
        GameObject muzzleObject = Instantiate(muzzleFlash, muzzlePoint.position, muzzlePoint.rotation);
        Destroy(muzzleObject, 5);

        //play a gunshot sound
        AudioManager.instance.PlayGunshot(WeaponSwitching.currentType);

        //reduce ammo accordingly
        _currentAmmo--;
        _totalAmmo--;

        //perform raycast check
        RaycastHit hit;
        if (Physics.Raycast(mainCam.transform.position, mainCam.transform.forward, out hit, range))
        {
            //damage target if we hit one
            if (hit.transform.GetComponent<Target>() != null)
            {
                Target target = hit.transform.GetComponent<Target>();
                target.TakeDamage(damage);
            }

            //move object if it has a rigidbody
            if (hit.rigidbody != null)
            {
                hit.rigidbody.AddForce(-hit.normal * impactForce);
            }

            if (hit.transform.GetComponent<Grenade>() != null)
            {
                Grenade nade = hit.transform.GetComponent<Grenade>();
                nade.Explode();
            }

            if (hit.transform.GetComponentInParent<Dummy>() != null)
            {
                hit.transform.GetComponentInParent<Dummy>().Damage(damage);
                print("Hit dummy");
            }

            #region ||Impact Effects
            //check that we have impact effects
            if (impactEffects.Length <= 0)
                return;
            //randomise effect choice
            int randomImpact = Random.Range(0, impactEffects.Length);

            //create and destroy impact effect
            GameObject impactObject = Instantiate(impactEffects[randomImpact], hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(impactObject, 5);
            #endregion
        }

        AmmoCountUpdate();

        //Start Cooldown
        StartCoroutine(Cooldown());
    }
    #endregion

    #region |Text Element Updating
    void AmmoCountUpdate()
    {
        #region ||Current Ammo
        if (currentAmmoText == null)
        {
            print("**NULL**");
            return;
        }
        else
        {
            currentAmmoText.text = _currentAmmo.ToString();
        }
        #endregion

        #region ||Total Ammo
        if (totalAmmoText == null)
        {
            print("**NULL**");
            return;
        }
        else
        {
            totalAmmoText.text = _totalAmmo.ToString();
        }
        #endregion
    }
    #endregion
}