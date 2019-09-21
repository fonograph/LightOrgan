using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Feedback : MonoBehaviour
{
    public TMPro.TextMeshPro Text;

    void Start() {
        this.Reset();
    }

    private void Reset() {
        this.Text.transform.localPosition = new Vector3(this.Text.transform.localPosition.x, 0,  this.Text.transform.localPosition.z);
        Color col = this.Text.color; col.a = 0; this.Text.color = col;
    }

    public void Show(string text) {
        this.Text.text = text;
        this.Reset();
        this.Text.transform.DOLocalMoveY(0.3f, 2).SetEase(Ease.OutQuint);
        DOTween.To(() => this.Text.color.a, (float val) => {Color c = this.Text.color; c.a = val; this.Text.color = c;}, 1f, 0.5f);
        DOTween.To(() => this.Text.color.a, (float val) => {Color c = this.Text.color; c.a = val; this.Text.color = c;}, 0f, 0.3f).SetDelay(2);
    }
}
