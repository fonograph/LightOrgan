using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Score : MonoBehaviour
{
    public TMPro.TMP_Text Text;

    private int score = 0;

    private Vector3 defaultPosition;

    void Awake() {
        this.defaultPosition = this.transform.position;
    }

    public void Reset() {
        this.score = 0;
        this.Text.text = "0";
        this.transform.position = this.defaultPosition;
    }

    public void AddToScore(int points) {
        this.score += points;
        this.Text.text = this.score.ToString();
    }

    public void EndTransition(float duration, float delay = 0) {
        this.transform.DOMoveY(0, duration).SetEase(Ease.InOutQuad).SetDelay(delay);
    }
}
