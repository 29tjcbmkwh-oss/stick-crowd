using System;
using System.Collections;
using System.Collections.Generic;
using _Scripts.Core;
using Lofelt.NiceVibrations;
using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    public List<AudioSource> sources;
        
    public AudioClip gameOverSound;
    public AudioClip gameWinSound;
    public AudioClip mainThemeGame;
    public AudioClip doorMinusSound;
    public AudioClip doorIncreaseSound;
    public AudioClip clickSound;
    public AudioClip cartoonFightSound;
    public AudioClip catBallRollSound;
    
    
    public bool isOneShotMuted;
    public bool isThemeMuted;
    private bool soundState = true;
    private bool musicState= true;
    private bool vibrateState= true;

    private void Start()
    {
        SetPlayerPrefs();
    }

    public void PlayOneShot(AudioClip audioClip)
    {
        if (isOneShotMuted)
        {
            return;
        }

        sources[0].PlayOneShot(audioClip);
    }

    public void PlayTheme(AudioClip audioClip)
    {
        if (isThemeMuted)
        {
            return;
        }

        sources[1].clip = audioClip;
        sources[1].Play();
    }
    
    public void PlayFightSound(AudioClip audioClip)
    {
        if (isOneShotMuted)
        {
            return;
        }

        sources[2].loop = true;
        sources[2].clip = audioClip;
        sources[2].Play();
    }
    
    public void StopFightSound()
    { 
        sources[2].Stop();
        sources[2].loop = false;
    }
    
    public void StopTheme()
    {
        sources[1].Stop();
    }
    
    bool GetPlayerPref(string key) {
        if (PlayerPrefs.HasKey(key))
        {
            return Convert.ToBoolean(PlayerPrefs.GetInt(key));
        }
        else
        {
            return true;
        }
    }
    public void SetPlayerPrefs()
    {
        Debug.Log("setplayerprefs");
        soundState = GetPlayerPref("sound");
        musicState = GetPlayerPref("music");
        vibrateState = GetPlayerPref("vibration");

        SetSound(soundState);
        SetTheme(musicState);
        SetVibration(vibrateState);

        if (!UIManager.Instance) return;
        UIManager.Instance.ChangeToggleStatus(UIManager.Instance.soundButton, soundState);
        UIManager.Instance.ChangeToggleStatus(UIManager.Instance.musicButton, musicState);
        UIManager.Instance.ChangeToggleStatus(UIManager.Instance.vibrationButton, vibrateState);
    }
    
    private void SetTheme(bool state)
    {
        musicState = state;
        sources[1].volume = state ? 0.4f : 0f;
        isThemeMuted = !state;
    }

    private void SetSound(bool state)
    {
        soundState = state;
        sources[0].volume = state ? 0.8f : 0f;
        sources[2].volume = state ? 0.8f : 0f;
        isOneShotMuted = !state;
    }


    private void SetVibration(bool state)
    {
        
        vibrateState = state;
        HapticController.hapticsEnabled = state;
    }

    public bool ToggleTheme()
    {
        bool currentState = !musicState;
        SetTheme(currentState);
        return currentState;
    }

    public bool ToggleVibration()
    {
        bool currentState = !vibrateState;
        SetVibration(currentState);
        return currentState;
    }

    public bool ToggleSound()
    {
        bool currentState = !soundState;
        SetSound(currentState);
        return currentState;
    }

}
