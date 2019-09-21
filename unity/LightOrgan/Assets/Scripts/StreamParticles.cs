using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StreamParticles : MonoBehaviour
{
    public ParticleSystem particles;
    public ParticleSystem parallelEffect;

    private bool active;

    void Start() {
        this.SetActive(true);
        this.SetActive(false);
    }

    public void SetActive(bool toggle, bool closeToEnd = false) {
        ParticleSystem.EmissionModule emission = this.particles.emission;
        ParticleSystem.MainModule main = this.particles.main;
        if (toggle) {
            emission.rateOverTime = closeToEnd ? 150 : 50;
            main.startSpeed = closeToEnd ? 10 : 5;
        } else {
            emission.rateOverTime = 0;
        }

        this.active = toggle;
    }

    public void SetColor(Color color) {
        ParticleSystem.MainModule main = this.particles.main;
        main.startColor = color;
    }

    public void ShowParallelEffect() {
        if (!this.parallelEffect.IsAlive()) {
            this.parallelEffect.Simulate(0, true, true);
            this.parallelEffect.Play();
        }
    }
}
