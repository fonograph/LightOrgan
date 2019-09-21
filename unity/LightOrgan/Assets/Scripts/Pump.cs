using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Pump : MonoBehaviour
{
    public float Value = 0;
    private float startPosition = 0;
    private Color color = Color.white;

    public SpriteRenderer sprite;

    public ParticleSystem particles;

    void Start() {
        this.startPosition = this.transform.position.y;
    }

    void Update() {
        this.sprite.transform.localScale = new Vector3(Config.PULSE_UNITS_PER_VOLUME * this.Value, this.sprite.transform.localScale.y, 1);

        Color fadedColor = this.color;
        fadedColor.a = this.Value * 0.5f;
        ParticleSystem.MainModule main = particles.main;
        main.startColor = fadedColor;
    }

    public void SetColor(Color color) {
        this.color = color;
    }

    // NOT THREAD SAFE
    public void SetPressure(float value) {
        this.Value = value;
    }
}
